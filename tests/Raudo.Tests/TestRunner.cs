using System;
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
            store.Save(settings);
            Assert(store.Load().DurationMinutes == 60, "La configuración no sobrevivió al guardado.");

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

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
