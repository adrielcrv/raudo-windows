using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Raudo
{
    internal static class UpdateInstaller
    {
        private const int ParentExitTimeoutMilliseconds = 30000;
        private const int CleanupWaitMilliseconds = 10000;

        public static string UpdateRootDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Raudo",
                    "Updates");
            }
        }

        public static bool IsApplyRequest(string[] arguments)
        {
            return HasFlag(arguments, "--apply-update");
        }

        public static int Apply(string[] arguments)
        {
            string target = GetValue(arguments, "--target=");
            string expectedVersionText = GetValue(arguments, "--expected-version=");
            string updateDirectory = GetValue(arguments, "--update-dir=");
            int parentProcessId;
            Version expectedVersion;
            bool replaced = false;
            string previousPath = null;

            try
            {
                if (!int.TryParse(
                    GetValue(arguments, "--wait-pid="),
                    out parentProcessId)
                    || parentProcessId <= 0
                    || !Version.TryParse(expectedVersionText, out expectedVersion)
                    || !UpdateService.IsInstalledLocation(target)
                    || !IsApprovedUpdateDirectory(updateDirectory))
                {
                    throw new InvalidOperationException("La solicitud de actualización no es válida.");
                }

                string self = Assembly.GetExecutingAssembly().Location;
                if (!PathsEqual(Path.GetDirectoryName(self), updateDirectory)
                    || !string.Equals(
                        Path.GetFileName(self),
                        "Raudo.exe",
                        StringComparison.OrdinalIgnoreCase)
                    || !VersionsEqual(
                        Assembly.GetExecutingAssembly().GetName().Version,
                        expectedVersion))
                {
                    throw new InvalidOperationException("El actualizador no procede del paquete preparado.");
                }

                WaitForParent(parentProcessId, target);
                previousPath = target + ".previous";
                ReplaceExecutable(self, target, previousPath);
                replaced = true;

                ProcessStartInfo restart = new ProcessStartInfo();
                restart.FileName = target;
                restart.WorkingDirectory = Path.GetDirectoryName(target);
                restart.UseShellExecute = true;
                restart.Arguments = string.Join(
                    " ",
                    "--cleanup-update",
                    "--cleanup-pid=" + Process.GetCurrentProcess().Id,
                    "--cleanup-dir=" + QuoteArgument(updateDirectory));
                Process.Start(restart);
                return 0;
            }
            catch (Exception exception)
            {
                if (replaced)
                {
                    RestorePrevious(target, previousPath);
                }

                MessageBox.Show(
                    "Raudo no pudo completar la actualización. Se conservó la versión anterior.\n\n"
                        + exception.Message,
                    "Actualización de Raudo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                TryRestart(target, updateDirectory);
                return 1;
            }
        }

        public static void Cleanup(string[] arguments)
        {
            if (!HasFlag(arguments, "--cleanup-update"))
            {
                return;
            }

            string updateDirectory = GetValue(arguments, "--cleanup-dir=");
            int updaterProcessId;
            if (!int.TryParse(
                GetValue(arguments, "--cleanup-pid="),
                out updaterProcessId)
                || updaterProcessId <= 0
                || !IsApprovedUpdateDirectory(updateDirectory))
            {
                return;
            }

            WaitForExit(updaterProcessId, CleanupWaitMilliseconds);
            DeleteWithRetry(updateDirectory);

            string installed = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Raudo",
                "Raudo.exe");
            DeleteFileWithRetry(installed + ".previous");
            DeleteFileWithRetry(installed + ".update");
        }

        internal static bool IsApprovedUpdateDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
                string root = Path.GetFullPath(UpdateRootDirectory).TrimEnd(Path.DirectorySeparatorChar);
                DirectoryInfo parent = Directory.GetParent(full);
                return parent != null
                    && PathsEqual(parent.FullName, root)
                    && Path.GetFileName(full).StartsWith("v", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void WaitForParent(int processId, string expectedPath)
        {
            Process parent;
            try
            {
                parent = Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                return;
            }

            using (parent)
            {
                string processPath;
                try
                {
                    processPath = parent.MainModule.FileName;
                }
                catch (InvalidOperationException)
                {
                    processPath = null;
                }

                if (!PathsEqual(processPath, expectedPath))
                {
                    throw new InvalidOperationException("El proceso que solicitó la actualización no coincide.");
                }

                if (!parent.WaitForExit(ParentExitTimeoutMilliseconds))
                {
                    throw new TimeoutException("Raudo no se cerró a tiempo para actualizarse.");
                }
            }
        }

        private static void ReplaceExecutable(string source, string target, string previous)
        {
            string staged = target + ".update";
            DeleteFileWithRetry(staged);
            DeleteFileWithRetry(previous);
            File.Copy(source, staged, true);
            if (!string.Equals(
                UpdateService.ComputeSha256(source),
                UpdateService.ComputeSha256(staged),
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("La copia preparada no conserva su suma SHA-256.");
            }

            Exception lastError = null;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                try
                {
                    File.Replace(staged, target, previous, true);
                    return;
                }
                catch (IOException exception)
                {
                    lastError = exception;
                    Thread.Sleep(250);
                }
                catch (UnauthorizedAccessException exception)
                {
                    lastError = exception;
                    Thread.Sleep(250);
                }
            }

            throw new IOException("Windows mantuvo el ejecutable en uso.", lastError);
        }

        private static void RestorePrevious(string target, string previous)
        {
            try
            {
                if (File.Exists(previous))
                {
                    File.Replace(previous, target, null, true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void TryRestart(string target, string updateDirectory)
        {
            try
            {
                if (UpdateService.IsInstalledLocation(target) && File.Exists(target))
                {
                    ProcessStartInfo restart = new ProcessStartInfo();
                    restart.FileName = target;
                    restart.WorkingDirectory = Path.GetDirectoryName(target);
                    restart.UseShellExecute = true;
                    if (IsApprovedUpdateDirectory(updateDirectory))
                    {
                        restart.Arguments = string.Join(
                            " ",
                            "--cleanup-update",
                            "--cleanup-pid=" + Process.GetCurrentProcess().Id,
                            "--cleanup-dir=" + QuoteArgument(updateDirectory));
                    }
                    Process.Start(restart);
                }
            }
            catch (Exception)
            {
            }
        }

        private static void WaitForExit(int processId, int timeoutMilliseconds)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    process.WaitForExit(timeoutMilliseconds);
                }
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static void DeleteWithRetry(string path)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }

                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(200);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(200);
                }
            }
        }

        private static void DeleteFileWithRetry(string path)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(200);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(200);
                }
            }
        }

        private static bool HasFlag(string[] arguments, string flag)
        {
            if (arguments == null)
            {
                return false;
            }

            foreach (string argument in arguments)
            {
                if (string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetValue(string[] arguments, string prefix)
        {
            if (arguments == null)
            {
                return null;
            }

            foreach (string argument in arguments)
            {
                if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return argument.Substring(prefix.Length);
                }
            }

            return null;
        }

        private static bool VersionsEqual(Version left, Version right)
        {
            return left != null
                && right != null
                && left.Major == right.Major
                && left.Minor == right.Minor
                && left.Build == right.Build;
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string QuoteArgument(string value)
        {
            if (value == null || value.IndexOf('"') >= 0)
            {
                throw new ArgumentException("El argumento contiene caracteres no válidos.");
            }

            return "\"" + value + "\"";
        }
    }
}
