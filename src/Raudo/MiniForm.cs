using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
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
        private const int TrackZoneWidth = 40;
        private const int MoreZoneWidth = 40;
        private const uint SetWindowPosNoZOrder = 0x0004;
        private const uint SetWindowPosNoActivate = 0x0010;

        private readonly VirtualDesktopService desktopService;
        private readonly IMediaSessionService mediaSessionService;
        private readonly bool ownsMediaSessionService;
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
        private bool mediaCommandPending;
        private bool menuOpening;
        private int testingDpi;

        public MiniForm(VirtualDesktopService service, RaudoSettings settings)
            : this(service, settings, new MediaControlService(), null)
        {
        }

        internal MiniForm(
            VirtualDesktopService service,
            RaudoSettings settings,
            MediaControlService mediaControlService,
            IMediaSessionService sessionService)
        {
            if (service == null)
            {
                throw new ArgumentNullException("service");
            }
            if (mediaControlService == null)
            {
                throw new ArgumentNullException("mediaControlService");
            }

            desktopService = service;
            if (sessionService == null)
            {
                mediaSessionService = new MediaSessionService(mediaControlService);
                ownsMediaSessionService = true;
            }
            else
            {
                mediaSessionService = sessionService;
                ownsMediaSessionService = false;
            }

            Text = "Raudo Mini";
            AccessibleName = "Modo Mini de Raudo";
            AccessibleDescription = "Controla reproducción, navega entre escritorios y abre opciones de Raudo.";
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

        internal void RefreshMediaStateForTesting()
        {
            ApplyMediaSelectionChange();
        }

        internal void ExecuteMediaCommandForTesting(MediaCommand command)
        {
            ExecuteMediaCommand(command);
        }

        internal void SetDpiForTesting(int dpi)
        {
            testingDpi = Math.Max(96, dpi);
            ApplyRevealProgress(revealProgress);
        }

        internal IList<string> BuildMenuLabelsForTesting(MediaSessionSnapshot snapshot)
        {
            BuildWindowMenu(snapshot);
            List<string> labels = new List<string>();
            foreach (ToolStripItem item in windowMenu.Items)
            {
                if (!string.IsNullOrWhiteSpace(item.Text))
                {
                    labels.Add(item.Text);
                }
            }
            return labels;
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

            if (canNavigateLeft
                && GetZoneBounds(MiniHitZone.DesktopLeft).Right <= ClientSize.Width)
            {
                DrawHitZone(eventArgs.Graphics, MiniHitZone.DesktopLeft);
                DrawArrow(eventArgs.Graphics, true, contentOpacity);
            }

            if (mediaSessionService.CanPrevious
                && GetZoneBounds(MiniHitZone.MediaPrevious).Right <= ClientSize.Width)
            {
                DrawHitZone(eventArgs.Graphics, MiniHitZone.MediaPrevious);
                DrawMediaGlyph(
                    eventArgs.Graphics,
                    MiniHitZone.MediaPrevious,
                    RaudoActionGlyph.MediaPrevious,
                    contentOpacity);
            }

            int markSize = ScaleLogical(32);
            Rectangle centerBounds = GetZoneBounds(MiniHitZone.MediaPlayPause);
            Rectangle markBounds = new Rectangle(
                centerBounds.Left + (centerBounds.Width - markSize) / 2,
                (ClientSize.Height - markSize) / 2,
                markSize,
                markSize);
            if (markBounds.Right <= ClientSize.Width)
            {
                DrawHitZone(eventArgs.Graphics, MiniHitZone.MediaPlayPause);
                DrawPrimaryMediaControl(
                    eventArgs.Graphics,
                    markBounds,
                    accent,
                    contentOpacity,
                    progress);
            }

            if (mediaSessionService.CanNext
                && GetZoneBounds(MiniHitZone.MediaNext).Right <= ClientSize.Width)
            {
                DrawHitZone(eventArgs.Graphics, MiniHitZone.MediaNext);
                DrawMediaGlyph(
                    eventArgs.Graphics,
                    MiniHitZone.MediaNext,
                    RaudoActionGlyph.MediaNext,
                    contentOpacity);
            }

            if (GetZoneBounds(MiniHitZone.More).Right <= ClientSize.Width)
            {
                DrawHitZone(eventArgs.Graphics, MiniHitZone.More);
                DrawMoreGlyph(eventArgs.Graphics, contentOpacity);
                DrawSelectedSourceIndicator(eventArgs.Graphics, contentOpacity);
            }

            if (canNavigateRight
                && GetZoneBounds(MiniHitZone.DesktopRight).Right <= ClientSize.Width)
            {
                DrawHitZone(eventArgs.Graphics, MiniHitZone.DesktopRight);
                DrawArrow(eventArgs.Graphics, false, contentOpacity);
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
                && (pressedZone == MiniHitZone.Edge
                    || pressedZone == MiniHitZone.MediaPlayPause))
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
                toolTip.SetToolTip(this, GetZoneTooltip(nextZone));
                Invalidate();
            }

            Cursor = Cursors.Hand;
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
                if (releasedZone == MiniHitZone.Edge)
                {
                    SetExpanded(true);
                }
                else if (releasedZone == MiniHitZone.DesktopLeft)
                {
                    SwitchDesktop(DesktopDirection.Left);
                }
                else if (releasedZone == MiniHitZone.DesktopRight)
                {
                    SwitchDesktop(DesktopDirection.Right);
                }
                else if (releasedZone == MiniHitZone.MediaPrevious)
                {
                    ExecuteMediaCommand(MediaCommand.PreviousTrack);
                }
                else if (releasedZone == MiniHitZone.MediaPlayPause)
                {
                    ExecuteMediaCommand(MediaCommand.TogglePlayPause);
                }
                else if (releasedZone == MiniHitZone.MediaNext)
                {
                    ExecuteMediaCommand(MediaCommand.NextTrack);
                }
                else if (releasedZone == MiniHitZone.More)
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
            if (eventArgs.Control && eventArgs.KeyCode == Keys.Left)
            {
                if (mediaSessionService.CanPrevious)
                {
                    ExecuteMediaCommand(MediaCommand.PreviousTrack);
                }
                eventArgs.Handled = true;
            }
            else if (eventArgs.Control && eventArgs.KeyCode == Keys.Right)
            {
                if (mediaSessionService.CanNext)
                {
                    ExecuteMediaCommand(MediaCommand.NextTrack);
                }
                eventArgs.Handled = true;
            }
            else if (eventArgs.KeyCode == Keys.Left)
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
            else if (eventArgs.KeyCode == Keys.Space)
            {
                ExecuteMediaCommand(MediaCommand.TogglePlayPause);
                eventArgs.Handled = true;
            }
            else if (eventArgs.KeyCode == Keys.Enter
                || eventArgs.KeyCode == Keys.Apps
                || (eventArgs.Shift && eventArgs.KeyCode == Keys.F10))
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
                if (ownsMediaSessionService)
                {
                    mediaSessionService.Dispose();
                }
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

        private async void ShowWindowMenu()
        {
            if (menuOpening || windowMenu.Visible || IsDisposed || Disposing)
            {
                return;
            }

            menuOpening = true;
            SetExpanded(true, false);
            collapseTimer.Stop();
            Cursor = Cursors.WaitCursor;
            try
            {
                MediaSessionSnapshot snapshot = await mediaSessionService.GetSnapshotAsync();
                if (IsDisposed || Disposing)
                {
                    return;
                }

                BuildWindowMenu(snapshot);
                windowMenu.Show(this, new Point(ClientSize.Width / 2, ClientSize.Height));
                UpdateWindowOpacity();
            }
            finally
            {
                menuOpening = false;
                Cursor = Cursors.Hand;
                Invalidate();
            }
        }

        private void BuildWindowMenu(MediaSessionSnapshot snapshot)
        {
            ClearWindowMenuItems();

            ToolStripMenuItem mediaHeader = new ToolStripMenuItem("Reproductor");
            mediaHeader.Enabled = false;
            mediaHeader.Font = windowMenuHeaderFont;
            windowMenu.Items.Add(mediaHeader);

            ToolStripMenuItem automatic = new ToolStripMenuItem("Automático de Windows");
            automatic.Checked = !mediaSessionService.HasSelectedSession;
            automatic.ToolTipText = "Windows decide qué reproductor recibe el comando";
            automatic.Click += AutomaticSessionItemClick;
            windowMenu.Items.Add(automatic);

            if (snapshot.IsAvailable && snapshot.Sessions.Count > 0)
            {
                for (int index = 0; index < snapshot.Sessions.Count; index++)
                {
                    MediaSessionDescriptor descriptor = snapshot.Sessions[index];
                    string label = descriptor.MenuLabel
                        + (descriptor.IsCurrent ? " · actual" : string.Empty);
                    ToolStripMenuItem sessionItem = new ToolStripMenuItem(label);
                    sessionItem.Tag = descriptor.Id;
                    sessionItem.Checked = descriptor.IsSelected;
                    sessionItem.Enabled = descriptor.CanPlayPause;
                    sessionItem.ToolTipText = "Controlar esta sesión multimedia";
                    sessionItem.Click += MediaSessionItemClick;
                    windowMenu.Items.Add(sessionItem);
                }
            }
            else
            {
                ToolStripMenuItem unavailable = new ToolStripMenuItem(
                    snapshot.IsAvailable
                        ? "No hay reproductores disponibles"
                        : "Selección de reproductor no disponible");
                unavailable.Enabled = false;
                unavailable.ToolTipText = snapshot.Error;
                windowMenu.Items.Add(unavailable);
            }

            ToolStripMenuItem volume = new ToolStripMenuItem("Volumen de Windows");
            AddMiniMediaMenuItem(volume, "Silenciar o restaurar audio", MediaCommand.ToggleMute);
            volume.DropDownItems.Add(new ToolStripSeparator());
            AddMiniMediaMenuItem(volume, "Bajar volumen", MediaCommand.VolumeDown);
            AddMiniMediaMenuItem(volume, "Subir volumen", MediaCommand.VolumeUp);
            windowMenu.Items.Add(volume);

            windowMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem windowsMenu = new ToolStripMenuItem("Traer ventana");
            BuildWindowItems(windowsMenu);
            windowMenu.Items.Add(windowsMenu);

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

        private void BuildWindowItems(ToolStripMenuItem windowsMenu)
        {
            IList<DesktopWindow> windows;
            string error;
            if (!desktopService.TryGetWindowsOutsideCurrentDesktop(out windows, out error))
            {
                AddDisabledSubmenuItem(windowsMenu, error);
                return;
            }
            if (!desktopService.CanBringWindows)
            {
                AddDisabledSubmenuItem(
                    windowsMenu,
                    "Esta versión de Windows requiere usar Win + Tab");
                return;
            }
            if (windows.Count == 0)
            {
                AddDisabledSubmenuItem(windowsMenu, "No hay ventanas para traer");
                return;
            }

            int count = Math.Min(15, windows.Count);
            for (int index = 0; index < count; index++)
            {
                DesktopWindow candidate = windows[index];
                ToolStripMenuItem item = new ToolStripMenuItem(candidate.DisplayName);
                item.Tag = candidate.Handle;
                item.ToolTipText = "Traer al escritorio actual";
                item.Click += WindowItemClick;
                windowsMenu.DropDownItems.Add(item);
            }

            if (windows.Count > count)
            {
                AddDisabledSubmenuItem(
                    windowsMenu,
                    string.Format("{0} ventanas más no mostradas", windows.Count - count));
            }
        }

        private static void AddDisabledSubmenuItem(
            ToolStripMenuItem parent,
            string label)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label);
            item.Enabled = false;
            parent.DropDownItems.Add(item);
        }

        private void AddMiniMediaMenuItem(
            ToolStripMenuItem parent,
            string label,
            MediaCommand command)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label);
            item.Tag = command;
            item.Click += MiniMediaMenuItemClick;
            parent.DropDownItems.Add(item);
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

        private void AutomaticSessionItemClick(object sender, EventArgs eventArgs)
        {
            mediaSessionService.SelectAutomatic();
            ApplyMediaSelectionChange();
        }

        private void MediaSessionItemClick(object sender, EventArgs eventArgs)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            string id = item == null ? null : item.Tag as string;
            if (!string.IsNullOrWhiteSpace(id) && mediaSessionService.TrySelect(id))
            {
                ApplyMediaSelectionChange();
            }
        }

        private void MiniMediaMenuItemClick(object sender, EventArgs eventArgs)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item != null && item.Tag is MediaCommand)
            {
                ExecuteMediaCommand((MediaCommand)item.Tag);
            }
        }

        private void ApplyMediaSelectionChange()
        {
            if (expanded)
            {
                ApplyRevealProgress(revealProgress);
            }
            toolTip.SetToolTip(this, GetZoneTooltip(hoverZone));
            Invalidate();
        }

        private async void ExecuteMediaCommand(MediaCommand command)
        {
            if (mediaCommandPending || IsDisposed || Disposing)
            {
                return;
            }

            mediaCommandPending = true;
            Invalidate();
            try
            {
                string error = await mediaSessionService.TryExecuteAsync(command);
                if (!string.IsNullOrWhiteSpace(error) && !IsDisposed && !Disposing)
                {
                    toolTip.Show(
                        error,
                        this,
                        new Point(ClientSize.Width / 2, ClientSize.Height),
                        3200);
                }
            }
            finally
            {
                mediaCommandPending = false;
                if (!IsDisposed && !Disposing)
                {
                    Invalidate();
                }
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
                return MiniHitZone.Edge;
            }

            if (canNavigateLeft
                && GetZoneBounds(MiniHitZone.DesktopLeft).Contains(point))
            {
                return MiniHitZone.DesktopLeft;
            }

            if (mediaSessionService.CanPrevious
                && GetZoneBounds(MiniHitZone.MediaPrevious).Contains(point))
            {
                return MiniHitZone.MediaPrevious;
            }

            if (GetZoneBounds(MiniHitZone.MediaPlayPause).Contains(point))
            {
                return MiniHitZone.MediaPlayPause;
            }

            if (mediaSessionService.CanNext
                && GetZoneBounds(MiniHitZone.MediaNext).Contains(point))
            {
                return MiniHitZone.MediaNext;
            }

            if (GetZoneBounds(MiniHitZone.More).Contains(point))
            {
                return MiniHitZone.More;
            }

            if (canNavigateRight
                && GetZoneBounds(MiniHitZone.DesktopRight).Contains(point))
            {
                return MiniHitZone.DesktopRight;
            }

            return MiniHitZone.None;
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

        private void DrawMediaGlyph(
            Graphics graphics,
            MiniHitZone zone,
            RaudoActionGlyph glyph,
            float opacity)
        {
            Rectangle area = GetZoneBounds(zone);
            int size = ScaleLogical(20);
            Rectangle glyphBounds = new Rectangle(
                area.Left + (area.Width - size) / 2,
                area.Top + (area.Height - size) / 2,
                size,
                size);
            RaudoActionGlyphDrawing.Draw(
                graphics,
                glyphBounds,
                glyph,
                WithAlpha(palette.TextMuted, opacity));
        }

        private void DrawPrimaryMediaControl(
            Graphics graphics,
            Rectangle bounds,
            Color accent,
            float opacity,
            float progress)
        {
            float mediaOpacity = Remap(progress, 0.62F, 1F);
            float brandOpacity = opacity * (1F - mediaOpacity);
            if (brandOpacity > 0.01F)
            {
                BrandDrawing.DrawMark(
                    graphics,
                    bounds,
                    WithAlpha(accent, brandOpacity),
                    WithAlpha(Color.White, brandOpacity));
            }

            if (mediaOpacity <= 0F)
            {
                return;
            }

            Color buttonColor = mediaCommandPending
                ? BlendColor(accent, palette.SurfaceRaised, 0.24F)
                : accent;
            using (GraphicsPath tile = DrawingPaths.RoundedRectangle(
                bounds,
                Math.Max(2, bounds.Width / 4)))
            using (SolidBrush fill = new SolidBrush(
                WithAlpha(buttonColor, opacity * mediaOpacity)))
            {
                graphics.FillPath(fill, tile);
            }

            Rectangle glyphBounds = bounds;
            glyphBounds.Inflate(-ScaleLogical(6), -ScaleLogical(6));
            RaudoActionGlyphDrawing.Draw(
                graphics,
                glyphBounds,
                RaudoActionGlyph.MediaPlayPause,
                WithAlpha(Color.White, opacity * mediaOpacity));
        }

        private void DrawMoreGlyph(Graphics graphics, float opacity)
        {
            Rectangle zone = GetZoneBounds(MiniHitZone.More);
            int dotSize = Math.Max(2, ScaleLogical(2));
            int gap = ScaleLogical(5);
            int totalWidth = (dotSize * 3) + (gap * 2);
            int left = zone.Left + (zone.Width - totalWidth) / 2;
            int top = zone.Top + (zone.Height - dotSize) / 2;
            using (SolidBrush brush = new SolidBrush(WithAlpha(palette.TextMuted, opacity)))
            {
                for (int index = 0; index < 3; index++)
                {
                    graphics.FillEllipse(
                        brush,
                        left + (index * (dotSize + gap)),
                        top,
                        dotSize,
                        dotSize);
                }
            }
        }

        private void DrawSelectedSourceIndicator(Graphics graphics, float opacity)
        {
            if (!mediaSessionService.HasSelectedSession)
            {
                return;
            }

            Rectangle zone = GetZoneBounds(MiniHitZone.More);
            int size = ScaleLogical(5);
            Rectangle dot = new Rectangle(
                zone.Right - ScaleLogical(9),
                zone.Top + ScaleLogical(8),
                size,
                size);
            using (SolidBrush brush = new SolidBrush(
                WithAlpha(palette.Active, opacity)))
            {
                graphics.FillEllipse(brush, dot);
            }
        }

        private string GetZoneTooltip(MiniHitZone zone)
        {
            switch (zone)
            {
                case MiniHitZone.Edge:
                    return "Mostrar controles de Raudo";
                case MiniHitZone.DesktopLeft:
                    return "Escritorio anterior";
                case MiniHitZone.MediaPrevious:
                    return "Pista anterior";
                case MiniHitZone.MediaPlayPause:
                    return mediaSessionService.HasSelectedSession
                        ? "Reproducir o pausar · "
                            + mediaSessionService.SelectedDisplayName
                            + " · arrastra para mover"
                        : "Reproducir o pausar · arrastra para mover";
                case MiniHitZone.MediaNext:
                    return "Pista siguiente";
                case MiniHitZone.More:
                    return "Elegir reproductor y abrir opciones";
                case MiniHitZone.DesktopRight:
                    return "Escritorio siguiente";
                default:
                    return GetSessionTooltip();
            }
        }

        private void DrawArrow(Graphics graphics, bool left, float opacity)
        {
            Rectangle zone = GetZoneBounds(
                left ? MiniHitZone.DesktopLeft : MiniHitZone.DesktopRight);
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
            int trackWidth = ScaleLogical(TrackZoneWidth);
            int moreWidth = ScaleLogical(MoreZoneWidth);
            bool dockedLeft = IsDockedLeft(
                dockAnchor,
                Screen.FromPoint(dockAnchor).WorkingArea);
            int contentOffset = dockedLeft ? 0 : ClientSize.Width - GetExpandedWidth();
            int cursor = contentOffset;
            if (zone == MiniHitZone.DesktopLeft)
            {
                return new Rectangle(cursor, 0, arrowWidth, ClientSize.Height);
            }
            if (canNavigateLeft)
            {
                cursor += arrowWidth;
            }

            if (zone == MiniHitZone.MediaPrevious)
            {
                return new Rectangle(cursor, 0, trackWidth, ClientSize.Height);
            }
            if (mediaSessionService.CanPrevious)
            {
                cursor += trackWidth;
            }

            if (zone == MiniHitZone.MediaPlayPause)
            {
                return new Rectangle(cursor, 0, centerWidth, ClientSize.Height);
            }
            cursor += centerWidth;

            if (zone == MiniHitZone.MediaNext)
            {
                return new Rectangle(cursor, 0, trackWidth, ClientSize.Height);
            }
            if (mediaSessionService.CanNext)
            {
                cursor += trackWidth;
            }

            if (zone == MiniHitZone.More)
            {
                return new Rectangle(cursor, 0, moreWidth, ClientSize.Height);
            }
            cursor += moreWidth;

            if (zone == MiniHitZone.DesktopRight)
            {
                return new Rectangle(cursor, 0, arrowWidth, ClientSize.Height);
            }

            return Rectangle.Empty;
        }

        private int GetExpandedWidth()
        {
            return ScaleLogical(
                CenterZoneWidth
                    + (mediaSessionService.CanPrevious ? TrackZoneWidth : 0)
                    + (mediaSessionService.CanNext ? TrackZoneWidth : 0)
                    + MoreZoneWidth
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
            int dpi = testingDpi > 0
                ? testingDpi
                : (IsHandleCreated ? DeviceDpi : 96);
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
            Edge,
            DesktopLeft,
            MediaPrevious,
            MediaPlayPause,
            MediaNext,
            More,
            DesktopRight
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
