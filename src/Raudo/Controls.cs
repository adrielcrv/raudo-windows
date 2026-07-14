using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Raudo
{
    internal static class BrandDrawing
    {
        public static void DrawMark(Graphics graphics, Rectangle bounds, Color background, Color foreground)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                int side = Math.Min(bounds.Width, bounds.Height);
                Rectangle square = new Rectangle(
                    bounds.Left + (bounds.Width - side) / 2,
                    bounds.Top + (bounds.Height - side) / 2,
                    side,
                    side);
                using (GraphicsPath tile = DrawingPaths.RoundedRectangle(
                    square,
                    Math.Max(2, side / 4)))
                using (SolidBrush fill = new SolidBrush(background))
                using (GraphicsPath mark = CreateMark(square))
                using (Pen stroke = new Pen(
                    foreground,
                    Math.Max(1.5F, side * 0.104F)))
                {
                    graphics.FillPath(fill, tile);
                    stroke.StartCap = LineCap.Round;
                    stroke.EndCap = LineCap.Round;
                    stroke.LineJoin = LineJoin.Round;
                    graphics.DrawPath(stroke, mark);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private static GraphicsPath CreateMark(Rectangle bounds)
        {
            float scale = bounds.Width / 48F;
            float left = bounds.Left;
            float top = bounds.Top;
            GraphicsPath path = new GraphicsPath();
            path.StartFigure();
            path.AddLine(left + (14F * scale), top + (38F * scale), left + (14F * scale), top + (10F * scale));
            path.AddLine(left + (14F * scale), top + (10F * scale), left + (24F * scale), top + (10F * scale));
            path.AddBezier(
                left + (24F * scale), top + (10F * scale),
                left + (31.5F * scale), top + (10F * scale),
                left + (35F * scale), top + (14.5F * scale),
                left + (35F * scale), top + (21F * scale));
            path.AddBezier(
                left + (35F * scale), top + (21F * scale),
                left + (35F * scale), top + (27.5F * scale),
                left + (31.5F * scale), top + (32F * scale),
                left + (24F * scale), top + (32F * scale));
            path.AddLine(left + (24F * scale), top + (32F * scale), left + (14F * scale), top + (32F * scale));
            path.StartFigure();
            path.AddLine(left + (24F * scale), top + (32F * scale), left + (35F * scale), top + (39F * scale));
            return path;
        }
    }

    internal static class DrawingPaths
    {
        public static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(1, radius * 2);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180F, 90F);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270F, 90F);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0F, 90F);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90F, 90F);
            path.CloseFigure();
            return path;
        }

        public static GraphicsPath RoundedRectangle(
            Rectangle bounds,
            float topLeftRadius,
            float topRightRadius,
            float bottomRightRadius,
            float bottomLeftRadius)
        {
            RectangleF area = new RectangleF(
                bounds.Left,
                bounds.Top,
                Math.Max(1, bounds.Width),
                Math.Max(1, bounds.Height));
            float maximumRadius = Math.Min(area.Width, area.Height) / 2F;
            float topLeft = Math.Max(0F, Math.Min(maximumRadius, topLeftRadius));
            float topRight = Math.Max(0F, Math.Min(maximumRadius, topRightRadius));
            float bottomRight = Math.Max(0F, Math.Min(maximumRadius, bottomRightRadius));
            float bottomLeft = Math.Max(0F, Math.Min(maximumRadius, bottomLeftRadius));

            GraphicsPath path = new GraphicsPath();
            path.StartFigure();
            path.AddLine(area.Left + topLeft, area.Top, area.Right - topRight, area.Top);
            AddCorner(path, area.Right - (topRight * 2F), area.Top, topRight, 270F);
            path.AddLine(area.Right, area.Top + topRight, area.Right, area.Bottom - bottomRight);
            AddCorner(
                path,
                area.Right - (bottomRight * 2F),
                area.Bottom - (bottomRight * 2F),
                bottomRight,
                0F);
            path.AddLine(
                area.Right - bottomRight,
                area.Bottom,
                area.Left + bottomLeft,
                area.Bottom);
            AddCorner(
                path,
                area.Left,
                area.Bottom - (bottomLeft * 2F),
                bottomLeft,
                90F);
            path.AddLine(area.Left, area.Bottom - bottomLeft, area.Left, area.Top + topLeft);
            AddCorner(path, area.Left, area.Top, topLeft, 180F);
            path.CloseFigure();
            return path;
        }

        private static void AddCorner(
            GraphicsPath path,
            float x,
            float y,
            float radius,
            float startAngle)
        {
            if (radius <= 0F)
            {
                return;
            }

            float diameter = radius * 2F;
            path.AddArc(x, y, diameter, diameter, startAngle, 90F);
        }
    }

    internal class RoundedPanel : Panel
    {
        private Color borderColor;

        public RoundedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            borderColor = Color.Transparent;
            Radius = 14;
        }

        public int Radius { get; set; }

        public Color BorderColor
        {
            get { return borderColor; }
            set
            {
                borderColor = value;
                Invalidate();
            }
        }

        protected override void OnResize(EventArgs eventArgs)
        {
            base.OnResize(eventArgs);
            if (Width > 1 && Height > 1)
            {
                using (GraphicsPath path = DrawingPaths.RoundedRectangle(
                    new Rectangle(0, 0, Width, Height), Radius))
                {
                    Region oldRegion = Region;
                    Region = new Region(path);
                    if (oldRegion != null)
                    {
                        oldRegion.Dispose();
                    }
                }
            }
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = DrawingPaths.RoundedRectangle(bounds, Radius))
            using (Pen border = new Pen(BorderColor))
            {
                eventArgs.Graphics.DrawPath(border, path);
            }

            base.OnPaint(eventArgs);
        }
    }

    internal enum ButtonGlyph
    {
        None,
        Play,
        Stop
    }

    internal sealed class RoundedButton : Control
    {
        private bool pointerOver;
        private bool pointerDown;
        private bool keyboardDown;
        private Color normalColor;
        private Color hoverColor;
        private Color focusColor;
        private ButtonGlyph glyph;

        public RoundedButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable
                | ControlStyles.SupportsTransparentBackColor,
                true);
            SetStyle(ControlStyles.StandardClick, false);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Radius = 9;
            ForeColor = Color.White;
            focusColor = Color.White;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
        }

        public int Radius { get; set; }

        public Color NormalColor
        {
            get { return normalColor; }
            set
            {
                normalColor = value;
                Invalidate();
            }
        }

        public Color HoverColor
        {
            get { return hoverColor; }
            set
            {
                hoverColor = value;
                Invalidate();
            }
        }

        public ButtonGlyph Glyph
        {
            get { return glyph; }
            set
            {
                glyph = value;
                Invalidate();
            }
        }

        public Color FocusColor
        {
            get { return focusColor; }
            set
            {
                focusColor = value;
                Invalidate();
            }
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new RoundedButtonAccessibleObject(this);
        }

        private void PerformAccessibleClick()
        {
            if (Enabled)
            {
                Focus();
                OnClick(EventArgs.Empty);
            }
        }

        protected override void OnMouseEnter(EventArgs eventArgs)
        {
            pointerOver = true;
            Invalidate();
            base.OnMouseEnter(eventArgs);
        }

        protected override void OnMouseLeave(EventArgs eventArgs)
        {
            pointerOver = false;
            pointerDown = false;
            Invalidate();
            base.OnMouseLeave(eventArgs);
        }

        protected override void OnMouseDown(MouseEventArgs eventArgs)
        {
            if (eventArgs.Button == MouseButtons.Left)
            {
                Focus();
                Capture = true;
                pointerDown = true;
                Invalidate();
            }
            base.OnMouseDown(eventArgs);
        }

        protected override void OnMouseUp(MouseEventArgs eventArgs)
        {
            bool invoke = pointerDown
                && eventArgs.Button == MouseButtons.Left
                && ClientRectangle.Contains(eventArgs.Location);
            pointerDown = false;
            Capture = false;
            Invalidate();
            base.OnMouseUp(eventArgs);
            if (invoke)
            {
                OnClick(EventArgs.Empty);
            }
        }

        protected override void OnKeyDown(KeyEventArgs eventArgs)
        {
            if ((eventArgs.KeyCode == Keys.Space || eventArgs.KeyCode == Keys.Enter) && !keyboardDown)
            {
                keyboardDown = true;
                pointerDown = true;
                Invalidate();
                eventArgs.Handled = true;
            }

            base.OnKeyDown(eventArgs);
        }

        protected override void OnKeyUp(KeyEventArgs eventArgs)
        {
            if (keyboardDown && (eventArgs.KeyCode == Keys.Space || eventArgs.KeyCode == Keys.Enter))
            {
                keyboardDown = false;
                pointerDown = false;
                Invalidate();
                OnClick(EventArgs.Empty);
                eventArgs.Handled = true;
            }

            base.OnKeyUp(eventArgs);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = pointerOver ? HoverColor : NormalColor;
            if (!Enabled)
            {
                fill = Color.FromArgb(148, 163, 184);
            }
            else if (pointerDown)
            {
                fill = ControlPaint.Dark(fill, 0.08F);
            }

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = DrawingPaths.RoundedRectangle(bounds, Radius))
            using (SolidBrush brush = new SolidBrush(fill))
            using (SolidBrush textBrush = new SolidBrush(Enabled ? ForeColor : Color.FromArgb(226, 232, 240)))
            using (StringFormat format = new StringFormat())
            {
                eventArgs.Graphics.FillPath(brush, path);
                format.LineAlignment = StringAlignment.Center;
                if (Glyph == ButtonGlyph.None)
                {
                    format.Alignment = StringAlignment.Center;
                    eventArgs.Graphics.DrawString(Text, Font, textBrush, bounds, format);
                }
                else
                {
                    SizeF textSize = eventArgs.Graphics.MeasureString(Text, Font);
                    const float glyphSize = 12F;
                    const float gap = 9F;
                    float contentWidth = glyphSize + gap + textSize.Width;
                    float left = (Width - contentWidth) / 2F;
                    float top = (Height - glyphSize) / 2F;
                    DrawGlyph(eventArgs.Graphics, textBrush.Color, left, top, glyphSize);
                    format.Alignment = StringAlignment.Near;
                    eventArgs.Graphics.DrawString(
                        Text,
                        Font,
                        textBrush,
                        new RectangleF(
                            left + glyphSize + gap,
                            0F,
                            Math.Max(1F, Width - left - glyphSize - gap),
                            Height),
                        format);
                }
            }

            if (Focused && ShowFocusCues)
            {
                Rectangle focus = Rectangle.Inflate(bounds, -4, -4);
                ControlPaint.DrawFocusRectangle(eventArgs.Graphics, focus, FocusColor, fill);
            }
        }

        private sealed class RoundedButtonAccessibleObject : ControlAccessibleObject
        {
            private readonly RoundedButton owner;

            public RoundedButtonAccessibleObject(RoundedButton button)
                : base(button)
            {
                owner = button;
            }

            public override AccessibleRole Role
            {
                get { return AccessibleRole.PushButton; }
            }

            public override AccessibleStates State
            {
                get
                {
                    AccessibleStates state = base.State | AccessibleStates.Focusable;
                    if (owner.Focused)
                    {
                        state |= AccessibleStates.Focused;
                    }

                    if (!owner.Enabled)
                    {
                        state |= AccessibleStates.Unavailable;
                    }

                    return state;
                }
            }

            public override string DefaultAction
            {
                get { return "Presionar"; }
            }

            public override void DoDefaultAction()
            {
                owner.PerformAccessibleClick();
            }
        }

        private void DrawGlyph(
            Graphics graphics,
            Color color,
            float left,
            float top,
            float size)
        {
            using (SolidBrush brush = new SolidBrush(color))
            {
                if (Glyph == ButtonGlyph.Stop)
                {
                    float stopSize = size * 0.72F;
                    graphics.FillRectangle(
                        brush,
                        left + ((size - stopSize) / 2F),
                        top + ((size - stopSize) / 2F),
                        stopSize,
                        stopSize);
                    return;
                }

                PointF[] triangle =
                {
                    new PointF(left + 1F, top),
                    new PointF(left + size, top + (size / 2F)),
                    new PointF(left + 1F, top + size)
                };
                graphics.FillPolygon(brush, triangle);
            }
        }
    }

    internal sealed class DurationPicker : Control
    {
        private readonly ContextMenuStrip menu;
        private ThemePalette palette;
        private int selectedMinutes;
        private bool pointerOver;

        public DurationPicker()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable
                | ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.ComboBox;
            selectedMinutes = 30;

            menu = new ContextMenuStrip();
            menu.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            AddItem("15 minutos", 15);
            AddItem("30 minutos", 30);
            AddItem("1 hora", 60);
            AddItem("2 horas", 120);
        }

        public event EventHandler SelectionChanged;

        public int SelectedMinutes
        {
            get { return selectedMinutes; }
        }

        public void SetSelected(int minutes, bool notify)
        {
            int normalized = DurationOption.IsSupported(minutes) ? minutes : 30;
            if (selectedMinutes == normalized)
            {
                return;
            }

            selectedMinutes = normalized;
            UpdateChecks();
            Invalidate();
            if (notify)
            {
                EventHandler handler = SelectionChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            Invalidate();
        }

        protected override void OnClick(EventArgs eventArgs)
        {
            base.OnClick(eventArgs);
            ShowMenu();
        }

        protected override void OnKeyDown(KeyEventArgs eventArgs)
        {
            if (eventArgs.KeyCode == Keys.Enter || eventArgs.KeyCode == Keys.Space || eventArgs.KeyCode == Keys.F4)
            {
                ShowMenu();
                eventArgs.Handled = true;
            }
            else if (eventArgs.KeyCode == Keys.Up || eventArgs.KeyCode == Keys.Down)
            {
                int[] values = { 15, 30, 60, 120 };
                int index = Array.IndexOf(values, selectedMinutes);
                index += eventArgs.KeyCode == Keys.Down ? 1 : -1;
                index = Math.Max(0, Math.Min(values.Length - 1, index));
                SetSelected(values[index], true);
                eventArgs.Handled = true;
            }

            base.OnKeyDown(eventArgs);
        }

        protected override void OnMouseEnter(EventArgs eventArgs)
        {
            pointerOver = true;
            Invalidate();
            base.OnMouseEnter(eventArgs);
        }

        protected override void OnMouseLeave(EventArgs eventArgs)
        {
            pointerOver = false;
            Invalidate();
            base.OnMouseLeave(eventArgs);
        }

        protected override void OnGotFocus(EventArgs eventArgs)
        {
            Invalidate();
            base.OnGotFocus(eventArgs);
        }

        protected override void OnLostFocus(EventArgs eventArgs)
        {
            Invalidate();
            base.OnLostFocus(eventArgs);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            ThemePalette colors = palette ?? ThemeService.Current();
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Color borderColor = Focused || pointerOver ? colors.Primary : colors.Border;
            Color textColor = Enabled ? colors.Text : colors.TextFaint;

            using (GraphicsPath path = DrawingPaths.RoundedRectangle(bounds, 8))
            using (SolidBrush fill = new SolidBrush(colors.SurfaceRaised))
            using (Pen border = new Pen(borderColor))
            using (SolidBrush text = new SolidBrush(textColor))
            using (Pen arrow = new Pen(textColor, 1.6F))
            using (StringFormat format = new StringFormat())
            {
                eventArgs.Graphics.FillPath(fill, path);
                eventArgs.Graphics.DrawPath(border, path);
                format.LineAlignment = StringAlignment.Center;
                eventArgs.Graphics.DrawString(
                    DurationOption.GetLabel(selectedMinutes),
                    Font,
                    text,
                    new RectangleF(13, 0, Width - 48, Height),
                    format);

                arrow.StartCap = LineCap.Round;
                arrow.EndCap = LineCap.Round;
                float centerX = Width - 20F;
                float centerY = Height / 2F;
                eventArgs.Graphics.DrawLine(arrow, centerX - 4F, centerY - 2F, centerX, centerY + 2F);
                eventArgs.Graphics.DrawLine(arrow, centerX, centerY + 2F, centerX + 4F, centerY - 2F);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                menu.Dispose();
            }

            base.Dispose(disposing);
        }

        private void AddItem(string label, int minutes)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label);
            item.Tag = minutes;
            item.Click += MenuItemClick;
            menu.Items.Add(item);
        }

        private void MenuItemClick(object sender, EventArgs eventArgs)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item != null)
            {
                SetSelected((int)item.Tag, true);
            }
        }

        private void ShowMenu()
        {
            if (!Enabled)
            {
                return;
            }

            Focus();
            UpdateChecks();
            menu.Show(this, new Point(0, Height + 3));
        }

        private void UpdateChecks()
        {
            foreach (ToolStripItem rawItem in menu.Items)
            {
                ToolStripMenuItem item = rawItem as ToolStripMenuItem;
                if (item != null)
                {
                    item.Checked = (int)item.Tag == selectedMinutes;
                }
            }
        }
    }

    internal sealed class StatusPill : Control
    {
        private bool active;
        private ThemePalette palette;

        public StatusPill()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Size = new Size(96, 28);
            TabStop = false;
        }

        public void SetState(bool isActive, ThemePalette currentPalette)
        {
            active = isActive;
            palette = currentPalette;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            ThemePalette colors = palette ?? ThemeService.Current();
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color text = active ? colors.Active : colors.TextMuted;

            using (SolidBrush dotBrush = new SolidBrush(text))
            using (SolidBrush textBrush = new SolidBrush(text))
            using (StringFormat format = new StringFormat())
            {
                eventArgs.Graphics.FillEllipse(dotBrush, 1, (Height - 7) / 2, 7, 7);
                format.LineAlignment = StringAlignment.Center;
                eventArgs.Graphics.DrawString(
                    active ? "Activo" : "Inactivo",
                    Font,
                    textBrush,
                    new RectangleF(16, 0, Width - 16, Height),
                    format);
            }
        }
    }

    internal sealed class ToggleSwitch : Control
    {
        private ThemePalette palette;
        private bool isChecked;

        public ToggleSwitch()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable
                | ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
            Size = new Size(46, 26);
            Cursor = Cursors.Hand;
            Text = string.Empty;
            TabStop = true;
            AccessibleRole = AccessibleRole.CheckButton;
        }

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked == value)
                {
                    return;
                }

                isChecked = value;
                Invalidate();
                EventHandler handler = CheckedChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            Invalidate();
        }

        protected override void OnClick(EventArgs eventArgs)
        {
            Checked = !Checked;
            base.OnClick(eventArgs);
        }

        protected override void OnMouseDown(MouseEventArgs eventArgs)
        {
            if (eventArgs.Button == MouseButtons.Left)
            {
                Focus();
            }
            base.OnMouseDown(eventArgs);
        }

        protected override void OnKeyDown(KeyEventArgs eventArgs)
        {
            if (eventArgs.KeyCode == Keys.Space || eventArgs.KeyCode == Keys.Enter)
            {
                Checked = !Checked;
                eventArgs.Handled = true;
            }
            base.OnKeyDown(eventArgs);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            ThemePalette colors = palette ?? ThemeService.Current();
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(0, 2, Width - 1, Height - 5);
            Color trackColor = Checked ? colors.Active : colors.TextFaint;
            int thumbSize = Height - 10;
            int thumbX = Checked ? Width - thumbSize - 5 : 5;

            using (GraphicsPath path = DrawingPaths.RoundedRectangle(track, track.Height / 2))
            using (SolidBrush trackBrush = new SolidBrush(trackColor))
            using (SolidBrush thumbBrush = new SolidBrush(Color.White))
            {
                eventArgs.Graphics.FillPath(trackBrush, path);
                eventArgs.Graphics.FillEllipse(thumbBrush, thumbX, 5, thumbSize, thumbSize);
            }

            if (Focused && ShowFocusCues)
            {
                ControlPaint.DrawFocusRectangle(eventArgs.Graphics, ClientRectangle);
            }
        }
    }

    internal sealed class BrandMarkControl : Control
    {
        private ThemePalette palette;

        public BrandMarkControl()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Size = new Size(54, 54);
            TabStop = false;
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            ThemePalette colors = palette ?? ThemeService.Current();
            Rectangle bounds = new Rectangle(1, 1, Width - 2, Height - 2);
            BrandDrawing.DrawMark(
                eventArgs.Graphics,
                bounds,
                colors.Primary,
                colors.PrimaryForeground);
        }
    }

    internal sealed class CaptureGlyph : Control
    {
        private ThemePalette palette;

        public CaptureGlyph()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Size = new Size(44, 44);
            TabStop = false;
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            ThemePalette colors = palette ?? ThemeService.Current();
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle background = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = DrawingPaths.RoundedRectangle(background, 11))
            using (SolidBrush fill = new SolidBrush(colors.SurfaceRaised))
            using (Pen line = new Pen(colors.Primary, 2F))
            {
                line.StartCap = LineCap.Round;
                line.EndCap = LineCap.Round;
                eventArgs.Graphics.FillPath(fill, path);
                eventArgs.Graphics.DrawLine(line, 13, 19, 13, 13);
                eventArgs.Graphics.DrawLine(line, 13, 13, 19, 13);
                eventArgs.Graphics.DrawLine(line, 25, 13, 31, 13);
                eventArgs.Graphics.DrawLine(line, 31, 13, 31, 19);
                eventArgs.Graphics.DrawLine(line, 31, 25, 31, 31);
                eventArgs.Graphics.DrawLine(line, 31, 31, 25, 31);
                eventArgs.Graphics.DrawLine(line, 19, 31, 13, 31);
                eventArgs.Graphics.DrawLine(line, 13, 31, 13, 25);
            }
        }
    }

    internal sealed class DesktopWorkspaceGlyph : Control
    {
        private ThemePalette palette;

        public DesktopWorkspaceGlyph()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Size = new Size(44, 44);
            TabStop = false;
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            ThemePalette colors = palette ?? ThemeService.Current();
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle background = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = DrawingPaths.RoundedRectangle(background, 11))
            using (SolidBrush fill = new SolidBrush(colors.SurfaceRaised))
            using (Pen line = new Pen(colors.Primary, 1.8F))
            {
                line.StartCap = LineCap.Round;
                line.EndCap = LineCap.Round;
                line.LineJoin = LineJoin.Round;
                eventArgs.Graphics.FillPath(fill, path);
                eventArgs.Graphics.DrawRectangle(line, 10, 13, 15, 12);
                eventArgs.Graphics.DrawRectangle(line, 19, 19, 15, 12);
                eventArgs.Graphics.DrawLine(line, 12, 29, 17, 29);
                eventArgs.Graphics.DrawLine(line, 14.5F, 26.5F, 14.5F, 31.5F);
            }
        }
    }
}
