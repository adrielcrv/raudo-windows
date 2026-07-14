using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace Raudo
{
    internal sealed class RaudoSettings
    {
        public RaudoSettings()
        {
            DurationMinutes = 30;
            MiniCenterX = -1;
            MiniCenterY = -1;
            SaltoCenterX = -1;
            SaltoTopY = -1;
            SaltoOpacityPercent = 100;
        }

        public int DurationMinutes { get; set; }
        public bool MiniModeEnabled { get; set; }
        public bool MiniHintShown { get; set; }
        public bool DesktopGuideShown { get; set; }
        public int MiniCenterX { get; set; }
        public int MiniCenterY { get; set; }
        public int SaltoCenterX { get; set; }
        public int SaltoTopY { get; set; }
        public int SaltoOpacityPercent { get; set; }

        public void Normalize()
        {
            if (!DurationOption.IsSupported(DurationMinutes))
            {
                DurationMinutes = 30;
            }

            if (MiniCenterX < -1 || MiniCenterY < -1)
            {
                MiniCenterX = -1;
                MiniCenterY = -1;
            }

            if (SaltoCenterX < -1 || SaltoTopY < -1)
            {
                SaltoCenterX = -1;
                SaltoTopY = -1;
            }

            if (SaltoOpacityPercent != 100
                && SaltoOpacityPercent != 82
                && SaltoOpacityPercent != 64)
            {
                SaltoOpacityPercent = 100;
            }
        }
    }

    internal sealed class SettingsStore
    {
        private readonly string settingsPath;
        private readonly JavaScriptSerializer serializer;

        public SettingsStore()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Raudo",
                "settings.json"))
        {
        }

        internal SettingsStore(string path)
        {
            settingsPath = path;
            serializer = new JavaScriptSerializer();
        }

        public RaudoSettings Load()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    RaudoSettings loaded = serializer.Deserialize<RaudoSettings>(
                        File.ReadAllText(settingsPath, Encoding.UTF8));
                    if (loaded != null)
                    {
                        loaded.Normalize();
                        return loaded;
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return new RaudoSettings();
        }

        public void Save(RaudoSettings settings)
        {
            settings.Normalize();
            string directory = Path.GetDirectoryName(settingsPath);
            Directory.CreateDirectory(directory);

            string temporaryPath = settingsPath + ".tmp";
            File.WriteAllText(
                temporaryPath,
                serializer.Serialize(settings),
                new UTF8Encoding(false));

            if (File.Exists(settingsPath))
            {
                File.Replace(temporaryPath, settingsPath, null, true);
            }
            else
            {
                File.Move(temporaryPath, settingsPath);
            }
        }
    }

    internal static class StartupManager
    {
        private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string ValueName = "Raudo";

        public static bool IsEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                return key != null && key.GetValue(ValueName) != null;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (enabled)
                {
                    string executable = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    key.SetValue(ValueName, "\"" + executable + "\" --background", RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }
            }
        }
    }
}
