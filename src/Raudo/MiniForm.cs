using System;
using System.Collections.Generic;
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
        private const int CollapsedWidth = 18;
        private const int CollapsedHeight = 44;
        private const int CollapsedHiddenOffset = 6;
        private const int ExpandedHeight = 52;
        private const int ArrowZoneWidth = 52;
        private const int CenterZoneWidth = 52;
        private const uint SetWindowPosNoZOrder = 0x0004;
        private const uint SetWindowPosNoActivate = 0x0010;

        private readonly VirtualDesktopService desktopService;
        private readonly ContextMenuStrip windowMenu;
        private readonly Font windowMenuHeaderFont;
        private readonly Timer collapseTimer;
        private readonly Timer navigationRefreshTimer;
        private readonly ToolTip toolTip;

        private ThemePalette palette;
        private Point dockAnchor;
        private Point dragStartCursor;
        private Point dragStartLocation;
        private MiniHitZone hoverZone;
        private MiniHitZone pressedZone;
        private bool expanded;
        private bool dragging;
        private bool active;
        private bool allowClose;
        private bool canNavigateLeft = true;
        private bool canNavigateRight = true;
        private bool followsActiveDesktop;

        public MiniForm(VirtualDesktopService service, RaudoSettings settings)
        {
            desktopService = service;

            Text = "Raudo Mini";
            AccessibleName = "Modo Mini de Raudo";
            AccessibleDescription = "Navega entre escritorios y trae ventanas al escritorio actual.";
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(CollapsedWidth, CollapsedHeight);
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
            windowMenu.Closed += delegate { ScheduleCollapse(); };

            collapseTimer = new Timer();
            collapseTimer.Interval = 350;
            collapseTimer.Tick += CollapseTimerTick;

            navigationRefreshTimer = new Timer();
            navigationRefreshTimer.Interval = 450;
            navigationRefreshTimer.Tick += NavigationRefreshTimerTick;

            toolTip = new ToolTip();
            toolTip.InitialDelay = 350;
            toolTip.ReshowDelay = 100;
            toolTip.SetToolTip(this, "Raudo Mini");

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

        public void SetActive(bool isActive)
        {
            if (active != isActive)
            {
                active = isActive;
                Invalidate();
            }
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
            collapseTimer.Stop();
            navigationRefreshTimer.Stop();
            Close();
        }

        internal void SetExpandedForTesting(bool shouldExpand)
        {
            SetExpanded(shouldExpand);
        }

        internal void SetNavigationAvailabilityForTesting(bool left, bool right)
        {
            canNavigateLeft = left;
            canNavigateRight = right;
            UpdateClientSizeForState();
            LayoutAtAnchor();
        }

        protected override void OnHandleCreated(EventArgs eventArgs)
        {
            base.OnHandleCreated(eventArgs);
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

            Rectangle bounds = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            using (GraphicsPath path = DrawingPaths.RoundedRectangle(bounds, ClientSize.Height / 2))
            using (SolidBrush background = new SolidBrush(palette.Surface))
            using (Pen border = new Pen(palette.Border, 1F))
            {
                eventArgs.Graphics.FillPath(background, path);
                eventArgs.Graphics.DrawPath(border, path);
            }

            if (!expanded)
            {
                DrawDockHandle(eventArgs.Graphics);
                return;
            }

            if (canNavigateLeft)
            {
                DrawHitZone(eventArgs.Graphics, MiniHitZone.Left);
                DrawArrow(eventArgs.Graphics, true);
            }

            if (canNavigateRight)
            {
                DrawHitZone(eventArgs.Graphics, MiniHitZone.Right);
                DrawArrow(eventArgs.Graphics, false);
            }

            int markSize = 34;
            Rectangle centerBounds = GetZoneBounds(MiniHitZone.Center);
            Rectangle markBounds = new Rectangle(
                centerBounds.Left + (centerBounds.Width - markSize) / 2,
                (ClientSize.Height - markSize) / 2,
                markSize,
                markSize);
            BrandDrawing.DrawMark(
                eventArgs.Graphics,
                markBounds,
                active ? palette.Active : palette.Primary,
                Color.White);
        }

        protected override void OnMouseEnter(EventArgs eventArgs)
        {
            base.OnMouseEnter(eventArgs);
            collapseTimer.Stop();
            RefreshNavigationAvailability();
            SetExpanded(true);
        }

        protected override void OnMouseLeave(EventArgs eventArgs)
        {
            base.OnMouseLeave(eventArgs);
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

            Cursor = nextZone == MiniHitZone.Center ? Cursors.SizeAll : Cursors.Hand;
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
                collapseTimer.Dispose();
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
            collapseTimer.Stop();
            BuildWindowMenu();
            windowMenu.Show(this, new Point(ClientSize.Width / 2, ClientSize.Height));
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
            if (expanded == shouldExpand)
            {
                UpdateClientSizeForState();
                return;
            }

            expanded = shouldExpand;
            UpdateClientSizeForState();
            LayoutAtAnchor();
            Invalidate();
        }

        private void UpdateClientSizeForState()
        {
            Size requested = GetRequestedSize();
            if (ClientSize != requested)
            {
                ClientSize = requested;
            }
        }

        private Size GetRequestedSize()
        {
            return expanded
                ? new Size(
                    CenterZoneWidth
                        + (canNavigateLeft ? ArrowZoneWidth : 0)
                        + (canNavigateRight ? ArrowZoneWidth : 0),
                    ExpandedHeight)
                : new Size(CollapsedWidth, CollapsedHeight);
        }

        private void ScheduleCollapse()
        {
            collapseTimer.Stop();
            collapseTimer.Start();
        }

        private void CollapseTimerTick(object sender, EventArgs eventArgs)
        {
            if (!windowMenu.Visible && !Bounds.Contains(Cursor.Position))
            {
                collapseTimer.Stop();
                SetExpanded(false);
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
                UpdateClientSizeForState();
                LayoutAtAnchor();
            }

            Invalidate();
        }

        private MiniHitZone HitTest(Point point)
        {
            if (!expanded)
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

        private void DrawArrow(Graphics graphics, bool left)
        {
            Rectangle zone = GetZoneBounds(left ? MiniHitZone.Left : MiniHitZone.Right);
            int centerX = zone.Left + zone.Width / 2;
            int centerY = ClientSize.Height / 2;
            int tipX = centerX + (left ? -3 : 3);
            int tailX = centerX + (left ? 3 : -3);
            using (Pen pen = new Pen(palette.Text, 2F))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                graphics.DrawLine(
                    pen,
                    tailX,
                    centerY - 6,
                    tipX,
                    centerY);
                graphics.DrawLine(
                    pen,
                    tipX,
                    centerY,
                    tailX,
                    centerY + 6);
            }
        }

        private void DrawDockHandle(Graphics graphics)
        {
            Rectangle accent = new Rectangle(
                (ClientSize.Width - 5) / 2,
                9,
                5,
                ClientSize.Height - 18);
            using (GraphicsPath path = DrawingPaths.RoundedRectangle(accent, 3))
            using (SolidBrush brush = new SolidBrush(active ? palette.Active : palette.Primary))
            {
                graphics.FillPath(brush, path);
            }
        }

        private Rectangle GetZoneBounds(MiniHitZone zone)
        {
            int centerLeft = canNavigateLeft ? ArrowZoneWidth : 0;
            if (zone == MiniHitZone.Left)
            {
                return new Rectangle(0, 0, ArrowZoneWidth, ClientSize.Height);
            }

            if (zone == MiniHitZone.Right)
            {
                return new Rectangle(
                    centerLeft + CenterZoneWidth,
                    0,
                    ArrowZoneWidth,
                    ClientSize.Height);
            }

            return new Rectangle(centerLeft, 0, CenterZoneWidth, ClientSize.Height);
        }

        private void LayoutAtAnchor()
        {
            dockAnchor = ClampDockAnchor(dockAnchor);
            Screen screen = Screen.FromPoint(dockAnchor);
            Rectangle area = screen.WorkingArea;
            bool dockedLeft = IsDockedLeft(dockAnchor, area);
            Size requested = GetRequestedSize();
            int x;
            if (expanded)
            {
                x = dockedLeft
                    ? area.Left + 4
                    : area.Right - requested.Width - 4;
            }
            else
            {
                x = dockedLeft
                    ? area.Left - CollapsedHiddenOffset
                    : area.Right - requested.Width + CollapsedHiddenOffset;
            }

            int y = Math.Max(
                area.Top + 4,
                Math.Min(
                    area.Bottom - requested.Height - 4,
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
            int yMargin = CollapsedHeight / 2 + 4;
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

            using (GraphicsPath path = DrawingPaths.RoundedRectangle(
                new Rectangle(0, 0, ClientSize.Width, ClientSize.Height),
                ClientSize.Height / 2))
            {
                Region previous = Region;
                Region = new Region(path);
                if (previous != null)
                {
                    previous.Dispose();
                }
            }
        }

        private enum MiniHitZone
        {
            None,
            Left,
            Center,
            Right
        }
    }
}
