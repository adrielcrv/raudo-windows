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

    internal sealed class MiniModeChangedEventArgs : EventArgs
    {
        public MiniModeChangedEventArgs(bool enabled)
        {
            Enabled = enabled;
        }

        public bool Enabled { get; private set; }
    }

    internal sealed class MinimizeRequestedEventArgs : EventArgs
    {
        public bool Handled { get; set; }
    }

    internal sealed class MainForm : Form
    {
        private static readonly Size LogicalClientSize = new Size(520, 724);
        private readonly KeepActiveService keepActiveService;
        private readonly RaudoSettings settings;
        private readonly BrandMarkControl brandMark;
        private readonly Label titleLabel;
        private readonly Label subtitleLabel;
        private readonly LinkLabel updateLink;
        private readonly Label pulseSectionLabel;
        private readonly PulseSurface pulseSurface;
        private readonly Label actionsSectionLabel;
        private readonly ScreenCaptureSurface captureSurface;
        private readonly DesktopWorkspaceSurface desktopWorkspaceSurface;
        private readonly Label preferencesSectionLabel;
        private readonly PreferencesSurface preferencesSurface;
        private readonly Label trayHintLabel;
        private readonly Timer visibleTimer;
        private readonly ToolTip toolTip;

        private ThemePalette palette;
        private DpiMetrics dpiMetrics;
        private bool applyingDpiLayout;
        private bool allowClose;
        private bool suppressStartupChange;
        private bool suppressMiniModeChange;

        public MainForm(KeepActiveService service, RaudoSettings currentSettings, Icon appIcon)
        {
            keepActiveService = service;
            settings = currentSettings;
            dpiMetrics = DpiMetrics.ForDpi(DpiMetrics.DefaultDpi);

            Text = "Raudo";
            AccessibleDescription = "Herramientas locales y ligeras para Windows";
            ClientSize = LogicalClientSize;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            AutoScroll = true;
            Font = new Font("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point);
            Icon = appIcon;

            brandMark = new BrandMarkControl();
            brandMark.Location = new Point(24, 18);
            brandMark.Size = new Size(44, 44);
            Controls.Add(brandMark);

            titleLabel = CreateLabel(
                "Raudo",
                18F,
                FontStyle.Bold,
                new Point(82, 13),
                new Size(250, 34));
            Controls.Add(titleLabel);

            subtitleLabel = CreateLabel(
                "Herramientas rápidas para Windows",
                9F,
                FontStyle.Regular,
                new Point(83, 45),
                new Size(280, 22));
            Controls.Add(subtitleLabel);

            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            updateLink = new LinkLabel();
            updateLink.Text = "v" + version + "  ·  Buscar";
            updateLink.Font = new Font(
                "Segoe UI Semibold",
                8.25F,
                FontStyle.Bold,
                GraphicsUnit.Point);
            updateLink.TextAlign = ContentAlignment.MiddleCenter;
            updateLink.LinkBehavior = LinkBehavior.NeverUnderline;
            updateLink.Location = new Point(386, 24);
            updateLink.Size = new Size(110, 32);
            updateLink.TabIndex = 0;
            updateLink.TabStop = true;
            updateLink.AccessibleName = "Buscar actualizaciones de Raudo";
            updateLink.LinkClicked += UpdateLinkClicked;
            Controls.Add(updateLink);

            pulseSectionLabel = CreateSectionLabel("PULSO", new Point(24, 84));
            Controls.Add(pulseSectionLabel);

            pulseSurface = new PulseSurface();
            pulseSurface.Location = new Point(24, 108);
            pulseSurface.TabIndex = 1;
            pulseSurface.DurationChanged += DurationSelectorChanged;
            pulseSurface.ToggleRequested += delegate { OnToggleRequested(); };
            Controls.Add(pulseSurface);

            actionsSectionLabel = CreateSectionLabel("ACCIONES", new Point(24, 304));
            Controls.Add(actionsSectionLabel);

            captureSurface = new ScreenCaptureSurface();
            captureSurface.Location = new Point(24, 328);
            captureSurface.TabIndex = 2;
            captureSurface.ActionRequested += delegate { OnScreenCaptureRequested(); };
            Controls.Add(captureSurface);

            desktopWorkspaceSurface = new DesktopWorkspaceSurface();
            desktopWorkspaceSurface.Location = new Point(24, 412);
            desktopWorkspaceSurface.TabIndex = 3;
            desktopWorkspaceSurface.CreateRequested += delegate { OnDesktopCreateRequested(); };
            desktopWorkspaceSurface.GuideRequested += delegate { OnDesktopGuideRequested(); };
            Controls.Add(desktopWorkspaceSurface);

            preferencesSectionLabel = CreateSectionLabel("PREFERENCIAS", new Point(24, 524));
            Controls.Add(preferencesSectionLabel);

            preferencesSurface = new PreferencesSurface();
            preferencesSurface.Location = new Point(24, 548);
            preferencesSurface.TabIndex = 4;
            preferencesSurface.MiniModeChanged += MiniToggleCheckedChanged;
            preferencesSurface.StartupChanged += StartupToggleCheckedChanged;
            Controls.Add(preferencesSurface);

            trayHintLabel = CreateLabel(
                "Ctrl + Alt + Espacio abre Salto  ·  Cerrar mantiene Raudo junto al reloj.",
                8F,
                FontStyle.Regular,
                new Point(25, 692),
                new Size(470, 20));
            Controls.Add(trayHintLabel);

            visibleTimer = new Timer();
            visibleTimer.Interval = 1000;
            visibleTimer.Tick += delegate { RefreshState(); };

            toolTip = new ToolTip();
            toolTip.SetToolTip(
                updateLink,
                "Consulta manualmente la última versión publicada en GitHub");
            toolTip.SetToolTip(
                preferencesSurface,
                "Preferencias locales de Raudo");

            FormClosing += MainFormClosing;
            Resize += MainFormResize;
            VisibleChanged += MainFormVisibleChanged;
            Shown += delegate { ActiveControl = null; };

            SelectDuration(settings.DurationMinutes);
            SetStartupChecked(StartupManager.IsEnabled());
            SetMiniModeChecked(settings.MiniModeEnabled);
            ApplyTheme(ThemeService.Current());
            RefreshState();
        }

        public event EventHandler ToggleRequested;
        public event EventHandler<DurationChangedEventArgs> DurationChanged;
        public event EventHandler ScreenCaptureRequested;
        public event EventHandler DesktopCreateRequested;
        public event EventHandler DesktopGuideRequested;
        public event EventHandler<StartupChangedEventArgs> StartupChanged;
        public event EventHandler<MiniModeChangedEventArgs> MiniModeChanged;
        public event EventHandler<MinimizeRequestedEventArgs> MinimizeRequested;
        public event EventHandler UpdateRestartRequested;

        internal int CurrentDpiForTesting
        {
            get { return dpiMetrics.Dpi; }
        }

        internal Size DesiredClientSizeForTesting
        {
            get { return dpiMetrics.Scale(LogicalClientSize); }
        }

        internal void ApplyDpiForTesting(int dpi, Rectangle workingArea)
        {
            ApplyDpiLayout(
                DpiMetrics.ForDpi(dpi),
                new Rectangle(Location, Size),
                workingArea);
        }

        public void SelectDuration(int minutes)
        {
            pulseSurface.SetSelectedDuration(minutes, false);
        }

        public void SetStartupChecked(bool enabled)
        {
            suppressStartupChange = true;
            preferencesSurface.StartupEnabled = enabled;
            suppressStartupChange = false;
        }

        public void SetMiniModeChecked(bool enabled)
        {
            suppressMiniModeChange = true;
            preferencesSurface.MiniModeEnabled = enabled;
            suppressMiniModeChange = false;
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
            pulseSectionLabel.ForeColor = palette.TextFaint;
            actionsSectionLabel.ForeColor = palette.TextFaint;
            preferencesSectionLabel.ForeColor = palette.TextFaint;

            updateLink.BackColor = palette.Window;
            updateLink.LinkColor = palette.TextMuted;
            updateLink.ActiveLinkColor = palette.Primary;
            updateLink.VisitedLinkColor = palette.TextMuted;

            brandMark.ApplyTheme(palette);
            pulseSurface.ApplyTheme(palette);
            captureSurface.ApplyTheme(palette);
            desktopWorkspaceSurface.ApplyTheme(palette);
            preferencesSurface.ApplyTheme(palette);
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
            TimeSpan remaining = active
                ? keepActiveService.GetRemaining() ?? TimeSpan.Zero
                : TimeSpan.Zero;
            pulseSurface.SetState(
                active,
                settings.DurationMinutes,
                remaining,
                keepActiveService.PulseCount,
                keepActiveService.StatusMessage);

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

        public void HideToTrayImmediately()
        {
            HideToTray();
        }

        public void SetSaltoShortcutAvailable(bool available)
        {
            trayHintLabel.Text = available
                ? "Ctrl + Alt + Espacio abre Salto  ·  Cerrar mantiene Raudo junto al reloj."
                : "Abre Salto desde la bandeja  ·  Cerrar mantiene Raudo junto al reloj.";
        }

        protected override void OnHandleCreated(EventArgs eventArgs)
        {
            base.OnHandleCreated(eventArgs);
            dpiMetrics = DpiMetrics.FromControl(this);
            ApplyDpiLayout(dpiMetrics, Rectangle.Empty, Rectangle.Empty);
            if (palette != null)
            {
                WindowTheme.Apply(Handle, palette.IsDark);
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == DpiMessage.WindowDpiChanged)
            {
                int nextDpi = DpiMessage.GetDpi(message);
                Rectangle suggestedBounds = DpiMessage.GetSuggestedBounds(message);
                base.WndProc(ref message);
                ApplyDpiLayout(
                    DpiMetrics.ForDpi(nextDpi),
                    suggestedBounds,
                    Rectangle.Empty);
                return;
            }

            const int windowSystemCommand = 0x0112;
            const int minimizeCommand = 0xF020;
            if (!allowClose
                && message.Msg == windowSystemCommand
                && ((int)message.WParam & 0xFFF0) == minimizeCommand)
            {
                MinimizeRequestedEventArgs eventArgs = new MinimizeRequestedEventArgs();
                EventHandler<MinimizeRequestedEventArgs> handler = MinimizeRequested;
                if (handler != null)
                {
                    handler(this, eventArgs);
                }

                if (eventArgs.Handled)
                {
                    return;
                }
            }

            base.WndProc(ref message);
        }

        private void ApplyDpiLayout(
            DpiMetrics metrics,
            Rectangle suggestedBounds,
            Rectangle workingAreaOverride)
        {
            if (metrics == null || applyingDpiLayout)
            {
                return;
            }

            applyingDpiLayout = true;
            SuspendLayout();
            try
            {
                dpiMetrics = metrics;
                AutoScrollPosition = Point.Empty;
                ClientSize = metrics.Scale(LogicalClientSize);

                SetScaledBounds(brandMark, 24, 18, 44, 44);
                SetScaledBounds(titleLabel, 82, 13, 250, 34);
                SetScaledBounds(subtitleLabel, 83, 45, 280, 22);
                SetScaledBounds(updateLink, 386, 24, 110, 32);
                SetScaledBounds(pulseSectionLabel, 24, 84, 180, 20);
                SetScaledBounds(pulseSurface, 24, 108, 472, 176);
                pulseSurface.ApplyDpiLayout(metrics);
                SetScaledBounds(actionsSectionLabel, 24, 304, 180, 20);
                SetScaledBounds(captureSurface, 24, 328, 472, 76);
                captureSurface.ApplyDpiLayout(metrics);
                SetScaledBounds(desktopWorkspaceSurface, 24, 412, 472, 92);
                desktopWorkspaceSurface.ApplyDpiLayout(metrics);
                SetScaledBounds(preferencesSectionLabel, 24, 524, 180, 20);
                SetScaledBounds(preferencesSurface, 24, 548, 472, 124);
                preferencesSurface.ApplyDpiLayout(metrics);
                SetScaledBounds(trayHintLabel, 25, 692, 470, 20);

                Rectangle referenceBounds = suggestedBounds.IsEmpty
                    ? Bounds
                    : suggestedBounds;
                Rectangle workingArea = workingAreaOverride.Width > 0
                        && workingAreaOverride.Height > 0
                    ? workingAreaOverride
                    : Screen.FromRectangle(referenceBounds).WorkingArea;
                Point preferredLocation = suggestedBounds.IsEmpty
                    ? Location
                    : suggestedBounds.Location;
                Rectangle resolved = MainWindowLayout.ResolveBounds(
                    preferredLocation,
                    Size,
                    workingArea);
                if (suggestedBounds.IsEmpty)
                {
                    Size = resolved.Size;
                }
                else
                {
                    Bounds = resolved;
                }

                PerformLayout();
                Invalidate(true);
            }
            finally
            {
                ResumeLayout(true);
                applyingDpiLayout = false;
            }
        }

        private void SetScaledBounds(
            Control control,
            int left,
            int top,
            int width,
            int height)
        {
            DashboardLayoutMetrics.SetBounds(
                control,
                dpiMetrics,
                left,
                top,
                width,
                height);
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

        private static Label CreateSectionLabel(string text, Point location)
        {
            Label label = CreateLabel(
                text,
                7.75F,
                FontStyle.Bold,
                location,
                new Size(180, 20));
            label.AccessibleRole = AccessibleRole.StaticText;
            return label;
        }

        private void DurationSelectorChanged(object sender, EventArgs eventArgs)
        {
            int minutes = pulseSurface.SelectedMinutes;
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
                handler(
                    this,
                    new StartupChangedEventArgs(preferencesSurface.StartupEnabled));
            }
        }

        private void MiniToggleCheckedChanged(object sender, EventArgs eventArgs)
        {
            if (suppressMiniModeChange)
            {
                return;
            }

            EventHandler<MiniModeChangedEventArgs> handler = MiniModeChanged;
            if (handler != null)
            {
                handler(
                    this,
                    new MiniModeChangedEventArgs(preferencesSurface.MiniModeEnabled));
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

        private void OnDesktopCreateRequested()
        {
            EventHandler handler = DesktopCreateRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void OnDesktopGuideRequested()
        {
            EventHandler handler = DesktopGuideRequested;
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
                if (result.CanInstall)
                {
                    DialogResult installChoice = MessageBox.Show(
                        this,
                        result.Message
                            + "\n\n¿Quieres descargarla e instalarla ahora? "
                            + "Raudo verificará el paquete y se reiniciará.",
                        "Actualización disponible",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);
                    if (installChoice == DialogResult.Yes)
                    {
                        updateLink.Enabled = false;
                        updateLink.Text = "Preparando…";
                        UpdateInstallResult install = await UpdateService.InstallAsync(result);
                        if (install.Started)
                        {
                            EventHandler restartHandler = UpdateRestartRequested;
                            if (restartHandler != null)
                            {
                                restartHandler(this, EventArgs.Empty);
                            }

                            return;
                        }

                        updateLink.Text = previousText;
                        updateLink.Enabled = true;
                        MessageBox.Show(
                            this,
                            install.Message,
                            "No se pudo actualizar Raudo",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }

                    return;
                }

                DialogResult choice = MessageBox.Show(
                    this,
                    result.Message
                        + "\n\nEsta copia es portable. ¿Quieres abrir la página oficial para descargarla?",
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
