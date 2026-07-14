using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Raudo
{
    internal sealed class DesktopGuideForm : Form
    {
        private readonly Label eyebrowLabel;
        private readonly Label titleLabel;
        private readonly Label descriptionLabel;
        private readonly DesktopGuideMap map;
        private readonly Label firstStepLabel;
        private readonly Label secondStepLabel;
        private readonly Label thirdStepLabel;
        private readonly RoundedButton createButton;
        private readonly RoundedButton closeButton;
        private ThemePalette palette;
        private bool allowClose;

        public DesktopGuideForm(Icon appIcon)
        {
            Text = "Escritorios de trabajo · Raudo";
            AccessibleDescription = "Guía para separar tareas en escritorios de Windows";
            ClientSize = new Size(540, 430);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point);
            Icon = appIcon;

            eyebrowLabel = CreateLabel(
                "ESCRITORIOS DE TRABAJO",
                7.75F,
                FontStyle.Bold,
                new Point(24, 20),
                new Size(260, 20));
            Controls.Add(eyebrowLabel);

            titleLabel = CreateLabel(
                "Separa tareas, no cierres nada",
                17F,
                FontStyle.Bold,
                new Point(24, 42),
                new Size(492, 34));
            Controls.Add(titleLabel);

            descriptionLabel = CreateLabel(
                "Cada escritorio conserva sus propias ventanas. Cambia entre proyectos sin perder tu lugar.",
                9.25F,
                FontStyle.Regular,
                new Point(24, 77),
                new Size(492, 42));
            Controls.Add(descriptionLabel);

            map = new DesktopGuideMap();
            map.Location = new Point(24, 124);
            map.Size = new Size(492, 108);
            Controls.Add(map);

            firstStepLabel = CreateStepLabel(
                "1",
                "Crear abre un espacio nuevo y cambia a él.",
                new Point(24, 250));
            Controls.Add(firstStepLabel);

            secondStepLabel = CreateStepLabel(
                "2",
                "Mini o Ctrl + Win + flecha cambia de escritorio.",
                new Point(24, 286));
            Controls.Add(secondStepLabel);

            thirdStepLabel = CreateStepLabel(
                "3",
                "Salto puede traer aquí una ventana que quedó en otro.",
                new Point(24, 322));
            Controls.Add(thirdStepLabel);

            createButton = CreateButton("Crear escritorio", new Point(310, 376), new Size(128, 36));
            createButton.TabIndex = 0;
            createButton.AccessibleName = "Crear un escritorio de trabajo";
            createButton.Click += delegate
            {
                EventHandler handler = CreateRequested;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            };
            Controls.Add(createButton);

            closeButton = CreateButton("Cerrar", new Point(446, 376), new Size(70, 36));
            closeButton.TabIndex = 1;
            closeButton.AccessibleName = "Cerrar la guía";
            closeButton.Click += delegate { Hide(); };
            Controls.Add(closeButton);

            FormClosing += DesktopGuideFormClosing;
            ApplyTheme(ThemeService.Current());
        }

        public event EventHandler CreateRequested;

        public void ShowIntroduction()
        {
            titleLabel.Text = "Separa tareas, no cierres nada";
            descriptionLabel.Text =
                "Cada escritorio conserva sus propias ventanas. Cambia entre proyectos sin perder tu lugar.";
            map.ShowCreatedState = false;
            firstStepLabel.Text = "1     Crear abre un espacio nuevo y cambia a él.";
            secondStepLabel.Text = "2     Mini o Ctrl + Win + flecha cambia de escritorio.";
            thirdStepLabel.Text = "3     Salto puede traer aquí una ventana que quedó en otro.";
            createButton.Visible = true;
            closeButton.Text = "Cerrar";
            closeButton.Location = new Point(446, 376);
            closeButton.Size = new Size(70, 36);
            ShowGuide();
        }

        public void ShowCreated()
        {
            titleLabel.Text = "Ya estás en un escritorio nuevo";
            descriptionLabel.Text =
                "Tu trabajo anterior sigue abierto en el escritorio de la izquierda.";
            map.ShowCreatedState = true;
            firstStepLabel.Text = "1     Abre aquí las aplicaciones para esta tarea.";
            secondStepLabel.Text = "2     Vuelve con Mini o Ctrl + Win + flecha izquierda.";
            thirdStepLabel.Text = "3     Usa Salto para traer una ventana sin cerrarla.";
            createButton.Visible = false;
            closeButton.Text = "Entendido";
            closeButton.Location = new Point(416, 376);
            closeButton.Size = new Size(100, 36);
            ShowGuide();
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            BackColor = palette.Window;
            ForeColor = palette.Text;
            eyebrowLabel.ForeColor = palette.Primary;
            titleLabel.ForeColor = palette.Text;
            descriptionLabel.ForeColor = palette.TextMuted;
            firstStepLabel.ForeColor = palette.Text;
            secondStepLabel.ForeColor = palette.Text;
            thirdStepLabel.ForeColor = palette.Text;
            map.ApplyTheme(palette);
            ApplyButtonTheme(createButton, true);
            ApplyButtonTheme(closeButton, false);

            if (IsHandleCreated)
            {
                WindowTheme.Apply(Handle, palette.IsDark);
            }
        }

        public void AllowCloseAndClose()
        {
            allowClose = true;
            Close();
        }

        protected override void OnHandleCreated(EventArgs eventArgs)
        {
            base.OnHandleCreated(eventArgs);
            if (palette != null)
            {
                WindowTheme.Apply(Handle, palette.IsDark);
            }
        }

        private void ShowGuide()
        {
            if (!Visible)
            {
                Show();
            }

            WindowState = FormWindowState.Normal;
            BringToFront();
            Activate();
        }

        private void ApplyButtonTheme(RoundedButton button, bool primary)
        {
            button.ForeColor = primary ? palette.PrimaryForeground : palette.Text;
            button.NormalColor = primary ? palette.Primary : palette.SurfaceRaised;
            button.HoverColor = primary ? palette.PrimaryHover : palette.Border;
            button.FocusColor = primary ? palette.PrimaryForeground : palette.Primary;
        }

        private void DesktopGuideFormClosing(object sender, FormClosingEventArgs eventArgs)
        {
            bool systemIsClosing = eventArgs.CloseReason == CloseReason.WindowsShutDown
                || eventArgs.CloseReason == CloseReason.TaskManagerClosing;
            if (!allowClose && !systemIsClosing)
            {
                eventArgs.Cancel = true;
                Hide();
            }
        }

        private static RoundedButton CreateButton(string text, Point location, Size size)
        {
            RoundedButton button = new RoundedButton();
            button.Text = text;
            button.Font = new Font(
                "Segoe UI Semibold",
                8.5F,
                FontStyle.Bold,
                GraphicsUnit.Point);
            button.Location = location;
            button.Size = size;
            button.Radius = 9;
            return button;
        }

        private static Label CreateStepLabel(string number, string text, Point location)
        {
            return CreateLabel(
                number + "     " + text,
                9F,
                FontStyle.Regular,
                location,
                new Size(492, 28));
        }

        private static Label CreateLabel(
            string text,
            float size,
            FontStyle style,
            Point location,
            Size bounds)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = new Font(
                style == FontStyle.Regular ? "Segoe UI" : "Segoe UI Semibold",
                size,
                style,
                GraphicsUnit.Point);
            label.Location = location;
            label.Size = bounds;
            label.BackColor = Color.Transparent;
            return label;
        }
    }

    internal sealed class DesktopGuideMap : Control
    {
        private ThemePalette palette;
        private bool showCreatedState;

        public DesktopGuideMap()
        {
            DoubleBuffered = true;
            TabStop = false;
            AccessibleRole = AccessibleRole.Graphic;
            AccessibleName = "Dos escritorios de trabajo separados";
        }

        public bool ShowCreatedState
        {
            get { return showCreatedState; }
            set
            {
                showCreatedState = value;
                Invalidate();
            }
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            BackColor = palette.Window;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            ThemePalette colors = palette ?? ThemeService.Current();
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            DrawDesktop(eventArgs.Graphics, new Rectangle(10, 10, 202, 86), "Trabajo", !showCreatedState, colors);
            DrawDesktop(eventArgs.Graphics, new Rectangle(280, 10, 202, 86), "Nuevo escritorio", showCreatedState, colors);

            using (Pen arrow = new Pen(colors.TextFaint, 1.8F))
            {
                arrow.StartCap = LineCap.Round;
                arrow.EndCap = LineCap.Round;
                eventArgs.Graphics.DrawLine(arrow, 230, 53, 262, 53);
                eventArgs.Graphics.DrawLine(arrow, 256, 47, 262, 53);
                eventArgs.Graphics.DrawLine(arrow, 256, 59, 262, 53);
            }
        }

        private static void DrawDesktop(
            Graphics graphics,
            Rectangle bounds,
            string title,
            bool selected,
            ThemePalette palette)
        {
            Color fillColor = selected ? palette.ActiveSoft : palette.Surface;
            Color borderColor = selected ? palette.Active : palette.Border;
            using (GraphicsPath path = DrawingPaths.RoundedRectangle(bounds, 12))
            using (SolidBrush fill = new SolidBrush(fillColor))
            using (Pen border = new Pen(borderColor, selected ? 2F : 1F))
            using (SolidBrush titleBrush = new SolidBrush(palette.Text))
            using (SolidBrush windowBrush = new SolidBrush(palette.SurfaceRaised))
            using (Font titleFont = new Font(
                "Segoe UI Semibold",
                8.5F,
                FontStyle.Bold,
                GraphicsUnit.Point))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
                graphics.DrawString(title, titleFont, titleBrush, bounds.Left + 14, bounds.Top + 11);
                graphics.FillRectangle(windowBrush, bounds.Left + 14, bounds.Top + 39, 76, 31);
                graphics.FillRectangle(windowBrush, bounds.Left + 98, bounds.Top + 39, 90, 31);
            }
        }
    }
}
