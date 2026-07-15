using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace Raudo
{
    internal sealed class WelcomeForm : Form
    {
        private readonly bool showChanges;
        private readonly bool canInstall;
        private readonly BrandMarkControl brandMark;
        private readonly Label eyebrowLabel;
        private readonly Label titleLabel;
        private readonly Label descriptionLabel;
        private readonly WelcomeShowcaseControl showcase;
        private readonly WelcomeFeatureCard[] featureCards;
        private readonly Label statusLabel;
        private readonly WelcomeButton secondaryButton;
        private readonly WelcomeButton primaryButton;
        private readonly Timer rotationTimer;
        private ThemePalette palette;
        private bool busy;
        private bool dismissing;
        private bool disposed;

        public WelcomeForm(Icon appIcon, bool changes, bool installAvailable)
        {
            showChanges = changes;
            canInstall = installAvailable;

            Text = changes ? "Novedades de Raudo" : "Bienvenido a Raudo";
            AccessibleDescription = changes
                ? "Resumen de cambios de esta versión de Raudo"
                : "Introducción a las funciones principales de Raudo";
            ClientSize = new Size(620, 536);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point);
            Icon = appIcon;
            KeyPreview = true;

            brandMark = new BrandMarkControl();
            brandMark.Location = new Point(24, 19);
            brandMark.Size = new Size(44, 44);
            Controls.Add(brandMark);

            eyebrowLabel = CreateLabel(
                changes ? "NUEVA VERSIÓN" : "PRIMEROS PASOS",
                7.75F,
                FontStyle.Bold,
                new Point(82, 16),
                new Size(250, 18));
            Controls.Add(eyebrowLabel);

            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            titleLabel = CreateLabel(
                changes ? "Novedades de Raudo " + version : "Raudo, listo cuando lo necesites",
                18F,
                FontStyle.Bold,
                new Point(82, 34),
                new Size(510, 34));
            Controls.Add(titleLabel);

            descriptionLabel = CreateLabel(
                changes
                    ? "La tipografía se mantiene clara al 200% y las órdenes de voz funcionan en español o inglés."
                    : "Acciones rápidas, voz local y controles discretos para trabajar en Windows.",
                9.25F,
                FontStyle.Regular,
                new Point(24, 76),
                new Size(572, 24));
            Controls.Add(descriptionLabel);

            showcase = new WelcomeShowcaseControl(changes);
            showcase.Location = new Point(24, 108);
            showcase.Size = new Size(572, 194);
            Controls.Add(showcase);

            WelcomeFeatureDefinition[] definitions = changes
                ? CreateChangeDefinitions()
                : CreateIntroductionDefinitions();
            featureCards = new WelcomeFeatureCard[definitions.Length];
            for (int index = 0; index < definitions.Length; index++)
            {
                WelcomeFeatureCard card = new WelcomeFeatureCard(index, definitions[index]);
                card.Location = new Point(24 + (index * 196), 318);
                card.Size = new Size(180, 78);
                card.TabIndex = index;
                card.Selected += FeatureCardSelected;
                featureCards[index] = card;
                Controls.Add(card);
            }

            statusLabel = CreateLabel(
                StatusText(),
                8.5F,
                FontStyle.Regular,
                new Point(24, 415),
                new Size(572, 38));
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(statusLabel);

            secondaryButton = CreateButton(
                canInstall ? "Usar sin instalar" : "Cerrar",
                canInstall ? new Point(306, 472) : new Point(390, 472),
                canInstall ? new Size(136, 40) : new Size(90, 40));
            secondaryButton.TabIndex = 4;
            secondaryButton.AccessibleName = canInstall
                ? "Usar Raudo sin instalarlo"
                : "Cerrar la bienvenida";
            secondaryButton.Click += delegate { Dismiss(); };
            Controls.Add(secondaryButton);

            primaryButton = CreateButton(
                canInstall
                    ? "Instalar en esta PC"
                    : (changes ? "Continuar" : "Empezar"),
                canInstall ? new Point(450, 472) : new Point(488, 472),
                canInstall ? new Size(146, 40) : new Size(108, 40));
            primaryButton.TabIndex = 3;
            primaryButton.AccessibleName = canInstall
                ? "Instalar Raudo para este usuario"
                : "Cerrar la bienvenida y abrir Raudo";
            primaryButton.Click += PrimaryButtonClick;
            Controls.Add(primaryButton);

            rotationTimer = new Timer();
            rotationTimer.Interval = 3800;
            rotationTimer.Tick += RotationTimerTick;

            FormClosing += WelcomeFormClosing;
            VisibleChanged += WelcomeFormVisibleChanged;
            Shown += delegate { primaryButton.Focus(); };

            SelectFeature(0, false);
            ApplyTheme(ThemeService.Current());
        }

        public event EventHandler InstallRequested;
        public event EventHandler Dismissed;

        public void ShowWelcome()
        {
            dismissing = false;
            if (!Visible)
            {
                Show();
            }

            WindowState = FormWindowState.Normal;
            BringToFront();
            Activate();
            primaryButton.Focus();
        }

        public void ShowInstallFailure(string message)
        {
            busy = false;
            primaryButton.Enabled = true;
            secondaryButton.Enabled = true;
            primaryButton.Text = "Intentar de nuevo";
            statusLabel.Text = message;
            statusLabel.ForeColor = palette.Danger;
            primaryButton.Focus();
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            BackColor = palette.Window;
            ForeColor = palette.Text;
            brandMark.ApplyTheme(palette);
            eyebrowLabel.ForeColor = palette.Primary;
            titleLabel.ForeColor = palette.Text;
            descriptionLabel.ForeColor = palette.TextMuted;
            statusLabel.ForeColor = palette.TextMuted;
            showcase.ApplyTheme(palette);
            foreach (WelcomeFeatureCard card in featureCards)
            {
                card.ApplyTheme(palette);
            }

            ApplyButtonTheme(primaryButton, true);
            ApplyButtonTheme(secondaryButton, false);
            if (IsHandleCreated)
            {
                WindowTheme.Apply(Handle, palette.IsDark);
            }
        }

        public void AllowCloseAndClose()
        {
            dismissing = true;
            rotationTimer.Stop();
            showcase.StopMotion();
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

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (keyData == Keys.Escape && !busy)
            {
                Dismiss();
                return true;
            }

            return base.ProcessCmdKey(ref message, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true;
                rotationTimer.Stop();
                rotationTimer.Dispose();
            }

            base.Dispose(disposing);
        }

        private void PrimaryButtonClick(object sender, EventArgs eventArgs)
        {
            if (!canInstall)
            {
                Dismiss();
                return;
            }

            if (busy)
            {
                return;
            }

            busy = true;
            rotationTimer.Stop();
            showcase.StopMotion();
            primaryButton.Text = "Instalando…";
            primaryButton.Enabled = false;
            secondaryButton.Enabled = false;
            statusLabel.Text = "Verificando y preparando el acceso en Inicio…";
            statusLabel.ForeColor = palette.Primary;
            Refresh();

            EventHandler handler = InstallRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void FeatureCardSelected(object sender, EventArgs eventArgs)
        {
            WelcomeFeatureCard selectedCard = sender as WelcomeFeatureCard;
            if (selectedCard == null)
            {
                return;
            }

            SelectFeature(selectedCard.FeatureIndex, true);
            RestartRotation();
        }

        private void SelectFeature(int selectedIndex, bool animate)
        {
            for (int index = 0; index < featureCards.Length; index++)
            {
                featureCards[index].IsSelected = index == selectedIndex;
            }

            showcase.SelectFeature(selectedIndex, animate);
        }

        private void RotationTimerTick(object sender, EventArgs eventArgs)
        {
            int selected = showcase.SelectedFeature;
            SelectFeature((selected + 1) % featureCards.Length, true);
        }

        private void RestartRotation()
        {
            rotationTimer.Stop();
            if (Visible && MotionAllowed())
            {
                rotationTimer.Start();
            }
        }

        private void WelcomeFormVisibleChanged(object sender, EventArgs eventArgs)
        {
            if (Visible && MotionAllowed() && !busy)
            {
                rotationTimer.Start();
            }
            else
            {
                rotationTimer.Stop();
                showcase.StopMotion();
            }
        }

        private void WelcomeFormClosing(object sender, FormClosingEventArgs eventArgs)
        {
            bool systemIsClosing = eventArgs.CloseReason == CloseReason.WindowsShutDown
                || eventArgs.CloseReason == CloseReason.TaskManagerClosing;
            if (!dismissing && !systemIsClosing)
            {
                eventArgs.Cancel = true;
                Dismiss();
            }
        }

        private void Dismiss()
        {
            if (busy)
            {
                return;
            }

            rotationTimer.Stop();
            showcase.StopMotion();
            Hide();
            EventHandler handler = Dismissed;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private bool MotionAllowed()
        {
            return !SystemInformation.HighContrast
                && MotionSettings.ClientAreaAnimationsEnabled();
        }

        private string StatusText()
        {
            if (canInstall)
            {
                return "Se instala sólo para tu usuario, sin administrador. También puedes conservar esta copia como portable.";
            }

            return showChanges
                ? "Puedes volver a esta guía desde Bienvenida y novedades, en el icono junto al reloj."
                : "Después de cerrar, Raudo permanece disponible en el icono junto al reloj.";
        }

        private void ApplyButtonTheme(WelcomeButton button, bool primary)
        {
            button.ForeColor = primary ? palette.PrimaryForeground : palette.Text;
            button.NormalColor = primary ? palette.Primary : palette.SurfaceRaised;
            button.HoverColor = primary ? palette.PrimaryHover : palette.Border;
            button.FocusColor = primary ? palette.PrimaryForeground : palette.Primary;
        }

        private static WelcomeFeatureDefinition[] CreateIntroductionDefinitions()
        {
            return new[]
            {
                new WelcomeFeatureDefinition(
                    "Salto",
                    "Abre, encuentra y calcula",
                    WelcomeFeatureGlyph.Search),
                new WelcomeFeatureDefinition(
                    "Voz local",
                    "Órdenes bajo demanda",
                    WelcomeFeatureGlyph.Voice),
                new WelcomeFeatureDefinition(
                    "Pulso y Mini",
                    "Controles siempre cerca",
                    WelcomeFeatureGlyph.Pulse)
            };
        }

        private static WelcomeFeatureDefinition[] CreateChangeDefinitions()
        {
            return new[]
            {
                new WelcomeFeatureDefinition(
                    "Escala 200%",
                    "Tipografía sin recortes",
                    WelcomeFeatureGlyph.Displays),
                new WelcomeFeatureDefinition(
                    "Voz bilingüe",
                    "Español e inglés local",
                    WelcomeFeatureGlyph.Voice),
                new WelcomeFeatureDefinition(
                    "Cambio de pantalla",
                    "Composición inmediata",
                    WelcomeFeatureGlyph.Welcome)
            };
        }

        private static WelcomeButton CreateButton(string text, Point location, Size size)
        {
            WelcomeButton button = new WelcomeButton();
            button.Text = text;
            button.Font = new Font(
                "Segoe UI Semibold",
                8.5F,
                FontStyle.Bold,
                GraphicsUnit.Point);
            button.Location = location;
            button.Size = size;
            button.Radius = 10;
            return button;
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

    internal sealed class WelcomeButton : Button
    {
        private bool pointerOver;
        private bool pointerDown;

        public WelcomeButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw
                    | ControlStyles.UserPaint,
                true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            TabStop = true;
            Radius = 10;
            AccessibleRole = AccessibleRole.PushButton;
        }

        public int Radius { get; set; }
        public Color NormalColor { get; set; }
        public Color HoverColor { get; set; }
        public Color FocusColor { get; set; }

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
                pointerDown = true;
                Invalidate();
            }

            base.OnMouseDown(eventArgs);
        }

        protected override void OnMouseUp(MouseEventArgs eventArgs)
        {
            pointerDown = false;
            Invalidate();
            base.OnMouseUp(eventArgs);
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
            using (SolidBrush fillBrush = new SolidBrush(fill))
            using (SolidBrush textBrush = new SolidBrush(
                Enabled ? ForeColor : Color.FromArgb(226, 232, 240)))
            using (StringFormat format = new StringFormat())
            {
                eventArgs.Graphics.FillPath(fillBrush, path);
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                eventArgs.Graphics.DrawString(Text, Font, textBrush, bounds, format);
            }

            if (Focused && ShowFocusCues)
            {
                Rectangle focus = Rectangle.Inflate(bounds, -4, -4);
                ControlPaint.DrawFocusRectangle(eventArgs.Graphics, focus, FocusColor, fill);
            }
        }
    }

    internal enum WelcomeFeatureGlyph
    {
        Search,
        Voice,
        Pulse,
        Welcome,
        Install,
        Tray,
        Clipboard,
        Displays,
        Privacy
    }

    internal sealed class WelcomeFeatureDefinition
    {
        public WelcomeFeatureDefinition(
            string title,
            string description,
            WelcomeFeatureGlyph glyph)
        {
            Title = title;
            Description = description;
            Glyph = glyph;
        }

        public string Title { get; private set; }
        public string Description { get; private set; }
        public WelcomeFeatureGlyph Glyph { get; private set; }
    }

    internal sealed class WelcomeFeatureCard : Button
    {
        private readonly WelcomeFeatureDefinition definition;
        private ThemePalette palette;
        private bool selected;
        private bool pointerOver;

        public WelcomeFeatureCard(int featureIndex, WelcomeFeatureDefinition featureDefinition)
        {
            FeatureIndex = featureIndex;
            definition = featureDefinition;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw
                    | ControlStyles.Selectable
                    | ControlStyles.UserPaint,
                true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = definition.Title + ". " + definition.Description;
        }

        public event EventHandler Selected;

        public int FeatureIndex { get; private set; }

        public bool IsSelected
        {
            get { return selected; }
            set
            {
                selected = value;
                Invalidate();
            }
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            Invalidate();
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

        protected override void OnClick(EventArgs eventArgs)
        {
            base.OnClick(eventArgs);
            OnSelected();
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            ThemePalette colors = palette ?? ThemeService.Current();
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = selected
                ? colors.ActiveSoft
                : (pointerOver ? colors.SurfaceRaised : colors.Surface);
            Color border = selected ? colors.Active : colors.Border;

            using (GraphicsPath path = DrawingPaths.RoundedRectangle(bounds, 12))
            using (SolidBrush fillBrush = new SolidBrush(fill))
            using (Pen borderPen = new Pen(border, selected ? 1.7F : 1F))
            using (SolidBrush titleBrush = new SolidBrush(colors.Text))
            using (SolidBrush descriptionBrush = new SolidBrush(colors.TextMuted))
            using (Font titleFont = new Font(
                "Segoe UI Semibold",
                8.5F,
                FontStyle.Bold,
                GraphicsUnit.Point))
            using (Font descriptionFont = new Font(
                "Segoe UI",
                7.75F,
                FontStyle.Regular,
                GraphicsUnit.Point))
            {
                eventArgs.Graphics.FillPath(fillBrush, path);
                eventArgs.Graphics.DrawPath(borderPen, path);
                DrawGlyph(
                    eventArgs.Graphics,
                    definition.Glyph,
                    new Rectangle(14, 16, 26, 26),
                    selected ? colors.Active : colors.Primary);
                eventArgs.Graphics.DrawString(
                    definition.Title,
                    titleFont,
                    titleBrush,
                    new PointF(48, 14));
                eventArgs.Graphics.DrawString(
                    definition.Description,
                    descriptionFont,
                    descriptionBrush,
                    new RectangleF(48, 35, Width - 58, 32));

                if (Focused && ShowFocusCues)
                {
                    Rectangle focus = Rectangle.Inflate(bounds, -4, -4);
                    using (Pen focusPen = new Pen(colors.Primary, 1F))
                    {
                        focusPen.DashStyle = DashStyle.Dot;
                        eventArgs.Graphics.DrawRectangle(focusPen, focus);
                    }
                }
            }
        }

        private void OnSelected()
        {
            EventHandler handler = Selected;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        internal static void DrawGlyph(
            Graphics graphics,
            WelcomeFeatureGlyph glyph,
            Rectangle bounds,
            Color color)
        {
            using (Pen line = new Pen(color, 1.8F))
            using (SolidBrush fill = new SolidBrush(color))
            {
                line.StartCap = LineCap.Round;
                line.EndCap = LineCap.Round;
                line.LineJoin = LineJoin.Round;
                float centerX = bounds.Left + (bounds.Width / 2F);
                float centerY = bounds.Top + (bounds.Height / 2F);
                switch (glyph)
                {
                    case WelcomeFeatureGlyph.Search:
                        graphics.DrawEllipse(line, bounds.Left + 3, bounds.Top + 3, 13, 13);
                        graphics.DrawLine(line, bounds.Left + 14, bounds.Top + 14, bounds.Right - 3, bounds.Bottom - 3);
                        break;
                    case WelcomeFeatureGlyph.Voice:
                        graphics.DrawArc(line, bounds.Left + 7, bounds.Top + 2, 12, 17, 0, 180);
                        graphics.DrawLine(line, centerX, bounds.Top + 3, centerX, bounds.Top + 14);
                        graphics.DrawArc(line, bounds.Left + 4, bounds.Top + 7, 18, 13, 0, 180);
                        graphics.DrawLine(line, centerX, bounds.Top + 20, centerX, bounds.Bottom - 2);
                        break;
                    case WelcomeFeatureGlyph.Pulse:
                        graphics.DrawEllipse(line, bounds.Left + 2, bounds.Top + 2, 22, 22);
                        graphics.DrawLine(line, bounds.Left + 5, centerY, bounds.Left + 10, centerY);
                        graphics.DrawLine(line, bounds.Left + 10, centerY, bounds.Left + 13, bounds.Top + 7);
                        graphics.DrawLine(line, bounds.Left + 13, bounds.Top + 7, bounds.Left + 16, bounds.Bottom - 7);
                        graphics.DrawLine(line, bounds.Left + 16, bounds.Bottom - 7, bounds.Right - 4, centerY);
                        break;
                    case WelcomeFeatureGlyph.Welcome:
                        graphics.DrawRectangle(line, bounds.Left + 2, bounds.Top + 4, 22, 17);
                        graphics.DrawLine(line, bounds.Left + 2, bounds.Top + 9, bounds.Right - 2, bounds.Top + 9);
                        graphics.FillEllipse(fill, bounds.Left + 5, bounds.Top + 6, 2, 2);
                        graphics.FillEllipse(fill, bounds.Left + 9, bounds.Top + 6, 2, 2);
                        break;
                    case WelcomeFeatureGlyph.Install:
                        graphics.DrawLine(line, centerX, bounds.Top + 2, centerX, bounds.Top + 15);
                        graphics.DrawLine(line, bounds.Left + 8, bounds.Top + 11, centerX, bounds.Top + 16);
                        graphics.DrawLine(line, centerX, bounds.Top + 16, bounds.Right - 8, bounds.Top + 11);
                        graphics.DrawLine(line, bounds.Left + 4, bounds.Bottom - 5, bounds.Right - 4, bounds.Bottom - 5);
                        break;
                    case WelcomeFeatureGlyph.Tray:
                        graphics.DrawRectangle(line, bounds.Left + 3, bounds.Top + 4, 20, 17);
                        graphics.DrawLine(line, bounds.Left + 3, bounds.Top + 16, bounds.Right - 3, bounds.Top + 16);
                        graphics.FillEllipse(fill, bounds.Right - 9, bounds.Bottom - 8, 4, 4);
                        break;
                    case WelcomeFeatureGlyph.Clipboard:
                        graphics.DrawRectangle(
                            line,
                            bounds.Left + 5,
                            bounds.Top + 4,
                            bounds.Width - 10,
                            bounds.Height - 7);
                        graphics.DrawLine(
                            line,
                            bounds.Left + 9,
                            bounds.Top + 10,
                            bounds.Right - 9,
                            bounds.Top + 10);
                        graphics.DrawLine(
                            line,
                            bounds.Left + 9,
                            bounds.Top + 15,
                            bounds.Right - 11,
                            bounds.Top + 15);
                        graphics.DrawLine(
                            line,
                            centerX - 4,
                            bounds.Top + 4,
                            centerX + 4,
                            bounds.Top + 4);
                        break;
                    case WelcomeFeatureGlyph.Displays:
                        graphics.DrawRectangle(
                            line,
                            bounds.Left + 1,
                            bounds.Top + 5,
                            14,
                            11);
                        graphics.DrawRectangle(
                            line,
                            bounds.Left + 12,
                            bounds.Top + 9,
                            13,
                            11);
                        graphics.DrawLine(
                            line,
                            bounds.Left + 8,
                            bounds.Top + 16,
                            bounds.Left + 8,
                            bounds.Bottom - 2);
                        break;
                    case WelcomeFeatureGlyph.Privacy:
                        PointF[] shield =
                        {
                            new PointF(centerX, bounds.Top + 2),
                            new PointF(bounds.Right - 4, bounds.Top + 6),
                            new PointF(bounds.Right - 6, bounds.Bottom - 7),
                            new PointF(centerX, bounds.Bottom - 2),
                            new PointF(bounds.Left + 6, bounds.Bottom - 7),
                            new PointF(bounds.Left + 4, bounds.Top + 6),
                            new PointF(centerX, bounds.Top + 2)
                        };
                        graphics.DrawLines(line, shield);
                        graphics.DrawLine(
                            line,
                            bounds.Left + 9,
                            centerY,
                            centerX - 1,
                            bounds.Bottom - 8);
                        graphics.DrawLine(
                            line,
                            centerX - 1,
                            bounds.Bottom - 8,
                            bounds.Right - 8,
                            bounds.Top + 9);
                        break;
                }
            }
        }
    }

    internal sealed class WelcomeShowcaseControl : Control
    {
        private readonly bool showChanges;
        private readonly Timer transitionTimer;
        private ThemePalette palette;
        private int selectedFeature;
        private int previousFeature;
        private DateTime transitionStarted;
        private float transitionProgress = 1F;

        public WelcomeShowcaseControl(bool changes)
        {
            showChanges = changes;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw,
                true);
            AccessibleRole = AccessibleRole.Graphic;
            AccessibleName = "Demostración visual de Raudo";

            transitionTimer = new Timer();
            transitionTimer.Interval = 30;
            transitionTimer.Tick += TransitionTimerTick;
        }

        public int SelectedFeature
        {
            get { return selectedFeature; }
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            Invalidate();
        }

        public void SelectFeature(int featureIndex, bool animate)
        {
            if (featureIndex == selectedFeature && transitionProgress >= 1F)
            {
                return;
            }

            previousFeature = selectedFeature;
            selectedFeature = featureIndex;
            bool motionAllowed = animate
                && !SystemInformation.HighContrast
                && MotionSettings.ClientAreaAnimationsEnabled();
            if (!motionAllowed)
            {
                transitionTimer.Stop();
                transitionProgress = 1F;
                Invalidate();
                return;
            }

            transitionStarted = DateTime.UtcNow;
            transitionProgress = 0F;
            transitionTimer.Start();
            Invalidate();
        }

        public void StopMotion()
        {
            transitionTimer.Stop();
            transitionProgress = 1F;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                transitionTimer.Stop();
                transitionTimer.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            ThemePalette colors = palette ?? ThemeService.Current();
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = DrawingPaths.RoundedRectangle(bounds, 18))
            using (SolidBrush fill = new SolidBrush(colors.Surface))
            using (Pen border = new Pen(colors.Border, 1F))
            {
                eventArgs.Graphics.FillPath(fill, path);
                eventArgs.Graphics.DrawPath(border, path);
            }

            if (transitionProgress < 1F)
            {
                DrawFeature(eventArgs.Graphics, previousFeature, 1F - transitionProgress, colors);
            }

            DrawFeature(eventArgs.Graphics, selectedFeature, transitionProgress, colors);
        }

        private void TransitionTimerTick(object sender, EventArgs eventArgs)
        {
            double elapsed = (DateTime.UtcNow - transitionStarted).TotalMilliseconds;
            transitionProgress = (float)Math.Min(1D, elapsed / 220D);
            transitionProgress = 1F - ((1F - transitionProgress) * (1F - transitionProgress));
            if (transitionProgress >= 1F)
            {
                transitionTimer.Stop();
            }

            Invalidate();
        }

        private void DrawFeature(
            Graphics graphics,
            int feature,
            float opacity,
            ThemePalette colors)
        {
            int alpha = Math.Max(0, Math.Min(255, (int)(255F * opacity)));
            Color primary = Color.FromArgb(alpha, colors.Primary);
            Color active = Color.FromArgb(alpha, colors.Active);
            Color text = Color.FromArgb(alpha, colors.Text);
            Color muted = Color.FromArgb(alpha, colors.TextMuted);
            Color raised = Color.FromArgb(alpha, colors.SurfaceRaised);
            Color activeSoft = Color.FromArgb(alpha, colors.ActiveSoft);

            if (!colors.IsHighContrast)
            {
                using (SolidBrush accentGlow = new SolidBrush(
                    Color.FromArgb(alpha / 5, colors.Primary)))
                {
                    graphics.FillEllipse(accentGlow, Width - 215, -75, 270, 270);
                }
            }

            WelcomeFeatureGlyph glyph;
            if (showChanges)
            {
                glyph = feature == 0
                    ? WelcomeFeatureGlyph.Displays
                    : (feature == 1
                        ? WelcomeFeatureGlyph.Voice
                        : WelcomeFeatureGlyph.Welcome);
            }
            else
            {
                glyph = (WelcomeFeatureGlyph)feature;
            }
            switch (glyph)
            {
                case WelcomeFeatureGlyph.Search:
                    DrawSearch(graphics, primary, active, text, muted, raised);
                    break;
                case WelcomeFeatureGlyph.Voice:
                    DrawVoice(graphics, primary, active, text, muted, raised);
                    break;
                case WelcomeFeatureGlyph.Pulse:
                    DrawPulse(graphics, primary, active, text, muted, raised, activeSoft);
                    break;
                case WelcomeFeatureGlyph.Welcome:
                    DrawWelcome(graphics, primary, active, text, muted, raised);
                    break;
                case WelcomeFeatureGlyph.Install:
                    DrawInstall(graphics, primary, active, text, muted, raised);
                    break;
                case WelcomeFeatureGlyph.Tray:
                    DrawTray(graphics, primary, active, text, muted, raised);
                    break;
                case WelcomeFeatureGlyph.Clipboard:
                    DrawClipboardHistory(graphics, primary, active, text, muted, raised);
                    break;
                case WelcomeFeatureGlyph.Displays:
                    DrawDisplays(graphics, primary, active, text, muted, raised);
                    break;
                case WelcomeFeatureGlyph.Privacy:
                    DrawPrivacy(graphics, primary, active, text, muted, raised);
                    break;
            }
        }

        private static void DrawClipboardHistory(
            Graphics graphics,
            Color primary,
            Color active,
            Color text,
            Color muted,
            Color raised)
        {
            DrawHeading(
                graphics,
                "Activa el historial sin perder el flujo",
                "Enter abre la configuración de Windows",
                text,
                muted);
            FillRound(graphics, new Rectangle(266, 40, 268, 120), 16, raised);
            for (int index = 0; index < 3; index++)
            {
                int top = 56 + (index * 33);
                WelcomeFeatureCard.DrawGlyph(
                    graphics,
                    WelcomeFeatureGlyph.Clipboard,
                    new Rectangle(282, top, 22, 22),
                    index == 0 ? active : primary);
                DrawLine(
                    graphics,
                    new Point(318, top + 7),
                    new Point(485 - (index * 18), top + 7),
                    text,
                    2.5F);
                DrawLine(
                    graphics,
                    new Point(318, top + 16),
                    new Point(455 - (index * 12), top + 16),
                    muted,
                    1.5F);
            }
        }

        private static void DrawDisplays(
            Graphics graphics,
            Color primary,
            Color active,
            Color text,
            Color muted,
            Color raised)
        {
            DrawHeading(
                graphics,
                "La interfaz conserva sus proporciones",
                "Controles nítidos hasta 200% de escala",
                text,
                muted);
            FillRound(graphics, new Rectangle(264, 47, 122, 91), 14, raised);
            FillRound(graphics, new Rectangle(397, 38, 137, 111), 16, raised);
            DrawLine(graphics, new Point(281, 63), new Point(368, 63), muted, 2F);
            DrawLine(graphics, new Point(414, 57), new Point(516, 57), muted, 2F);
            FillRound(graphics, new Rectangle(372, 75, 14, 43), 7, active);
            FillRound(graphics, new Rectangle(397, 86, 14, 50), 7, primary);
            DrawLine(graphics, new Point(311, 138), new Point(340, 138), text, 2F);
            DrawLine(graphics, new Point(446, 149), new Point(482, 149), text, 2F);
        }

        private static void DrawPrivacy(
            Graphics graphics,
            Color primary,
            Color active,
            Color text,
            Color muted,
            Color raised)
        {
            DrawHeading(
                graphics,
                "Sólo consulta cuando tú lo pides",
                "Sin listener, archivos ni historial propio",
                text,
                muted);
            FillRound(graphics, new Rectangle(306, 39, 172, 122), 28, raised);
            WelcomeFeatureCard.DrawGlyph(
                graphics,
                WelcomeFeatureGlyph.Privacy,
                new Rectangle(355, 57, 74, 74),
                primary);
            using (SolidBrush state = new SolidBrush(active))
            {
                graphics.FillEllipse(state, 422, 121, 12, 12);
            }
        }

        private static void DrawSearch(
            Graphics graphics,
            Color primary,
            Color active,
            Color text,
            Color muted,
            Color raised)
        {
            DrawHeading(graphics, "Salto encuentra lo que necesitas", "Ctrl + Alt + Espacio", text, muted);
            Rectangle island = new Rectangle(250, 45, 282, 104);
            FillRound(graphics, island, 18, raised);
            DrawSearchIcon(graphics, new Rectangle(272, 64, 20, 20), primary);
            DrawLine(graphics, new Point(304, 74), new Point(431, 74), muted, 2F);
            DrawLine(graphics, new Point(274, 105), new Point(390, 105), text, 3F);
            DrawLine(graphics, new Point(274, 124), new Point(448, 124), muted, 2F);
            using (SolidBrush dot = new SolidBrush(active))
            {
                graphics.FillEllipse(dot, 493, 111, 10, 10);
            }
        }

        private static void DrawVoice(
            Graphics graphics,
            Color primary,
            Color active,
            Color text,
            Color muted,
            Color raised)
        {
            DrawHeading(graphics, "Órdenes en español o inglés", "Idioma de voz de Windows · procesamiento local", text, muted);
            Rectangle pill = new Rectangle(270, 59, 232, 76);
            FillRound(graphics, pill, 38, raised);
            WelcomeFeatureCard.DrawGlyph(
                graphics,
                WelcomeFeatureGlyph.Voice,
                new Rectangle(294, 83, 28, 28),
                primary);
            int[] heights = { 12, 26, 18, 34, 22, 14 };
            for (int index = 0; index < heights.Length; index++)
            {
                int x = 350 + (index * 18);
                int height = heights[index];
                DrawLine(
                    graphics,
                    new Point(x, 97 - (height / 2)),
                    new Point(x, 97 + (height / 2)),
                    index == 3 ? active : primary,
                    4F);
            }
        }

        private static void DrawPulse(
            Graphics graphics,
            Color primary,
            Color active,
            Color text,
            Color muted,
            Color raised,
            Color activeSoft)
        {
            DrawHeading(graphics, "Controles presentes, sin invadir", "Pulso y Modo Mini", text, muted);
            using (Pen orbit = new Pen(activeSoft, 12F))
            using (Pen progress = new Pen(active, 12F))
            {
                orbit.StartCap = LineCap.Round;
                orbit.EndCap = LineCap.Round;
                progress.StartCap = LineCap.Round;
                progress.EndCap = LineCap.Round;
                graphics.DrawArc(orbit, 278, 50, 82, 82, -90, 360);
                graphics.DrawArc(progress, 278, 50, 82, 82, -90, 245);
            }

            FillRound(graphics, new Rectangle(403, 57, 106, 68), 24, raised);
            DrawLine(graphics, new Point(425, 91), new Point(444, 91), primary, 2F);
            DrawLine(graphics, new Point(466, 81), new Point(466, 101), active, 3F);
            DrawLine(graphics, new Point(488, 91), new Point(469, 91), primary, 2F);
        }

        private static void DrawWelcome(
            Graphics graphics,
            Color primary,
            Color active,
            Color text,
            Color muted,
            Color raised)
        {
            DrawHeading(graphics, "Cambia de pantalla con naturalidad", "Raudo recompone la ventana al instante", text, muted);
            Rectangle window = new Rectangle(263, 40, 270, 118);
            FillRound(graphics, window, 15, raised);
            DrawLine(graphics, new Point(284, 66), new Point(394, 66), text, 3F);
            DrawLine(graphics, new Point(284, 84), new Point(476, 84), muted, 2F);
            for (int index = 0; index < 3; index++)
            {
                Rectangle card = new Rectangle(284 + (index * 78), 106, 64, 34);
                FillRound(graphics, card, 8, index == 0 ? active : primary);
            }
        }

        private static void DrawInstall(
            Graphics graphics,
            Color primary,
            Color active,
            Color text,
            Color muted,
            Color raised)
        {
            DrawHeading(graphics, "Descarga, abre e instala", "Sin administrador ni asistentes externos", text, muted);
            FillRound(graphics, new Rectangle(270, 47, 90, 102), 15, raised);
            WelcomeFeatureCard.DrawGlyph(
                graphics,
                WelcomeFeatureGlyph.Install,
                new Rectangle(301, 78, 28, 28),
                primary);
            DrawLine(graphics, new Point(382, 98), new Point(432, 98), active, 3F);
            DrawLine(graphics, new Point(425, 91), new Point(432, 98), active, 3F);
            DrawLine(graphics, new Point(425, 105), new Point(432, 98), active, 3F);
            FillRound(graphics, new Rectangle(452, 62, 72, 72), 18, raised);
            BrandDrawing.DrawMark(graphics, new Rectangle(470, 80, 36, 36), primary, Color.White);
        }

        private static void DrawTray(
            Graphics graphics,
            Color primary,
            Color active,
            Color text,
            Color muted,
            Color raised)
        {
            DrawHeading(graphics, "Todo sigue disponible", "Scroll sólo si el área no es suficiente", text, muted);
            FillRound(graphics, new Rectangle(274, 48, 250, 98), 16, raised);
            DrawLine(graphics, new Point(294, 76), new Point(452, 76), text, 3F);
            DrawLine(graphics, new Point(294, 98), new Point(429, 98), muted, 2F);
            DrawLine(graphics, new Point(294, 121), new Point(465, 121), muted, 2F);
            BrandDrawing.DrawMark(graphics, new Rectangle(476, 107, 24, 24), primary, Color.White);
            using (SolidBrush state = new SolidBrush(active))
            {
                graphics.FillEllipse(state, 498, 108, 7, 7);
            }
        }

        private static void DrawHeading(
            Graphics graphics,
            string title,
            string caption,
            Color text,
            Color muted)
        {
            using (Font titleFont = new Font(
                "Segoe UI Semibold",
                12.5F,
                FontStyle.Bold,
                GraphicsUnit.Point))
            using (Font captionFont = new Font(
                "Segoe UI",
                8.5F,
                FontStyle.Regular,
                GraphicsUnit.Point))
            using (SolidBrush titleBrush = new SolidBrush(text))
            using (SolidBrush captionBrush = new SolidBrush(muted))
            {
                graphics.DrawString(title, titleFont, titleBrush, new RectangleF(28, 52, 200, 54));
                graphics.DrawString(caption, captionFont, captionBrush, new RectangleF(28, 119, 210, 38));
            }
        }

        private static void FillRound(Graphics graphics, Rectangle bounds, int radius, Color color)
        {
            using (GraphicsPath path = DrawingPaths.RoundedRectangle(bounds, radius))
            using (SolidBrush fill = new SolidBrush(color))
            {
                graphics.FillPath(fill, path);
            }
        }

        private static void DrawLine(
            Graphics graphics,
            Point start,
            Point end,
            Color color,
            float width)
        {
            using (Pen line = new Pen(color, width))
            {
                line.StartCap = LineCap.Round;
                line.EndCap = LineCap.Round;
                graphics.DrawLine(line, start, end);
            }
        }

        private static void DrawSearchIcon(Graphics graphics, Rectangle bounds, Color color)
        {
            WelcomeFeatureCard.DrawGlyph(
                graphics,
                WelcomeFeatureGlyph.Search,
                bounds,
                color);
        }
    }
}
