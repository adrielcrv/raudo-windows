using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Raudo;

internal static class TestRunner
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0].StartsWith("--capture-ui=", StringComparison.Ordinal))
            {
                CaptureUi(args[0].Substring("--capture-ui=".Length), null);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && args[0].StartsWith("--capture-ui-dark=", StringComparison.Ordinal))
            {
                CaptureUi(args[0].Substring("--capture-ui-dark=".Length), true);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && args[0].StartsWith("--capture-ui-light=", StringComparison.Ordinal))
            {
                CaptureUi(args[0].Substring("--capture-ui-light=".Length), false);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && args[0].StartsWith("--capture-mini-dark=", StringComparison.Ordinal))
            {
                CaptureMiniUi(args[0].Substring("--capture-mini-dark=".Length), true);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && args[0].StartsWith("--capture-mini-light=", StringComparison.Ordinal))
            {
                CaptureMiniUi(args[0].Substring("--capture-mini-light=".Length), false);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && args[0].StartsWith("--desktop-probe-window=", StringComparison.Ordinal))
            {
                ShowDesktopProbeWindow(args[0].Substring("--desktop-probe-window=".Length));
                return 0;
            }

            if (args.Length == 1 && string.Equals(args[0], "--desktop-integration", StringComparison.Ordinal))
            {
                bool executed = RunVirtualDesktopIntegrationTest();
                Console.WriteLine(executed ? "PASS" : "SKIP: se requieren al menos dos escritorios virtuales.");
                return 0;
            }

            RunUnitTests();
            if (args.Length == 1 && string.Equals(args[0], "--integration", StringComparison.Ordinal))
            {
                RunIntegrationTests();
            }

            Console.WriteLine("PASS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void CaptureUi(string path, bool? dark)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (KeepActiveService service = new KeepActiveService())
        using (Icon icon = IconFactory.Create(false))
        using (MainForm form = new MainForm(service, new RaudoSettings(), icon))
        {
            form.Show();
            Application.DoEvents();
            if (dark.HasValue)
            {
                form.ApplyTheme(ThemePalette.Create(dark.Value));
            }
            Thread.Sleep(150);
            Application.DoEvents();
            using (Bitmap full = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(full, new Rectangle(0, 0, form.Width, form.Height));
                int border = Math.Max(0, (form.Width - form.ClientSize.Width) / 2);
                int top = Math.Max(0, form.Height - form.ClientSize.Height - border);
                Rectangle clientBounds = new Rectangle(
                    border,
                    top,
                    form.ClientSize.Width,
                    form.ClientSize.Height);
                using (Bitmap client = full.Clone(clientBounds, PixelFormat.Format32bppArgb))
                {
                    client.Save(path, ImageFormat.Png);
                }
            }
            form.AllowCloseAndClose();
        }
    }

    private static void CaptureMiniUi(string path, bool dark)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (VirtualDesktopService service = new VirtualDesktopService())
        using (MiniForm form = new MiniForm(service, new RaudoSettings()))
        {
            form.ShowMini();
            form.ApplyTheme(ThemePalette.Create(dark));
            form.SetExpandedForTesting(true);
            Application.DoEvents();
            Thread.Sleep(100);
            Application.DoEvents();
            using (Bitmap bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
                bitmap.Save(path, ImageFormat.Png);
            }
            form.AllowCloseAndClose();
        }
    }

    private static void RunUnitTests()
    {
        Assert(DurationOption.IsSupported(15), "15 minutos debe ser válido.");
        Assert(DurationOption.IsSupported(120), "120 minutos debe ser válido.");
        Assert(!DurationOption.IsSupported(0), "La duración ilimitada no debe estar disponible.");
        Assert(DurationOption.GetLabel(60) == "1 hora", "La etiqueta de una hora no coincide.");

        string root = Path.Combine(Path.GetTempPath(), "Raudo.Tests", Guid.NewGuid().ToString("N"));
        string settingsPath = Path.Combine(root, "settings.json");
        try
        {
            SettingsStore store = new SettingsStore(settingsPath);
            RaudoSettings settings = new RaudoSettings();
            settings.DurationMinutes = 60;
            settings.MiniModeEnabled = true;
            settings.MiniHintShown = true;
            settings.MiniCenterX = 420;
            settings.MiniCenterY = 360;
            store.Save(settings);
            RaudoSettings loaded = store.Load();
            Assert(loaded.DurationMinutes == 60, "La configuración no sobrevivió al guardado.");
            Assert(loaded.MiniModeEnabled, "El estado del Modo Mini no sobrevivió al guardado.");
            Assert(loaded.MiniHintShown, "La ayuda del Modo Mini no sobrevivió al guardado.");
            Assert(
                loaded.MiniCenterX == 420 && loaded.MiniCenterY == 360,
                "La posición del Modo Mini no sobrevivió al guardado.");

            settings.DurationMinutes = 0;
            store.Save(settings);
            Assert(store.Load().DurationMinutes == 30, "La normalización de duración no funcionó.");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        ThemePalette light = ThemePalette.Create(false);
        ThemePalette dark = ThemePalette.Create(true);
        Assert(!light.IsDark && dark.IsDark, "Los temas no conservan su modo.");
        Assert(light.Primary != dark.Primary, "Los temas deben tener paletas independientes.");

        DesktopWindow desktopWindow = new DesktopWindow(
            new IntPtr(42),
            "Excel",
            new string('A', 90));
        Assert(desktopWindow.DisplayName.Length == 72, "El título de ventana no se limitó correctamente.");

        using (Icon idle = IconFactory.Create(false))
        using (Icon active = IconFactory.Create(true))
        {
            Assert(idle.Width > 0 && active.Height > 0, "No se pudieron generar los iconos de bandeja.");
        }
    }

    private static void RunIntegrationTests()
    {
        NativeMethods.Point before;
        Assert(NativeMethods.GetCursorPos(out before), "No se pudo leer el cursor.");

        uint idleBefore;
        Assert(NativeInput.TryGetIdleMilliseconds(out idleBefore), "No se pudo leer la inactividad.");

        string error;
        Assert(NativeInput.TryPulse(out error), error ?? "Falló el pulso de entrada.");
        Thread.Sleep(100);

        NativeMethods.Point after;
        Assert(NativeMethods.GetCursorPos(out after), "No se pudo releer el cursor.");
        Assert(before.X == after.X && before.Y == after.Y, "El cursor no regresó a su posición.");

        uint idleAfter;
        Assert(NativeInput.TryGetIdleMilliseconds(out idleAfter), "No se pudo releer la inactividad.");
        Assert(idleAfter < 5000, "El pulso no reinició la inactividad.");

        Assert(Raudo.PowerState.TryKeepAwake(), "Windows rechazó SetThreadExecutionState.");
        Raudo.PowerState.Release();
        Assert(ScreenCaptureLauncher.IsAvailable(), "La Herramienta Recortes no está registrada.");
    }

    private static bool RunVirtualDesktopIntegrationTest()
    {
        string root = Path.Combine(Path.GetTempPath(), "Raudo.Tests", Guid.NewGuid().ToString("N"));
        string handlePath = Path.Combine(root, "window.handle");
        Directory.CreateDirectory(root);

        Process child = null;
        Form sourceAnchor = null;
        Form destinationAnchor = null;
        DesktopDirection? returnDirection = null;

        try
        {
            using (VirtualDesktopService service = new VirtualDesktopService())
            {
                Assert(service.IsAvailable, "IVirtualDesktopManager no está disponible.");

                sourceAnchor = CreateDesktopAnchor();
                Guid originalDesktop;
                Assert(
                    service.TryGetDesktopId(sourceAnchor.Handle, out originalDesktop),
                    "No se pudo identificar el escritorio original.");

                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Application.ExecutablePath;
                startInfo.Arguments = "--desktop-probe-window=\"" + handlePath + "\"";
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                child = Process.Start(startInfo);

                IntPtr childWindow = WaitForProbeHandle(handlePath, child, TimeSpan.FromSeconds(5));
                Guid childDesktop;
                Assert(
                    service.TryGetDesktopId(childWindow, out childDesktop)
                        && childDesktop == originalDesktop,
                    "La ventana de prueba no se abrió en el escritorio original.");

                if (TrySwitchToDifferentDesktop(
                    service,
                    originalDesktop,
                    DesktopDirection.Right,
                    out destinationAnchor))
                {
                    returnDirection = DesktopDirection.Left;
                }
                else if (TrySwitchToDifferentDesktop(
                    service,
                    originalDesktop,
                    DesktopDirection.Left,
                    out destinationAnchor))
                {
                    returnDirection = DesktopDirection.Right;
                }
                else
                {
                    return false;
                }

                System.Collections.Generic.IList<DesktopWindow> windows;
                string listError;
                Assert(
                    service.TryGetWindowsOutsideCurrentDesktop(out windows, out listError),
                    listError ?? "No se pudieron consultar las ventanas externas.");
                bool foundProbe = false;
                foreach (DesktopWindow window in windows)
                {
                    if (window.Handle == childWindow)
                    {
                        foundProbe = true;
                        break;
                    }
                }
                Assert(foundProbe, "La ventana de prueba no apareció en el selector.");

                string error;
                Assert(
                    service.TryBringHere(childWindow, out error),
                    error ?? "No se pudo mover la ventana de otro proceso.");

                Guid destinationDesktop;
                Guid movedDesktop;
                Assert(
                    service.TryGetDesktopId(destinationAnchor.Handle, out destinationDesktop)
                        && service.TryGetDesktopId(childWindow, out movedDesktop)
                        && destinationDesktop == movedDesktop,
                    "La ventana no terminó en el escritorio de destino.");

                return true;
            }
        }
        finally
        {
            if (returnDirection.HasValue)
            {
                string ignored;
                DesktopNavigation.TrySwitch(returnDirection.Value, out ignored);
                Thread.Sleep(800);
            }

            if (destinationAnchor != null)
            {
                destinationAnchor.Dispose();
            }

            if (sourceAnchor != null)
            {
                sourceAnchor.Dispose();
            }

            if (child != null)
            {
                try
                {
                    if (!child.HasExited)
                    {
                        child.Kill();
                        child.WaitForExit(3000);
                    }
                }
                finally
                {
                    child.Dispose();
                }
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static bool TrySwitchToDifferentDesktop(
        VirtualDesktopService service,
        Guid originalDesktop,
        DesktopDirection direction,
        out Form anchor)
    {
        anchor = null;
        string error;
        Assert(DesktopNavigation.TrySwitch(direction, out error), error);
        Thread.Sleep(900);

        Form candidate = CreateDesktopAnchor();
        Guid candidateDesktop;
        if (!service.TryGetDesktopId(candidate.Handle, out candidateDesktop)
            || candidateDesktop == originalDesktop)
        {
            candidate.Dispose();
            return false;
        }

        anchor = candidate;
        return true;
    }

    private static Form CreateDesktopAnchor()
    {
        Form anchor = new Form();
        anchor.FormBorderStyle = FormBorderStyle.None;
        anchor.ShowInTaskbar = false;
        anchor.StartPosition = FormStartPosition.Manual;
        anchor.Location = new Point(-32000, -32000);
        anchor.Size = new Size(1, 1);
        anchor.Opacity = 0;
        anchor.Show();
        Application.DoEvents();
        return anchor;
    }

    private static IntPtr WaitForProbeHandle(string path, Process process, TimeSpan timeout)
    {
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed < timeout)
        {
            if (File.Exists(path))
            {
                long value;
                if (long.TryParse(File.ReadAllText(path), out value) && value != 0)
                {
                    return new IntPtr(value);
                }
            }

            if (process.HasExited)
            {
                throw new InvalidOperationException("La ventana de prueba terminó antes de iniciar.");
            }

            Thread.Sleep(50);
        }

        throw new TimeoutException("La ventana de prueba no estuvo disponible a tiempo.");
    }

    private static void ShowDesktopProbeWindow(string handlePath)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (Form form = new Form())
        {
            form.Text = "Raudo · Prueba de escritorios";
            form.ClientSize = new Size(320, 96);
            form.StartPosition = FormStartPosition.CenterScreen;
            form.Shown += delegate
            {
                File.WriteAllText(handlePath, form.Handle.ToInt64().ToString());
            };
            Application.Run(form);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
