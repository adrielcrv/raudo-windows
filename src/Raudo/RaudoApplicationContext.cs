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
        private readonly Icon idleIcon;
        private readonly Icon activeIcon;
        private readonly Control dispatcher;
        private readonly RegisteredWaitHandle showRequestRegistration;
        private readonly MainForm form;

        private bool exiting;

        public RaudoApplicationContext(bool showWindowAtStartup, WaitHandle showRequestEvent)
        {
            settingsStore = new SettingsStore();
            settings = settingsStore.Load();
            idleIcon = IconFactory.Create(false);
            activeIcon = IconFactory.Create(true);
            keepActiveService = new KeepActiveService();
            keepActiveService.StateChanged += KeepActiveServiceStateChanged;

            form = new MainForm(keepActiveService, settings, idleIcon);
            form.ToggleRequested += ToggleRequested;
            form.DurationChanged += DurationChanged;
            form.ScreenCaptureRequested += ScreenCaptureRequested;
            form.StartupChanged += StartupChanged;

            trayMenu = new ContextMenuStrip();
            trayMenu.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            trayMenu.Opening += TrayMenuOpening;

            statusItem = new ToolStripMenuItem("Mantener activo: apagado");
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
            notifyIcon.Text = "Raudo · Mantener activo apagado";
            notifyIcon.Visible = true;
            notifyIcon.MouseClick += NotifyIconMouseClick;

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

            UpdatePresentation();
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
            UpdatePresentation();
        }

        private void KeepActiveServiceStateChanged(object sender, EventArgs eventArgs)
        {
            UpdatePresentation();
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
                RunOnUiThread(delegate { form.ApplyTheme(ThemeService.Current()); });
            }
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
            statusItem.Text = active ? "Mantener activo: encendido" : "Mantener activo: apagado";
            toggleItem.Text = active
                ? "Detener"
                : "Activar por " + durationLabel.ToLowerInvariant();
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
                notifyIcon.Text = LimitTooltip("Raudo · Activo · " + FormatRemaining(remaining));
            }
            else
            {
                notifyIcon.Text = "Raudo · Mantener activo apagado";
            }

            form.RefreshState();
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
            notifyIcon.Visible = false;
            keepActiveService.Dispose();
            form.AllowCloseAndClose();
            showRequestRegistration.Unregister(null);
            dispatcher.Dispose();
            trayMenu.Dispose();
            notifyIcon.Dispose();
            activeIcon.Dispose();
            idleIcon.Dispose();
            base.ExitThreadCore();
        }
    }
}
