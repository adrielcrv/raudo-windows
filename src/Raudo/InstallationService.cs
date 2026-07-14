using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Raudo
{
    internal sealed class InstallationCommand
    {
        public bool DesktopShortcut { get; set; }
        public bool NoLaunch { get; set; }
    }

    internal sealed class InstallationResult
    {
        public bool Succeeded { get; set; }
        public string InstalledExecutablePath { get; set; }
        public string Message { get; set; }
    }

    internal static class InstallationService
    {
        internal const int InvalidArgumentsExitCode = 64;

        public static string InstalledExecutablePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    "Raudo",
                    "Raudo.exe");
            }
        }

        public static bool IsCurrentExecutableInstalled()
        {
            return IsInstalledLocation(Assembly.GetExecutingAssembly().Location);
        }

        public static bool IsInstalledLocation(string executablePath)
        {
            return PathsEqual(executablePath, InstalledExecutablePath);
        }

        public static bool HasInstallationArguments(string[] arguments)
        {
            if (arguments == null)
            {
                return false;
            }

            foreach (string argument in arguments)
            {
                if (string.Equals(argument, "--install", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(argument, "--no-launch", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(argument, "--desktop-shortcut", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryParseCommand(
            string[] arguments,
            out InstallationCommand command,
            out string error)
        {
            command = null;
            error = null;
            bool install = false;
            bool noLaunch = false;
            bool desktopShortcut = false;

            if (arguments == null)
            {
                error = "Falta el argumento --install.";
                return false;
            }

            foreach (string argument in arguments)
            {
                if (string.Equals(argument, "--install", StringComparison.OrdinalIgnoreCase))
                {
                    if (install)
                    {
                        error = "El argumento --install sólo puede indicarse una vez.";
                        return false;
                    }

                    install = true;
                }
                else if (string.Equals(argument, "--no-launch", StringComparison.OrdinalIgnoreCase))
                {
                    noLaunch = true;
                }
                else if (string.Equals(
                    argument,
                    "--desktop-shortcut",
                    StringComparison.OrdinalIgnoreCase))
                {
                    desktopShortcut = true;
                }
                else
                {
                    error = "La solicitud de instalación contiene un argumento no permitido.";
                    return false;
                }
            }

            if (!install)
            {
                error = "Falta el argumento --install.";
                return false;
            }

            command = new InstallationCommand
            {
                DesktopShortcut = desktopShortcut,
                NoLaunch = noLaunch
            };
            return true;
        }

        public static InstallationResult InstallCurrentExecutable(bool desktopShortcut)
        {
            return InstallExecutable(
                Assembly.GetExecutingAssembly().Location,
                InstalledExecutablePath,
                StartMenuShortcutPath(),
                desktopShortcut ? DesktopShortcutPath() : null);
        }

        internal static InstallationResult InstallExecutable(
            string sourcePath,
            string targetPath,
            string startMenuShortcutPath,
            string desktopShortcutPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourcePath)
                    || !File.Exists(sourcePath)
                    || !IsInstalledLocation(targetPath)
                    || string.IsNullOrWhiteSpace(startMenuShortcutPath))
                {
                    throw new InvalidOperationException("La solicitud de instalación no es válida.");
                }

                string targetDirectory = Path.GetDirectoryName(targetPath);
                Directory.CreateDirectory(targetDirectory);

                if (!PathsEqual(sourcePath, targetPath))
                {
                    CopyExecutableAtomically(sourcePath, targetPath);
                }

                CreateShortcut(startMenuShortcutPath, targetPath);
                if (!string.IsNullOrWhiteSpace(desktopShortcutPath))
                {
                    CreateShortcut(desktopShortcutPath, targetPath);
                }

                if (StartupManager.IsEnabled())
                {
                    StartupManager.SetEnabledForExecutable(true, targetPath);
                }

                return new InstallationResult
                {
                    Succeeded = true,
                    InstalledExecutablePath = targetPath,
                    Message = "Raudo quedó instalado para este usuario."
                };
            }
            catch (Exception exception)
            {
                return new InstallationResult
                {
                    Succeeded = false,
                    InstalledExecutablePath = null,
                    Message = FriendlyInstallError(exception)
                };
            }
        }

        internal static void CopyExecutableAtomically(string sourcePath, string targetPath)
        {
            string stagedPath = targetPath + ".installing";
            string previousPath = targetPath + ".previous-install";
            bool targetExisted = File.Exists(targetPath);
            DeleteIfPresent(stagedPath);
            DeleteIfPresent(previousPath);

            try
            {
                File.Copy(sourcePath, stagedPath, true);
                string expectedHash = UpdateService.ComputeSha256(sourcePath);
                if (!string.Equals(
                    expectedHash,
                    UpdateService.ComputeSha256(stagedPath),
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("La copia no conserva su suma SHA-256.");
                }

                if (targetExisted)
                {
                    File.Replace(stagedPath, targetPath, previousPath, true);
                }
                else
                {
                    File.Move(stagedPath, targetPath);
                }

                if (!string.Equals(
                    expectedHash,
                    UpdateService.ComputeSha256(targetPath),
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (targetExisted)
                    {
                        RestorePrevious(targetPath, previousPath);
                    }
                    else
                    {
                        DeleteIfPresent(targetPath);
                    }

                    throw new InvalidDataException("La instalación no superó la verificación final.");
                }

                DeleteIfPresent(previousPath);
            }
            finally
            {
                DeleteIfPresent(stagedPath);
            }
        }

        public static void LaunchInstalledExecutable(string executablePath)
        {
            if (!IsInstalledLocation(executablePath) || !File.Exists(executablePath))
            {
                throw new InvalidOperationException("La copia instalada no está disponible.");
            }

            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = executablePath;
            start.WorkingDirectory = Path.GetDirectoryName(executablePath);
            start.UseShellExecute = true;
            Process process = Process.Start(start);
            if (process != null)
            {
                process.Dispose();
            }
        }

        internal static string StartMenuShortcutPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                "Raudo.lnk");
        }

        internal static string DesktopShortcutPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "Raudo.lnk");
        }

        internal static void CreateShortcut(string shortcutPath, string targetPath)
        {
            string directory = Path.GetDirectoryName(shortcutPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            ShellLink shellLink = new ShellLink();
            try
            {
                IShellLinkW link = (IShellLinkW)(object)shellLink;
                link.SetPath(targetPath);
                link.SetWorkingDirectory(Path.GetDirectoryName(targetPath));
                link.SetDescription("Herramientas rápidas para Windows");
                link.SetIconLocation(targetPath, 0);
                ((IPersistFile)link).Save(shortcutPath, false);
            }
            finally
            {
                Marshal.FinalReleaseComObject(shellLink);
            }
        }

        private static void RestorePrevious(string targetPath, string previousPath)
        {
            try
            {
                if (File.Exists(previousPath))
                {
                    File.Replace(previousPath, targetPath, null, true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void DeleteIfPresent(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
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

        private static string FriendlyInstallError(Exception exception)
        {
            if (exception is UnauthorizedAccessException)
            {
                return "Windows no permitió escribir en la carpeta de aplicaciones del usuario.";
            }

            if (exception is IOException)
            {
                return "No se pudo copiar Raudo. Cierra cualquier copia abierta e inténtalo de nuevo.";
            }

            return "No se pudo instalar Raudo. " + exception.Message;
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    internal sealed class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    internal interface IShellLinkW
    {
        void GetPath(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file,
            int maximumPath,
            IntPtr findData,
            uint flags);

        void GetIDList(out IntPtr identifierList);
        void SetIDList(IntPtr identifierList);
        void GetDescription(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description,
            int maximumName);

        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory,
            int maximumPath);

        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments,
            int maximumPath);

        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCommand);
        void SetShowCmd(int showCommand);
        void GetIconLocation(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath,
            int maximumPath,
            out int iconIndex);

        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}
