using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Raudo
{
    internal sealed class RaudoApplicationContext : ApplicationContext
    {
        private readonly SettingsStore settingsStore;
        private readonly RaudoSettings settings;
        private readonly KeepActiveService keepActiveService;
        private readonly NotifyIcon notifyIcon;
        private readonly ContextMenuStrip trayMenu;
        private readonly ToolStripMenuItem statusItem;
        private readonly ToolStripMenuItem saltoItem;
        private readonly ToolStripMenuItem voiceItem;
        private readonly ToolStripMenuItem toggleItem;
        private readonly ToolStripMenuItem durationMenu;
        private readonly ToolStripMenuItem mediaMenu;
        private readonly ToolStripMenuItem startupItem;
        private readonly ToolStripMenuItem miniModeItem;
        private readonly System.Windows.Forms.Timer reminderRetryTimer;
        private readonly Icon idleIcon;
        private readonly Icon activeIcon;
        private readonly Control dispatcher;
        private readonly RegisteredWaitHandle showRequestRegistration;
        private readonly MainForm form;
        private readonly VirtualDesktopService virtualDesktopService;
        private readonly InstalledApplicationCatalog installedApplicationCatalog;
        private readonly QuickResultProvider quickResultProvider;
        private readonly MediaControlService mediaControlService;
        private readonly RaudoActionCatalog actionCatalog;
        private readonly GlobalHotKey saltoHotKey;
        private readonly GlobalHotKey voiceHotKey;
        private readonly VoiceRecognitionService voiceRecognitionService;

        private MiniForm miniForm;
        private SaltoForm saltoForm;
        private VoiceOverlayForm voiceOverlayForm;
        private ConnectedMinimizeTransition minimizeTransition;
        private KeepActivePhase? pendingReminder;
        private DateTime pendingReminderExpiresUtc;
        private KeepActivePhase observedPhase;
        private bool voiceSessionActive;
        private int voiceSessionVersion;

        private bool exiting;

        public RaudoApplicationContext(bool showWindowAtStartup, WaitHandle showRequestEvent)
        {
            settingsStore = new SettingsStore();
            settings = settingsStore.Load();
            idleIcon = IconFactory.Create(false);
            activeIcon = IconFactory.Create(true);
            keepActiveService = new KeepActiveService();
            keepActiveService.StateChanged += KeepActiveServiceStateChanged;
            keepActiveService.AttentionRequired += KeepActiveServiceAttentionRequired;
            virtualDesktopService = new VirtualDesktopService();
            installedApplicationCatalog = new InstalledApplicationCatalog();
            installedApplicationCatalog.LoadCompleted += InstalledApplicationsLoadCompleted;
            quickResultProvider = new QuickResultProvider(ClipboardWriter.TryCopy);
            mediaControlService = new MediaControlService();
            voiceRecognitionService = new VoiceRecognitionService();
            voiceRecognitionService.PhaseChanged += VoiceRecognitionPhaseChanged;
            actionCatalog = new RaudoActionCatalog(
                CreateSaltoActions,
                quickResultProvider.CreateActions);

            form = new MainForm(keepActiveService, settings, idleIcon);
            form.ToggleRequested += ToggleRequested;
            form.DurationChanged += DurationChanged;
            form.ScreenCaptureRequested += ScreenCaptureRequested;
            form.StartupChanged += StartupChanged;
            form.MiniModeChanged += MiniModeChanged;
            form.MinimizeRequested += MainFormMinimizeRequested;
            form.UpdateRestartRequested += MainFormUpdateRestartRequested;

            trayMenu = new ContextMenuStrip();
            trayMenu.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            trayMenu.Opening += TrayMenuOpening;

            statusItem = new ToolStripMenuItem("Pulso: apagado");
            statusItem.Enabled = false;
            statusItem.Font = new Font(trayMenu.Font, FontStyle.Bold);
            trayMenu.Items.Add(statusItem);

            saltoItem = new ToolStripMenuItem("Abrir Salto");
            saltoItem.ShortcutKeyDisplayString = "Ctrl + Alt + Espacio";
            saltoItem.Click += delegate { ShowSalto(); };
            trayMenu.Items.Add(saltoItem);

            voiceItem = new ToolStripMenuItem("Hablar con Raudo");
            voiceItem.ShortcutKeyDisplayString = "Ctrl + Alt + V";
            voiceItem.Click += delegate { ToggleVoiceSession(); };
            trayMenu.Items.Add(voiceItem);
            trayMenu.Items.Add(new ToolStripSeparator());

            toggleItem = new ToolStripMenuItem();
            toggleItem.Click += ToggleRequested;
            trayMenu.Items.Add(toggleItem);

            durationMenu = new ToolStripMenuItem("Duración");
            AddDurationItem("15 minutos", 15);
            AddDurationItem("30 minutos", 30);
            AddDurationItem("1 hora", 60);
            AddDurationItem("2 horas", 120);
            trayMenu.Items.Add(durationMenu);
            trayMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem captureItem = new ToolStripMenuItem("Recortar pantalla");
            captureItem.ShortcutKeyDisplayString = "Win + Shift + S";
            captureItem.Click += ScreenCaptureRequested;
            trayMenu.Items.Add(captureItem);

            mediaMenu = new ToolStripMenuItem("Multimedia");
            AddMediaMenuItems();
            trayMenu.Items.Add(mediaMenu);

            miniModeItem = new ToolStripMenuItem("Modo Mini");
            miniModeItem.CheckOnClick = true;
            miniModeItem.Checked = settings.MiniModeEnabled;
            miniModeItem.Enabled = virtualDesktopService.IsAvailable;
            miniModeItem.Click += MiniModeMenuItemClick;
            trayMenu.Items.Add(miniModeItem);

            ToolStripMenuItem openItem = new ToolStripMenuItem("Abrir Raudo");
            openItem.Click += delegate { ShowWindow(); };
            trayMenu.Items.Add(openItem);

            startupItem = new ToolStripMenuItem("Iniciar con Windows");
            startupItem.CheckOnClick = true;
            startupItem.Checked = StartupManager.IsEnabled();
            startupItem.Click += StartupMenuItemClick;
            trayMenu.Items.Add(startupItem);
            trayMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exitItem = new ToolStripMenuItem("Salir de Raudo");
            exitItem.Click += delegate { ExitThread(); };
            trayMenu.Items.Add(exitItem);

            notifyIcon = new NotifyIcon();
            notifyIcon.ContextMenuStrip = trayMenu;
            notifyIcon.Icon = idleIcon;
            notifyIcon.Text = "Raudo · Pulso apagado";
            notifyIcon.Visible = true;
            notifyIcon.MouseClick += NotifyIconMouseClick;
            notifyIcon.BalloonTipClicked += NotifyIconBalloonTipClicked;

            reminderRetryTimer = new System.Windows.Forms.Timer();
            reminderRetryTimer.Interval = 5000;
            reminderRetryTimer.Tick += ReminderRetryTimerTick;

            dispatcher = new Control();
            IntPtr dispatcherHandle = dispatcher.Handle;
            saltoHotKey = new GlobalHotKey(
                HotKeyModifiers.Control
                    | HotKeyModifiers.Alt
                    | HotKeyModifiers.NoRepeat,
                Keys.Space);
            saltoHotKey.Pressed += SaltoHotKeyPressed;
            form.SetSaltoShortcutAvailable(saltoHotKey.IsRegistered);
            if (!saltoHotKey.IsRegistered)
            {
                saltoItem.ShortcutKeyDisplayString = "Atajo no disponible";
            }

            voiceHotKey = new GlobalHotKey(
                HotKeyModifiers.Control
                    | HotKeyModifiers.Alt
                    | HotKeyModifiers.NoRepeat,
                Keys.V);
            voiceHotKey.Pressed += VoiceHotKeyPressed;
            if (!voiceHotKey.IsRegistered)
            {
                voiceItem.ShortcutKeyDisplayString = "Atajo no disponible";
            }

            showRequestRegistration = ThreadPool.RegisterWaitForSingleObject(
                showRequestEvent,
                ShowRequestSignaled,
                null,
                Timeout.Infinite,
                false);

            SystemEvents.SessionSwitch += SystemSessionSwitch;
            SystemEvents.PowerModeChanged += SystemPowerModeChanged;
            SystemEvents.UserPreferenceChanged += SystemUserPreferenceChanged;
            SystemEvents.DisplaySettingsChanged += SystemDisplaySettingsChanged;

            UpdatePresentation();
            if (settings.MiniModeEnabled && virtualDesktopService.IsAvailable)
            {
                EnsureMiniForm();
            }

            if (showWindowAtStartup)
            {
                ShowWindow();
            }
        }

        private void AddDurationItem(string label, int minutes)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label);
            item.Tag = minutes;
            item.Click += DurationMenuItemClick;
            durationMenu.DropDownItems.Add(item);
        }

        private void AddMediaMenuItems()
        {
            IList<MediaControlDefinition> definitions = MediaControlCatalog.GetDefinitions();
            for (int index = 0; index < definitions.Count; index++)
            {
                if (index == 3)
                {
                    mediaMenu.DropDownItems.Add(new ToolStripSeparator());
                }

                MediaControlDefinition definition = definitions[index];
                ToolStripMenuItem item = new ToolStripMenuItem(definition.Title);
                item.Tag = definition.Command;
                item.Click += MediaControlMenuItemClick;
                mediaMenu.DropDownItems.Add(item);
            }
        }

        private IList<RaudoAction> CreateSaltoActions()
        {
            List<RaudoAction> actions = new List<RaudoAction>();
            bool active = keepActiveService.IsActive;
            string duration = DurationOption.GetLabel(settings.DurationMinutes).ToLowerInvariant();
            actions.Add(new RaudoAction(
                "voice.listen",
                "Hablar con Raudo",
                "Una orden en español · procesamiento local",
                "voz hablar microfono escuchar comando local",
                voiceHotKey != null && voiceHotKey.IsRegistered
                    ? "Ctrl + Alt + V"
                    : string.Empty,
                RaudoActionGlyph.Voice,
                delegate { ToggleVoiceSession(); }));

            actions.Add(new RaudoAction(
                "pulse.toggle",
                active ? "Detener Pulso" : "Iniciar Pulso",
                active
                    ? "Sesión activa · "
                        + FormatRemaining(keepActiveService.GetRemaining() ?? TimeSpan.Zero)
                    : "Mantener disponible durante " + duration,
                "mantener activo disponibilidad ausencia mouse iniciar detener",
                string.Empty,
                RaudoActionGlyph.Pulse,
                delegate { ToggleRequested(this, EventArgs.Empty); }));

            actions.Add(new RaudoAction(
                "capture.screen",
                "Recortar pantalla",
                "Seleccionar una región con la herramienta de Windows",
                "captura screenshot recorte crop pantalla imagen",
                "Win + Shift + S",
                RaudoActionGlyph.Capture,
                delegate { ScreenCaptureRequested(this, EventArgs.Empty); }));

            actions.Add(new RaudoAction(
                "window.main",
                "Abrir Raudo",
                "Mostrar controles y preferencias",
                "ventana principal configuracion ajustes preferencias",
                string.Empty,
                RaudoActionGlyph.MainWindow,
                ShowWindow));

            if (virtualDesktopService.IsAvailable)
            {
                actions.Add(new RaudoAction(
                    "mini.toggle",
                    settings.MiniModeEnabled ? "Ocultar Modo Mini" : "Mostrar Modo Mini",
                    settings.MiniModeEnabled
                        ? "Retirar el control del borde"
                        : "Navegar entre escritorios desde el borde",
                    "mini burbuja borde escritorios ventanas mostrar ocultar",
                    string.Empty,
                    RaudoActionGlyph.Mini,
                    delegate { SetMiniMode(!settings.MiniModeEnabled, true); }));

                bool canNavigateLeft;
                bool canNavigateRight;
                if (virtualDesktopService.TryGetNavigationAvailability(
                    out canNavigateLeft,
                    out canNavigateRight))
                {
                    if (canNavigateLeft)
                    {
                        actions.Add(new RaudoAction(
                            "desktop.left",
                            "Escritorio anterior",
                            "Cambiar al escritorio virtual de la izquierda",
                            "escritorio anterior izquierda cambiar navegar",
                            "Ctrl + Win + ←",
                            RaudoActionGlyph.DesktopLeft,
                            delegate { SwitchDesktop(DesktopDirection.Left); }));
                    }

                    if (canNavigateRight)
                    {
                        actions.Add(new RaudoAction(
                            "desktop.right",
                            "Escritorio siguiente",
                            "Cambiar al escritorio virtual de la derecha",
                            "escritorio siguiente derecha cambiar navegar",
                            "Ctrl + Win + →",
                            RaudoActionGlyph.DesktopRight,
                            delegate { SwitchDesktop(DesktopDirection.Right); }));
                    }
                }
            }

            foreach (RaudoAction folderAction in KnownFolderCatalog.CreateActions())
            {
                actions.Add(folderAction);
            }

            foreach (RaudoAction mediaAction in MediaControlCatalog.CreateActions(
                mediaControlService))
            {
                actions.Add(mediaAction);
            }

            IList<DesktopWindow> windows;
            string windowError;
            if (virtualDesktopService.TryGetOpenWindows(out windows, out windowError))
            {
                foreach (DesktopWindow rawWindow in windows)
                {
                    DesktopWindow window = rawWindow;
                    string applicationName = string.IsNullOrWhiteSpace(window.ApplicationName)
                        ? "Aplicación"
                        : window.ApplicationName;
                    string displayTitle = string.IsNullOrWhiteSpace(window.ApplicationName)
                        ? window.Title
                        : applicationName + " — " + window.Title;
                    actions.Add(new RaudoAction(
                        "open-window:" + window.Handle.ToInt64().ToString("X"),
                        displayTitle,
                        window.IsOnCurrentDesktop
                            ? "Ventana · Este escritorio"
                            : "Ventana · Otro escritorio",
                        applicationName + " ventana escritorio " + window.Title,
                        window.IsOnCurrentDesktop ? "Abrir" : "Traer",
                        RaudoActionGlyph.Window,
                        RaudoActionKind.Window,
                        false,
                        0,
                        delegate { return BringWindow(window.Handle); }));
                }
            }

            IList<InstalledApplication> installed = installedApplicationCatalog.GetSnapshot();
            foreach (InstalledApplication rawApplication in installed)
            {
                InstalledApplication application = rawApplication;
                actions.Add(new RaudoAction(
                    "open-application:" + application.Identifier,
                    application.Name,
                    "Aplicación instalada",
                    "aplicacion programa iniciar abrir " + application.Name,
                    "Abrir",
                    RaudoActionGlyph.Application,
                    RaudoActionKind.Application,
                    false,
                    15,
                    delegate
                    {
                        return InstalledApplicationLauncher.TryLaunch(
                            application.Identifier);
                    }));
            }

            return actions;
        }

        private string BringWindow(IntPtr handle)
        {
            string error;
            return virtualDesktopService.TryBringHere(handle, out error)
                ? null
                : error ?? "Windows no pudo abrir la ventana seleccionada.";
        }

        private void MediaControlMenuItemClick(object sender, EventArgs eventArgs)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null || !(item.Tag is MediaCommand))
            {
                return;
            }

            string error = mediaControlService.TryExecute((MediaCommand)item.Tag);
            if (!string.IsNullOrWhiteSpace(error))
            {
                notifyIcon.ShowBalloonTip(
                    2500,
                    "Control multimedia",
                    error,
                    ToolTipIcon.Warning);
            }
        }

        private void ToggleRequested(object sender, EventArgs eventArgs)
        {
            if (keepActiveService.IsActive)
            {
                keepActiveService.Stop("Detenido por ti");
            }
            else
            {
                keepActiveService.Start(settings.DurationMinutes);
            }
        }

        private void DurationChanged(object sender, DurationChangedEventArgs eventArgs)
        {
            settings.DurationMinutes = eventArgs.Minutes;
            SaveSettings();
            UpdatePresentation();
        }

        private void DurationMenuItemClick(object sender, EventArgs eventArgs)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null)
            {
                return;
            }

            settings.DurationMinutes = (int)item.Tag;
            form.SelectDuration(settings.DurationMinutes);
            SaveSettings();
            UpdatePresentation();
        }

        private void ScreenCaptureRequested(object sender, EventArgs eventArgs)
        {
            try
            {
                ScreenCaptureLauncher.Launch();
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    form,
                    "No se pudo abrir la Herramienta Recortes.\n\n" + exception.Message,
                    "Raudo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void SwitchDesktop(DesktopDirection direction)
        {
            string error;
            if (!DesktopNavigation.TrySwitch(direction, out error))
            {
                MessageBox.Show(
                    error ?? "Windows no pudo cambiar de escritorio.",
                    "Raudo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void StartupChanged(object sender, StartupChangedEventArgs eventArgs)
        {
            SetStartup(eventArgs.Enabled);
        }

        private void StartupMenuItemClick(object sender, EventArgs eventArgs)
        {
            SetStartup(startupItem.Checked);
        }

        private void MiniModeChanged(object sender, MiniModeChangedEventArgs eventArgs)
        {
            SetMiniMode(eventArgs.Enabled, true);
        }

        private void MainFormMinimizeRequested(
            object sender,
            MinimizeRequestedEventArgs eventArgs)
        {
            if (!settings.MiniModeEnabled || miniForm == null || !miniForm.Visible)
            {
                return;
            }

            eventArgs.Handled = true;
            if (minimizeTransition != null)
            {
                minimizeTransition.Cancel();
                minimizeTransition = null;
            }

            if (!MotionSettings.ClientAreaAnimationsEnabled())
            {
                form.HideToTrayImmediately();
                miniForm.PulseLanding();
                return;
            }

            minimizeTransition = ConnectedMinimizeTransition.Start(
                form,
                miniForm.Bounds,
                delegate
                {
                    minimizeTransition = null;
                    if (miniForm != null && !miniForm.IsDisposed)
                    {
                        miniForm.PulseLanding();
                    }
                });
            if (minimizeTransition == null)
            {
                form.HideToTrayImmediately();
                miniForm.PulseLanding();
            }
        }

        private void MainFormUpdateRestartRequested(object sender, EventArgs eventArgs)
        {
            ExitThread();
        }

        private void MiniModeMenuItemClick(object sender, EventArgs eventArgs)
        {
            SetMiniMode(miniModeItem.Checked, true);
        }

        private void SetStartup(bool enabled)
        {
            try
            {
                StartupManager.SetEnabled(enabled);
                startupItem.Checked = enabled;
                form.SetStartupChecked(enabled);
            }
            catch (Exception exception)
            {
                bool actual = StartupManager.IsEnabled();
                startupItem.Checked = actual;
                form.SetStartupChecked(actual);
                MessageBox.Show(
                    form,
                    "No se pudo cambiar el inicio con Windows.\n\n" + exception.Message,
                    "Raudo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void SetMiniMode(bool enabled, bool offerPinHelp)
        {
            if (enabled && !virtualDesktopService.IsAvailable)
            {
                enabled = false;
                MessageBox.Show(
                    form,
                    "El Modo Mini requiere Windows 10 o posterior.",
                    "Raudo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            settings.MiniModeEnabled = enabled;
            miniModeItem.Checked = enabled;
            form.SetMiniModeChecked(enabled);

            if (enabled)
            {
                EnsureMiniForm();
            }
            else
            {
                DestroyMiniForm();
            }

            SaveSettings();

            if (enabled
                && offerPinHelp
                && miniForm != null
                && !miniForm.FollowsActiveDesktop
                && !settings.MiniHintShown)
            {
                settings.MiniHintShown = true;
                SaveSettings();
                ShowMiniPinHelp();
            }
        }

        private void SaveSettings()
        {
            try
            {
                settingsStore.Save(settings);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    form,
                    "No se pudo guardar la configuración.\n\n" + exception.Message,
                    "Raudo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void NotifyIconMouseClick(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button == MouseButtons.Left)
            {
                ShowWindow();
            }
        }

        private void TrayMenuOpening(object sender, System.ComponentModel.CancelEventArgs eventArgs)
        {
            miniModeItem.Checked = settings.MiniModeEnabled;
            UpdatePresentation();
        }

        private void KeepActiveServiceStateChanged(object sender, EventArgs eventArgs)
        {
            if (keepActiveService.Phase != observedPhase)
            {
                ClearPendingReminder();
                observedPhase = keepActiveService.Phase;
            }

            UpdatePresentation();
        }

        private void KeepActiveServiceAttentionRequired(
            object sender,
            KeepActiveAttentionEventArgs eventArgs)
        {
            if (miniForm != null)
            {
                miniForm.PulseAttention();
            }

            TryShowReminder(eventArgs.Phase);
        }

        private void NotifyIconBalloonTipClicked(object sender, EventArgs eventArgs)
        {
            ShowWindow();
        }

        private void ReminderRetryTimerTick(object sender, EventArgs eventArgs)
        {
            if (!pendingReminder.HasValue || DateTime.UtcNow >= pendingReminderExpiresUtc)
            {
                ClearPendingReminder();
                return;
            }

            if (ShellUserState.AcceptsNotifications(ShellUserState.Current()))
            {
                KeepActivePhase phase = pendingReminder.Value;
                ClearPendingReminder();
                ShowReminder(phase);
            }
        }

        private void SystemSessionSwitch(object sender, SessionSwitchEventArgs eventArgs)
        {
            if (eventArgs.Reason == SessionSwitchReason.SessionLock
                || eventArgs.Reason == SessionSwitchReason.SessionLogoff
                || eventArgs.Reason == SessionSwitchReason.RemoteDisconnect)
            {
                RunOnUiThread(delegate
                {
                    if (saltoForm != null)
                    {
                        saltoForm.HideSalto();
                    }

                    voiceSessionVersion++;
                    voiceSessionActive = false;
                    voiceRecognitionService.Cancel();
                    if (voiceOverlayForm != null)
                    {
                        voiceOverlayForm.HideOverlay();
                    }

                    if (keepActiveService.IsActive)
                    {
                        keepActiveService.Stop("Detenido al cerrar la sesión interactiva");
                    }
                });
            }
            else if (eventArgs.Reason == SessionSwitchReason.SessionUnlock
                || eventArgs.Reason == SessionSwitchReason.RemoteConnect)
            {
                RunOnUiThread(delegate
                {
                    if (settings.MiniModeEnabled)
                    {
                        EnsureMiniForm();
                    }
                });
            }
        }

        private void SystemPowerModeChanged(object sender, PowerModeChangedEventArgs eventArgs)
        {
            if (eventArgs.Mode == PowerModes.Suspend)
            {
                RunOnUiThread(delegate
                {
                    voiceSessionVersion++;
                    voiceSessionActive = false;
                    voiceRecognitionService.Cancel();
                    if (voiceOverlayForm != null)
                    {
                        voiceOverlayForm.HideOverlay();
                    }

                    if (keepActiveService.IsActive)
                    {
                        keepActiveService.Stop("Detenido al suspender Windows");
                    }
                });
            }
        }

        private void SystemUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs eventArgs)
        {
            if (eventArgs.Category == UserPreferenceCategory.General
                || eventArgs.Category == UserPreferenceCategory.VisualStyle)
            {
                RunOnUiThread(delegate
                {
                    ThemePalette current = ThemeService.Current();
                    form.ApplyTheme(current);
                    if (saltoForm != null)
                    {
                        saltoForm.ApplyTheme(current);
                    }

                    if (miniForm != null)
                    {
                        miniForm.ApplyTheme(current);
                    }

                    if (voiceOverlayForm != null)
                    {
                        voiceOverlayForm.ApplyTheme(current);
                    }
                });
            }
        }

        private void SystemDisplaySettingsChanged(object sender, EventArgs eventArgs)
        {
            RunOnUiThread(delegate
            {
                if (miniForm != null)
                {
                    miniForm.EnsureVisibleOnScreen();
                }

                if (saltoForm != null)
                {
                    saltoForm.EnsureVisibleOnScreen();
                }

                if (voiceOverlayForm != null)
                {
                    voiceOverlayForm.EnsureVisibleOnScreen();
                }
            });
        }

        private void RunOnUiThread(MethodInvoker action)
        {
            if (exiting || dispatcher.IsDisposed || !dispatcher.IsHandleCreated)
            {
                return;
            }

            try
            {
                dispatcher.BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void ShowWindow()
        {
            if (!exiting)
            {
                form.ShowFromTray();
            }
        }

        private void ShowSalto()
        {
            if (exiting)
            {
                return;
            }

            EnsureSaltoForm();
            installedApplicationCatalog.EnsureLoading();
            saltoForm.SetApplicationCatalogState(
                installedApplicationCatalog.IsLoading,
                installedApplicationCatalog.LoadError);
            saltoForm.ShowSalto();
        }

        private async void ToggleVoiceSession()
        {
            if (exiting)
            {
                return;
            }

            if (voiceSessionActive || voiceRecognitionService.IsListening)
            {
                voiceSessionVersion++;
                voiceSessionActive = false;
                voiceRecognitionService.Cancel();
                if (voiceOverlayForm != null)
                {
                    voiceOverlayForm.HideOverlay();
                }

                return;
            }

            VoiceAvailability availability = VoiceRecognitionService.GetAvailability();
            EnsureVoiceOverlayForm();
            if (!availability.IsAvailable)
            {
                voiceOverlayForm.ShowState(
                    VoiceOverlayState.Unavailable,
                    "Voz no disponible",
                    availability.Message);
                voiceOverlayForm.DismissAfter(6000);
                return;
            }

            voiceSessionActive = true;
            int sessionVersion = ++voiceSessionVersion;
            voiceOverlayForm.ShowState(
                VoiceOverlayState.Preparing,
                "Preparando voz...",
                "Español (México) · procesamiento local");

            installedApplicationCatalog.EnsureLoading();
            for (int attempt = 0;
                attempt < 30
                    && installedApplicationCatalog.IsLoading
                    && sessionVersion == voiceSessionVersion;
                attempt++)
            {
                await Task.Delay(100);
            }

            if (exiting || sessionVersion != voiceSessionVersion)
            {
                return;
            }

            VoiceRecognitionOutcome outcome = await voiceRecognitionService
                .ListenOnceAsync(installedApplicationCatalog.GetSnapshot());
            if (exiting || sessionVersion != voiceSessionVersion)
            {
                return;
            }

            voiceSessionActive = false;
            PresentVoiceOutcome(outcome);
        }

        private void VoiceHotKeyPressed(object sender, EventArgs eventArgs)
        {
            ToggleVoiceSession();
        }

        private void VoiceRecognitionPhaseChanged(
            object sender,
            VoiceRecognitionPhaseEventArgs eventArgs)
        {
            RunOnUiThread(delegate
            {
                if (!voiceSessionActive)
                {
                    return;
                }

                EnsureVoiceOverlayForm();
                if (eventArgs.Phase == VoiceRecognitionPhase.Listening)
                {
                    voiceOverlayForm.ShowState(
                        VoiceOverlayState.Listening,
                        "Escuchando...",
                        "Prueba “abre Excel” o “cuánto es 12 por 8”.");
                }
                else
                {
                    voiceOverlayForm.ShowState(
                        VoiceOverlayState.Preparing,
                        "Preparando voz...",
                        "Windows está cargando las órdenes locales.");
                }
            });
        }

        private void PresentVoiceOutcome(VoiceRecognitionOutcome outcome)
        {
            EnsureVoiceOverlayForm();
            switch (outcome.Kind)
            {
                case VoiceRecognitionOutcomeKind.Cancelled:
                    voiceOverlayForm.HideOverlay();
                    return;
                case VoiceRecognitionOutcomeKind.Unavailable:
                    voiceOverlayForm.ShowState(
                        VoiceOverlayState.Unavailable,
                        "Voz no disponible",
                        outcome.Message);
                    voiceOverlayForm.DismissAfter(6000);
                    return;
                case VoiceRecognitionOutcomeKind.Error:
                    voiceOverlayForm.ShowState(
                        VoiceOverlayState.Unavailable,
                        "No se pudo escuchar",
                        outcome.Message);
                    voiceOverlayForm.DismissAfter(5000);
                    return;
                case VoiceRecognitionOutcomeKind.NotUnderstood:
                    voiceOverlayForm.ShowState(
                        VoiceOverlayState.NotUnderstood,
                        "No entendí esa orden",
                        string.IsNullOrWhiteSpace(outcome.Text)
                            ? outcome.Message
                            : "Escuché “" + outcome.Text + "”. Intenta de nuevo.");
                    voiceOverlayForm.DismissAfter(4500);
                    return;
            }

            VoiceCommand command = VoiceCommandParser.Parse(
                outcome.Text,
                installedApplicationCatalog.GetSnapshot());
            if (!command.IsRecognized)
            {
                voiceOverlayForm.ShowState(
                    VoiceOverlayState.NotUnderstood,
                    command.Title,
                    command.Detail);
                voiceOverlayForm.DismissAfter(4500);
                return;
            }

            ExecuteVoiceCommand(command);
        }

        private void ExecuteVoiceCommand(VoiceCommand command)
        {
            string error = null;
            switch (command.Kind)
            {
                case VoiceCommandKind.OpenApplication:
                    error = InstalledApplicationLauncher.TryLaunch(
                        command.ApplicationIdentifier);
                    break;
                case VoiceCommandKind.OpenYouTube:
                    error = BrowserLauncher.TryOpenYouTube();
                    break;
                case VoiceCommandKind.OpenWeather:
                    error = BrowserLauncher.TryOpenWeather();
                    break;
                case VoiceCommandKind.OpenSalto:
                    voiceOverlayForm.HideOverlay();
                    ShowSalto();
                    return;
                case VoiceCommandKind.OpenRaudo:
                    voiceOverlayForm.HideOverlay();
                    ShowWindow();
                    return;
                case VoiceCommandKind.StartPulse:
                    if (!keepActiveService.IsActive)
                    {
                        keepActiveService.Start(settings.DurationMinutes);
                    }
                    break;
                case VoiceCommandKind.StopPulse:
                    if (keepActiveService.IsActive)
                    {
                        keepActiveService.Stop("Detenido por voz");
                    }
                    break;
                case VoiceCommandKind.DesktopLeft:
                    DesktopNavigation.TrySwitch(DesktopDirection.Left, out error);
                    break;
                case VoiceCommandKind.DesktopRight:
                    DesktopNavigation.TrySwitch(DesktopDirection.Right, out error);
                    break;
                case VoiceCommandKind.DesktopAdjacent:
                    bool canNavigateLeft;
                    bool canNavigateRight;
                    if (!virtualDesktopService.TryGetNavigationAvailability(
                        out canNavigateLeft,
                        out canNavigateRight))
                    {
                        error = "Windows no pudo consultar los escritorios virtuales.";
                    }
                    else if (canNavigateRight && !canNavigateLeft)
                    {
                        DesktopNavigation.TrySwitch(DesktopDirection.Right, out error);
                    }
                    else if (canNavigateLeft && !canNavigateRight)
                    {
                        DesktopNavigation.TrySwitch(DesktopDirection.Left, out error);
                    }
                    else if (canNavigateLeft && canNavigateRight)
                    {
                        error = "Di “escritorio izquierdo” o “escritorio derecho”.";
                    }
                    else
                    {
                        error = "No hay otro escritorio virtual disponible.";
                    }
                    break;
                case VoiceCommandKind.ScreenCapture:
                    voiceOverlayForm.HideOverlay();
                    ScreenCaptureRequested(this, EventArgs.Empty);
                    return;
                case VoiceCommandKind.MediaPlayPause:
                    error = mediaControlService.TryExecute(MediaCommand.TogglePlayPause);
                    break;
                case VoiceCommandKind.MediaPrevious:
                    error = mediaControlService.TryExecute(MediaCommand.PreviousTrack);
                    break;
                case VoiceCommandKind.MediaNext:
                    error = mediaControlService.TryExecute(MediaCommand.NextTrack);
                    break;
                case VoiceCommandKind.VolumeMute:
                    error = mediaControlService.TryExecute(MediaCommand.ToggleMute);
                    break;
                case VoiceCommandKind.VolumeDown:
                    error = mediaControlService.TryExecute(MediaCommand.VolumeDown);
                    break;
                case VoiceCommandKind.VolumeUp:
                    error = mediaControlService.TryExecute(MediaCommand.VolumeUp);
                    break;
                case VoiceCommandKind.DictationUnavailable:
                    voiceOverlayForm.ShowState(
                        VoiceOverlayState.Unavailable,
                        command.Title,
                        command.Detail);
                    voiceOverlayForm.DismissAfter(6000);
                    return;
                case VoiceCommandKind.Calculation:
                case VoiceCommandKind.Conversion:
                    break;
                default:
                    error = "La orden no pertenece al catálogo seguro de Raudo.";
                    break;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                voiceOverlayForm.ShowState(
                    VoiceOverlayState.NotUnderstood,
                    "No se pudo completar",
                    error);
                voiceOverlayForm.DismissAfter(4500);
                return;
            }

            voiceOverlayForm.ShowState(
                VoiceOverlayState.Success,
                command.Title,
                command.Detail);
            voiceOverlayForm.DismissAfter(
                command.Kind == VoiceCommandKind.Calculation
                    || command.Kind == VoiceCommandKind.Conversion
                    ? 4500
                    : 2500);
        }

        private void EnsureVoiceOverlayForm()
        {
            if (voiceOverlayForm == null || voiceOverlayForm.IsDisposed)
            {
                voiceOverlayForm = new VoiceOverlayForm();
                voiceOverlayForm.CancelRequested += VoiceOverlayCancelRequested;
                voiceOverlayForm.ApplyTheme(ThemeService.Current());
            }
        }

        private void VoiceOverlayCancelRequested(object sender, EventArgs eventArgs)
        {
            if (voiceSessionActive || voiceRecognitionService.IsListening)
            {
                ToggleVoiceSession();
            }
        }

        private void SaltoHotKeyPressed(object sender, EventArgs eventArgs)
        {
            if (exiting)
            {
                return;
            }

            EnsureSaltoForm();
            if (saltoForm.Visible)
            {
                saltoForm.HideSalto();
            }
            else
            {
                ShowSalto();
            }
        }

        private void EnsureSaltoForm()
        {
            if (saltoForm == null || saltoForm.IsDisposed)
            {
                saltoForm = new SaltoForm(actionCatalog, settings);
                saltoForm.PositionChangedByUser += SaltoPositionChangedByUser;
                saltoForm.OpacityChangedByUser += SaltoOpacityChangedByUser;
                saltoForm.ApplyTheme(ThemeService.Current());
            }
        }

        private void SaltoPositionChangedByUser(
            object sender,
            SaltoPositionChangedEventArgs eventArgs)
        {
            settings.SaltoCenterX = eventArgs.Anchor.X;
            settings.SaltoTopY = eventArgs.Anchor.Y;
            SaveSettings();
        }

        private void SaltoOpacityChangedByUser(
            object sender,
            SaltoOpacityChangedEventArgs eventArgs)
        {
            settings.SaltoOpacityPercent = eventArgs.OpacityPercent;
            SaveSettings();
        }

        private void InstalledApplicationsLoadCompleted(object sender, EventArgs eventArgs)
        {
            RunOnUiThread(delegate
            {
                if (saltoForm == null || saltoForm.IsDisposed)
                {
                    return;
                }

                saltoForm.SetApplicationCatalogState(
                    false,
                    installedApplicationCatalog.LoadError);
                if (saltoForm.Visible)
                {
                    saltoForm.RefreshCatalog();
                }
            });
        }

        private void ShowRequestSignaled(object state, bool timedOut)
        {
            RunOnUiThread(ShowWindow);
        }

        private void UpdatePresentation()
        {
            bool active = keepActiveService.IsActive;
            string durationLabel = DurationOption.GetLabel(settings.DurationMinutes);

            notifyIcon.Icon = active ? activeIcon : idleIcon;
            statusItem.Text = active ? "Pulso: encendido" : "Pulso: apagado";
            toggleItem.Text = active
                ? "Detener Pulso"
                : "Iniciar Pulso por " + durationLabel.ToLowerInvariant();
            durationMenu.Enabled = !active;

            foreach (ToolStripItem rawItem in durationMenu.DropDownItems)
            {
                ToolStripMenuItem item = rawItem as ToolStripMenuItem;
                if (item != null)
                {
                    item.Checked = (int)item.Tag == settings.DurationMinutes;
                }
            }

            if (active)
            {
                TimeSpan remaining = keepActiveService.GetRemaining() ?? TimeSpan.Zero;
                notifyIcon.Text = LimitTooltip("Raudo · Pulso · " + FormatRemaining(remaining));
            }
            else
            {
                notifyIcon.Text = "Raudo · Pulso apagado";
            }

            form.RefreshState();
            if (miniForm != null)
            {
                miniForm.SetSessionPhase(keepActiveService.Phase);
            }
        }

        private void EnsureMiniForm()
        {
            if (miniForm == null || miniForm.IsDisposed)
            {
                miniForm = new MiniForm(
                    virtualDesktopService,
                    settings,
                    mediaControlService,
                    null);
                miniForm.OpenMainRequested += MiniOpenMainRequested;
                miniForm.HideRequested += MiniHideRequested;
                miniForm.PinHelpRequested += MiniPinHelpRequested;
                miniForm.PositionChangedByUser += MiniPositionChangedByUser;
                miniForm.ApplyTheme(ThemeService.Current());
                miniForm.SetSessionPhase(keepActiveService.Phase);
            }

            miniForm.ShowMini();
        }

        private void DestroyMiniForm()
        {
            if (miniForm == null)
            {
                return;
            }

            miniForm.OpenMainRequested -= MiniOpenMainRequested;
            miniForm.HideRequested -= MiniHideRequested;
            miniForm.PinHelpRequested -= MiniPinHelpRequested;
            miniForm.PositionChangedByUser -= MiniPositionChangedByUser;
            miniForm.AllowCloseAndClose();
            miniForm.Dispose();
            miniForm = null;
        }

        private void MiniOpenMainRequested(object sender, EventArgs eventArgs)
        {
            ShowWindow();
        }

        private void MiniHideRequested(object sender, EventArgs eventArgs)
        {
            RunOnUiThread(delegate { SetMiniMode(false, false); });
        }

        private void MiniPinHelpRequested(object sender, EventArgs eventArgs)
        {
            ShowMiniPinHelp();
        }

        private void MiniPositionChangedByUser(
            object sender,
            MiniPositionChangedEventArgs eventArgs)
        {
            settings.MiniCenterX = eventArgs.Center.X;
            settings.MiniCenterY = eventArgs.Center.Y;
            SaveSettings();
        }

        private void ShowMiniPinHelp()
        {
            IWin32Window owner = miniForm == null ? (IWin32Window)form : miniForm;
            MessageBox.Show(
                owner,
                "Windows no permitió fijar Raudo automáticamente. "
                    + "Puedes configurarlo una sola vez:\n\n"
                    + "1. Presiona Win + Tab.\n"
                    + "2. Haz clic derecho sobre Raudo Mini.\n"
                    + "3. Elige Mostrar esta ventana en todos los escritorios.\n\n"
                    + "Raudo seguirá funcionando aunque esta opción no esté disponible.",
                "Visibilidad en escritorios",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void TryShowReminder(KeepActivePhase phase)
        {
            if (phase != KeepActivePhase.EndingSoon
                && phase != KeepActivePhase.Critical
                && phase != KeepActivePhase.Completed)
            {
                return;
            }

            if (ShellUserState.AcceptsNotifications(ShellUserState.Current()))
            {
                ClearPendingReminder();
                ShowReminder(phase);
                return;
            }

            pendingReminder = phase;
            pendingReminderExpiresUtc = DateTime.UtcNow.AddMinutes(20);
            reminderRetryTimer.Stop();
            reminderRetryTimer.Start();
        }

        private void ShowReminder(KeepActivePhase phase)
        {
            string title;
            string message;
            ToolTipIcon icon;
            if (phase == KeepActivePhase.EndingSoon)
            {
                title = "Raudo · Pulso: 15 minutos";
                message = "Pulso terminará pronto. Abre Raudo si necesitas extender el tiempo.";
                icon = ToolTipIcon.Info;
            }
            else if (phase == KeepActivePhase.Critical)
            {
                title = "Raudo · Pulso: 5 minutos";
                message = "Pulso está por terminar. Abre Raudo para elegir otra duración.";
                icon = ToolTipIcon.Warning;
            }
            else
            {
                title = "Raudo · Pulso finalizó";
                message = "El equipo vuelve a su comportamiento normal.";
                icon = ToolTipIcon.Warning;
            }

            notifyIcon.ShowBalloonTip(6000, title, message, icon);
        }

        private void ClearPendingReminder()
        {
            reminderRetryTimer.Stop();
            pendingReminder = null;
            pendingReminderExpiresUtc = DateTime.MinValue;
        }

        private static string FormatRemaining(TimeSpan remaining)
        {
            if (remaining.TotalHours >= 1)
            {
                return string.Format("{0} h {1} min restantes", (int)remaining.TotalHours, remaining.Minutes);
            }

            return string.Format("{0} min restantes", Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes)));
        }

        private static string LimitTooltip(string value)
        {
            return value.Length <= 63 ? value : value.Substring(0, 63);
        }

        protected override void ExitThreadCore()
        {
            if (exiting)
            {
                return;
            }

            exiting = true;
            SystemEvents.SessionSwitch -= SystemSessionSwitch;
            SystemEvents.PowerModeChanged -= SystemPowerModeChanged;
            SystemEvents.UserPreferenceChanged -= SystemUserPreferenceChanged;
            SystemEvents.DisplaySettingsChanged -= SystemDisplaySettingsChanged;
            keepActiveService.StateChanged -= KeepActiveServiceStateChanged;
            keepActiveService.AttentionRequired -= KeepActiveServiceAttentionRequired;
            form.MinimizeRequested -= MainFormMinimizeRequested;
            form.UpdateRestartRequested -= MainFormUpdateRestartRequested;
            saltoHotKey.Pressed -= SaltoHotKeyPressed;
            saltoHotKey.Dispose();
            voiceHotKey.Pressed -= VoiceHotKeyPressed;
            voiceHotKey.Dispose();
            voiceSessionVersion++;
            voiceSessionActive = false;
            voiceRecognitionService.PhaseChanged -= VoiceRecognitionPhaseChanged;
            voiceRecognitionService.Cancel();
            voiceRecognitionService.Dispose();
            reminderRetryTimer.Stop();
            if (minimizeTransition != null)
            {
                minimizeTransition.Cancel();
                minimizeTransition = null;
            }
            notifyIcon.Visible = false;
            installedApplicationCatalog.LoadCompleted -= InstalledApplicationsLoadCompleted;
            keepActiveService.Dispose();
            if (saltoForm != null)
            {
                saltoForm.PositionChangedByUser -= SaltoPositionChangedByUser;
                saltoForm.OpacityChangedByUser -= SaltoOpacityChangedByUser;
                saltoForm.AllowCloseAndClose();
                saltoForm.Dispose();
                saltoForm = null;
            }

            if (voiceOverlayForm != null)
            {
                voiceOverlayForm.CancelRequested -= VoiceOverlayCancelRequested;
                voiceOverlayForm.AllowCloseAndClose();
                voiceOverlayForm.Dispose();
                voiceOverlayForm = null;
            }

            DestroyMiniForm();
            virtualDesktopService.Dispose();
            form.AllowCloseAndClose();
            showRequestRegistration.Unregister(null);
            dispatcher.Dispose();
            reminderRetryTimer.Dispose();
            trayMenu.Dispose();
            notifyIcon.BalloonTipClicked -= NotifyIconBalloonTipClicked;
            notifyIcon.Dispose();
            activeIcon.Dispose();
            idleIcon.Dispose();
            base.ExitThreadCore();
        }
    }
}
