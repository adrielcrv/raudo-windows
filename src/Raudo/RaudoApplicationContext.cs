using System;
using System.Drawing;
using System.Threading;
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
        private readonly ToolStripMenuItem toggleItem;
        private readonly ToolStripMenuItem durationMenu;
        private readonly ToolStripMenuItem startupItem;
        private readonly ToolStripMenuItem miniModeItem;
        private readonly System.Windows.Forms.Timer reminderRetryTimer;
        private readonly Icon idleIcon;
        private readonly Icon activeIcon;
        private readonly Control dispatcher;
        private readonly RegisteredWaitHandle showRequestRegistration;
        private readonly MainForm form;
        private readonly VirtualDesktopService virtualDesktopService;

        private MiniForm miniForm;
        private ConnectedMinimizeTransition minimizeTransition;
        private KeepActivePhase? pendingReminder;
        private DateTime pendingReminderExpiresUtc;
        private KeepActivePhase observedPhase;

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
                    if (miniForm != null)
                    {
                        miniForm.ApplyTheme(current);
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
                miniForm = new MiniForm(virtualDesktopService, settings);
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
            reminderRetryTimer.Stop();
            if (minimizeTransition != null)
            {
                minimizeTransition.Cancel();
                minimizeTransition = null;
            }
            notifyIcon.Visible = false;
            keepActiveService.Dispose();
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
