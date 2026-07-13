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
        private const int CollapsedSize = 52;
        private const int ExpandedWidth = 172;
        private const int ExpandedHeight = 52;
        private const int ArrowZoneWidth = 56;

        private readonly VirtualDesktopService desktopService;
        private readonly ContextMenuStrip windowMenu;
        private readonly Font windowMenuHeaderFont;
        private readonly Timer collapseTimer;

        private ThemePalette palette;
        private Point collapsedCenter;
        private Point dragStartCursor;
        private Point dragStartLocation;
        private MiniHitZone hoverZone;
        private MiniHitZone pressedZone;
        private bool expanded;
        private bool dragging;
        private bool active;
        private bool allowClose;

        public MiniForm(VirtualDesktopService service, RaudoSettings settings)
        {
            desktopService = service;

            Text = "Raudo Mini";
            AccessibleName = "Modo Mini de Raudo";
            AccessibleDescription = "Navega entre escritorios y trae ventanas al escritorio actual.";
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(CollapsedSize, CollapsedSize);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            DoubleBuffered = true;
            KeyPreview = true;

            collapsedCenter = GetInitialCenter(settings);

            windowMenu = new ContextMenuStrip();
            windowMenu.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            windowMenuHeaderFont = new Font(windowMenu.Font, FontStyle.Bold);
            windowMenu.Closed += delegate { ScheduleCollapse(); };

            collapseTimer = new Timer();
            collapseTimer.Interval = 350;
            collapseTimer.Tick += CollapseTimerTick;

            ApplyTheme(ThemeService.Current());
            LayoutAtCenter();
        }

        public event EventHandler OpenMainRequested;
        public event EventHandler HideRequested;
        public event EventHandler PinHelpRequested;
        public event EventHandler<MiniPositionChangedEventArgs> PositionChangedByUser;

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

            TopMost = true;
        }

        public void EnsureVisibleOnScreen()
        {
            collapsedCenter = ClampCenter(collapsedCenter);
            LayoutAtCenter();
        }

        public void AllowCloseAndClose()
        {
            allowClose = true;
            collapseTimer.Stop();
            Close();
        }

        internal void SetExpandedForTesting(bool shouldExpand)
        {
            SetExpanded(shouldExpand);
        }

        protected override void OnHandleCreated(EventArgs eventArgs)
        {
            base.OnHandleCreated(eventArgs);
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

            if (expanded)
            {
                DrawHitZone(eventArgs.Graphics, MiniHitZone.Left);
                DrawHitZone(eventArgs.Graphics, MiniHitZone.Right);
                DrawArrow(eventArgs.Graphics, true);
                DrawArrow(eventArgs.Graphics, false);
            }

            int markSize = 34;
            Rectangle markBounds = new Rectangle(
                (ClientSize.Width - markSize) / 2,
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
                    Location = ClampLocation(requested, Size);
                    collapsedCenter = new Point(
                        Left + Width / 2,
                        Top + Height / 2);
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
                collapsedCenter = ClampCenter(collapsedCenter);
                EventHandler<MiniPositionChangedEventArgs> positionHandler = PositionChangedByUser;
                if (positionHandler != null)
                {
                    positionHandler(this, new MiniPositionChangedEventArgs(collapsedCenter));
                }
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
                SwitchDesktop(DesktopDirection.Left);
                eventArgs.Handled = true;
            }
            else if (eventArgs.KeyCode == Keys.Right)
            {
                SwitchDesktop(DesktopDirection.Right);
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
                ClearWindowMenuItems();
                windowMenu.Dispose();
                windowMenuHeaderFont.Dispose();
            }

            base.Dispose(disposing);
        }

        private void SwitchDesktop(DesktopDirection direction)
        {
            string error;
            if (!DesktopNavigation.TrySwitch(direction, out error))
            {
                MessageBox.Show(
                    this,
                    error,
                    "Raudo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
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

            ToolStripMenuItem pinHelp = new ToolStripMenuItem("Mostrar en todos los escritorios…");
            pinHelp.Click += delegate
            {
                EventHandler handler = PinHelpRequested;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            };
            windowMenu.Items.Add(pinHelp);

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
                return;
            }

            expanded = shouldExpand;
            ClientSize = shouldExpand
                ? new Size(ExpandedWidth, ExpandedHeight)
                : new Size(CollapsedSize, CollapsedSize);
            LayoutAtCenter();
            Invalidate();
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

        private MiniHitZone HitTest(Point point)
        {
            if (!expanded)
            {
                return MiniHitZone.Center;
            }

            if (point.X < ArrowZoneWidth)
            {
                return MiniHitZone.Left;
            }

            if (point.X >= ClientSize.Width - ArrowZoneWidth)
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

            Rectangle area = zone == MiniHitZone.Left
                ? new Rectangle(4, 4, ArrowZoneWidth - 6, ClientSize.Height - 8)
                : new Rectangle(ClientSize.Width - ArrowZoneWidth + 2, 4, ArrowZoneWidth - 6, ClientSize.Height - 8);
            using (GraphicsPath path = DrawingPaths.RoundedRectangle(area, area.Height / 2))
            using (SolidBrush brush = new SolidBrush(palette.SurfaceRaised))
            {
                graphics.FillPath(brush, path);
            }
        }

        private void DrawArrow(Graphics graphics, bool left)
        {
            int centerX = left ? 29 : ClientSize.Width - 29;
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

        private void LayoutAtCenter()
        {
            Point requested = new Point(
                collapsedCenter.X - Width / 2,
                collapsedCenter.Y - Height / 2);
            Location = ClampLocation(requested, Size);
        }

        private Point ClampCenter(Point center)
        {
            Screen screen = Screen.FromPoint(center);
            Rectangle area = screen.WorkingArea;
            int margin = CollapsedSize / 2 + 4;
            return new Point(
                Math.Max(area.Left + margin, Math.Min(area.Right - margin, center.X)),
                Math.Max(area.Top + margin, Math.Min(area.Bottom - margin, center.Y)));
        }

        private static Point ClampLocation(Point requested, Size size)
        {
            Screen screen = Screen.FromPoint(new Point(
                requested.X + size.Width / 2,
                requested.Y + size.Height / 2));
            Rectangle area = screen.WorkingArea;
            return new Point(
                Math.Max(area.Left + 4, Math.Min(area.Right - size.Width - 4, requested.X)),
                Math.Max(area.Top + 4, Math.Min(area.Bottom - size.Height - 4, requested.Y)));
        }

        private static Point GetInitialCenter(RaudoSettings settings)
        {
            if (settings.MiniCenterX >= 0 && settings.MiniCenterY >= 0)
            {
                return new Point(settings.MiniCenterX, settings.MiniCenterY);
            }

            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            return new Point(
                area.Right - CollapsedSize / 2 - 18,
                area.Bottom - CollapsedSize / 2 - 18);
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
