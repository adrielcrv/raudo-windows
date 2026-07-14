using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Raudo;

internal static class TestRunner
{
    private const int PresentationDurationForEvidence = 167;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1
                && args[0].StartsWith("--capture-voice-listening-dark=", StringComparison.Ordinal))
            {
                CaptureVoiceOverlay(
                    args[0].Substring("--capture-voice-listening-dark=".Length),
                    ThemePalette.Create(true),
                    VoiceOverlayState.Listening,
                    "Escuchando...",
                    "Prueba “abre Excel” o “cuánto es 12 por 8”.",
                    96);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-voice-success-light=", StringComparison.Ordinal))
            {
                CaptureVoiceOverlay(
                    args[0].Substring("--capture-voice-success-light=".Length),
                    ThemePalette.Create(false),
                    VoiceOverlayState.Success,
                    "57,024",
                    "132 × 432",
                    96);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-voice-retry-dark=", StringComparison.Ordinal))
            {
                CaptureVoiceOverlay(
                    args[0].Substring("--capture-voice-retry-dark=".Length),
                    ThemePalette.Create(true),
                    VoiceOverlayState.NotUnderstood,
                    "No entendí · intento 2 de 2",
                    "Escuché “abre notas”. Vuelvo a escuchar una vez más.",
                    96);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-voice-high-contrast=", StringComparison.Ordinal))
            {
                CaptureVoiceOverlay(
                    args[0].Substring("--capture-voice-high-contrast=".Length),
                    ThemePalette.CreateHighContrast(),
                    VoiceOverlayState.Unavailable,
                    "Voz no disponible",
                    "Permite el acceso al micrófono en Windows.",
                    96);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-voice-listening-dark-150=", StringComparison.Ordinal))
            {
                CaptureVoiceOverlay(
                    args[0].Substring("--capture-voice-listening-dark-150=".Length),
                    ThemePalette.Create(true),
                    VoiceOverlayState.Listening,
                    "Escuchando...",
                    "Prueba “abre Excel” o “cuánto es 12 por 8”.",
                    144);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && string.Equals(args[0], "--voice-grammar-probe", StringComparison.Ordinal))
            {
                RunVoiceGrammarProbe();
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && string.Equals(args[0], "--resource-probe-voice-idle", StringComparison.Ordinal))
            {
                RunVoiceIdleResourceProbe();
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && args[0].StartsWith("--capture-ui=", StringComparison.Ordinal))
            {
                CaptureUi(args[0].Substring("--capture-ui=".Length), null);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-desktop-guide-light=", StringComparison.Ordinal))
            {
                CaptureDesktopGuide(
                    args[0].Substring("--capture-desktop-guide-light=".Length),
                    false,
                    false);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-desktop-guide-created-dark=", StringComparison.Ordinal))
            {
                CaptureDesktopGuide(
                    args[0].Substring("--capture-desktop-guide-created-dark=".Length),
                    true,
                    true);
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

            if (args.Length == 1 && args[0].StartsWith("--capture-ui-active-dark=", StringComparison.Ordinal))
            {
                CaptureUi(
                    args[0].Substring("--capture-ui-active-dark=".Length),
                    true,
                    true);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && args[0].StartsWith("--capture-ui-dark-150=", StringComparison.Ordinal))
            {
                CaptureUiScaled(
                    args[0].Substring("--capture-ui-dark-150=".Length),
                    true,
                    1.5F);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && string.Equals(args[0], "--resource-probe", StringComparison.Ordinal))
            {
                RunResourceProbe();
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && args[0].StartsWith("--capture-salto-dark=", StringComparison.Ordinal))
            {
                CaptureSalto(
                    args[0].Substring("--capture-salto-dark=".Length),
                    true,
                    string.Empty);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && args[0].StartsWith("--capture-salto-light=", StringComparison.Ordinal))
            {
                CaptureSalto(
                    args[0].Substring("--capture-salto-light=".Length),
                    false,
                    string.Empty);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && args[0].StartsWith("--capture-salto-filtered-dark=", StringComparison.Ordinal))
            {
                CaptureSalto(
                    args[0].Substring("--capture-salto-filtered-dark=".Length),
                    true,
                    "captura");
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && args[0].StartsWith("--capture-salto-search-dark=", StringComparison.Ordinal))
            {
                CaptureSalto(
                    args[0].Substring("--capture-salto-search-dark=".Length),
                    true,
                    "excel");
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-salto-calculation-dark=", StringComparison.Ordinal))
            {
                CaptureSalto(
                    args[0].Substring("--capture-salto-calculation-dark=".Length),
                    true,
                    "12.5 * 8");
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-salto-conversion-light=", StringComparison.Ordinal))
            {
                CaptureSalto(
                    args[0].Substring("--capture-salto-conversion-light=".Length),
                    false,
                    "10 km a m");
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-salto-numeric-dark=", StringComparison.Ordinal))
            {
                CaptureSalto(
                    args[0].Substring("--capture-salto-numeric-dark=".Length),
                    true,
                    "125");
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-salto-high-contrast=", StringComparison.Ordinal))
            {
                CaptureSaltoHighContrast(
                    args[0].Substring("--capture-salto-high-contrast=".Length));
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-salto-loading-dark=", StringComparison.Ordinal))
            {
                CaptureSaltoLoading(
                    args[0].Substring("--capture-salto-loading-dark=".Length),
                    true);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-salto-transition-dark=", StringComparison.Ordinal))
            {
                CaptureSaltoTransition(
                    args[0].Substring("--capture-salto-transition-dark=".Length));
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-salto-folder-dark=", StringComparison.Ordinal))
            {
                CaptureSalto(
                    args[0].Substring("--capture-salto-folder-dark=".Length),
                    true,
                    "descargas");
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-salto-media-dark=", StringComparison.Ordinal))
            {
                CaptureSalto(
                    args[0].Substring("--capture-salto-media-dark=".Length),
                    true,
                    "media");
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-salto-media-light=", StringComparison.Ordinal))
            {
                CaptureSalto(
                    args[0].Substring("--capture-salto-media-light=".Length),
                    false,
                    "media");
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-salto-calculation-dark-150=", StringComparison.Ordinal))
            {
                CaptureSaltoScaled(
                    args[0].Substring("--capture-salto-calculation-dark-150=".Length),
                    true,
                    "12.5 * 8",
                    1.5F);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-salto-media-dark-150=", StringComparison.Ordinal))
            {
                CaptureSaltoScaled(
                    args[0].Substring("--capture-salto-media-dark-150=".Length),
                    true,
                    "media",
                    1.5F);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-salto-search-dark-150=", StringComparison.Ordinal))
            {
                CaptureSaltoScaled(
                    args[0].Substring("--capture-salto-search-dark-150=".Length),
                    true,
                    "excel",
                    1.5F);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && string.Equals(args[0], "--resource-probe-salto", StringComparison.Ordinal))
            {
                RunSaltoResourceProbe();
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && string.Equals(args[0], "--resource-probe-salto-loading", StringComparison.Ordinal))
            {
                RunSaltoLoadingResourceProbe();
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && string.Equals(args[0], "--resource-probe-quick-results", StringComparison.Ordinal))
            {
                RunQuickResultsResourceProbe();
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && string.Equals(args[0], "--hotkey-probe", StringComparison.Ordinal))
            {
                RunHotKeyProbe();
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && string.Equals(args[0], "--resource-probe-unified-salto", StringComparison.Ordinal))
            {
                RunUnifiedSaltoResourceProbe();
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && string.Equals(args[0], "--application-catalog-probe", StringComparison.Ordinal))
            {
                RunApplicationCatalogProbe();
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && string.Equals(args[0], "--resource-probe-mini-media", StringComparison.Ordinal))
            {
                RunMiniMediaResourceProbe();
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && string.Equals(args[0], "--media-session-probe", StringComparison.Ordinal))
            {
                RunMediaSessionProbe();
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--application-launch-probe=", StringComparison.Ordinal))
            {
                RunApplicationLaunchProbe(
                    args[0].Substring("--application-launch-probe=".Length));
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && args[0].StartsWith("--capture-mini-dark=", StringComparison.Ordinal))
            {
                CaptureMiniUi(args[0].Substring("--capture-mini-dark=".Length), true, true);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && args[0].StartsWith("--capture-mini-light=", StringComparison.Ordinal))
            {
                CaptureMiniUi(args[0].Substring("--capture-mini-light=".Length), false, true);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-mini-media-selected-dark=", StringComparison.Ordinal))
            {
                CaptureMiniMediaUi(
                    args[0].Substring("--capture-mini-media-selected-dark=".Length),
                    true,
                    96,
                    true);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-mini-media-dark-150=", StringComparison.Ordinal))
            {
                CaptureMiniMediaUi(
                    args[0].Substring("--capture-mini-media-dark-150=".Length),
                    true,
                    144,
                    false);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1
                && args[0].StartsWith("--capture-mini-media-high-contrast=", StringComparison.Ordinal))
            {
                CaptureMiniMediaUi(
                    args[0].Substring("--capture-mini-media-high-contrast=".Length),
                    true,
                    96,
                    false,
                    true);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && args[0].StartsWith("--capture-mini-edge-dark=", StringComparison.Ordinal))
            {
                CaptureMiniUi(
                    args[0].Substring("--capture-mini-edge-dark=".Length),
                    true,
                    false);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && args[0].StartsWith("--capture-mini-motion-dark=", StringComparison.Ordinal))
            {
                CaptureMiniMotion(
                    args[0].Substring("--capture-mini-motion-dark=".Length),
                    true);
                Console.WriteLine("PASS");
                return 0;
            }

            if (args.Length == 1 && args[0].StartsWith("--capture-mini-states-dark=", StringComparison.Ordinal))
            {
                CaptureMiniStates(
                    args[0].Substring("--capture-mini-states-dark=".Length),
                    true);
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

            if (args.Length == 1 && args[0].StartsWith("--mini-process-integration=", StringComparison.Ordinal))
            {
                int processId;
                if (!int.TryParse(
                    args[0].Substring("--mini-process-integration=".Length),
                    out processId))
                {
                    throw new ArgumentException("El identificador de proceso no es válido.");
                }

                bool executed = RunMiniProcessIntegrationTest(processId);
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
        CaptureUi(path, dark, false);
    }

    private static void CaptureUi(string path, bool? dark, bool active)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (KeepActiveService service = new KeepActiveService())
        using (Icon icon = IconFactory.Create(false))
        using (MainForm form = new MainForm(service, new RaudoSettings(), icon))
        {
            if (active)
            {
                service.Start(30);
                form.RefreshState();
            }

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

    private static void CaptureUiScaled(string path, bool dark, float scaleFactor)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (KeepActiveService service = new KeepActiveService())
        using (Icon icon = IconFactory.Create(false))
        using (MainForm form = new MainForm(service, new RaudoSettings(), icon))
        {
            form.ApplyTheme(ThemePalette.Create(dark));
            form.Scale(new SizeF(scaleFactor, scaleFactor));
            form.Show();
            Application.DoEvents();
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

    private static void RunResourceProbe()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (KeepActiveService service = new KeepActiveService())
        using (Icon icon = IconFactory.Create(false))
        using (MainForm form = new MainForm(service, new RaudoSettings(), icon))
        using (Process process = Process.GetCurrentProcess())
        {
            form.Show();
            Application.DoEvents();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            TimeSpan cpuBefore = process.TotalProcessorTime;
            Stopwatch elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
            {
                Application.DoEvents();
                Thread.Sleep(50);
            }

            elapsed.Stop();
            process.Refresh();
            TimeSpan cpuUsed = process.TotalProcessorTime - cpuBefore;
            double normalizedCpuPercent = cpuUsed.TotalMilliseconds
                / elapsed.Elapsed.TotalMilliseconds
                / Math.Max(1, Environment.ProcessorCount)
                * 100D;
            Console.WriteLine(
                "Idle CPU: {0:F3}% · Working set: {1:F1} MB · Private: {2:F1} MB",
                normalizedCpuPercent,
                process.WorkingSet64 / 1024D / 1024D,
                process.PrivateMemorySize64 / 1024D / 1024D);
            Assert(normalizedCpuPercent < 1D, "La interfaz excedió 1% de CPU en reposo.");
            form.AllowCloseAndClose();
        }
    }

    private static void CaptureSalto(string path, bool dark, string query)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        RaudoActionCatalog catalog = CreateTestActionCatalog(null);
        using (SaltoForm form = new SaltoForm(catalog))
        {
            form.ApplyTheme(ThemePalette.Create(dark));
            form.ShowSalto();
            if (!string.IsNullOrWhiteSpace(query))
            {
                form.SetQueryForTesting(query);
            }

            Application.DoEvents();
            Thread.Sleep(220);
            Application.DoEvents();
            Assert(!form.TransitionRunningForTesting, "La transición de Salto no se detuvo.");
            using (Bitmap bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
                bitmap.Save(path, ImageFormat.Png);
            }

            form.AllowCloseAndClose();
        }
    }

    private static void CaptureSaltoHighContrast(string path)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        RaudoActionCatalog catalog = CreateTestActionCatalog(null);
        RaudoSettings settings = new RaudoSettings();
        settings.SaltoOpacityPercent = 64;
        using (SaltoForm form = new SaltoForm(catalog, settings))
        {
            form.ApplyTheme(ThemePalette.CreateHighContrast());
            form.ShowSalto();
            form.SetQueryForTesting("12.5 * 8");
            WaitWithMessages(220);
            Assert(
                Math.Abs(form.EffectiveOpacityForTesting - 1D) < 0.001D,
                "La captura de alto contraste no forzó opacidad completa.");
            using (Bitmap bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
                bitmap.Save(path, ImageFormat.Png);
            }

            form.AllowCloseAndClose();
        }
    }

    private static void CaptureSaltoLoading(string path, bool dark)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        RaudoActionCatalog catalog = CreateTestActionCatalog(null);
        using (SaltoForm form = new SaltoForm(
            catalog,
            new RaudoSettings(),
            delegate { return true; }))
        {
            form.ApplyTheme(ThemePalette.Create(dark));
            form.SetApplicationCatalogState(true, null);
            form.ShowSalto();
            form.SetQueryForTesting("aplicación en preparación");
            WaitWithMessages(420);
            Assert(
                form.PresentationModeForTesting == SaltoPresentationMode.Loading,
                "La captura no alcanzó el estado Loading de Salto.");
            Assert(
                form.LoadingAnimationRunningForTesting,
                "El indicador de carga no estuvo activo mientras Salto era visible.");
            using (Bitmap bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
                bitmap.Save(path, ImageFormat.Png);
            }

            form.SetApplicationCatalogState(false, null);
            Application.DoEvents();
            Assert(
                !form.LoadingAnimationRunningForTesting,
                "El indicador de carga no se detuvo al completar el catálogo.");
            form.AllowCloseAndClose();
        }
    }

    private static void CaptureSaltoTransition(string path)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        RaudoActionCatalog catalog = CreateTestActionCatalog(null);
        ThemePalette palette = ThemePalette.Create(true);
        using (SaltoForm form = new SaltoForm(
            catalog,
            new RaudoSettings(),
            delegate { return true; }))
        using (Bitmap strip = new Bitmap(660 * 4, 468))
        using (Graphics graphics = Graphics.FromImage(strip))
        using (Font labelFont = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point))
        using (SolidBrush labelBrush = new SolidBrush(palette.TextMuted))
        {
            form.ApplyTheme(palette);
            form.ShowSalto();
            form.SetQueryForTesting("media");
            WaitWithMessages(230);
            Assert(
                form.ClientSize == new Size(640, 432),
                "Salto no alcanzó el estado expandido antes de capturar la transición.");

            graphics.Clear(Color.FromArgb(7, 12, 24));
            form.SetQueryForTesting("12.5 * 8");
            Assert(
                form.PresentationStartBoundsForTesting.Size == new Size(640, 432)
                    && form.PresentationTargetBoundsForTesting.Size == new Size(520, 216),
                "La transición no conservó los límites de origen y destino.");
            int[] frameTimes = new int[] { 0, 50, 105, 167 };
            for (int index = 0; index < frameTimes.Length; index++)
            {
                form.ApplyPresentationProgressForTesting(
                    frameTimes[index] / (double)PresentationDurationForEvidence);
                Application.DoEvents();
                using (Bitmap frame = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                {
                    form.DrawToBitmap(frame, new Rectangle(Point.Empty, form.ClientSize));
                    int frameLeft = (index * 660) + ((660 - frame.Width) / 2);
                    graphics.DrawImageUnscaled(frame, frameLeft, 12);
                }

                graphics.DrawString(
                    frameTimes[index] + " ms",
                    labelFont,
                    labelBrush,
                    new PointF((index * 660) + 20, 444));
            }

            Assert(
                !form.TransitionRunningForTesting
                    && form.PresentationModeForTesting == SaltoPresentationMode.Answer
                    && form.ClientSize == new Size(520, 216),
                "La transición adaptativa no terminó en Answer.");
            graphics.Flush();
            strip.Save(path, ImageFormat.Png);
            form.AllowCloseAndClose();
        }
    }

    private static void WaitWithMessages(int milliseconds)
    {
        Stopwatch watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    private static void RunSaltoResourceProbe()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        RaudoActionCatalog catalog = CreateTestActionCatalog(null);
        using (SaltoForm form = new SaltoForm(catalog))
        using (Process process = Process.GetCurrentProcess())
        {
            form.ShowSalto();
            Application.DoEvents();
            Thread.Sleep(220);
            Application.DoEvents();
            Assert(!form.TransitionRunningForTesting, "La transición de Salto no se detuvo.");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            TimeSpan cpuBefore = process.TotalProcessorTime;
            Stopwatch elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
            {
                Application.DoEvents();
                Thread.Sleep(50);
            }

            elapsed.Stop();
            process.Refresh();
            TimeSpan cpuUsed = process.TotalProcessorTime - cpuBefore;
            double normalizedCpuPercent = cpuUsed.TotalMilliseconds
                / elapsed.Elapsed.TotalMilliseconds
                / Math.Max(1, Environment.ProcessorCount)
                * 100D;
            Console.WriteLine(
                "Salto idle CPU: {0:F3}% · Working set: {1:F1} MB · Private: {2:F1} MB",
                normalizedCpuPercent,
                process.WorkingSet64 / 1024D / 1024D,
                process.PrivateMemorySize64 / 1024D / 1024D);
            Assert(normalizedCpuPercent < 1D, "Salto excedió 1% de CPU en reposo.");
            form.AllowCloseAndClose();
        }
    }

    private static void RunSaltoLoadingResourceProbe()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        RaudoActionCatalog catalog = CreateTestActionCatalog(null);
        using (SaltoForm form = new SaltoForm(
            catalog,
            new RaudoSettings(),
            delegate { return true; }))
        using (Process process = Process.GetCurrentProcess())
        {
            form.SetApplicationCatalogState(true, null);
            form.ShowSalto();
            form.SetQueryForTesting("aplicación en preparación");
            WaitWithMessages(400);
            Assert(
                form.LoadingAnimationRunningForTesting,
                "El loading no estuvo activo durante la sonda.");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            TimeSpan cpuBefore = process.TotalProcessorTime;
            Stopwatch elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
            {
                Application.DoEvents();
                Thread.Sleep(50);
            }

            elapsed.Stop();
            process.Refresh();
            TimeSpan cpuUsed = process.TotalProcessorTime - cpuBefore;
            double normalizedCpuPercent = cpuUsed.TotalMilliseconds
                / elapsed.Elapsed.TotalMilliseconds
                / Math.Max(1, Environment.ProcessorCount)
                * 100D;
            Console.WriteLine(
                "Salto loading CPU: {0:F3}% · Working set: {1:F1} MB · Private: {2:F1} MB",
                normalizedCpuPercent,
                process.WorkingSet64 / 1024D / 1024D,
                process.PrivateMemorySize64 / 1024D / 1024D);
            Assert(normalizedCpuPercent < 1D, "El loading de Salto excedió 1% de CPU.");
            form.SetApplicationCatalogState(false, null);
            Assert(
                !form.LoadingAnimationRunningForTesting,
                "El loading no se detuvo después de la sonda.");
            form.AllowCloseAndClose();
        }
    }

    private static void CaptureSaltoScaled(
        string path,
        bool dark,
        string query,
        float scaleFactor)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        RaudoActionCatalog catalog = CreateTestActionCatalog(null);
        using (SaltoForm form = new SaltoForm(catalog))
        {
            form.ApplyTheme(ThemePalette.Create(dark));
            form.Scale(new SizeF(scaleFactor, scaleFactor));
            form.ShowSalto();
            form.SetQueryForTesting(query);
            Application.DoEvents();
            Thread.Sleep(220);
            Application.DoEvents();
            using (Bitmap bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
                bitmap.Save(path, ImageFormat.Png);
            }

            form.AllowCloseAndClose();
        }
    }

    private static void RunUnifiedSaltoResourceProbe()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        InstalledApplicationCatalog applications = new InstalledApplicationCatalog();
        applications.LoadNowForTesting();
        IList<InstalledApplication> applicationSnapshot = applications.GetSnapshot();
        QuickResultProvider quickResults = new QuickResultProvider(delegate
        {
            return null;
        });
        RaudoActionCatalog catalog = new RaudoActionCatalog(delegate
        {
            List<RaudoAction> actions = new List<RaudoAction>();
            actions.Add(new RaudoAction(
                "window.main",
                "Abrir Raudo",
                "Mostrar controles y preferencias",
                "configuracion ajustes",
                string.Empty,
                RaudoActionGlyph.MainWindow,
                delegate { }));
            foreach (InstalledApplication rawApplication in applicationSnapshot)
            {
                InstalledApplication application = rawApplication;
                string aliases = application.Aliases == null
                    ? string.Empty
                    : string.Join(" ", application.Aliases);
                actions.Add(new RaudoAction(
                    "open-application:" + application.Identifier,
                    application.Name,
                    "Aplicación instalada",
                    "aplicacion programa iniciar abrir " + application.Name + " " + aliases,
                    "Abrir",
                    RaudoActionGlyph.Application,
                    RaudoActionKind.Application,
                    false,
                    15,
                    delegate { return null; }));
            }

            return actions;
        }, quickResults.CreateActions);

        using (SaltoForm form = new SaltoForm(catalog))
        using (Process process = Process.GetCurrentProcess())
        {
            form.ShowSalto();
            form.SetQueryForTesting("12.5 * 8");
            Application.DoEvents();
            Thread.Sleep(220);
            Application.DoEvents();
            Assert(
                form.SelectedActionIdForTesting == "quick.calculation",
                "El catálogo completo no priorizó el resultado rápido.");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            TimeSpan cpuBefore = process.TotalProcessorTime;
            Stopwatch elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
            {
                Application.DoEvents();
                Thread.Sleep(50);
            }

            elapsed.Stop();
            process.Refresh();
            TimeSpan cpuUsed = process.TotalProcessorTime - cpuBefore;
            double normalizedCpuPercent = cpuUsed.TotalMilliseconds
                / elapsed.Elapsed.TotalMilliseconds
                / Math.Max(1, Environment.ProcessorCount)
                * 100D;
            Console.WriteLine(
                "Salto unificado: {0} apps · CPU {1:F3}% · Working set {2:F1} MB · Private {3:F1} MB",
                applicationSnapshot.Count,
                normalizedCpuPercent,
                process.WorkingSet64 / 1024D / 1024D,
                process.PrivateMemorySize64 / 1024D / 1024D);
            Assert(
                normalizedCpuPercent < 1D,
                "Salto unificado excedió 1% de CPU en reposo.");
            form.AllowCloseAndClose();
        }
    }

    private static RaudoActionCatalog CreateTestActionCatalog(Action invoked)
    {
        Action execute = invoked ?? delegate { };
        QuickResultProvider quickResults = new QuickResultProvider(delegate(string value)
        {
            execute();
            return null;
        });
        MediaControlService mediaControls = new MediaControlService(
            delegate(NativeMethods.Input[] inputs)
            {
                execute();
                return (uint)inputs.Length;
            });
        return new RaudoActionCatalog(delegate
        {
            List<RaudoAction> actions = new List<RaudoAction>
            {
                new RaudoAction(
                    "pulse.toggle",
                    "Iniciar Pulso",
                    "Mantener disponible durante 30 minutos",
                    "mouse ausencia activo",
                    string.Empty,
                    RaudoActionGlyph.Pulse,
                    execute),
                new RaudoAction(
                    "capture.screen",
                    "Recortar pantalla",
                    "Seleccionar una región con la herramienta de Windows",
                    "captura screenshot crop",
                    "Win + Shift + S",
                    RaudoActionGlyph.Capture,
                    execute),
                new RaudoAction(
                    "window.main",
                    "Abrir Raudo",
                    "Mostrar controles y preferencias",
                    "configuración ajustes",
                    string.Empty,
                    RaudoActionGlyph.MainWindow,
                    execute),
                new RaudoAction(
                    "mini.toggle",
                    "Mostrar Modo Mini",
                    "Navegar entre escritorios desde el borde",
                    "ventanas burbuja",
                    string.Empty,
                    RaudoActionGlyph.Mini,
                    execute),
                new RaudoAction(
                    "known-folder:downloads",
                    "Descargas",
                    "Carpeta local de Windows",
                    "downloads descargas archivos carpeta",
                    "Abrir",
                    RaudoActionGlyph.Folder,
                    RaudoActionKind.Folder,
                    false,
                    5,
                    delegate
                    {
                        execute();
                        return null;
                    }),
                new RaudoAction(
                    "open-window:1234",
                    "Excel — Presupuesto trimestral.xlsx",
                    "Ventana · Otro escritorio",
                    "excel ventana presupuesto escritorio",
                    "Traer",
                    RaudoActionGlyph.Window,
                    RaudoActionKind.Window,
                    false,
                    0,
                    delegate
                    {
                        execute();
                        return null;
                    }),
                new RaudoAction(
                    "open-application:excel",
                    "Excel",
                    "Aplicación instalada",
                    "aplicacion programa iniciar abrir excel",
                    "Abrir",
                    RaudoActionGlyph.Application,
                    RaudoActionKind.Application,
                    false,
                    15,
                    delegate
                    {
                        execute();
                        return null;
                    })
            };
            foreach (RaudoAction mediaAction in MediaControlCatalog.CreateActions(
                mediaControls))
            {
                actions.Add(mediaAction);
            }

            return actions;
        }, quickResults.CreateActions);
    }

    private static void RunQuickResultsResourceProbe()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        RaudoActionCatalog catalog = CreateTestActionCatalog(null);
        using (SaltoForm form = new SaltoForm(catalog))
        using (Process process = Process.GetCurrentProcess())
        {
            form.ShowSalto();
            form.SetQueryForTesting("12.5 * 8");
            Application.DoEvents();
            Thread.Sleep(220);
            Application.DoEvents();
            Assert(
                form.SelectedActionIdForTesting == "quick.calculation",
                "El resultado rápido no quedó visible durante la prueba de recursos.");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            TimeSpan cpuBefore = process.TotalProcessorTime;
            Stopwatch elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
            {
                Application.DoEvents();
                Thread.Sleep(50);
            }

            elapsed.Stop();
            process.Refresh();
            TimeSpan cpuUsed = process.TotalProcessorTime - cpuBefore;
            double normalizedCpuPercent = cpuUsed.TotalMilliseconds
                / elapsed.Elapsed.TotalMilliseconds
                / Math.Max(1, Environment.ProcessorCount)
                * 100D;
            Console.WriteLine(
                "Resultados rápidos: CPU {0:F3}% · Working set {1:F1} MB · Private {2:F1} MB",
                normalizedCpuPercent,
                process.WorkingSet64 / 1024D / 1024D,
                process.PrivateMemorySize64 / 1024D / 1024D);
            Assert(
                normalizedCpuPercent < 1D,
                "Los resultados rápidos excedieron 1% de CPU en reposo.");
            form.AllowCloseAndClose();
        }
    }

    private static void RunMiniMediaResourceProbe()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        MediaControlService fallback = new MediaControlService(
            delegate(NativeMethods.Input[] inputs) { return (uint)inputs.Length; });
        using (MediaSessionService sessions = new MediaSessionService(fallback))
        using (VirtualDesktopService desktopService = new VirtualDesktopService())
        {
            MediaSessionSnapshot snapshot = sessions
                .GetSnapshotAsync()
                .GetAwaiter()
                .GetResult();
            Assert(snapshot.IsAvailable, snapshot.Error);
            using (MiniForm form = new MiniForm(
                desktopService,
                new RaudoSettings(),
                fallback,
                sessions))
            {
                form.ShowMini();
                form.SetNavigationAvailabilityForTesting(true, true);
                form.SetExpandedForTesting(true);
                Application.DoEvents();

                Process process = Process.GetCurrentProcess();
                process.Refresh();
                TimeSpan cpuBefore = process.TotalProcessorTime;
                Stopwatch elapsed = Stopwatch.StartNew();
                while (elapsed.Elapsed < TimeSpan.FromSeconds(2))
                {
                    Application.DoEvents();
                    Thread.Sleep(25);
                }

                process.Refresh();
                double cpuMilliseconds =
                    (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
                double normalizedCpuPercent = cpuMilliseconds
                    / elapsed.Elapsed.TotalMilliseconds
                    / Math.Max(1, Environment.ProcessorCount)
                    * 100D;
                Console.WriteLine(
                    "Mini multimedia: {0} sesiones · CPU {1:F3}% · Working set {2:F1} MB · Private {3:F1} MB",
                    snapshot.Sessions.Count,
                    normalizedCpuPercent,
                    process.WorkingSet64 / 1024D / 1024D,
                    process.PrivateMemorySize64 / 1024D / 1024D);
                Assert(
                    normalizedCpuPercent < 1D,
                    "Mini multimedia excedió 1% de CPU en reposo.");
                form.AllowCloseAndClose();
            }
        }
    }

    private static void RunApplicationCatalogProbe()
    {
        InstalledApplicationCatalog catalog = new InstalledApplicationCatalog();
        Stopwatch elapsed = Stopwatch.StartNew();
        using (ManualResetEvent completed = new ManualResetEvent(false))
        {
            catalog.LoadCompleted += delegate { completed.Set(); };
            catalog.EnsureLoading();
            Assert(
                completed.WaitOne(TimeSpan.FromSeconds(10)),
                "El catálogo de aplicaciones excedió el tiempo permitido.");
        }

        elapsed.Stop();

        IList<InstalledApplication> applications = catalog.GetSnapshot();
        Assert(catalog.IsLoaded, "El catálogo de aplicaciones no terminó de cargar.");
        Assert(
            string.IsNullOrWhiteSpace(catalog.LoadError),
            catalog.LoadError ?? "El catálogo de aplicaciones falló.");
        Assert(applications.Count > 0, "Windows no devolvió aplicaciones instaladas.");
        Console.WriteLine(
            "Aplicaciones disponibles: {0} · Carga: {1:F0} ms",
            applications.Count,
            elapsed.Elapsed.TotalMilliseconds);
    }

    private static void RunMediaSessionProbe()
    {
        MediaControlService fallback = new MediaControlService(
            delegate(NativeMethods.Input[] inputs) { return (uint)inputs.Length; });
        using (MediaSessionService service = new MediaSessionService(fallback))
        {
            MediaSessionSnapshot snapshot = service.GetSnapshotAsync().GetAwaiter().GetResult();
            Assert(snapshot.IsAvailable, snapshot.Error);
            Console.WriteLine("Sesiones multimedia: " + snapshot.Sessions.Count);
            for (int index = 0; index < snapshot.Sessions.Count; index++)
            {
                MediaSessionDescriptor session = snapshot.Sessions[index];
                Console.WriteLine(
                    "- "
                    + session.DisplayName
                    + " · "
                    + session.StatusText
                    + (session.IsCurrent ? " · actual" : string.Empty));
            }
        }
    }

    private static void RunApplicationLaunchProbe(string applicationName)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
        {
            throw new ArgumentException("La aplicación de prueba es obligatoria.");
        }

        InstalledApplicationCatalog catalog = new InstalledApplicationCatalog();
        catalog.LoadNowForTesting();
        InstalledApplication selected = null;
        foreach (InstalledApplication application in catalog.GetSnapshot())
        {
            if (string.Equals(
                application.Name,
                applicationName,
                StringComparison.CurrentCultureIgnoreCase))
            {
                selected = application;
                break;
            }
        }

        Assert(selected != null, "La aplicación de prueba no está en AppsFolder.");
        string error = InstalledApplicationLauncher.TryLaunch(selected.Identifier);
        Assert(string.IsNullOrWhiteSpace(error), error ?? "No se pudo abrir la aplicación.");
        Console.WriteLine("Aplicación iniciada: " + selected.Name);
    }

    private static void RunHotKeyProbe()
    {
        Application.EnableVisualStyles();
        using (GlobalHotKey hotKey = new GlobalHotKey(
            HotKeyModifiers.Control
                | HotKeyModifiers.Alt
                | HotKeyModifiers.NoRepeat,
            Keys.Space))
        {
            Console.WriteLine(
                hotKey.IsRegistered
                    ? "Ctrl + Alt + Espacio disponible"
                    : "Registro fallido: " + hotKey.RegistrationError);
            Assert(hotKey.IsRegistered, "No se pudo registrar el atajo global de Salto.");
        }
    }

    private static void CaptureMiniUi(string path, bool dark, bool expanded)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (VirtualDesktopService service = new VirtualDesktopService())
        using (MiniForm form = new MiniForm(service, new RaudoSettings()))
        {
            form.ShowMini();
            form.ApplyTheme(ThemePalette.Create(dark));
            form.SetExpandedForTesting(expanded);
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

    private static void CaptureMiniMotion(string path, bool dark)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        double[] progressValues = { 0D, 0.33D, 0.66D, 1D };
        const int panelWidth = 160;
        const int panelGap = 12;
        using (VirtualDesktopService service = new VirtualDesktopService())
        using (Bitmap strip = new Bitmap(
            (panelWidth * progressValues.Length) + (panelGap * (progressValues.Length - 1)),
            64))
        using (Graphics stripGraphics = Graphics.FromImage(strip))
        {
            stripGraphics.Clear(dark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(246, 248, 252));
            for (int index = 0; index < progressValues.Length; index++)
            {
                using (MiniForm form = new MiniForm(service, new RaudoSettings()))
                {
                    form.ShowMini();
                    form.ApplyTheme(ThemePalette.Create(dark));
                    form.SetNavigationAvailabilityForTesting(true, true);
                    form.SetRevealProgressForTesting(progressValues[index]);
                    Application.DoEvents();
                    using (Bitmap frame = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                    {
                        form.DrawToBitmap(frame, new Rectangle(Point.Empty, form.ClientSize));
                        int panelLeft = index * (panelWidth + panelGap);
                        stripGraphics.DrawImageUnscaled(
                            frame,
                            panelLeft + panelWidth - frame.Width,
                            (strip.Height - frame.Height) / 2);
                    }

                    form.AllowCloseAndClose();
                }
            }

            strip.Save(path, ImageFormat.Png);
        }
    }

    private static void CaptureMiniStates(string path, bool dark)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        KeepActivePhase[] phases =
        {
            KeepActivePhase.Inactive,
            KeepActivePhase.Active,
            KeepActivePhase.EndingSoon,
            KeepActivePhase.Critical,
            KeepActivePhase.Completed
        };
        const int panelWidth = 160;
        const int panelGap = 12;
        using (VirtualDesktopService service = new VirtualDesktopService())
        using (Bitmap strip = new Bitmap(
            (panelWidth * phases.Length) + (panelGap * (phases.Length - 1)),
            76))
        using (Graphics stripGraphics = Graphics.FromImage(strip))
        using (Font captionFont = new Font("Segoe UI", 8F, FontStyle.Regular, GraphicsUnit.Point))
        {
            Color canvas = dark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(246, 248, 252);
            Color text = dark ? Color.FromArgb(203, 213, 225) : Color.FromArgb(51, 65, 85);
            stripGraphics.Clear(canvas);
            for (int index = 0; index < phases.Length; index++)
            {
                using (MiniForm form = new MiniForm(service, new RaudoSettings()))
                {
                    form.ShowMini();
                    form.ApplyTheme(ThemePalette.Create(dark));
                    form.SetSessionPhase(phases[index]);
                    form.SetNavigationAvailabilityForTesting(true, true);
                    form.SetExpandedForTesting(true);
                    Application.DoEvents();
                    using (Bitmap frame = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                    {
                        form.DrawToBitmap(frame, new Rectangle(Point.Empty, form.ClientSize));
                        int panelLeft = index * (panelWidth + panelGap);
                        stripGraphics.DrawImageUnscaled(
                            frame,
                            panelLeft + ((panelWidth - frame.Width) / 2),
                            2);
                        using (StringFormat format = new StringFormat())
                        using (SolidBrush brush = new SolidBrush(text))
                        {
                            format.Alignment = StringAlignment.Center;
                            stripGraphics.DrawString(
                                phases[index].ToString(),
                                captionFont,
                                brush,
                                new RectangleF(panelLeft, 54, panelWidth, 20),
                                format);
                        }
                    }

                    form.AllowCloseAndClose();
                }
            }

            strip.Save(path, ImageFormat.Png);
        }
    }

    private static void CaptureVoiceOverlay(
        string path,
        ThemePalette palette,
        VoiceOverlayState state,
        string title,
        string detail,
        int targetDpi)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (VoiceOverlayForm form = new VoiceOverlayForm(delegate { return false; }))
        {
            form.ApplyTheme(palette);
            if (targetDpi > 96)
            {
                ScaleToTargetDpi(form, targetDpi);
            }

            form.ShowState(state, title, detail);
            Application.DoEvents();
            Assert(!form.MotionActiveForTesting, "Movimiento reducido inició una animación de voz.");
            Assert(form.StateForTesting == state, "La isla de voz no conservó su estado visual.");
            AssertAccessibleControls(form);
            using (Bitmap bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
            {
                form.DrawToBitmap(
                    bitmap,
                    new Rectangle(Point.Empty, form.ClientSize));
                bitmap.Save(path, ImageFormat.Png);
            }

            form.AllowCloseAndClose();
        }
    }

    private static void CaptureDesktopGuide(string path, bool dark, bool created)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (Icon icon = IconFactory.Create(false))
        using (DesktopGuideForm form = new DesktopGuideForm(icon))
        {
            form.ApplyTheme(ThemePalette.Create(dark));
            if (created)
            {
                form.ShowCreated();
            }
            else
            {
                form.ShowIntroduction();
            }

            Application.DoEvents();
            Thread.Sleep(100);
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

    private static void RunVoiceGrammarProbe()
    {
        VoiceAvailability availability = VoiceRecognitionService.GetAvailability();
        Assert(availability.IsAvailable, availability.Message);
        InstalledApplicationCatalog catalog = new InstalledApplicationCatalog();
        catalog.LoadNowForTesting();
        IList<InstalledApplication> applications = catalog.GetSnapshot();
        Assert(
            applications.Count > 0,
            catalog.LoadError ?? "Windows no devolvió aplicaciones instaladas.");
        Stopwatch watch = Stopwatch.StartNew();
        Windows.Media.SpeechRecognition.SpeechRecognitionResultStatus status =
            VoiceRecognitionService
                .CompileForTestingAsync(applications)
                .GetAwaiter()
                .GetResult();
        watch.Stop();
        Console.WriteLine(
            "Voice grammar: {0} · {1} · {2} apps · {3:F0} ms",
            availability.LanguageTag,
            status,
            Math.Min(applications.Count, VoiceGrammarBuilder.MaximumApplications),
            watch.Elapsed.TotalMilliseconds);
        Assert(
            status == Windows.Media.SpeechRecognition.SpeechRecognitionResultStatus.Success,
            "Windows no compiló la gramática local de Raudo: " + status);
    }

    private static void RunVoiceIdleResourceProbe()
    {
        using (VoiceRecognitionService service = new VoiceRecognitionService())
        using (Process process = Process.GetCurrentProcess())
        {
            Assert(!service.IsListening, "El servicio de voz abrió una sesión al construirse.");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            process.Refresh();
            long privateBefore = process.PrivateMemorySize64;
            TimeSpan cpuBefore = process.TotalProcessorTime;
            Stopwatch elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
            {
                Thread.Sleep(50);
            }

            elapsed.Stop();
            process.Refresh();
            double normalizedCpuPercent = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds
                / elapsed.Elapsed.TotalMilliseconds
                / Math.Max(1, Environment.ProcessorCount)
                * 100D;
            double privateDeltaMb = (process.PrivateMemorySize64 - privateBefore)
                / 1024D
                / 1024D;
            Console.WriteLine(
                "Voice idle CPU: {0:F3}% · Private delta: {1:F1} MB · Private: {2:F1} MB",
                normalizedCpuPercent,
                privateDeltaMb,
                process.PrivateMemorySize64 / 1024D / 1024D);
            Assert(normalizedCpuPercent < 1D, "Voz excedió 1% de CPU sin una sesión activa.");
            Assert(privateDeltaMb < 2D, "Voz retuvo memoria sin una sesión activa.");
            Assert(!service.IsListening, "El servicio de voz inició escucha durante el reposo.");
        }
    }

    private static void TestVoiceCommands()
    {
        for (int value = 0; value <= 999; value++)
        {
            int parsed;
            Assert(
                VoiceNumberWords.TryParse(VoiceNumberWords.ToSpanish(value), out parsed)
                    && parsed == value,
                "El número hablado no conservó su valor: " + value);
        }

        IList<InstalledApplication> applications = new List<InstalledApplication>
        {
            new InstalledApplication("Google Chrome", "test.chrome"),
            new InstalledApplication("Microsoft Excel", "test.excel"),
            new InstalledApplication(
                "Notepad",
                "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App")
        };
        VoiceCommand calculation = VoiceCommandParser.Parse(
            "Raudo cuánto es ciento treinta y dos por cuatrocientos treinta y dos",
            applications);
        Assert(
            calculation.Kind == VoiceCommandKind.Calculation
                && calculation.Title == "57024"
                && calculation.Detail == "132 * 432",
            "La operación hablada no produjo 57024.");

        VoiceCommand app = VoiceCommandParser.Parse(
            "raudo abre microsoft excel",
            applications);
        Assert(
            app.Kind == VoiceCommandKind.OpenApplication
                && app.ApplicationIdentifier == "test.excel",
            "La orden de aplicación no conservó el identificador seguro del catálogo.");
        InstalledApplication notepad = applications[2];
        Assert(
            notepad.Aliases.Contains("Bloc de notas"),
            "El catálogo embebido no vinculó Bloc de notas con la identidad de Notepad.");
        VoiceCommand localizedApp = VoiceCommandParser.Parse(
            "raudo abre bloc de notas",
            applications);
        Assert(
            localizedApp.Kind == VoiceCommandKind.OpenApplication
                && localizedApp.ApplicationIdentifier
                    == "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App",
            "El alias localizado no conservó la identidad instalada de Notepad.");
        Assert(
            VoiceCommandParser.Parse("abre youtube", applications).Kind
                == VoiceCommandKind.OpenYouTube,
            "YouTube no se clasificó como destino fijo.");
        Assert(
            VoiceCommandParser.Parse("enciende pulso", applications).Kind
                == VoiceCommandKind.StartPulse,
            "Pulso no se clasificó como acción local.");
        Assert(
            VoiceCommandParser.Parse("escritorio siguiente", applications).Kind
                == VoiceCommandKind.DesktopRight,
            "El escritorio siguiente no se clasificó correctamente.");
        Assert(
            VoiceCommandParser.Parse("cambia de escritorio", applications).Kind
                == VoiceCommandKind.DesktopAdjacent,
            "El cambio al escritorio adyacente no se clasificó correctamente.");
        Assert(
            VoiceCommandParser.Parse("crea un nuevo escritorio", applications).Kind
                == VoiceCommandKind.DesktopCreate,
            "La creación de escritorio no se clasificó correctamente.");
        Assert(
            VoiceCommandParser.Parse("muéstrame los escritorios", applications).Kind
                == VoiceCommandKind.DesktopOverview,
            "La vista de escritorios no se clasificó correctamente.");
        VoiceCommand conversion = VoiceCommandParser.Parse(
            "convierte diez kilómetros a millas",
            applications);
        Assert(
            conversion.Kind == VoiceCommandKind.Conversion
                && conversion.Title.EndsWith(" mi", StringComparison.Ordinal),
            "La conversión hablada no reutilizó el motor local de Salto.");
        Assert(
            VoiceCommandParser.Parse("borra documentos", applications).Kind
                == VoiceCommandKind.Unknown,
            "Una orden destructiva salió del estado Unknown.");

        IList<InstalledApplication> ambiguousApplications =
            new List<InstalledApplication>
            {
                new InstalledApplication("Microsoft Teams", "test.teams"),
                new InstalledApplication("Microsoft Excel", "test.excel")
            };
        Assert(
            VoiceCommandParser.Parse("abre microsoft", ambiguousApplications).Kind
                == VoiceCommandKind.Unknown,
            "Una aplicación ambigua se eligió sin confirmación.");

        IList<InstalledApplication> ambiguousAliases = new List<InstalledApplication>
        {
            new InstalledApplication(
                "Editor uno",
                "test.editor.one",
                new List<string> { "editor" }),
            new InstalledApplication(
                "Editor dos",
                "test.editor.two",
                new List<string> { "editor" })
        };
        Assert(
            VoiceCommandParser.Parse("abre editor", ambiguousAliases).Kind
                == VoiceCommandKind.Unknown,
            "Un alias compartido eligió una aplicación sin confirmación.");

        string grammar = VoiceGrammarBuilder.BuildSrgs("es-MX");
        string spanishSpainGrammar = VoiceGrammarBuilder.BuildSrgs("es-ES");
        Assert(
            grammar.IndexOf("xml:lang=\"es-MX\"", StringComparison.Ordinal) >= 0
                && spanishSpainGrammar.IndexOf(
                    "xml:lang=\"es-ES\"",
                    StringComparison.Ordinal) >= 0
                && spanishSpainGrammar.IndexOf(
                    "xml:lang=\"es-MX\"",
                    StringComparison.Ordinal) < 0,
            "La gramática no conserva el idioma del reconocedor seleccionado.");
        Assert(
            grammar.IndexOf("SpeechRecognitionTopicConstraint", StringComparison.OrdinalIgnoreCase) < 0
                && grammar.IndexOf("webSearch", StringComparison.OrdinalIgnoreCase) < 0
                && grammar.IndexOf("dictation", StringComparison.OrdinalIgnoreCase) < 0,
            "La gramática local contiene una referencia a reconocimiento remoto.");
        Assert(
            grammar.IndexOf("cuatrocientos treinta y dos", StringComparison.Ordinal) >= 0,
            "La gramática no incluye operandos hablados hasta 999.");

        IList<string> safePhrases = VoiceGrammarBuilder.BuildApplicationPhrases(
            new List<InstalledApplication>
            {
                new InstalledApplication("Bad <App> & Tool", "test.bad")
            });
        Assert(
            safePhrases.Count == 2
                && safePhrases[0] == "abre bad app tool"
                && safePhrases[1] == "raudo abre bad app tool",
            "Los nombres de aplicación no se normalizaron para la gramática.");

        IList<string> localizedPhrases = VoiceGrammarBuilder.BuildApplicationPhrases(
            applications);
        Assert(
            localizedPhrases.Contains("abre bloc de notas")
                && localizedPhrases.Contains("raudo abre bloc de notas"),
            "La gramática de voz no incluyó el alias localizado de Notepad.");

        Assert(
            VoiceSessionPolicy.ShouldRetry(
                new VoiceRecognitionOutcome(
                    VoiceRecognitionOutcomeKind.NotUnderstood,
                    string.Empty,
                    "Audio poco claro"),
                applications),
            "Una orden no entendida no habilitó la reescucha.");
        Assert(
            !VoiceSessionPolicy.ShouldRetry(
                new VoiceRecognitionOutcome(
                    VoiceRecognitionOutcomeKind.Success,
                    "abre bloc de notas",
                    string.Empty),
                applications),
            "Una orden resuelta por alias pidió una reescucha innecesaria.");
        Assert(
            VoiceSessionPolicy.ShouldRetry(
                new VoiceRecognitionOutcome(
                    VoiceRecognitionOutcomeKind.Success,
                    "orden desconocida",
                    string.Empty),
                applications),
            "Una orden reconocida pero no resuelta no habilitó la reescucha.");
        Assert(
            !VoiceSessionPolicy.ShouldRetry(
                new VoiceRecognitionOutcome(
                    VoiceRecognitionOutcomeKind.Error,
                    string.Empty,
                    "Error"),
                applications),
            "Un error de voz intentó reiniciar la escucha.");

        using (VoiceRecognitionService service = new VoiceRecognitionService())
        {
            Assert(!service.IsListening, "El servicio de voz escucha durante el reposo.");
        }
    }

    private static void RunUnitTests()
    {
        TestVoiceCommands();
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
            settings.SaltoCenterX = 760;
            settings.SaltoTopY = 180;
            settings.SaltoOpacityPercent = 82;
            settings.PulseActiveUntilUtcTicks = new DateTime(
                2026,
                7,
                14,
                18,
                30,
                0,
                DateTimeKind.Utc).Ticks;
            store.Save(settings);
            RaudoSettings loaded = store.Load();
            Assert(loaded.DurationMinutes == 60, "La configuración no sobrevivió al guardado.");
            Assert(loaded.MiniModeEnabled, "El estado del Modo Mini no sobrevivió al guardado.");
            Assert(loaded.MiniHintShown, "La ayuda del Modo Mini no sobrevivió al guardado.");
            Assert(
                loaded.MiniCenterX == 420 && loaded.MiniCenterY == 360,
                "La posición del Modo Mini no sobrevivió al guardado.");
            Assert(
                loaded.SaltoCenterX == 760 && loaded.SaltoTopY == 180,
                "La posición de Salto no sobrevivió al guardado.");
            Assert(
                loaded.SaltoOpacityPercent == 82,
                "La opacidad de Salto no sobrevivió al guardado.");
            Assert(
                loaded.PulseActiveUntilUtcTicks == settings.PulseActiveUntilUtcTicks,
                "La expiración de Pulso no sobrevivió al guardado.");

            settings.DurationMinutes = 0;
            settings.SaltoCenterX = -4;
            settings.SaltoTopY = 180;
            settings.SaltoOpacityPercent = 73;
            settings.PulseActiveUntilUtcTicks = -1;
            store.Save(settings);
            Assert(store.Load().DurationMinutes == 30, "La normalización de duración no funcionó.");
            Assert(
                store.Load().SaltoCenterX == -1
                    && store.Load().SaltoTopY == -1
                    && store.Load().SaltoOpacityPercent == 100,
                "La normalización de presentación de Salto no funcionó.");
            Assert(
                store.Load().PulseActiveUntilUtcTicks == 0,
                "La normalización de la expiración de Pulso no funcionó.");
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
        ThemePalette highContrast = ThemePalette.CreateHighContrast();
        Assert(!light.IsDark && dark.IsDark, "Los temas no conservan su modo.");
        Assert(light.Primary != dark.Primary, "Los temas deben tener paletas independientes.");
        Assert(highContrast.IsHighContrast, "La paleta de alto contraste no se identificó.");
        Assert(
            highContrast.Text == SystemColors.WindowText,
            "Alto contraste debe respetar el color de texto del sistema.");
        Assert(
            highContrast.PrimaryForeground == SystemColors.HighlightText,
            "Alto contraste debe respetar el texto de selección del sistema.");

        MediaCommand[] mediaCommands =
        {
            MediaCommand.TogglePlayPause,
            MediaCommand.PreviousTrack,
            MediaCommand.NextTrack,
            MediaCommand.ToggleMute,
            MediaCommand.VolumeDown,
            MediaCommand.VolumeUp
        };
        ushort[] expectedMediaKeys = { 0xB3, 0xB1, 0xB0, 0xAD, 0xAE, 0xAF };
        for (int mediaIndex = 0; mediaIndex < mediaCommands.Length; mediaIndex++)
        {
            MediaCommand command = mediaCommands[mediaIndex];
            Assert(
                MediaControlService.GetVirtualKey(command) == expectedMediaKeys[mediaIndex],
                "Un control multimedia no usa el código virtual documentado.");
            NativeMethods.Input[] mediaInputs = MediaControlService.CreateInputs(command);
            Assert(mediaInputs.Length == 2, "Un control multimedia no generó dos eventos.");
            Assert(
                mediaInputs[0].Type == 1
                    && mediaInputs[1].Type == 1
                    && mediaInputs[0].Union.Keyboard.VirtualKey == expectedMediaKeys[mediaIndex]
                    && mediaInputs[1].Union.Keyboard.VirtualKey == expectedMediaKeys[mediaIndex],
                "Un control multimedia generó una entrada de teclado inesperada.");
            Assert(
                mediaInputs[0].Union.Keyboard.Flags == 0
                    && mediaInputs[1].Union.Keyboard.Flags == 0x0002,
                "Un control multimedia no conservó la secuencia presionar-soltar.");
        }

        int sentMediaInputCount = 0;
        MediaControlService testMediaService = new MediaControlService(
            delegate(NativeMethods.Input[] inputs)
            {
                sentMediaInputCount = inputs.Length;
                return (uint)inputs.Length;
            });
        Assert(
            string.IsNullOrWhiteSpace(
                testMediaService.TryExecute(MediaCommand.TogglePlayPause))
                && sentMediaInputCount == 2,
            "El servicio multimedia no confirmó un envío completo.");
        MediaControlService partialMediaService = new MediaControlService(
            delegate { return 1; });
        Assert(
            !string.IsNullOrWhiteSpace(
                partialMediaService.TryExecute(MediaCommand.NextTrack)),
            "El servicio multimedia ocultó un envío parcial.");
        IList<MediaControlDefinition> mediaDefinitions =
            MediaControlCatalog.GetDefinitions();
        Assert(mediaDefinitions.Count == 6, "El catálogo multimedia no está acotado.");
        Assert(
            MediaSessionService.GetFriendlySourceName("Chrome.exe") == "Google Chrome",
            "El selector no presenta Chrome con un nombre comprensible.");
        Assert(
            MediaSessionService.GetFriendlySourceName(
                "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify") == "Spotify",
            "El selector no presenta Spotify con un nombre comprensible.");

        RaudoActionCatalog actionCatalog = CreateTestActionCatalog(null);
        actionCatalog.Refresh();
        Assert(actionCatalog.Count == 13, "El catálogo no cargó todos los resultados.");
        Assert(
            actionCatalog.Search(string.Empty).Count == 4,
            "La consulta vacía mostró resultados dinámicos.");
        Assert(
            actionCatalog.Search("captura")[0].Id == "capture.screen",
            "La búsqueda por palabra clave no encontró Recortar.");
        Assert(
            actionCatalog.Search("configuracion")[0].Id == "window.main",
            "La búsqueda no ignoró acentos.");
        Assert(
            actionCatalog.Search("pulso")[0].Id == "pulse.toggle",
            "La coincidencia de título no conservó prioridad.");
        Assert(
            actionCatalog.Search("excel")[0].Kind == RaudoActionKind.Window,
            "Una ventana abierta no tuvo prioridad sobre iniciar otra aplicación.");
        Assert(
            actionCatalog.Search("sin coincidencia").Count == 0,
            "Una búsqueda desconocida devolvió acciones.");
        IList<RaudoAction> mediaMatches = actionCatalog.Search("media");
        Assert(mediaMatches.Count == 6, "Salto no encontró los seis controles multimedia.");
        foreach (RaudoAction mediaMatch in mediaMatches)
        {
            Assert(
                mediaMatch.Kind == RaudoActionKind.Media
                    && !mediaMatch.ShowWhenQueryEmpty,
                "Un control multimedia alteró la vista inicial de Salto.");
        }

        decimal arithmeticResult;
        Assert(
            ArithmeticParser.TryEvaluate("2 + 3 * 4", out arithmeticResult)
                && arithmeticResult == 14M,
            "El cálculo no respetó la precedencia de operadores.");
        Assert(
            ArithmeticParser.TryEvaluate("(2 + 3) * 4", out arithmeticResult)
                && arithmeticResult == 20M,
            "El cálculo no respetó los paréntesis.");
        Assert(
            ArithmeticParser.TryEvaluate("-5 + 2", out arithmeticResult)
                && arithmeticResult == -3M,
            "El cálculo no aceptó signos unarios.");
        Assert(
            ArithmeticParser.TryEvaluate("1,5 + 2.25", out arithmeticResult)
                && arithmeticResult == 3.75M,
            "El cálculo no aceptó separadores decimales locales.");
        Assert(
            !ArithmeticParser.TryEvaluate("42", out arithmeticResult),
            "Un número aislado se presentó como cálculo.");
        Assert(
            !ArithmeticParser.TryEvaluate("1 / 0", out arithmeticResult),
            "La división entre cero produjo un resultado.");
        Assert(
            !ArithmeticParser.TryEvaluate("abrir 2 + 2", out arithmeticResult),
            "Texto ajeno se interpretó como cálculo.");
        string excessiveDepth = new string('(', 17) + "1 + 1" + new string(')', 17);
        Assert(
            !ArithmeticParser.TryEvaluate(excessiveDepth, out arithmeticResult),
            "El cálculo excedió el límite de profundidad.");

        string conversionValue;
        string conversionDescription;
        Assert(
            QuickResultProvider.TryConvert(
                "1 km a m",
                out conversionValue,
                out conversionDescription)
                && conversionValue == "1000 m",
            "La conversión de longitud no fue correcta.");
        Assert(
            QuickResultProvider.TryConvert(
                "32 f a c",
                out conversionValue,
                out conversionDescription)
                && conversionValue == "0 °C",
            "La conversión de temperatura no fue correcta.");
        Assert(
            QuickResultProvider.TryConvert(
                "1 gb a mb",
                out conversionValue,
                out conversionDescription)
                && conversionValue == "1024 MB",
            "La conversión de almacenamiento no fue correcta.");
        Assert(
            QuickResultProvider.TryConvert(
                "2 horas en min",
                out conversionValue,
                out conversionDescription)
                && conversionValue == "120 min",
            "La conversión con alias en español no fue correcta.");
        Assert(
            !QuickResultProvider.TryConvert(
                "10 kg a km",
                out conversionValue,
                out conversionDescription),
            "Se permitió una conversión entre familias distintas.");

        string copiedValue = null;
        QuickResultProvider provider = new QuickResultProvider(delegate(string value)
        {
            copiedValue = value;
            return null;
        });
        IList<RaudoAction> calculationActions = provider.CreateActions("12.5 * 8");
        Assert(
            calculationActions.Count == 1
                && calculationActions[0].Kind == RaudoActionKind.Calculation,
            "La consulta no produjo un único resultado de cálculo.");
        Assert(
            string.IsNullOrWhiteSpace(calculationActions[0].Execute())
                && copiedValue == "100",
            "La acción de cálculo no copió el valor esperado.");
        Assert(
            actionCatalog.Search("12.5 * 8")[0].Id == "quick.calculation",
            "El resultado calculado no tuvo prioridad en Salto.");
        Assert(
            actionCatalog.Search("10 km a m")[0].Id == "quick.conversion",
            "El resultado convertido no tuvo prioridad en Salto.");

        IList<KnownFolderEntry> knownFolders = KnownFolderCatalog.GetFolders();
        Assert(knownFolders.Count > 0, "Windows no devolvió carpetas conocidas.");
        HashSet<string> knownFolderPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (KnownFolderEntry folder in knownFolders)
        {
            Assert(
                Path.IsPathRooted(folder.Path) && Directory.Exists(folder.Path),
                "Una carpeta conocida no resolvió a un directorio absoluto existente.");
            Assert(
                knownFolderPaths.Add(folder.Path),
                "El catálogo repitió una carpeta conocida.");
        }

        IList<RaudoAction> knownFolderActions = KnownFolderCatalog.CreateActions();
        Assert(
            knownFolderActions.Count == knownFolders.Count,
            "Las carpetas conocidas no se convirtieron en acciones.");
        Assert(
            knownFolderActions[0].Kind == RaudoActionKind.Folder
                && !knownFolderActions[0].ShowWhenQueryEmpty,
            "Una carpeta conocida apareció sin consulta.");
        RaudoActionCatalog folderCatalog = new RaudoActionCatalog(delegate
        {
            return KnownFolderCatalog.CreateActions();
        });
        folderCatalog.Refresh();
        Assert(
            folderCatalog.Search(knownFolderActions[0].Title).Count > 0
                && folderCatalog.Search(knownFolderActions[0].Title)[0].Kind
                    == RaudoActionKind.Folder,
            "Salto no encontró una carpeta conocida por su nombre.");
        Assert(
            !string.IsNullOrWhiteSpace(FolderLauncher.TryOpen(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")))),
            "El lanzador aceptó una carpeta inexistente.");
        string remoteFolder;
        Assert(
            !LocalFolderPolicy.TryNormalizeExistingDirectory(
                @"\\servidor-inexistente\carpeta",
                out remoteFolder),
            "La política local aceptó una ruta de red.");

        RaudoActionCatalog boundedCatalog = new RaudoActionCatalog(delegate
        {
            List<RaudoAction> manyActions = new List<RaudoAction>();
            for (int index = 0; index < 20; index++)
            {
                manyActions.Add(new RaudoAction(
                    "item:" + index,
                    "Elemento " + index,
                    "Resultado de prueba",
                    "elemento",
                    string.Empty,
                    RaudoActionGlyph.Application,
                    delegate { }));
            }

            return manyActions;
        });
        boundedCatalog.Refresh();
        Assert(
            boundedCatalog.Search("elemento").Count == 12,
            "La consulta dejó de limitarse a doce resultados.");

        int actionInvocationCount = 0;
        RaudoActionCatalog executableCatalog = CreateTestActionCatalog(
            delegate { actionInvocationCount++; });
        executableCatalog.Refresh();
        executableCatalog.Search("pulso")[0].Execute();
        Assert(actionInvocationCount == 1, "El catálogo no ejecutó la acción seleccionada.");
        executableCatalog.Search("reproducir")[0].Execute();
        Assert(
            actionInvocationCount == 2,
            "El catálogo no ejecutó el control multimedia seleccionado.");

        InstalledApplicationCatalog installedCatalog =
            new InstalledApplicationCatalog(delegate
            {
                return new List<InstalledApplication>
                {
                    new InstalledApplication("Excel", "office.excel"),
                    new InstalledApplication("excel", "duplicate.excel"),
                    new InstalledApplication("Calculadora", "windows.calculator"),
                    new InstalledApplication(string.Empty, "invalid")
                };
            });
        installedCatalog.LoadNowForTesting();
        IList<InstalledApplication> installedApplications =
            installedCatalog.GetSnapshot();
        Assert(installedCatalog.IsLoaded, "El catálogo de aplicaciones no terminó.");
        Assert(
            installedApplications.Count == 2,
            "El catálogo de aplicaciones no deduplicó sus resultados.");

        using (SaltoForm salto = new SaltoForm(actionCatalog))
        {
            salto.ApplyTheme(highContrast);
            salto.SetQueryForTesting(string.Empty);
            Assert(salto.ResultCountForTesting == 4, "Salto no limitó la vista inicial.");
            Assert(
                salto.PresentationModeForTesting == SaltoPresentationMode.Ready
                    && salto.VisibleRowsForTesting == 4
                    && salto.ClientSize == new Size(640, 378),
                "Salto no aplicó la geometría inicial adaptativa.");
            salto.MoveSelectionForTesting(-1);
            Assert(
                salto.SelectedActionIdForTesting == "mini.toggle",
                "La navegación hacia arriba no envolvió la selección.");
            salto.MoveSelectionForTesting(1);
            Assert(
                salto.SelectedActionIdForTesting == "pulse.toggle",
                "La navegación hacia abajo no envolvió la selección.");
            salto.SetQueryForTesting("captura");
            Assert(salto.ResultCountForTesting == 1, "Salto no filtró los resultados.");
            Assert(
                salto.SelectedActionIdForTesting == "capture.screen",
                "Salto no seleccionó el primer resultado.");
            Assert(
                salto.PresentationModeForTesting == SaltoPresentationMode.Results
                    && salto.VisibleRowsForTesting == 1
                    && salto.ClientSize == new Size(640, 216),
                "Una búsqueda específica no redujo Salto a una fila.");
            Assert(
                salto.KeyboardHintForTesting.EndsWith("ejecutar", StringComparison.Ordinal),
                "La ayuda de teclado no describió una acción de Raudo.");
            salto.SetQueryForTesting("12.5 * 8");
            Assert(
                salto.ResultCountForTesting == 1
                    && salto.SelectedActionIdForTesting == "quick.calculation",
                "Salto no presentó el cálculo como resultado seleccionable.");
            Assert(
                salto.KeyboardHintForTesting.EndsWith("copiar", StringComparison.Ordinal),
                "La ayuda de teclado no describió la copia del resultado.");
            Assert(
                salto.PresentationModeForTesting == SaltoPresentationMode.Answer
                    && salto.VisibleRowsForTesting == 1
                    && salto.ClientSize == new Size(520, 216),
                "El cálculo no convirtió Salto en una isla compacta.");
            salto.SetQueryForTesting("125");
            Assert(
                salto.PresentationModeForTesting == SaltoPresentationMode.Answer
                    && salto.ClientSize == new Size(520, 216)
                    && salto.LoadingTextForTesting == "Continúa escribiendo la operación",
                "La entrada numérica no conservó la isla de cálculo.");
            salto.SetQueryForTesting("media");
            Assert(
                salto.ResultCountForTesting == 6
                    && salto.SelectedActionIdForTesting == "media.play-pause",
                "Salto no presentó los controles multimedia esperados.");
            Assert(
                salto.KeyboardHintForTesting.EndsWith("controlar", StringComparison.Ordinal),
                "La ayuda de teclado no describió el control multimedia.");
            Assert(
                salto.PresentationModeForTesting == SaltoPresentationMode.Results
                    && salto.VisibleRowsForTesting == 5
                    && salto.ClientSize == new Size(640, 432)
                    && salto.ScrollIndicatorVisibleForTesting,
                "La búsqueda amplia no conservó cinco filas y scroll discreto. "
                    + salto.PresentationModeForTesting
                    + ", filas="
                    + salto.VisibleRowsForTesting
                    + ", tamaño="
                    + salto.ClientSize
                    + ", scroll="
                    + salto.ScrollIndicatorVisibleForTesting);
            Assert(
                Math.Abs(salto.EffectiveOpacityForTesting - 1D) < 0.001D,
                "Alto contraste no forzó la opacidad completa de Salto.");
            AssertAccessibleControls(salto);
            ScaleToTargetDpi(salto, 144);
            AssertControlsWithinParent(salto);
            salto.AllowCloseAndClose();
        }

        RaudoSettings saltoSettings = new RaudoSettings();
        using (SaltoForm saltoOpacity = new SaltoForm(actionCatalog, saltoSettings))
        {
            int savedOpacity = 0;
            Point savedAnchor = Point.Empty;
            saltoOpacity.OpacityChangedByUser += delegate(
                object sender,
                SaltoOpacityChangedEventArgs eventArgs)
            {
                savedOpacity = eventArgs.OpacityPercent;
            };
            saltoOpacity.PositionChangedByUser += delegate(
                object sender,
                SaltoPositionChangedEventArgs eventArgs)
            {
                savedAnchor = eventArgs.Anchor;
            };
            saltoOpacity.ApplyTheme(dark);
            saltoOpacity.CycleOpacityForTesting();
            Assert(
                saltoOpacity.OpacityPercentForTesting == 82 && savedOpacity == 82,
                "Salto no cambió al nivel de opacidad suave.");
            saltoOpacity.CycleOpacityForTesting();
            Assert(
                saltoOpacity.OpacityPercentForTesting == 64,
                "Salto no cambió al nivel de opacidad transparente.");
            saltoOpacity.CycleOpacityForTesting();
            Assert(
                saltoOpacity.OpacityPercentForTesting == 100,
                "Salto no restauró la opacidad completa.");
            Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
            saltoOpacity.MoveForTesting(new Point(workArea.Left + 24, workArea.Top + 24));
            Assert(
                savedAnchor == new Point(
                    saltoOpacity.Left + (saltoOpacity.Width / 2),
                    saltoOpacity.Top)
                    && saltoSettings.SaltoCenterX == savedAnchor.X
                    && saltoSettings.SaltoTopY == savedAnchor.Y,
                "El movimiento de Salto no notificó su ancla persistible.");
            saltoOpacity.AllowCloseAndClose();
        }

        using (SaltoForm saltoLoading = new SaltoForm(actionCatalog))
        {
            saltoLoading.ApplyTheme(dark);
            saltoLoading.SetApplicationCatalogState(true, null);
            saltoLoading.SetQueryForTesting("consulta inexistente");
            Assert(
                saltoLoading.PresentationModeForTesting == SaltoPresentationMode.Loading
                    && saltoLoading.ClientSize == new Size(560, 216)
                    && saltoLoading.LoadingTextForTesting.StartsWith(
                        "Preparando aplicaciones",
                        StringComparison.Ordinal),
                "Salto no mostró el estado de carga compacto.");
            Assert(
                !saltoLoading.LoadingAnimationRunningForTesting,
                "El loading de Salto quedó activo mientras la ventana estaba oculta.");
            saltoLoading.SetApplicationCatalogState(false, null);
            saltoLoading.SetQueryForTesting("consulta inexistente");
            Assert(
                saltoLoading.PresentationModeForTesting == SaltoPresentationMode.Empty,
                "Salto no abandonó el estado de carga al completar el catálogo.");
            saltoLoading.AllowCloseAndClose();
        }

        using (SaltoForm saltoMotion = new SaltoForm(
            actionCatalog,
            new RaudoSettings(),
            delegate { return true; }))
        {
            saltoMotion.ApplyTheme(dark);
            saltoMotion.ShowSalto();
            WaitWithMessages(180);
            saltoMotion.SetQueryForTesting("media");
            WaitWithMessages(45);
            saltoMotion.SetQueryForTesting("captura");
            WaitWithMessages(25);
            saltoMotion.SetQueryForTesting("12.5 * 8");
            WaitWithMessages(210);
            Assert(
                !saltoMotion.TransitionRunningForTesting
                    && saltoMotion.PresentationModeForTesting == SaltoPresentationMode.Answer
                    && saltoMotion.ClientSize == new Size(520, 216),
                "La transición interrumpible no terminó en el estado más reciente.");
            saltoMotion.SetApplicationCatalogState(true, null);
            saltoMotion.SetQueryForTesting("consulta inexistente");
            WaitWithMessages(20);
            Assert(
                saltoMotion.LoadingAnimationRunningForTesting,
                "El loading real no se activó con Salto visible.");
            saltoMotion.SetApplicationCatalogState(false, null);
            Application.DoEvents();
            Assert(
                !saltoMotion.LoadingAnimationRunningForTesting,
                "El loading real no se detuvo al completar el catálogo.");
            saltoMotion.AllowCloseAndClose();
        }

        using (SaltoForm saltoReducedMotion = new SaltoForm(
            actionCatalog,
            new RaudoSettings(),
            delegate { return false; }))
        {
            saltoReducedMotion.ApplyTheme(dark);
            saltoReducedMotion.ShowSalto();
            saltoReducedMotion.SetQueryForTesting("media");
            Assert(
                !saltoReducedMotion.TransitionRunningForTesting
                    && saltoReducedMotion.ClientSize == new Size(640, 432),
                "Movimiento reducido no aplicó inmediatamente el estado final.");
            saltoReducedMotion.SetApplicationCatalogState(true, null);
            saltoReducedMotion.SetQueryForTesting("consulta inexistente");
            Assert(
                !saltoReducedMotion.LoadingAnimationRunningForTesting,
                "Movimiento reducido dejó animado el indicador de carga.");
            saltoReducedMotion.AllowCloseAndClose();
        }

        using (KeepActiveService mainService = new KeepActiveService())
        using (Icon mainIcon = IconFactory.Create(false))
        using (MainForm mainForm = new MainForm(
            mainService,
            new RaudoSettings(),
            mainIcon))
        {
            mainForm.ApplyTheme(highContrast);
            AssertAccessibleControls(mainForm);
            ScaleToTargetDpi(mainForm, 144);
            AssertControlsWithinParent(mainForm);
            mainForm.AllowCloseAndClose();
        }

        using (Icon guideIcon = IconFactory.Create(false))
        using (DesktopGuideForm guide = new DesktopGuideForm(guideIcon))
        {
            guide.ApplyTheme(highContrast);
            guide.ShowIntroduction();
            AssertAccessibleControls(guide);
            ScaleToTargetDpi(guide, 144);
            AssertControlsWithinParent(guide);
            guide.AllowCloseAndClose();
        }

        MediaControlService miniMediaControl = new MediaControlService(
            delegate(NativeMethods.Input[] inputs) { return (uint)inputs.Length; });
        using (FakeMediaSessionService miniMediaSessions = new FakeMediaSessionService())
        using (VirtualDesktopService desktopService = new VirtualDesktopService())
        using (MiniForm mini = new MiniForm(
            desktopService,
            new RaudoSettings(),
            miniMediaControl,
            miniMediaSessions))
        {
            Assert(
                mini.ClientSize == new Size(20, 48),
                "El control de borde no tiene el tamaño esperado.");
            Assert(
                !mini.IsAnimationRunningForTesting,
                "El temporizador de movimiento no debe ejecutarse en reposo.");
            mini.SetExpandedForTesting(true);
            mini.SetNavigationAvailabilityForTesting(true, true);
            Assert(
                mini.ClientSize.Width == 264,
                "Mini no mostró el controlador multimedia completo.");
            mini.SetNavigationAvailabilityForTesting(true, false);
            Assert(mini.ClientSize.Width == 216, "No se ocultó la dirección derecha.");
            mini.SetNavigationAvailabilityForTesting(false, true);
            Assert(mini.ClientSize.Width == 216, "No se ocultó la dirección izquierda.");
            mini.SetNavigationAvailabilityForTesting(false, false);
            Assert(
                mini.ClientSize.Width == 168,
                "El controlador sin escritorios adyacentes no se compactó.");
            miniMediaSessions.SelectForTesting("Google Chrome", false, true);
            mini.RefreshMediaStateForTesting();
            Assert(
                mini.ClientSize.Width == 128,
                "Mini no omitió la pista anterior que el reproductor no admite.");
            IList<string> miniMenuLabels = mini.BuildMenuLabelsForTesting(
                miniMediaSessions.GetSnapshotAsync().GetAwaiter().GetResult());
            Assert(
                miniMenuLabels.Contains("Automático de Windows")
                    && miniMenuLabels.Contains("Google Chrome · Reproduciendo · actual")
                    && miniMenuLabels.Contains("Volumen de Windows")
                    && miniMenuLabels.Contains("Traer ventana"),
                "El menú de Mini no reunió reproductor, volumen y ventanas.");
            mini.ExecuteMediaCommandForTesting(MediaCommand.TogglePlayPause);
            Application.DoEvents();
            Assert(
                miniMediaSessions.LastCommand == MediaCommand.TogglePlayPause,
                "Mini no dirigió play o pausa al servicio multimedia seleccionado.");
            miniMediaSessions.SelectAutomatic();
            mini.RefreshMediaStateForTesting();
            mini.SetExpandedForTesting(false);
            Assert(
                mini.ClientSize == new Size(20, 48),
                "El control no regresó al estado de borde.");
            mini.SetNavigationAvailabilityForTesting(true, true);
            mini.SetRevealProgressForTesting(0.5D);
            Assert(
                mini.ClientSize == new Size(142, 48),
                "La geometría intermedia de la transición no es estable.");
            mini.SetRevealProgressForTesting(0D);
            mini.SetNotificationStateForTesting(UserNotificationState.AcceptsNotifications);
            Assert(
                Math.Abs(mini.WindowOpacityForTesting - 0.82D) < 0.01D,
                "La presencia normal de Mini no conserva la opacidad esperada.");
            mini.SetNotificationStateForTesting(UserNotificationState.RunningDirect3DFullScreen);
            Assert(
                Math.Abs(mini.WindowOpacityForTesting - 0.38D) < 0.01D,
                "Mini no reduce su presencia durante pantalla completa.");
            mini.ApplyTheme(highContrast);
            mini.SetDpiForTesting(144);
            mini.SetNavigationAvailabilityForTesting(true, true);
            mini.SetExpandedForTesting(true);
            Assert(
                mini.ClientSize == new Size(396, 72),
                "El controlador Mini no conserva su geometría al 150 por ciento.");
            mini.AllowCloseAndClose();
        }

        Assert(
            MiniMotion.CollapseDelayMilliseconds == 1400,
            "El tiempo de lectura antes de ocultar Mini cambió inesperadamente.");
        Assert(
            Math.Abs(MiniMotion.EaseReveal(0D)) < 0.0001D
                && Math.Abs(MiniMotion.EaseReveal(1D) - 1D) < 0.0001D,
            "La curva de revelado no conserva sus extremos.");
        Assert(
            Math.Abs(MiniMotion.EaseHide(0D)) < 0.0001D
                && Math.Abs(MiniMotion.EaseHide(1D) - 1D) < 0.0001D,
            "La curva de ocultación no conserva sus extremos.");
        Assert(
            MiniMotion.EaseReveal(0.5D) > 0.5D
                && MiniMotion.EaseHide(0.5D) < 0.5D,
            "Las curvas no aplican desaceleración y aceleración respectivamente.");

        Assert(
            KeepActiveService.DeterminePhase(TimeSpan.FromMinutes(16))
                == KeepActivePhase.Active,
            "Una sesión con más de 15 minutos debe conservar el estado activo.");
        Assert(
            KeepActiveService.DeterminePhase(TimeSpan.FromMinutes(15))
                == KeepActivePhase.EndingSoon,
            "El recordatorio inicial debe comenzar al llegar a 15 minutos.");
        Assert(
            KeepActiveService.DeterminePhase(TimeSpan.FromMinutes(5))
                == KeepActivePhase.Critical,
            "El estado crítico debe comenzar al llegar a 5 minutos.");
        Assert(
            KeepActiveService.DeterminePhase(TimeSpan.Zero)
                == KeepActivePhase.Completed,
            "Una sesión agotada debe marcarse como completada.");

        DateTime pulseReferenceUtc = new DateTime(
            2026,
            7,
            14,
            18,
            0,
            0,
            DateTimeKind.Utc);
        DateTime restorablePulseExpiration;
        DateTime expectedPulseExpiration = pulseReferenceUtc.AddMinutes(30);
        Assert(
            PulseSessionState.TryGetRestorableExpiration(
                expectedPulseExpiration.Ticks,
                pulseReferenceUtc,
                out restorablePulseExpiration)
                && restorablePulseExpiration == expectedPulseExpiration,
            "Una sesión vigente de Pulso no se puede restaurar.");
        Assert(
            !PulseSessionState.TryGetRestorableExpiration(
                pulseReferenceUtc.Ticks,
                pulseReferenceUtc,
                out restorablePulseExpiration),
            "Una sesión vencida de Pulso se consideró restaurable.");
        Assert(
            !PulseSessionState.TryGetRestorableExpiration(
                pulseReferenceUtc.AddMinutes(126).Ticks,
                pulseReferenceUtc,
                out restorablePulseExpiration),
            "Una expiración implausible de Pulso se consideró restaurable.");
        Assert(
            !PulseSessionState.TryGetRestorableExpiration(
                -1,
                pulseReferenceUtc,
                out restorablePulseExpiration),
            "Una expiración inválida de Pulso se consideró restaurable.");

        using (KeepActiveService restoredPulse = new KeepActiveService())
        {
            DateTime liveExpiration = DateTime.UtcNow.AddMinutes(15);
            Assert(
                restoredPulse.TryResume(liveExpiration)
                    && restoredPulse.IsActive
                    && restoredPulse.ActiveUntilUtc.HasValue
                    && restoredPulse.ActiveUntilUtc.Value == liveExpiration,
                "Pulso no reanudó una sesión vigente.");
            restoredPulse.Stop("Prueba completada");
            Assert(
                !restoredPulse.IsActive && !restoredPulse.ActiveUntilUtc.HasValue,
                "Pulso conservó una expiración después de detenerse.");
        }

        Assert(
            ShellUserState.IsImmersive(UserNotificationState.RunningDirect3DFullScreen)
                && ShellUserState.IsImmersive(UserNotificationState.PresentationMode)
                && !ShellUserState.IsImmersive(UserNotificationState.AcceptsNotifications),
            "La detección de contexto inmersivo no conserva el contrato esperado.");
        Assert(
            ShellUserState.AcceptsNotifications(UserNotificationState.AcceptsNotifications)
                && !ShellUserState.AcceptsNotifications(UserNotificationState.Busy),
            "Las notificaciones deben respetar el estado informado por Windows.");

        Rectangle transitionSource = new Rectangle(100, 80, 540, 696);
        Rectangle transitionTarget = new Rectangle(1880, 900, 34, 48);
        ConnectedTransitionFrame firstFrame = ConnectedTransitionMath.GetFrame(
            transitionSource,
            transitionTarget,
            0D);
        ConnectedTransitionFrame lastFrame = ConnectedTransitionMath.GetFrame(
            transitionSource,
            transitionTarget,
            1D);
        Assert(
            firstFrame.Bounds == transitionSource && Math.Abs(firstFrame.Opacity - 1D) < 0.0001D,
            "La transición conectada no conserva su cuadro inicial.");
        Assert(
            lastFrame.Bounds == transitionTarget && Math.Abs(lastFrame.Opacity) < 0.0001D,
            "La transición conectada no termina en Mini de forma estable.");
        Assert(
            ConnectedTransitionMath.GetFrame(
                transitionSource,
                transitionTarget,
                0.5D).Bounds.Width < transitionSource.Width,
            "La transición conectada no reduce progresivamente la ventana.");

        string releaseDigest = new string('a', 64);
        string releaseJson = "{"
            + "\"tag_name\":\"v1.1.0\","
            + "\"html_url\":\"https://github.com/adrielcrv/raudo-windows/releases/tag/v1.1.0\","
            + "\"assets\":[{"
            + "\"name\":\"Raudo-v1.1.0-win.zip\","
            + "\"url\":\"https://api.github.com/repos/adrielcrv/raudo-windows/releases/assets/42\","
            + "\"size\":64000,"
            + "\"digest\":\"sha256:" + releaseDigest + "\""
            + "}]}";
        UpdateCheckResult parsedRelease = UpdateService.ParseRelease(
            releaseJson,
            new Version(1, 0, 2));
        Assert(
            parsedRelease.IsAvailable
                && parsedRelease.LatestVersion == new Version(1, 1, 0)
                && parsedRelease.Package != null
                && parsedRelease.Package.Sha256 == releaseDigest,
            "Los metadatos del paquete de actualización no se validaron correctamente.");
        Assert(
            !parsedRelease.CanInstall,
            "Una ejecución portable no debe intentar reemplazarse como instalación local.");

        string installedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "Raudo",
            "Raudo.exe");
        Assert(
            UpdateService.IsInstalledLocation(installedPath),
            "La ruta de instalación local no fue reconocida.");
        Assert(
            !UpdateService.IsInstalledLocation(Path.Combine(root, "Raudo.exe")),
            "Una copia portable fue confundida con la instalación local.");

        string approvedUpdateDirectory = Path.Combine(
            UpdateInstaller.UpdateRootDirectory,
            "v1.1.0-0123456789abcdef");
        Assert(
            UpdateInstaller.IsApprovedUpdateDirectory(approvedUpdateDirectory),
            "La carpeta controlada de actualización no fue reconocida.");
        Assert(
            !UpdateInstaller.IsApprovedUpdateDirectory(
                Path.Combine(approvedUpdateDirectory, "nested")),
            "El actualizador aceptó una carpeta fuera del nivel controlado.");

        string hashFixture = Path.GetTempFileName();
        try
        {
            File.WriteAllText(hashFixture, "Raudo", Encoding.UTF8);
            Assert(
                UpdateService.ComputeSha256(hashFixture).Length == 64,
                "La validación SHA-256 no produjo una suma completa.");
        }
        finally
        {
            File.Delete(hashFixture);
        }

        string packageRoot = Path.Combine(
            Path.GetTempPath(),
            "Raudo.Update.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(packageRoot);
        try
        {
            string packagePath = Path.Combine(packageRoot, "package.zip");
            string extractedPath = Path.Combine(packageRoot, "extracted.exe");
            string raudoAssemblyPath = typeof(UpdateService).Assembly.Location;
            string executableHash = UpdateService.ComputeSha256(raudoAssemblyPath);
            using (FileStream packageStream = new FileStream(
                packagePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            using (ZipArchive archive = new ZipArchive(packageStream, ZipArchiveMode.Create, false))
            {
                archive.CreateEntryFromFile(raudoAssemblyPath, "Raudo.exe");
                ZipArchiveEntry sums = archive.CreateEntry("SHA256SUMS.txt");
                using (StreamWriter writer = new StreamWriter(sums.Open(), Encoding.ASCII))
                {
                    writer.WriteLine(executableHash + "  Raudo.exe");
                }
            }

            UpdateService.ExtractAndValidateExecutable(
                packagePath,
                extractedPath,
                typeof(UpdateService).Assembly.GetName().Version);
            Assert(
                UpdateService.ComputeSha256(extractedPath) == executableHash,
                "El ejecutable validado no coincide con el contenido del paquete.");
        }
        finally
        {
            Directory.Delete(packageRoot, true);
        }

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
        string pinnedHandlePath = Path.Combine(root, "pinned-window.handle");
        Directory.CreateDirectory(root);

        Process child = null;
        Process pinnedChild = null;
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

                ProcessStartInfo pinnedStartInfo = new ProcessStartInfo();
                pinnedStartInfo.FileName = Application.ExecutablePath;
                pinnedStartInfo.Arguments = "--desktop-probe-window=\"" + pinnedHandlePath + "\"";
                pinnedStartInfo.UseShellExecute = false;
                pinnedStartInfo.CreateNoWindow = true;
                pinnedChild = Process.Start(pinnedStartInfo);
                IntPtr pinnedWindow = WaitForProbeHandle(
                    pinnedHandlePath,
                    pinnedChild,
                    TimeSpan.FromSeconds(5));
                string pinError;
                Assert(
                    service.TryKeepWindowVisibleAcrossDesktops(
                        pinnedWindow,
                        out pinError),
                    pinError ?? "No se pudo mantener la ventana en todos los escritorios.");

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

                IList<DesktopWindow> currentWindows;
                string currentListError;
                Assert(
                    service.TryGetOpenWindows(out currentWindows, out currentListError),
                    currentListError ?? "No se pudieron consultar las ventanas abiertas.");
                DesktopWindow currentProbe = null;
                foreach (DesktopWindow window in currentWindows)
                {
                    if (window.Handle == childWindow)
                    {
                        currentProbe = window;
                        break;
                    }
                }

                Assert(
                    currentProbe != null && currentProbe.IsOnCurrentDesktop,
                    "La ventana actual no apareció en la búsqueda unificada.");

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

                bool pinnedOnCurrentDesktop;
                Assert(
                    service.TryIsWindowOnCurrentDesktop(
                        pinnedWindow,
                        out pinnedOnCurrentDesktop)
                        && pinnedOnCurrentDesktop,
                    "La ventana fijada no siguió al escritorio activo.");

                bool canNavigateLeft;
                bool canNavigateRight;
                Assert(
                    service.TryGetNavigationAvailability(
                        out canNavigateLeft,
                        out canNavigateRight),
                    "No se pudieron consultar los escritorios adyacentes.");
                Assert(
                    returnDirection == DesktopDirection.Left
                        ? canNavigateLeft
                        : canNavigateRight,
                    "No se detectó la dirección para regresar al escritorio original.");

                string moveRaudoError;
                Assert(
                    service.TryMoveWindowToCurrentDesktop(
                        sourceAnchor.Handle,
                        out moveRaudoError),
                    moveRaudoError ?? "No se pudo trasladar una ventana de Raudo.");
                Guid raudoDesktop;
                Guid activeDesktop;
                Assert(
                    service.TryGetDesktopId(sourceAnchor.Handle, out raudoDesktop)
                        && service.TryGetDesktopId(destinationAnchor.Handle, out activeDesktop)
                        && raudoDesktop == activeDesktop,
                    "La ventana de Raudo no terminó en el escritorio activo.");

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

                Assert(
                    service.TryGetOpenWindows(out currentWindows, out currentListError),
                    currentListError ?? "No se pudieron actualizar las ventanas abiertas.");
                currentProbe = null;
                foreach (DesktopWindow window in currentWindows)
                {
                    if (window.Handle == childWindow)
                    {
                        currentProbe = window;
                        break;
                    }
                }

                Assert(
                    currentProbe != null && currentProbe.IsOnCurrentDesktop,
                    "La ventana trasladada no quedó marcada en el escritorio actual.");

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

            if (pinnedChild != null)
            {
                try
                {
                    if (!pinnedChild.HasExited)
                    {
                        pinnedChild.Kill();
                        pinnedChild.WaitForExit(3000);
                    }
                }
                finally
                {
                    pinnedChild.Dispose();
                }
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static bool RunMiniProcessIntegrationTest(int processId)
    {
        IntPtr miniWindow = FindMiniWindow(processId);
        Assert(miniWindow != IntPtr.Zero, "No se encontró la ventana visible de Raudo Mini.");

        DesktopDirection? returnDirection = null;
        bool switched = false;
        try
        {
            using (VirtualDesktopService service = new VirtualDesktopService())
            {
                bool initiallyCurrent;
                Assert(
                    service.TryIsWindowOnCurrentDesktop(miniWindow, out initiallyCurrent)
                        && initiallyCurrent,
                    "Raudo Mini no está visible en el escritorio inicial.");

                bool canNavigateLeft;
                bool canNavigateRight;
                Assert(
                    service.TryGetNavigationAvailability(
                        out canNavigateLeft,
                        out canNavigateRight),
                    "No se pudieron consultar los escritorios adyacentes.");

                DesktopDirection direction;
                if (canNavigateRight)
                {
                    direction = DesktopDirection.Right;
                    returnDirection = DesktopDirection.Left;
                }
                else if (canNavigateLeft)
                {
                    direction = DesktopDirection.Left;
                    returnDirection = DesktopDirection.Right;
                }
                else
                {
                    return false;
                }

                string error;
                Assert(DesktopNavigation.TrySwitch(direction, out error), error);
                switched = true;
                Thread.Sleep(900);

                bool currentAfterSwitch;
                Assert(
                    DesktopNativeMethods.IsWindowVisible(miniWindow)
                        && service.TryIsWindowOnCurrentDesktop(
                            miniWindow,
                            out currentAfterSwitch)
                        && currentAfterSwitch,
                    "Raudo Mini no siguió al escritorio activo.");

                return true;
            }
        }
        finally
        {
            if (switched && returnDirection.HasValue)
            {
                string ignored;
                DesktopNavigation.TrySwitch(returnDirection.Value, out ignored);
                Thread.Sleep(800);
            }
        }
    }

    private static IntPtr FindMiniWindow(int processId)
    {
        IntPtr result = IntPtr.Zero;
        DesktopNativeMethods.EnumWindows(
            delegate(IntPtr window, IntPtr parameter)
            {
                uint windowProcessId;
                DesktopNativeMethods.GetWindowThreadProcessId(window, out windowProcessId);
                if (windowProcessId != (uint)processId
                    || !DesktopNativeMethods.IsWindowVisible(window))
                {
                    return true;
                }

                StringBuilder title = new StringBuilder(64);
                DesktopNativeMethods.GetWindowText(window, title, title.Capacity);
                if (string.Equals(title.ToString(), "Raudo Mini", StringComparison.Ordinal))
                {
                    result = window;
                    return false;
                }

                return true;
            },
            IntPtr.Zero);
        return result;
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

    private static void CaptureMiniMediaUi(
        string path,
        bool dark,
        int dpi,
        bool selectedSession)
    {
        CaptureMiniMediaUi(path, dark, dpi, selectedSession, false);
    }

    private static void CaptureMiniMediaUi(
        string path,
        bool dark,
        int dpi,
        bool selectedSession,
        bool highContrast)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        MediaControlService mediaControl = new MediaControlService(
            delegate(NativeMethods.Input[] inputs) { return (uint)inputs.Length; });
        using (FakeMediaSessionService sessions = new FakeMediaSessionService())
        using (VirtualDesktopService service = new VirtualDesktopService())
        using (MiniForm form = new MiniForm(
            service,
            new RaudoSettings(),
            mediaControl,
            sessions))
        {
            if (selectedSession)
            {
                sessions.SelectForTesting("Google Chrome", false, true);
            }

            form.ShowMini();
            form.ApplyTheme(
                highContrast
                    ? ThemePalette.CreateHighContrast()
                    : ThemePalette.Create(dark));
            form.SetNavigationAvailabilityForTesting(true, true);
            form.SetDpiForTesting(dpi);
            form.SetExpandedForTesting(true);
            form.RefreshMediaStateForTesting();
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

    private sealed class FakeMediaSessionService : IMediaSessionService
    {
        public bool HasSelectedSession { get; private set; }
        public string SelectedDisplayName { get; private set; }
        public bool CanPrevious { get; private set; }
        public bool CanNext { get; private set; }
        public MediaCommand? LastCommand { get; private set; }

        public FakeMediaSessionService()
        {
            SelectedDisplayName = string.Empty;
            CanPrevious = true;
            CanNext = true;
        }

        public Task<MediaSessionSnapshot> GetSnapshotAsync()
        {
            List<MediaSessionDescriptor> sessions = new List<MediaSessionDescriptor>();
            sessions.Add(new MediaSessionDescriptor(
                "test",
                "Google Chrome",
                "Reproduciendo",
                true,
                HasSelectedSession,
                true,
                CanPrevious,
                CanNext));
            return Task.FromResult(new MediaSessionSnapshot(true, string.Empty, sessions));
        }

        public bool TrySelect(string id)
        {
            if (!string.Equals(id, "test", StringComparison.Ordinal))
            {
                return false;
            }

            SelectForTesting("Google Chrome", true, true);
            return true;
        }

        public void SelectAutomatic()
        {
            HasSelectedSession = false;
            SelectedDisplayName = string.Empty;
            CanPrevious = true;
            CanNext = true;
        }

        public Task<string> TryExecuteAsync(MediaCommand command)
        {
            LastCommand = command;
            return Task.FromResult(string.Empty);
        }

        public void SelectForTesting(string displayName, bool canPrevious, bool canNext)
        {
            HasSelectedSession = true;
            SelectedDisplayName = displayName;
            CanPrevious = canPrevious;
            CanNext = canNext;
        }

        public void Dispose()
        {
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertAccessibleControls(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child.TabStop && child.Enabled)
            {
                Assert(
                    !string.IsNullOrWhiteSpace(child.AccessibleName)
                        || !string.IsNullOrWhiteSpace(child.Text),
                    "Un control interactivo no tiene nombre accesible: "
                        + child.GetType().Name);
            }

            AssertAccessibleControls(child);
        }
    }

    private static void AssertControlsWithinParent(Control parent)
    {
        Rectangle available = parent.DisplayRectangle;
        foreach (Control child in parent.Controls)
        {
            Assert(
                child.Left >= available.Left
                    && child.Top >= available.Top
                    && child.Right <= available.Right
                    && child.Bottom <= available.Bottom,
                "Un control salió de su superficie a escala ampliada: "
                    + child.GetType().Name
                    + ". Control="
                    + child.Bounds
                    + ", superficie="
                    + available
                    + ", DPI="
                    + parent.DeviceDpi);
            AssertControlsWithinParent(child);
        }
    }

    private static void ScaleToTargetDpi(Control control, int targetDpi)
    {
        int currentDpi = Math.Max(96, control.DeviceDpi);
        float factor = targetDpi / (float)currentDpi;
        if (Math.Abs(factor - 1F) > 0.001F)
        {
            control.Scale(new SizeF(factor, factor));
        }

        control.PerformLayout();
    }
}
