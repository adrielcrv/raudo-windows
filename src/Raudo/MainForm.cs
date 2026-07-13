using System;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Raudo
{
    internal sealed class StartupChangedEventArgs : EventArgs
    {
        public StartupChangedEventArgs(bool enabled)
        {
            Enabled = enabled;
        }

        public bool Enabled { get; private set; }
    }

    internal sealed class MainForm : Form
    {
        private readonly KeepActiveService keepActiveService;
        private readonly RaudoSettings settings;
        private readonly BrandMarkControl brandMark;
        private readonly Label titleLabel;
        private readonly Label subtitleLabel;
        private readonly LinkLabel updateLink;
        private readonly RoundedPanel primaryCard;
        private readonly Label featureTitleLabel;
        private readonly Label descriptionLabel;
        private readonly Label durationLabel;
        private readonly DurationPicker durationSelector;
        private readonly RoundedButton toggleButton;
        private readonly StatusPill statusPill;
        private readonly Label countdownLabel;
        private readonly Label detailLabel;
        private readonly RoundedPanel captureCard;
        private readonly CaptureGlyph captureGlyph;
        private readonly Label captureTitleLabel;
        private readonly Label captureDescriptionLabel;
        private readonly RoundedButton captureButton;
        private readonly RoundedPanel startupCard;
        private readonly Label startupTitleLabel;
        private readonly Label startupDescriptionLabel;
        private readonly ToggleSwitch startupToggle;
        private readonly Label trayHintLabel;
        private readonly Label versionLabel;
        private readonly Timer visibleTimer;
        private readonly ToolTip toolTip;

        private ThemePalette palette;
        private bool allowClose;
        private bool suppressStartupChange;

        public MainForm(KeepActiveService service, RaudoSettings currentSettings, Icon appIcon)
        {
            keepActiveService = service;
            settings = currentSettings;

            Text = "Raudo";
            AccessibleDescription = "Utilidades locales y ligeras para Windows";
            ClientSize = new Size(540, 650);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            Icon = appIcon;

            brandMark = new BrandMarkControl();
            brandMark.Location = new Point(28, 23);
            Controls.Add(brandMark);

            titleLabel = CreateLabel("Raudo", 21F, FontStyle.Bold, new Point(96, 19), new Size(240, 40));
            Controls.Add(titleLabel);

            subtitleLabel = CreateLabel(
                "Utilidades rápidas para Windows",
                9.5F,
                FontStyle.Regular,
                new Point(98, 59),
                new Size(280, 24));
            Controls.Add(subtitleLabel);

            updateLink = new LinkLabel();
            updateLink.Text = "Buscar actualizaciones";
            updateLink.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point);
            updateLink.TextAlign = ContentAlignment.MiddleRight;
            updateLink.LinkBehavior = LinkBehavior.HoverUnderline;
            updateLink.Location = new Point(370, 31);
            updateLink.Size = new Size(142, 30);
            updateLink.TabStop = true;
            updateLink.LinkClicked += UpdateLinkClicked;
            Controls.Add(updateLink);

            primaryCard = new RoundedPanel();
            primaryCard.Location = new Point(28, 106);
            primaryCard.Size = new Size(484, 286);
            primaryCard.Radius = 16;
            Controls.Add(primaryCard);

            featureTitleLabel = CreateLabel(
                "Mantener activo",
                14F,
                FontStyle.Bold,
                new Point(22, 18),
                new Size(250, 32));
            primaryCard.Controls.Add(featureTitleLabel);

            statusPill = new StatusPill();
            statusPill.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold, GraphicsUnit.Point);
            statusPill.Location = new Point(362, 19);
            statusPill.Size = new Size(100, 28);
            statusPill.AccessibleName = "Estado de Mantener activo";
            primaryCard.Controls.Add(statusPill);

            descriptionLabel = CreateLabel(
                "Mantiene el equipo disponible durante tareas locales autorizadas.",
                9.5F,
                FontStyle.Regular,
                new Point(23, 56),
                new Size(430, 26));
            primaryCard.Controls.Add(descriptionLabel);

            durationLabel = CreateLabel(
                "Duración",
                8.5F,
                FontStyle.Bold,
                new Point(23, 92),
                new Size(150, 22));
            primaryCard.Controls.Add(durationLabel);

            durationSelector = new DurationPicker();
            durationSelector.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            durationSelector.Location = new Point(23, 117);
            durationSelector.Size = new Size(438, 36);
            durationSelector.AccessibleName = "Duración de Mantener activo";
            durationSelector.SelectionChanged += DurationSelectorChanged;
            primaryCard.Controls.Add(durationSelector);

            toggleButton = new RoundedButton();
            toggleButton.Text = "Activar";
            toggleButton.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold, GraphicsUnit.Point);
            toggleButton.Location = new Point(23, 166);
            toggleButton.Size = new Size(438, 48);
            toggleButton.AccessibleName = "Activar Mantener activo";
            toggleButton.Click += delegate { OnToggleRequested(); };
            primaryCard.Controls.Add(toggleButton);

            countdownLabel = CreateLabel(
                "Listo para activar",
                9F,
                FontStyle.Bold,
                new Point(23, 231),
                new Size(210, 24));
            primaryCard.Controls.Add(countdownLabel);

            detailLabel = CreateLabel(
                "Sin actividad en segundo plano",
                8.5F,
                FontStyle.Regular,
                new Point(224, 231),
                new Size(237, 24));
            detailLabel.TextAlign = ContentAlignment.MiddleRight;
            primaryCard.Controls.Add(detailLabel);

            captureCard = new RoundedPanel();
            captureCard.Location = new Point(28, 410);
            captureCard.Size = new Size(484, 90);
            captureCard.Radius = 16;
            Controls.Add(captureCard);

            captureGlyph = new CaptureGlyph();
            captureGlyph.Location = new Point(20, 23);
            captureCard.Controls.Add(captureGlyph);

            captureTitleLabel = CreateLabel(
                "Recortar pantalla",
                10.5F,
                FontStyle.Bold,
                new Point(78, 17),
                new Size(220, 26));
            captureCard.Controls.Add(captureTitleLabel);

            captureDescriptionLabel = CreateLabel(
                "Abre la herramienta incluida en Windows",
                8.5F,
                FontStyle.Regular,
                new Point(79, 45),
                new Size(250, 24));
            captureCard.Controls.Add(captureDescriptionLabel);

            captureButton = new RoundedButton();
            captureButton.Text = "Recortar";
            captureButton.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point);
            captureButton.Location = new Point(362, 23);
            captureButton.Size = new Size(100, 44);
            captureButton.AccessibleName = "Recortar pantalla";
            captureButton.Click += delegate { OnScreenCaptureRequested(); };
            captureCard.Controls.Add(captureButton);

            startupCard = new RoundedPanel();
            startupCard.Location = new Point(28, 518);
            startupCard.Size = new Size(484, 72);
            startupCard.Radius = 16;
            Controls.Add(startupCard);

            startupTitleLabel = CreateLabel(
                "Iniciar con Windows",
                9.5F,
                FontStyle.Bold,
                new Point(22, 12),
                new Size(260, 25));
            startupCard.Controls.Add(startupTitleLabel);

            startupDescriptionLabel = CreateLabel(
                "Raudo inicia apagado y permanece en la bandeja.",
                8.5F,
                FontStyle.Regular,
                new Point(23, 37),
                new Size(350, 22));
            startupCard.Controls.Add(startupDescriptionLabel);

            startupToggle = new ToggleSwitch();
            startupToggle.Location = new Point(414, 23);
            startupToggle.AccessibleName = "Iniciar Raudo con Windows";
            startupToggle.CheckedChanged += StartupToggleCheckedChanged;
            startupCard.Controls.Add(startupToggle);

            trayHintLabel = CreateLabel(
                "Cerrar la ventana mantiene Raudo disponible junto al reloj.",
                8.25F,
                FontStyle.Regular,
                new Point(30, 608),
                new Size(370, 24));
            Controls.Add(trayHintLabel);

            versionLabel = CreateLabel(
                "v" + Assembly.GetExecutingAssembly().GetName().Version.ToString(3),
                8.25F,
                FontStyle.Regular,
                new Point(432, 608),
                new Size(78, 24));
            versionLabel.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(versionLabel);

            visibleTimer = new Timer();
            visibleTimer.Interval = 1000;
            visibleTimer.Tick += delegate { RefreshState(); };

            toolTip = new ToolTip();
            toolTip.SetToolTip(updateLink, "Consulta manualmente la última versión publicada en GitHub");
            toolTip.SetToolTip(startupToggle, "Inicia Raudo en la bandeja, siempre apagado");

            FormClosing += MainFormClosing;
            Resize += MainFormResize;
            VisibleChanged += MainFormVisibleChanged;
            Shown += delegate { ActiveControl = null; };

            SelectDuration(settings.DurationMinutes);
            SetStartupChecked(StartupManager.IsEnabled());
            ApplyTheme(ThemeService.Current());
            RefreshState();
        }

        public event EventHandler ToggleRequested;
        public event EventHandler<DurationChangedEventArgs> DurationChanged;
        public event EventHandler ScreenCaptureRequested;
        public event EventHandler<StartupChangedEventArgs> StartupChanged;

        public void SelectDuration(int minutes)
        {
            durationSelector.SetSelected(minutes, false);
        }

        public void SetStartupChecked(bool enabled)
        {
            suppressStartupChange = true;
            startupToggle.Checked = enabled;
            suppressStartupChange = false;
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            SuspendLayout();

            BackColor = palette.Window;
            ForeColor = palette.Text;
            titleLabel.ForeColor = palette.Text;
            subtitleLabel.ForeColor = palette.TextMuted;
            trayHintLabel.ForeColor = palette.TextFaint;
            versionLabel.ForeColor = palette.TextFaint;
            updateLink.LinkColor = palette.Primary;
            updateLink.ActiveLinkColor = palette.PrimaryHover;
            updateLink.VisitedLinkColor = palette.Primary;

            ApplyCardTheme(primaryCard);
            ApplyCardTheme(captureCard);
            ApplyCardTheme(startupCard);

            featureTitleLabel.ForeColor = palette.Text;
            descriptionLabel.ForeColor = palette.TextMuted;
            durationLabel.ForeColor = palette.TextFaint;
            countdownLabel.ForeColor = palette.Text;
            detailLabel.ForeColor = palette.TextMuted;
            captureTitleLabel.ForeColor = palette.Text;
            captureDescriptionLabel.ForeColor = palette.TextMuted;
            startupTitleLabel.ForeColor = palette.Text;
            startupDescriptionLabel.ForeColor = palette.TextMuted;

            durationSelector.ForeColor = palette.Text;
            durationSelector.ApplyTheme(palette);
            brandMark.ApplyTheme(palette);
            captureGlyph.ApplyTheme(palette);
            startupToggle.ApplyTheme(palette);

            captureButton.NormalColor = palette.Primary;
            captureButton.HoverColor = palette.PrimaryHover;
            RefreshState();
            ResumeLayout();

            if (IsHandleCreated)
            {
                WindowTheme.Apply(Handle, palette.IsDark);
            }
        }

        public void RefreshState()
        {
            bool active = keepActiveService.IsActive;
            durationSelector.Enabled = !active;
            toggleButton.Text = active
                ? "Detener"
                : "Activar por " + DurationOption.GetLabel(settings.DurationMinutes).ToLowerInvariant();
            toggleButton.AccessibleName = active ? "Detener Mantener activo" : toggleButton.Text;
            toggleButton.NormalColor = active ? palette.Danger : palette.Primary;
            toggleButton.HoverColor = active ? palette.DangerHover : palette.PrimaryHover;
            statusPill.SetState(active, palette);

            if (active)
            {
                TimeSpan remaining = keepActiveService.GetRemaining() ?? TimeSpan.Zero;
                countdownLabel.Text = "Restante  " + FormatClock(remaining);
                detailLabel.Text = keepActiveService.PulseCount == 0
                    ? "Actúa después de 45 s sin entrada"
                    : string.Format("Pulsos mínimos: {0}", keepActiveService.PulseCount);
            }
            else
            {
                countdownLabel.Text = keepActiveService.StatusMessage;
                detailLabel.Text = "Sin actividad en segundo plano";
            }

            if (Visible && active)
            {
                visibleTimer.Start();
            }
            else
            {
                visibleTimer.Stop();
            }
        }

        public void ShowFromTray()
        {
            if (!Visible)
            {
                ShowInTaskbar = true;
                Show();
            }

            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            BringToFront();
            Activate();
            RefreshState();
        }

        public void AllowCloseAndClose()
        {
            allowClose = true;
            visibleTimer.Stop();
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                visibleTimer.Dispose();
                toolTip.Dispose();
            }

            base.Dispose(disposing);
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

        private void ApplyCardTheme(RoundedPanel card)
        {
            card.BackColor = palette.Surface;
            card.BorderColor = palette.Border;
        }

        private static string FormatClock(TimeSpan remaining)
        {
            if (remaining < TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
            }

            return string.Format(
                "{0:00}:{1:00}:{2:00}",
                (int)remaining.TotalHours,
                remaining.Minutes,
                remaining.Seconds);
        }

        private void DurationSelectorChanged(object sender, EventArgs eventArgs)
        {
            int minutes = durationSelector.SelectedMinutes;
            if (minutes != settings.DurationMinutes)
            {
                EventHandler<DurationChangedEventArgs> handler = DurationChanged;
                if (handler != null)
                {
                    handler(this, new DurationChangedEventArgs(minutes));
                }
            }
        }

        private void StartupToggleCheckedChanged(object sender, EventArgs eventArgs)
        {
            if (suppressStartupChange)
            {
                return;
            }

            EventHandler<StartupChangedEventArgs> handler = StartupChanged;
            if (handler != null)
            {
                handler(this, new StartupChangedEventArgs(startupToggle.Checked));
            }
        }

        private void OnToggleRequested()
        {
            EventHandler handler = ToggleRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void OnScreenCaptureRequested()
        {
            EventHandler handler = ScreenCaptureRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private async void UpdateLinkClicked(object sender, LinkLabelLinkClickedEventArgs eventArgs)
        {
            updateLink.Enabled = false;
            string previousText = updateLink.Text;
            updateLink.Text = "Consultando…";

            UpdateCheckResult result = await UpdateService.CheckAsync();

            updateLink.Text = previousText;
            updateLink.Enabled = true;

            if (result.IsAvailable)
            {
                DialogResult choice = MessageBox.Show(
                    this,
                    result.Message + "\n\n¿Quieres abrir la página oficial de la versión?",
                    "Actualización disponible",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);
                if (choice == DialogResult.Yes)
                {
                    BrowserLauncher.OpenGitHubRelease(result.ReleasePage);
                }

                return;
            }

            MessageBox.Show(
                this,
                result.Message,
                "Actualizaciones de Raudo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void MainFormClosing(object sender, FormClosingEventArgs eventArgs)
        {
            bool systemIsClosing = eventArgs.CloseReason == CloseReason.WindowsShutDown
                || eventArgs.CloseReason == CloseReason.TaskManagerClosing;

            if (!allowClose && !systemIsClosing)
            {
                eventArgs.Cancel = true;
                HideToTray();
            }
        }

        private void MainFormResize(object sender, EventArgs eventArgs)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                HideToTray();
            }
        }

        private void MainFormVisibleChanged(object sender, EventArgs eventArgs)
        {
            RefreshState();
        }

        private void HideToTray()
        {
            visibleTimer.Stop();
            Hide();
            ShowInTaskbar = false;
        }
    }

    internal sealed class DurationOption
    {
        public DurationOption(string label, int minutes)
        {
            Label = label;
            Minutes = minutes;
        }

        public string Label { get; private set; }
        public int Minutes { get; private set; }

        public static bool IsSupported(int minutes)
        {
            return minutes == 15 || minutes == 30 || minutes == 60 || minutes == 120;
        }

        public static string GetLabel(int minutes)
        {
            switch (minutes)
            {
                case 15:
                    return "15 minutos";
                case 60:
                    return "1 hora";
                case 120:
                    return "2 horas";
                default:
                    return "30 minutos";
            }
        }

        public override string ToString()
        {
            return Label;
        }
    }

    internal sealed class DurationChangedEventArgs : EventArgs
    {
        public DurationChangedEventArgs(int minutes)
        {
            Minutes = minutes;
        }

        public int Minutes { get; private set; }
    }
}
