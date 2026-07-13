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
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath tile = DrawingPaths.RoundedRectangle(bounds, Math.Max(2, bounds.Width / 4)))
            using (SolidBrush fill = new SolidBrush(background))
            using (GraphicsPath mark = CreateMark(bounds))
            using (Pen stroke = new Pen(foreground, Math.Max(2F, bounds.Width * 0.113F)))
            {
                graphics.FillPath(fill, tile);
                stroke.StartCap = LineCap.Round;
                stroke.EndCap = LineCap.Round;
                stroke.LineJoin = LineJoin.Round;
                graphics.DrawPath(stroke, mark);
            }
        }

        private static GraphicsPath CreateMark(Rectangle bounds)
        {
            float scale = bounds.Width / 256F;
            float left = bounds.Left;
            float top = bounds.Top;
            GraphicsPath path = new GraphicsPath();
            path.StartFigure();
            path.AddLine(left + (85F * scale), top + (205F * scale), left + (85F * scale), top + (65F * scale));
            path.AddLine(left + (85F * scale), top + (65F * scale), left + (133F * scale), top + (65F * scale));
            path.AddBezier(
                left + (133F * scale), top + (65F * scale),
                left + (169F * scale), top + (65F * scale),
                left + (189F * scale), top + (84F * scale),
                left + (189F * scale), top + (113F * scale));
            path.AddBezier(
                left + (189F * scale), top + (113F * scale),
                left + (189F * scale), top + (142F * scale),
                left + (169F * scale), top + (161F * scale),
                left + (133F * scale), top + (161F * scale));
            path.AddLine(left + (133F * scale), top + (161F * scale), left + (85F * scale), top + (161F * scale));
            path.StartFigure();
            path.AddLine(left + (133F * scale), top + (161F * scale), left + (198F * scale), top + (209F * scale));
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
    }

    internal sealed class RoundedPanel : Panel
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

    internal sealed class RoundedButton : Control
    {
        private bool pointerOver;
        private bool pointerDown;
        private bool keyboardDown;
        private Color normalColor;
        private Color hoverColor;

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
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                eventArgs.Graphics.DrawString(Text, Font, textBrush, bounds, format);
            }

            if (Focused && ShowFocusCues)
            {
                Rectangle focus = Rectangle.Inflate(bounds, -4, -4);
                ControlPaint.DrawFocusRectangle(eventArgs.Graphics, focus, Color.White, fill);
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
            Color fill = active ? colors.ActiveSoft : colors.SurfaceRaised;
            Color text = active ? colors.Active : colors.TextMuted;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            using (GraphicsPath path = DrawingPaths.RoundedRectangle(bounds, Height / 2))
            using (SolidBrush fillBrush = new SolidBrush(fill))
            using (SolidBrush dotBrush = new SolidBrush(text))
            using (SolidBrush textBrush = new SolidBrush(text))
            using (StringFormat format = new StringFormat())
            {
                eventArgs.Graphics.FillPath(fillBrush, path);
                eventArgs.Graphics.FillEllipse(dotBrush, 11, (Height - 7) / 2, 7, 7);
                format.LineAlignment = StringAlignment.Center;
                eventArgs.Graphics.DrawString(
                    active ? "Activo" : "Inactivo",
                    Font,
                    textBrush,
                    new RectangleF(25, 0, Width - 29, Height),
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
            BrandDrawing.DrawMark(eventArgs.Graphics, bounds, colors.Primary, Color.White);
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
}
