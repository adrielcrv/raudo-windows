using System;
using System.Drawing;
using Microsoft.Win32;
using System.Windows.Forms;

namespace Raudo
{
    internal sealed class ThemePalette
    {
        public bool IsDark { get; private set; }
        public bool IsHighContrast { get; private set; }
        public Color Window { get; private set; }
        public Color Surface { get; private set; }
        public Color SurfaceRaised { get; private set; }
        public Color Border { get; private set; }
        public Color Text { get; private set; }
        public Color TextMuted { get; private set; }
        public Color TextFaint { get; private set; }
        public Color Primary { get; private set; }
        public Color PrimaryForeground { get; private set; }
        public Color PrimaryHover { get; private set; }
        public Color Active { get; private set; }
        public Color ActiveSoft { get; private set; }
        public Color Warning { get; private set; }
        public Color Critical { get; private set; }
        public Color Danger { get; private set; }
        public Color DangerHover { get; private set; }

        public static ThemePalette Create(bool dark)
        {
            if (dark)
            {
                return new ThemePalette
                {
                    IsDark = true,
                    Window = Color.FromArgb(15, 23, 42),
                    Surface = Color.FromArgb(23, 33, 54),
                    SurfaceRaised = Color.FromArgb(30, 41, 59),
                    Border = Color.FromArgb(51, 65, 85),
                    Text = Color.FromArgb(241, 245, 249),
                    TextMuted = Color.FromArgb(148, 163, 184),
                    TextFaint = Color.FromArgb(100, 116, 139),
                    Primary = Color.FromArgb(59, 130, 246),
                    PrimaryForeground = Color.White,
                    PrimaryHover = Color.FromArgb(96, 165, 250),
                    Active = Color.FromArgb(45, 212, 191),
                    ActiveSoft = Color.FromArgb(19, 78, 74),
                    Warning = Color.FromArgb(245, 158, 11),
                    Critical = Color.FromArgb(249, 115, 22),
                    Danger = Color.FromArgb(248, 113, 113),
                    DangerHover = Color.FromArgb(239, 68, 68)
                };
            }

            return new ThemePalette
            {
                IsDark = false,
                Window = Color.FromArgb(246, 248, 252),
                Surface = Color.White,
                SurfaceRaised = Color.FromArgb(248, 250, 252),
                Border = Color.FromArgb(226, 232, 240),
                Text = Color.FromArgb(15, 23, 42),
                TextMuted = Color.FromArgb(71, 85, 105),
                TextFaint = Color.FromArgb(100, 116, 139),
                Primary = Color.FromArgb(37, 99, 235),
                PrimaryForeground = Color.White,
                PrimaryHover = Color.FromArgb(29, 78, 216),
                Active = Color.FromArgb(13, 148, 136),
                ActiveSoft = Color.FromArgb(204, 251, 241),
                Warning = Color.FromArgb(217, 119, 6),
                Critical = Color.FromArgb(234, 88, 12),
                Danger = Color.FromArgb(190, 24, 93),
                DangerHover = Color.FromArgb(157, 23, 77)
            };
        }

        public static ThemePalette CreateHighContrast()
        {
            return new ThemePalette
            {
                IsDark = SystemColors.Window.GetBrightness() < 0.5F,
                IsHighContrast = true,
                Window = SystemColors.Window,
                Surface = SystemColors.Window,
                SurfaceRaised = SystemColors.Control,
                Border = SystemColors.WindowText,
                Text = SystemColors.WindowText,
                TextMuted = SystemColors.WindowText,
                TextFaint = SystemColors.GrayText,
                Primary = SystemColors.Highlight,
                PrimaryForeground = SystemColors.HighlightText,
                PrimaryHover = SystemColors.HotTrack,
                Active = SystemColors.Highlight,
                ActiveSoft = SystemColors.Window,
                Warning = SystemColors.Highlight,
                Critical = SystemColors.Highlight,
                Danger = SystemColors.Highlight,
                DangerHover = SystemColors.HotTrack
            };
        }
    }

    internal static class ThemeService
    {
        public static ThemePalette Current()
        {
            if (SystemInformation.HighContrast)
            {
                return ThemePalette.CreateHighContrast();
            }

            return ThemePalette.Create(IsDarkMode());
        }

        private static bool IsDarkMode()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
                    false))
                {
                    object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    return value is int && (int)value == 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
