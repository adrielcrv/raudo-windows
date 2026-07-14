using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Raudo
{
    internal sealed class MiniPositionChangedEventArgs : EventArgs
    {
        public MiniPositionChangedEventArgs(Point center)
        {
            Center = center;
        }

        public Point Center { get; private set; }
    }

    internal sealed class MiniForm : Form
    {
        private const int EdgeWidth = 20;
        private const int ControlHeight = 48;
        private const int ArrowZoneWidth = 48;
        private const int CenterZoneWidth = 48;
        private const uint SetWindowPosNoZOrder = 0x0004;
        private const uint SetWindowPosNoActivate = 0x0010;

        private readonly VirtualDesktopService desktopService;
        private readonly ContextMenuStrip windowMenu;
        private readonly Font windowMenuHeaderFont;
        private readonly Timer revealTimer;
        private readonly Timer collapseTimer;
        private readonly Timer animationTimer;
        private readonly Timer attentionTimer;
        private readonly Timer presenceTimer;
        private readonly Timer opacityTimer;
        private readonly Timer navigationRefreshTimer;
        private readonly ToolTip toolTip;

        private ThemePalette palette;
        private Point dockAnchor;
        private Point dragStartCursor;
        private Point dragStartLocation;
        private MiniHitZone hoverZone;
        private MiniHitZone pressedZone;
        private bool expanded;
        private double revealProgress;
        private double animationStartProgress;
        private double animationTargetProgress;
        private long animationStartTimestamp;
        private int animationDurationMilliseconds;
        private long attentionStartTimestamp;
        private int attentionDurationMilliseconds;
        private int attentionCycles;
        private double attentionPulse;
        private MiniPulseKind pulseKind;
        private bool dragging;
        private bool pointerInside;
        private bool allowClose;
        private bool canNavigateLeft = true;
        private bool canNavigateRight = true;
        private bool followsActiveDesktop;
        private KeepActivePhase sessionPhase;
        private UserNotificationState userNotificationState;
        private double opacityStart;
        private double opacityTarget;
        private long opacityStartTimestamp;

        public MiniForm(VirtualDesktopService service, RaudoSettings settings)
        {
            desktopService = service;

            Text = "Raudo Mini";
            AccessibleName = "Modo Mini de Raudo";
            AccessibleDescription = "Navega entre escritorios y trae ventanas al escritorio actual.";
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(EdgeWidth, ControlHeight);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            DoubleBuffered = true;
            KeyPreview = true;

            dockAnchor = GetInitialDockAnchor(settings);

            windowMenu = new ContextMenuStrip();
            windowMenu.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            windowMenuHeaderFont = new Font(windowMenu.Font, FontStyle.Bold);
            windowMenu.Closed += delegate
            {
                ScheduleCollapse();
                UpdateWindowOpacity();
            };

            revealTimer = new Timer();
            revealTimer.Interval = MiniMotion.RevealIntentDelayMilliseconds;
            revealTimer.Tick += RevealTimerTick;

            collapseTimer = new Timer();
            collapseTimer.Interval = MiniMotion.CollapseDelayMilliseconds;
            collapseTimer.Tick += CollapseTimerTick;

            animationTimer = new Timer();
            animationTimer.Interval = MiniMotion.FrameIntervalMilliseconds;
            animationTimer.Tick += AnimationTimerTick;

            attentionTimer = new Timer();
            attentionTimer.Interval = 30;
            attentionTimer.Tick += AttentionTimerTick;

            presenceTimer = new Timer();
            presenceTimer.Interval = 2000;
            presenceTimer.Tick += PresenceTimerTick;

            opacityTimer = new Timer();
            opacityTimer.Interval = MiniMotion.FrameIntervalMilliseconds;
            opacityTimer.Tick += OpacityTimerTick;

            navigationRefreshTimer = new Timer();
            navigationRefreshTimer.Interval = 450;
            navigationRefreshTimer.Tick += NavigationRefreshTimerTick;

            toolTip = new ToolTip();
            toolTip.InitialDelay = 350;
            toolTip.ReshowDelay = 100;
            toolTip.SetToolTip(this, "Raudo Mini · Inactivo");

            sessionPhase = KeepActivePhase.Inactive;
            userNotificationState = ShellUserState.Current();

            ApplyTheme(ThemeService.Current());
            LayoutAtAnchor();
        }

        public event EventHandler OpenMainRequested;
        public event EventHandler HideRequested;
        public event EventHandler PinHelpRequested;
        public event EventHandler<MiniPositionChangedEventArgs> PositionChangedByUser;

        public bool FollowsActiveDesktop
        {
            get { return followsActiveDesktop; }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            BackColor = palette.Surface;
            ForeColor = palette.Text;
            Invalidate();

            if (IsHandleCreated)
            {
                WindowTheme.Apply(Handle, palette.IsDark);
            }
        }

        public void SetSessionPhase(KeepActivePhase phase)
        {
            if (sessionPhase != phase)
            {
                sessionPhase = phase;
                toolTip.SetToolTip(this, GetSessionTooltip());
                Invalidate();
            }
        }

        public void PulseAttention()
        {
            StartVisualPulse(MiniPulseKind.Attention, 1400, 2);
        }

        public void PulseLanding()
        {
            StartVisualPulse(MiniPulseKind.Landing, 520, 1);
        }

        public void ShowMini()
        {
            EnsureVisibleOnScreen();
            if (!Visible)
            {
                Show();
            }

            LayoutAtAnchor();
            TopMost = true;
            UpdatePresenceContext();
            presenceTimer.Start();
            string ignored;
            followsActiveDesktop = desktopService.TryKeepWindowVisibleAcrossDesktops(
                Handle,
                out ignored);
        }

        public void EnsureVisibleOnScreen()
        {
            dockAnchor = ClampDockAnchor(dockAnchor);
            LayoutAtAnchor();
        }

        public void AllowCloseAndClose()
        {
            allowClose = true;
            revealTimer.Stop();
            collapseTimer.Stop();
            animationTimer.Stop();
            attentionTimer.Stop();
            presenceTimer.Stop();
            opacityTimer.Stop();
            navigationRefreshTimer.Stop();
            Close();
        }

        internal void SetExpandedForTesting(bool shouldExpand)
        {
            SetExpanded(shouldExpand, false);
        }

        internal bool IsAnimationRunningForTesting
        {
            get { return animationTimer.Enabled; }
        }

        internal void SetRevealProgressForTesting(double progress)
        {
            animationTimer.Stop();
            expanded = progress > 0D;
            ApplyRevealProgress(progress);
        }

        internal void SetNavigationAvailabilityForTesting(bool left, bool right)
        {
            canNavigateLeft = left;
            canNavigateRight = right;
            ApplyRevealProgress(revealProgress);
        }

        internal void SetNotificationStateForTesting(UserNotificationState state)
        {
            userNotificationState = state;
            UpdateWindowOpacity(false);
        }

        internal double WindowOpacityForTesting
        {
            get { return Opacity; }
        }

        protected override void OnHandleCreated(EventArgs eventArgs)
        {
            base.OnHandleCreated(eventArgs);
            ApplyRevealProgress(revealProgress);
            LayoutAtAnchor();
            UpdateWindowRegion();
            if (palette != null)
            {
                WindowTheme.Apply(Handle, palette.IsDark);
            }
        }

        protected override void OnResize(EventArgs eventArgs)
        {
            base.OnResize(eventArgs);
            UpdateWindowRegion();
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            eventArgs.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Rectangle bounds = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            bool dockedLeft = IsDockedLeft(
                dockAnchor,
                Screen.FromPoint(dockAnchor).WorkingArea);
            float progress = (float)revealProgress;
            float edgeOpacity = 1F - Remap(progress, 0F, 0.42F);
            float contentOpacity = Remap(progress, 0.46F, 1F);
            Color accent = palette.Primary;
            if (attentionPulse > 0D)
            {
                Color pulseColor = pulseKind == MiniPulseKind.Landing
                    ? Color.White
                    : GetSessionStatusColor();
                float strength = pulseKind == MiniPulseKind.Landing ? 0.22F : 0.34F;
                accent = BlendColor(
                    accent,
                    pulseColor,
                    (float)attentionPulse * strength);
            }
            Color surface = BlendColor(accent, palette.Surface, progress);
            Color borderColor = BlendColor(accent, palette.Border, progress);
            using (GraphicsPath path = CreateContainerPath(bounds, dockedLeft, progress))
            using (SolidBrush background = new SolidBrush(surface))
            using (Pen border = new Pen(borderColor, Math.Max(1F, ScaleLogical(1))))
            {
                eventArgs.Graphics.FillPath(background, path);
                eventArgs.Graphics.DrawPath(border, path);
            }

            if (edgeOpacity > 0F)
            {
                DrawEdgeHandle(eventArgs.Graphics, dockedLeft, edgeOpacity);
                DrawEdgeSessionIndicator(eventArgs.Graphics, dockedLeft, edgeOpacity);
            }

            if (contentOpacity <= 0F)
            {
                return;
            }

            if (canNavigateLeft && GetZoneBounds(MiniHitZone.Left).Right <= ClientSize.Width)
            {
                DrawHitZone(eventArgs.Graphics, MiniHitZone.Left);
                DrawArrow(eventArgs.Graphics, true, contentOpacity);
            }

            if (canNavigateRight && GetZoneBounds(MiniHitZone.Right).Right <= ClientSize.Width)
            {
                DrawHitZone(eventArgs.Graphics, MiniHitZone.Right);
                DrawArrow(eventArgs.Graphics, false, contentOpacity);
            }

            int markSize = ScaleLogical(32);
            Rectangle centerBounds = GetZoneBounds(MiniHitZone.Center);
            Rectangle markBounds = new Rectangle(
                centerBounds.Left + (centerBounds.Width - markSize) / 2,
                (ClientSize.Height - markSize) / 2,
                markSize,
                markSize);
            if (markBounds.Right <= ClientSize.Width)
            {
                BrandDrawing.DrawMark(
                    eventArgs.Graphics,
                    markBounds,
                    WithAlpha(accent, contentOpacity),
                    WithAlpha(Color.White, contentOpacity));
            }

            DrawExpandedSessionIndicator(eventArgs.Graphics, contentOpacity, markBounds);
        }

        protected override void OnMouseEnter(EventArgs eventArgs)
        {
            base.OnMouseEnter(eventArgs);
            pointerInside = true;
            UpdateWindowOpacity();
            collapseTimer.Stop();
            RefreshNavigationAvailability();
            if (revealProgress > 0D)
            {
                SetExpanded(true);
            }
            else
            {
                revealTimer.Stop();
                revealTimer.Start();
            }
        }

        protected override void OnMouseLeave(EventArgs eventArgs)
        {
            base.OnMouseLeave(eventArgs);
            pointerInside = false;
            UpdateWindowOpacity();
            revealTimer.Stop();
            hoverZone = MiniHitZone.None;
            Invalidate();
            ScheduleCollapse();
        }

        protected override void OnMouseMove(MouseEventArgs eventArgs)
        {
            base.OnMouseMove(eventArgs);

            if (eventArgs.Button == MouseButtons.Left
                && pressedZone == MiniHitZone.Center)
            {
                Point cursor = Cursor.Position;
                if (!dragging
                    && (Math.Abs(cursor.X - dragStartCursor.X) > 4
                        || Math.Abs(cursor.Y - dragStartCursor.Y) > 4))
                {
                    dragging = true;
                }

                if (dragging)
                {
                    Point requested = new Point(
                        dragStartLocation.X + cursor.X - dragStartCursor.X,
                        dragStartLocation.Y + cursor.Y - dragStartCursor.Y);
                    Location = ClampDragLocation(requested, Size);
                }
            }

            MiniHitZone nextZone = HitTest(eventArgs.Location);
            if (hoverZone != nextZone)
            {
                hoverZone = nextZone;
                Invalidate();
            }

            Cursor = nextZone == MiniHitZone.Center && revealProgress >= 0.98D
                ? Cursors.SizeAll
                : Cursors.Hand;
        }

        protected override void OnMouseDown(MouseEventArgs eventArgs)
        {
            base.OnMouseDown(eventArgs);
            if (eventArgs.Button == MouseButtons.Right)
            {
                ShowWindowMenu();
                return;
            }

            if (eventArgs.Button != MouseButtons.Left)
            {
                return;
            }

            pressedZone = HitTest(eventArgs.Location);
            dragStartCursor = Cursor.Position;
            dragStartLocation = Location;
            dragging = false;
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs eventArgs)
        {
            base.OnMouseUp(eventArgs);
            if (eventArgs.Button != MouseButtons.Left)
            {
                return;
            }

            Capture = false;
            MiniHitZone releasedZone = HitTest(eventArgs.Location);
            bool wasDragging = dragging;
            dragging = false;

            if (wasDragging)
            {
                dockAnchor = GetDockAnchor(Cursor.Position);
                EventHandler<MiniPositionChangedEventArgs> positionHandler = PositionChangedByUser;
                if (positionHandler != null)
                {
                    positionHandler(this, new MiniPositionChangedEventArgs(dockAnchor));
                }

                SetExpanded(false);
            }
            else if (releasedZone == pressedZone)
            {
                if (releasedZone == MiniHitZone.Left)
                {
                    SwitchDesktop(DesktopDirection.Left);
                }
                else if (releasedZone == MiniHitZone.Right)
                {
                    SwitchDesktop(DesktopDirection.Right);
                }
                else
                {
                    ShowWindowMenu();
                }
            }

            pressedZone = MiniHitZone.None;
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs eventArgs)
        {
            base.OnKeyDown(eventArgs);
            if (eventArgs.KeyCode == Keys.Left)
            {
                if (canNavigateLeft)
                {
                    SwitchDesktop(DesktopDirection.Left);
                }
                eventArgs.Handled = true;
            }
            else if (eventArgs.KeyCode == Keys.Right)
            {
                if (canNavigateRight)
                {
                    SwitchDesktop(DesktopDirection.Right);
                }
                eventArgs.Handled = true;
            }
            else if (eventArgs.KeyCode == Keys.Enter || eventArgs.KeyCode == Keys.Space)
            {
                ShowWindowMenu();
                eventArgs.Handled = true;
            }
            else if (eventArgs.KeyCode == Keys.Escape)
            {
                SetExpanded(false);
                eventArgs.Handled = true;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs eventArgs)
        {
            bool systemIsClosing = eventArgs.CloseReason == CloseReason.WindowsShutDown
                || eventArgs.CloseReason == CloseReason.TaskManagerClosing;
            if (!allowClose && !systemIsClosing)
            {
                eventArgs.Cancel = true;
                EventHandler handler = HideRequested;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }

            base.OnFormClosing(eventArgs);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                revealTimer.Dispose();
                collapseTimer.Dispose();
                animationTimer.Dispose();
                attentionTimer.Dispose();
                presenceTimer.Dispose();
                opacityTimer.Dispose();
                navigationRefreshTimer.Dispose();
                toolTip.Dispose();
                ClearWindowMenuItems();
                windowMenu.Dispose();
                windowMenuHeaderFont.Dispose();
            }

            base.Dispose(disposing);
        }

        private void SwitchDesktop(DesktopDirection direction)
        {
            if ((direction == DesktopDirection.Left && !canNavigateLeft)
                || (direction == DesktopDirection.Right && !canNavigateRight))
            {
                return;
            }

            string error;
            if (!DesktopNavigation.TrySwitch(direction, out error))
            {
                MessageBox.Show(
                    this,
                    error,
                    "Raudo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            navigationRefreshTimer.Stop();
            navigationRefreshTimer.Start();
        }

        private void ShowWindowMenu()
        {
            SetExpanded(true, false);
            collapseTimer.Stop();
            BuildWindowMenu();
            windowMenu.Show(this, new Point(ClientSize.Width / 2, ClientSize.Height));
            UpdateWindowOpacity();
        }

        private void BuildWindowMenu()
        {
            ClearWindowMenuItems();

            ToolStripMenuItem header = new ToolStripMenuItem("Ventanas en otros escritorios");
            header.Enabled = false;
            header.Font = windowMenuHeaderFont;
            windowMenu.Items.Add(header);

            IList<DesktopWindow> windows;
            string error;
            if (!desktopService.TryGetWindowsOutsideCurrentDesktop(out windows, out error))
            {
                ToolStripMenuItem unavailable = new ToolStripMenuItem(error);
                unavailable.Enabled = false;
                windowMenu.Items.Add(unavailable);
            }
            else if (!desktopService.CanBringWindows)
            {
                ToolStripMenuItem unavailable = new ToolStripMenuItem(
                    "Esta versión de Windows requiere usar Win + Tab");
                unavailable.Enabled = false;
                windowMenu.Items.Add(unavailable);
            }
            else if (windows.Count == 0)
            {
                ToolStripMenuItem empty = new ToolStripMenuItem("No hay ventanas para traer");
                empty.Enabled = false;
                windowMenu.Items.Add(empty);
            }
            else
            {
                int count = Math.Min(15, windows.Count);
                for (int index = 0; index < count; index++)
                {
                    DesktopWindow candidate = windows[index];
                    ToolStripMenuItem item = new ToolStripMenuItem(candidate.DisplayName);
                    item.Tag = candidate.Handle;
                    item.ToolTipText = "Traer al escritorio actual";
                    item.Click += WindowItemClick;
                    windowMenu.Items.Add(item);
                }

                if (windows.Count > count)
                {
                    ToolStripMenuItem remainder = new ToolStripMenuItem(
                        string.Format("{0} ventanas más no mostradas", windows.Count - count));
                    remainder.Enabled = false;
                    windowMenu.Items.Add(remainder);
                }
            }

            windowMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem desktopVisibility;
            if (followsActiveDesktop)
            {
                desktopVisibility = new ToolStripMenuItem(
                    "Visible en todos los escritorios");
                desktopVisibility.Checked = true;
                desktopVisibility.Enabled = false;
            }
            else
            {
                desktopVisibility = new ToolStripMenuItem(
                    "Configurar visibilidad en escritorios…");
                desktopVisibility.Click += delegate
                {
                    EventHandler handler = PinHelpRequested;
                    if (handler != null)
                    {
                        handler(this, EventArgs.Empty);
                    }
                };
            }
            windowMenu.Items.Add(desktopVisibility);

            ToolStripMenuItem open = new ToolStripMenuItem("Abrir Raudo");
            open.Click += delegate
            {
                EventHandler handler = OpenMainRequested;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            };
            windowMenu.Items.Add(open);

            ToolStripMenuItem hide = new ToolStripMenuItem("Ocultar Modo Mini");
            hide.Click += delegate
            {
                EventHandler handler = HideRequested;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            };
            windowMenu.Items.Add(hide);
        }

        private void ClearWindowMenuItems()
        {
            while (windowMenu.Items.Count > 0)
            {
                ToolStripItem item = windowMenu.Items[0];
                windowMenu.Items.RemoveAt(0);
                item.Dispose();
            }
        }

        private void WindowItemClick(object sender, EventArgs eventArgs)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null || !(item.Tag is IntPtr))
            {
                return;
            }

            string error;
            if (!desktopService.TryBringHere((IntPtr)item.Tag, out error))
            {
                MessageBox.Show(
                    this,
                    error,
                    "No se pudo traer la ventana",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void SetExpanded(bool shouldExpand)
        {
            SetExpanded(shouldExpand, true);
        }

        private void SetExpanded(bool shouldExpand, bool animate)
        {
            revealTimer.Stop();
            double target = shouldExpand ? 1D : 0D;
            if (expanded == shouldExpand
                && Math.Abs(revealProgress - target) < 0.001D)
            {
                return;
            }

            expanded = shouldExpand;
            animationTimer.Stop();
            if (!animate || !MotionSettings.ClientAreaAnimationsEnabled())
            {
                ApplyRevealProgress(target);
                return;
            }

            animationStartProgress = revealProgress;
            animationTargetProgress = target;
            double distance = Math.Abs(animationTargetProgress - animationStartProgress);
            animationDurationMilliseconds = Math.Max(
                1,
                (int)Math.Round(MiniMotion.TransitionDurationMilliseconds * distance));
            animationStartTimestamp = Stopwatch.GetTimestamp();
            animationTimer.Start();
        }

        private void ApplyRevealProgress(double progress)
        {
            revealProgress = Math.Max(0D, Math.Min(1D, progress));
            Size requested = GetRequestedSize();
            if (ClientSize != requested)
            {
                ClientSize = requested;
            }

            LayoutAtAnchor();
            UpdateWindowRegion();
            UpdateWindowOpacity(false);
            Invalidate();
        }

        private Size GetRequestedSize()
        {
            int edgeWidth = ScaleLogical(EdgeWidth);
            int expandedWidth = GetExpandedWidth();
            int width = edgeWidth + (int)Math.Round(
                (expandedWidth - edgeWidth) * revealProgress);
            return new Size(width, ScaleLogical(ControlHeight));
        }

        private void ScheduleCollapse()
        {
            revealTimer.Stop();
            collapseTimer.Stop();
            collapseTimer.Start();
        }

        private void RevealTimerTick(object sender, EventArgs eventArgs)
        {
            revealTimer.Stop();
            if (Bounds.Contains(Cursor.Position))
            {
                SetExpanded(true);
            }
        }

        private void CollapseTimerTick(object sender, EventArgs eventArgs)
        {
            if (!windowMenu.Visible
                && !dragging
                && !Bounds.Contains(Cursor.Position))
            {
                collapseTimer.Stop();
                SetExpanded(false);
            }
        }

        private void AnimationTimerTick(object sender, EventArgs eventArgs)
        {
            double elapsedMilliseconds = (Stopwatch.GetTimestamp() - animationStartTimestamp)
                * 1000D
                / Stopwatch.Frequency;
            double time = Math.Min(1D, elapsedMilliseconds / animationDurationMilliseconds);
            double eased = animationTargetProgress > animationStartProgress
                ? MiniMotion.EaseReveal(time)
                : MiniMotion.EaseHide(time);
            double next = animationStartProgress
                + ((animationTargetProgress - animationStartProgress) * eased);
            ApplyRevealProgress(next);

            if (time >= 1D)
            {
                animationTimer.Stop();
                ApplyRevealProgress(animationTargetProgress);
            }
        }

        private void AttentionTimerTick(object sender, EventArgs eventArgs)
        {
            double elapsedMilliseconds = (Stopwatch.GetTimestamp() - attentionStartTimestamp)
                * 1000D
                / Stopwatch.Frequency;
            double progress = Math.Min(1D, elapsedMilliseconds / attentionDurationMilliseconds);
            attentionPulse = 0.5D - (0.5D * Math.Cos(
                progress * attentionCycles * Math.PI * 2D));
            Invalidate();

            if (progress >= 1D)
            {
                attentionTimer.Stop();
                attentionPulse = 0D;
                pulseKind = MiniPulseKind.None;
                Invalidate();
            }
        }

        private void PresenceTimerTick(object sender, EventArgs eventArgs)
        {
            UpdatePresenceContext();
        }

        private void StartVisualPulse(MiniPulseKind kind, int durationMilliseconds, int cycles)
        {
            UserNotificationState currentState = ShellUserState.Current();
            if (currentState != userNotificationState)
            {
                userNotificationState = currentState;
                UpdateWindowOpacity();
            }

            if (ShellUserState.IsImmersive(currentState)
                || !MotionSettings.ClientAreaAnimationsEnabled())
            {
                return;
            }

            attentionTimer.Stop();
            pulseKind = kind;
            attentionPulse = 0D;
            attentionDurationMilliseconds = durationMilliseconds;
            attentionCycles = cycles;
            attentionStartTimestamp = Stopwatch.GetTimestamp();
            attentionTimer.Start();
        }

        private void UpdatePresenceContext()
        {
            UserNotificationState next = ShellUserState.Current();
            if (next != userNotificationState)
            {
                userNotificationState = next;
                if (ShellUserState.IsImmersive(userNotificationState))
                {
                    attentionTimer.Stop();
                    attentionPulse = 0D;
                    pulseKind = MiniPulseKind.None;
                }

                UpdateWindowOpacity();
                Invalidate();
            }
        }

        private void UpdateWindowOpacity()
        {
            UpdateWindowOpacity(true);
        }

        private void UpdateWindowOpacity(bool animate)
        {
            double restingOpacity = ShellUserState.IsImmersive(userNotificationState)
                ? 0.38D
                : 0.82D;
            double target = pointerInside || windowMenu.Visible
                ? 1D
                : restingOpacity + ((1D - restingOpacity) * revealProgress);
            if (Math.Abs(Opacity - target) <= 0.005D)
            {
                opacityTimer.Stop();
                Opacity = target;
                return;
            }

            if (!animate || !Visible || !MotionSettings.ClientAreaAnimationsEnabled())
            {
                opacityTimer.Stop();
                Opacity = target;
                return;
            }

            opacityTimer.Stop();
            opacityStart = Opacity;
            opacityTarget = target;
            opacityStartTimestamp = Stopwatch.GetTimestamp();
            opacityTimer.Start();
        }

        private void OpacityTimerTick(object sender, EventArgs eventArgs)
        {
            double elapsedMilliseconds = (Stopwatch.GetTimestamp() - opacityStartTimestamp)
                * 1000D
                / Stopwatch.Frequency;
            double time = Math.Min(
                1D,
                elapsedMilliseconds / MiniMotion.PresenceTransitionDurationMilliseconds);
            double eased = MiniMotion.EaseReveal(time);
            Opacity = opacityStart + ((opacityTarget - opacityStart) * eased);
            if (time >= 1D)
            {
                opacityTimer.Stop();
                Opacity = opacityTarget;
            }
        }

        private void NavigationRefreshTimerTick(object sender, EventArgs eventArgs)
        {
            navigationRefreshTimer.Stop();
            RefreshNavigationAvailability();
        }

        private void RefreshNavigationAvailability()
        {
            bool left;
            bool right;
            if (!desktopService.TryGetNavigationAvailability(out left, out right))
            {
                left = true;
                right = true;
            }

            bool changed = canNavigateLeft != left || canNavigateRight != right;
            canNavigateLeft = left;
            canNavigateRight = right;
            if (expanded && changed)
            {
                ApplyRevealProgress(revealProgress);
            }

            Invalidate();
        }

        private MiniHitZone HitTest(Point point)
        {
            if (!expanded || revealProgress < 0.98D)
            {
                return MiniHitZone.Center;
            }

            if (canNavigateLeft && GetZoneBounds(MiniHitZone.Left).Contains(point))
            {
                return MiniHitZone.Left;
            }

            if (canNavigateRight && GetZoneBounds(MiniHitZone.Right).Contains(point))
            {
                return MiniHitZone.Right;
            }

            return MiniHitZone.Center;
        }

        private void DrawHitZone(Graphics graphics, MiniHitZone zone)
        {
            if (hoverZone != zone && pressedZone != zone)
            {
                return;
            }

            Rectangle area = GetZoneBounds(zone);
            area.Inflate(-4, -4);
            using (GraphicsPath path = DrawingPaths.RoundedRectangle(area, area.Height / 2))
            using (SolidBrush brush = new SolidBrush(palette.SurfaceRaised))
            {
                graphics.FillPath(brush, path);
            }
        }

        private void DrawArrow(Graphics graphics, bool left, float opacity)
        {
            Rectangle zone = GetZoneBounds(left ? MiniHitZone.Left : MiniHitZone.Right);
            int centerX = zone.Left + zone.Width / 2;
            int centerY = ClientSize.Height / 2;
            int horizontal = ScaleLogical(4);
            int vertical = ScaleLogical(6);
            int tipX = centerX + (left ? -horizontal : horizontal);
            int tailX = centerX + (left ? horizontal : -horizontal);
            using (Pen pen = new Pen(
                WithAlpha(palette.Text, opacity),
                Math.Max(1.5F, ScaleLogical(2))))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                graphics.DrawLine(
                    pen,
                    tailX,
                    centerY - vertical,
                    tipX,
                    centerY);
                graphics.DrawLine(
                    pen,
                    tipX,
                    centerY,
                    tailX,
                    centerY + vertical);
            }
        }

        private void DrawEdgeHandle(Graphics graphics, bool dockedLeft, float opacity)
        {
            int handleWidth = Math.Min(ScaleLogical(EdgeWidth), ClientSize.Width);
            Rectangle handle = new Rectangle(
                dockedLeft ? 0 : ClientSize.Width - handleWidth,
                0,
                handleWidth,
                ClientSize.Height);
            int centerX = handle.Left + handle.Width / 2 + (dockedLeft ? 1 : -1);
            int centerY = handle.Top + handle.Height / 2;
            int horizontal = ScaleLogical(3);
            int vertical = ScaleLogical(5);
            bool pointsRight = dockedLeft;
            int tipX = centerX + (pointsRight ? horizontal : -horizontal);
            int tailX = centerX + (pointsRight ? -horizontal : horizontal);
            using (Pen glyph = new Pen(
                WithAlpha(Color.White, opacity),
                Math.Max(1.5F, ScaleLogical(2))))
            {
                glyph.StartCap = LineCap.Round;
                glyph.EndCap = LineCap.Round;
                graphics.DrawLine(glyph, tailX, centerY - vertical, tipX, centerY);
                graphics.DrawLine(glyph, tipX, centerY, tailX, centerY + vertical);
            }
        }

        private void DrawEdgeSessionIndicator(
            Graphics graphics,
            bool dockedLeft,
            float opacity)
        {
            if (sessionPhase == KeepActivePhase.Inactive)
            {
                return;
            }

            int handleWidth = Math.Min(ScaleLogical(EdgeWidth), ClientSize.Width);
            int handleLeft = dockedLeft ? 0 : ClientSize.Width - handleWidth;
            int size = ScaleLogical(5);
            Rectangle dot = new Rectangle(
                handleLeft + (handleWidth - size) / 2,
                ScaleLogical(7),
                size,
                size);
            DrawStatusDot(graphics, dot, opacity);
        }

        private void DrawExpandedSessionIndicator(
            Graphics graphics,
            float opacity,
            Rectangle markBounds)
        {
            if (sessionPhase == KeepActivePhase.Inactive
                || markBounds.Left < 0
                || markBounds.Right > ClientSize.Width)
            {
                return;
            }

            int size = ScaleLogical(7);
            Rectangle dot = new Rectangle(
                markBounds.Right - size + ScaleLogical(1),
                markBounds.Bottom - size + ScaleLogical(1),
                size,
                size);
            DrawStatusDot(graphics, dot, opacity);
        }

        private void DrawStatusDot(Graphics graphics, Rectangle bounds, float opacity)
        {
            Color color = GetSessionStatusColor();
            if (pulseKind == MiniPulseKind.Attention && attentionPulse > 0D)
            {
                color = BlendColor(color, Color.White, (float)attentionPulse * 0.24F);
            }

            using (SolidBrush fill = new SolidBrush(WithAlpha(color, opacity)))
            using (Pen ring = new Pen(
                WithAlpha(Color.White, opacity * 0.9F),
                Math.Max(1F, ScaleLogical(1))))
            {
                graphics.FillEllipse(fill, bounds);
                graphics.DrawEllipse(ring, bounds);
            }
        }

        private Color GetSessionStatusColor()
        {
            if (sessionPhase == KeepActivePhase.EndingSoon)
            {
                return palette.Warning;
            }

            if (sessionPhase == KeepActivePhase.Critical)
            {
                return palette.Critical;
            }

            if (sessionPhase == KeepActivePhase.Completed)
            {
                return palette.Danger;
            }

            return palette.Active;
        }

        private string GetSessionTooltip()
        {
            if (sessionPhase == KeepActivePhase.Active)
            {
                return "Raudo Mini · Pulso encendido";
            }

            if (sessionPhase == KeepActivePhase.EndingSoon)
            {
                return "Raudo Mini · Quedan 15 minutos o menos";
            }

            if (sessionPhase == KeepActivePhase.Critical)
            {
                return "Raudo Mini · Quedan 5 minutos o menos";
            }

            if (sessionPhase == KeepActivePhase.Completed)
            {
                return "Raudo Mini · El tiempo terminó";
            }

            return "Raudo Mini · Inactivo";
        }

        private Rectangle GetZoneBounds(MiniHitZone zone)
        {
            int arrowWidth = ScaleLogical(ArrowZoneWidth);
            int centerWidth = ScaleLogical(CenterZoneWidth);
            bool dockedLeft = IsDockedLeft(
                dockAnchor,
                Screen.FromPoint(dockAnchor).WorkingArea);
            int contentOffset = dockedLeft ? 0 : ClientSize.Width - GetExpandedWidth();
            int centerLeft = contentOffset + (canNavigateLeft ? arrowWidth : 0);
            if (zone == MiniHitZone.Left)
            {
                return new Rectangle(contentOffset, 0, arrowWidth, ClientSize.Height);
            }

            if (zone == MiniHitZone.Right)
            {
                return new Rectangle(
                    centerLeft + centerWidth,
                    0,
                    arrowWidth,
                    ClientSize.Height);
            }

            return new Rectangle(centerLeft, 0, centerWidth, ClientSize.Height);
        }

        private int GetExpandedWidth()
        {
            return ScaleLogical(
                CenterZoneWidth
                    + (canNavigateLeft ? ArrowZoneWidth : 0)
                    + (canNavigateRight ? ArrowZoneWidth : 0));
        }

        private void LayoutAtAnchor()
        {
            dockAnchor = ClampDockAnchor(dockAnchor);
            Screen screen = Screen.FromPoint(dockAnchor);
            Rectangle area = screen.WorkingArea;
            bool dockedLeft = IsDockedLeft(dockAnchor, area);
            Size requested = GetRequestedSize();
            int x = dockedLeft
                ? area.Left
                : area.Right - requested.Width;

            int y = Math.Max(
                area.Top + ScaleLogical(4),
                Math.Min(
                    area.Bottom - requested.Height - ScaleLogical(4),
                    dockAnchor.Y - requested.Height / 2));
            if (IsHandleCreated)
            {
                DesktopNativeMethods.SetWindowPos(
                    Handle,
                    IntPtr.Zero,
                    x,
                    y,
                    requested.Width,
                    requested.Height,
                    SetWindowPosNoZOrder | SetWindowPosNoActivate);
            }
            else
            {
                Location = new Point(x, y);
            }
        }

        private static Point ClampDockAnchor(Point anchor)
        {
            Screen screen = Screen.FromPoint(anchor);
            Rectangle area = screen.WorkingArea;
            bool dockedLeft = IsDockedLeft(anchor, area);
            int yMargin = ControlHeight / 2 + 4;
            return new Point(
                dockedLeft ? area.Left : area.Right - 1,
                Math.Max(
                    area.Top + yMargin,
                    Math.Min(area.Bottom - yMargin, anchor.Y)));
        }

        private static Point GetDockAnchor(Point cursor)
        {
            Screen screen = Screen.FromPoint(cursor);
            Rectangle area = screen.WorkingArea;
            return new Point(
                Math.Abs(cursor.X - area.Left) <= Math.Abs(cursor.X - (area.Right - 1))
                    ? area.Left
                    : area.Right - 1,
                cursor.Y);
        }

        private static Point ClampDragLocation(Point requested, Size size)
        {
            Screen screen = Screen.FromPoint(new Point(
                requested.X + size.Width / 2,
                requested.Y + size.Height / 2));
            Rectangle area = screen.WorkingArea;
            return new Point(
                Math.Max(area.Left + 4, Math.Min(area.Right - size.Width - 4, requested.X)),
                Math.Max(area.Top + 4, Math.Min(area.Bottom - size.Height - 4, requested.Y)));
        }

        private static Point GetInitialDockAnchor(RaudoSettings settings)
        {
            if (settings.MiniCenterX >= 0 && settings.MiniCenterY >= 0)
            {
                return ClampDockAnchor(new Point(
                    settings.MiniCenterX,
                    settings.MiniCenterY));
            }

            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            return new Point(area.Right - 1, area.Bottom - 72);
        }

        private static bool IsDockedLeft(Point anchor, Rectangle area)
        {
            return Math.Abs(anchor.X - area.Left)
                <= Math.Abs(anchor.X - (area.Right - 1));
        }

        private void UpdateWindowRegion()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
            {
                return;
            }

            bool dockedLeft = IsDockedLeft(
                dockAnchor,
                Screen.FromPoint(dockAnchor).WorkingArea);
            using (GraphicsPath path = CreateContainerPath(
                new Rectangle(0, 0, ClientSize.Width, ClientSize.Height),
                dockedLeft,
                (float)revealProgress))
            {
                Region previous = Region;
                Region = new Region(path);
                if (previous != null)
                {
                    previous.Dispose();
                }
            }
        }

        private GraphicsPath CreateContainerPath(
            Rectangle bounds,
            bool dockedLeft,
            float progress)
        {
            float expandedRadius = ScaleLogical(12);
            float innerRadius = ScaleLogical(10) + (ScaleLogical(2) * progress);
            float screenRadius = expandedRadius * progress;
            float topLeft = dockedLeft ? screenRadius : innerRadius;
            float bottomLeft = topLeft;
            float topRight = dockedLeft ? innerRadius : screenRadius;
            float bottomRight = topRight;
            return DrawingPaths.RoundedRectangle(
                bounds,
                topLeft,
                topRight,
                bottomRight,
                bottomLeft);
        }

        private int ScaleLogical(int logicalPixels)
        {
            int dpi = IsHandleCreated ? DeviceDpi : 96;
            return Math.Max(1, (logicalPixels * dpi + 48) / 96);
        }

        private static Color BlendColor(Color from, Color to, float amount)
        {
            float value = Math.Max(0F, Math.Min(1F, amount));
            return Color.FromArgb(
                (int)Math.Round(from.A + ((to.A - from.A) * value)),
                (int)Math.Round(from.R + ((to.R - from.R) * value)),
                (int)Math.Round(from.G + ((to.G - from.G) * value)),
                (int)Math.Round(from.B + ((to.B - from.B) * value)));
        }

        private static Color WithAlpha(Color color, float opacity)
        {
            return Color.FromArgb(
                (int)Math.Round(color.A * Math.Max(0F, Math.Min(1F, opacity))),
                color.R,
                color.G,
                color.B);
        }

        private static float Remap(float value, float start, float end)
        {
            if (end <= start)
            {
                return value >= end ? 1F : 0F;
            }

            return Math.Max(0F, Math.Min(1F, (value - start) / (end - start)));
        }

        private enum MiniHitZone
        {
            None,
            Left,
            Center,
            Right
        }

        private enum MiniPulseKind
        {
            None,
            Attention,
            Landing
        }
    }

    internal static class MiniMotion
    {
        public const int RevealIntentDelayMilliseconds = 80;
        public const int CollapseDelayMilliseconds = 1400;
        public const int TransitionDurationMilliseconds = 167;
        public const int PresenceTransitionDurationMilliseconds = 180;
        public const int FrameIntervalMilliseconds = 15;

        public static double EaseReveal(double progress)
        {
            double value = Math.Max(0D, Math.Min(1D, progress));
            double inverse = 1D - value;
            return 1D - (inverse * inverse * inverse);
        }

        public static double EaseHide(double progress)
        {
            double value = Math.Max(0D, Math.Min(1D, progress));
            return value * value * value;
        }
    }
}
