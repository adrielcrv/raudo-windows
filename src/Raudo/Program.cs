using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Raudo
{
    internal static class Program
    {
        private const string MutexName = "Local\\Raudo-87EDB9C5-8CB7-49F3-B026-2D385480A13F";
        private const string ShowEventName = "Local\\Raudo-Show-87EDB9C5-8CB7-49F3-B026-2D385480A13F";

        [STAThread]
        private static int Main(string[] args)
        {
            if (UpdateInstaller.IsApplyRequest(args))
            {
                return UpdateInstaller.Apply(args);
            }

            UpdateInstaller.Cleanup(args);
            bool startInBackground = args.Any(
                argument => string.Equals(argument, "--background", StringComparison.OrdinalIgnoreCase));

            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            using (EventWaitHandle showEvent = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                ShowEventName))
            {
                if (!createdNew)
                {
                    if (!startInBackground)
                    {
                        showEvent.Set();
                    }

                    return 0;
                }

                DpiAwareness.Enable();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += HandleThreadException;
                Application.Run(new RaudoApplicationContext(!startInBackground, showEvent));
                GC.KeepAlive(mutex);
            }

            return 0;
        }

        private static void HandleThreadException(object sender, ThreadExceptionEventArgs e)
        {
            MessageBox.Show(
                "Raudo encontró un error y no pudo completar la operación.\n\n" + e.Exception.Message,
                "Raudo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
