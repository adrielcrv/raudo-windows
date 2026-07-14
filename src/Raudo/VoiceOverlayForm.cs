using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Raudo
{
    internal enum VoiceOverlayState
    {
        Preparing,
        Listening,
        Success,
        NotUnderstood,
        Unavailable
    }

    internal sealed class VoiceOverlayForm : Form
    {
        private readonly VoiceGlyphControl glyph;
        private readonly Label eyebrowLabel;
        private readonly Label titleLabel;
        private readonly Label detailLabel;
        private readonly Label hintLabel;
        private readonly Timer motionTimer;
        private readonly Stopwatch motionWatch;
        private readonly Timer dismissTimer;
        private readonly Func<bool> animationsEnabled;
        private ThemePalette palette;
        private VoiceOverlayState state;
        private bool allowClose;

        public VoiceOverlayForm()
            : this(null)
        {
        }

        internal VoiceOverlayForm(Func<bool> motionSetting)
        {
            animationsEnabled = motionSetting ?? MotionSettings.ClientAreaAnimationsEnabled;
            Text = "Voz · Raudo";
            AccessibleName = "Voz de Raudo";
            AccessibleDescription =
                "Reconoce una orden local de Raudo mientras mantienes esta ventana abierta";
            ClientSize = new Size(480, 152);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point);
            DoubleBuffered = true;
            Padding = new Padding(1);

            glyph = new VoiceGlyphControl();
            glyph.Location = new Point(20, 28);
            glyph.Size = new Size(76, 76);
            glyph.TabStop = false;
            glyph.AccessibleName = "Estado del micrófono";
            Controls.Add(glyph);

            eyebrowLabel = CreateLabel(
                "VOZ LOCAL",
                7.75F,
                FontStyle.Bold,
                new Point(112, 20),
                new Size(230, 20));
            Controls.Add(eyebrowLabel);

            titleLabel = CreateLabel(
                "Preparando voz...",
                14F,
                FontStyle.Bold,
                new Point(110, 40),
                new Size(340, 31));
            Controls.Add(titleLabel);

            detailLabel = CreateLabel(
                "El audio se procesa con el motor local de Windows.",
                9F,
                FontStyle.Regular,
                new Point(112, 74),
                new Size(344, 40));
            Controls.Add(detailLabel);

            hintLabel = CreateLabel(
                "Esc  cancelar   ·   Ctrl + Alt + V",
                8F,
                FontStyle.Regular,
                new Point(112, 119),
                new Size(344, 20));
            Controls.Add(hintLabel);

            motionTimer = new Timer();
            motionTimer.Interval = 50;
            motionTimer.Tick += MotionTimerTick;
            motionWatch = new Stopwatch();

            dismissTimer = new Timer();
            dismissTimer.Tick += delegate
            {
                dismissTimer.Stop();
                HideOverlay();
            };

            KeyDown += VoiceOverlayKeyDown;
            MouseDown += delegate { RequestCancelIfActive(); };
            foreach (Control control in Controls)
            {
                control.MouseDown += delegate { RequestCancelIfActive(); };
            }

            ApplyTheme(ThemeService.Current());
            SetState(
                VoiceOverlayState.Preparing,
                "Preparando voz...",
                "El audio se procesa con el motor local de Windows.");
        }

        public event EventHandler CancelRequested;

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            BackColor = palette.Surface;
            ForeColor = palette.Text;
            eyebrowLabel.ForeColor = palette.Primary;
            titleLabel.ForeColor = palette.Text;
            detailLabel.ForeColor = palette.TextMuted;
            hintLabel.ForeColor = palette.TextFaint;
            glyph.ApplyTheme(palette);
            ApplyStateColors();
            UpdateRoundedRegion();
            Invalidate();

            if (IsHandleCreated)
            {
                WindowTheme.Apply(Handle, palette.IsDark);
            }
        }

        public void ShowState(
            VoiceOverlayState nextState,
            string title,
            string detail)
        {
            dismissTimer.Stop();
            SetState(nextState, title, detail);
            PositionNearWorkingArea();
            if (!Visible)
            {
                Show();
            }

            Activate();
            BringToFront();
        }

        public void DismissAfter(int milliseconds)
        {
            dismissTimer.Stop();
            dismissTimer.Interval = Math.Max(600, milliseconds);
            dismissTimer.Start();
        }

        public void HideOverlay()
        {
            motionTimer.Stop();
            motionWatch.Reset();
            dismissTimer.Stop();
            if (Visible)
            {
                Hide();
            }
        }

        public void EnsureVisibleOnScreen()
        {
            if (Visible)
            {
                PositionNearWorkingArea();
            }
        }

        public void AllowCloseAndClose()
        {
            allowClose = true;
            motionTimer.Stop();
            dismissTimer.Stop();
            Close();
        }

        internal VoiceOverlayState StateForTesting
        {
            get { return state; }
        }

        internal bool MotionActiveForTesting
        {
            get { return motionTimer.Enabled; }
        }

        protected override void OnFormClosing(FormClosingEventArgs eventArgs)
        {
            if (!allowClose && eventArgs.CloseReason == CloseReason.UserClosing)
            {
                eventArgs.Cancel = true;
                HideOverlay();
                return;
            }

            base.OnFormClosing(eventArgs);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            Graphics graphics = eventArgs.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle border = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            using (GraphicsPath path = RoundedPath(border, 22))
            using (Pen pen = new Pen(palette.Border, palette.IsHighContrast ? 2F : 1F))
            {
                graphics.DrawPath(pen, path);
            }
        }

        protected override void OnResize(EventArgs eventArgs)
        {
            base.OnResize(eventArgs);
            UpdateRoundedRegion();
        }

        private void SetState(
            VoiceOverlayState nextState,
            string title,
            string detail)
        {
            state = nextState;
            titleLabel.Text = title ?? string.Empty;
            detailLabel.Text = detail ?? string.Empty;
            bool active = state == VoiceOverlayState.Preparing
                || state == VoiceOverlayState.Listening;
            hintLabel.Text = active
                ? "Esc  cancelar   ·   Ctrl + Alt + V"
                : "Procesamiento local   ·   Sin audio guardado";
            glyph.State = state;
            ApplyStateColors();

            motionTimer.Stop();
            motionWatch.Reset();
            if (active && animationsEnabled())
            {
                motionWatch.Start();
                motionTimer.Start();
            }
            else
            {
                glyph.Pulse = active ? 0.55F : 0F;
            }

            AccessibleDescription = titleLabel.Text + ". " + detailLabel.Text;
            Invalidate(true);
        }

        private void ApplyStateColors()
        {
            if (palette == null)
            {
                return;
            }

            Color accent;
            switch (state)
            {
                case VoiceOverlayState.Success:
                    accent = palette.Active;
                    break;
                case VoiceOverlayState.NotUnderstood:
                    accent = palette.Warning;
                    break;
                case VoiceOverlayState.Unavailable:
                    accent = palette.Danger;
                    break;
                default:
                    accent = palette.Primary;
                    break;
            }

            eyebrowLabel.ForeColor = accent;
            glyph.Accent = accent;
        }

        private void MotionTimerTick(object sender, EventArgs eventArgs)
        {
            double seconds = motionWatch.Elapsed.TotalSeconds;
            glyph.Pulse = (float)((Math.Sin(seconds * Math.PI * 2D) + 1D) / 2D);
        }

        private void VoiceOverlayKeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.KeyCode == Keys.Escape)
            {
                eventArgs.Handled = true;
                RequestCancelIfActive();
            }
        }

        private void RequestCancelIfActive()
        {
            if (state != VoiceOverlayState.Preparing
                && state != VoiceOverlayState.Listening)
            {
                return;
            }

            EventHandler handler = CancelRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void PositionNearWorkingArea()
        {
            Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(
                workingArea.Left + ((workingArea.Width - Width) / 2),
                workingArea.Bottom - Height - 28);
        }

        private void UpdateRoundedRegion()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
            {
                return;
            }

            using (GraphicsPath path = RoundedPath(
                new Rectangle(0, 0, ClientSize.Width, ClientSize.Height),
                22))
            {
                Region next = new Region(path);
                Region previous = Region;
                Region = next;
                if (previous != null)
                {
                    previous.Dispose();
                }
            }
        }

        private static GraphicsPath RoundedPath(Rectangle rectangle, int radius)
        {
            int diameter = Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height));
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(
                rectangle.Right - diameter,
                rectangle.Bottom - diameter,
                diameter,
                diameter,
                0,
                90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Label CreateLabel(
            string text,
            float size,
            FontStyle style,
            Point location,
            Size bounds)
        {
            Label label = new Label();
            label.AutoSize = false;
            label.Text = text;
            label.Font = new Font("Segoe UI", size, style, GraphicsUnit.Point);
            label.Location = location;
            label.Size = bounds;
            label.BackColor = Color.Transparent;
            label.UseMnemonic = false;
            return label;
        }
    }

    internal sealed class VoiceGlyphControl : Control
    {
        private ThemePalette palette;
        private Color accent;
        private float pulse;
        private VoiceOverlayState state;

        public VoiceGlyphControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw
                    | ControlStyles.UserPaint
                    | ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
        }

        public Color Accent
        {
            get { return accent; }
            set
            {
                accent = value;
                Invalidate();
            }
        }

        public float Pulse
        {
            get { return pulse; }
            set
            {
                pulse = Math.Max(0F, Math.Min(1F, value));
                Invalidate();
            }
        }

        public VoiceOverlayState State
        {
            get { return state; }
            set
            {
                state = value;
                Invalidate();
            }
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            if (palette == null)
            {
                return;
            }

            Graphics graphics = eventArgs.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF bounds = new RectangleF(8, 8, Width - 16, Height - 16);
            int alpha = state == VoiceOverlayState.Preparing
                    || state == VoiceOverlayState.Listening
                ? 26 + (int)(30F * pulse)
                : 26;
            using (SolidBrush halo = new SolidBrush(Color.FromArgb(alpha, accent)))
            using (SolidBrush surface = new SolidBrush(palette.SurfaceRaised))
            using (Pen ring = new Pen(accent, palette.IsHighContrast ? 2.5F : 1.5F))
            {
                float expansion = pulse * 3F;
                graphics.FillEllipse(
                    halo,
                    bounds.X - expansion,
                    bounds.Y - expansion,
                    bounds.Width + (expansion * 2F),
                    bounds.Height + (expansion * 2F));
                graphics.FillEllipse(surface, bounds);
                graphics.DrawEllipse(ring, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }

            DrawMicrophone(graphics, accent);
        }

        private void DrawMicrophone(Graphics graphics, Color color)
        {
            float center = Width / 2F;
            using (Pen pen = new Pen(color, 2.5F))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                RectangleF capsule = new RectangleF(center - 7F, 23F, 14F, 24F);
                graphics.DrawArc(pen, capsule, 180F, 180F);
                graphics.DrawLine(pen, capsule.Left, capsule.Top + 7F, capsule.Left, capsule.Bottom - 7F);
                graphics.DrawLine(pen, capsule.Right, capsule.Top + 7F, capsule.Right, capsule.Bottom - 7F);
                graphics.DrawArc(pen, new RectangleF(center - 13F, 31F, 26F, 22F), 0F, 180F);
                graphics.DrawLine(pen, center, 53F, center, 60F);
                graphics.DrawLine(pen, center - 7F, 60F, center + 7F, 60F);
            }
        }
    }
}
