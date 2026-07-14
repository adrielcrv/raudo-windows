using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Raudo
{
    internal static class DpiAwareness
    {
        public static void Enable()
        {
            try
            {
                if (NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4)))
                {
                    return;
                }
            }
            catch (EntryPointNotFoundException)
            {
            }

            NativeMethods.SetProcessDPIAware();
        }
    }

    internal static class ScreenCaptureLauncher
    {
        public static bool IsAvailable()
        {
            using (RegistryKey key = Registry.ClassesRoot.OpenSubKey("ms-screenclip", false))
            {
                return key != null;
            }
        }

        public static void Launch()
        {
            if (!IsAvailable())
            {
                throw new InvalidOperationException("La Herramienta Recortes no está disponible.");
            }

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = "ms-screenclip:";
            info.UseShellExecute = true;
            Process.Start(info);
        }
    }

    internal static class BrowserLauncher
    {
        public static void OpenGitHubRelease(Uri uri)
        {
            if (uri == null
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("La dirección de actualización no es válida.");
            }

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = uri.AbsoluteUri;
            info.UseShellExecute = true;
            Process.Start(info);
        }
    }

    internal static class IconFactory
    {
        public static Icon Create(bool active)
        {
            using (Bitmap bitmap = new Bitmap(32, 32))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                BrandDrawing.DrawMark(
                    graphics,
                    new Rectangle(1, 1, 30, 30),
                    active ? Color.FromArgb(13, 148, 136) : Color.FromArgb(37, 99, 235),
                    Color.White);

                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon temporary = Icon.FromHandle(handle))
                    {
                        return (Icon)temporary.Clone();
                    }
                }
                finally
                {
                    NativeMethods.DestroyIcon(handle);
                }
            }
        }

    }

    internal static class WindowTheme
    {
        private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
        private const int DwmwaUseImmersiveDarkMode = 20;

        public static void Apply(IntPtr handle, bool dark)
        {
            int value = dark ? 1 : 0;
            if (NativeMethods.DwmSetWindowAttribute(
                handle,
                DwmwaUseImmersiveDarkMode,
                ref value,
                Marshal.SizeOf(typeof(int))) != 0)
            {
                NativeMethods.DwmSetWindowAttribute(
                    handle,
                    DwmwaUseImmersiveDarkModeBefore20H1,
                    ref value,
                    Marshal.SizeOf(typeof(int)));
            }
        }
    }

    internal static class MotionSettings
    {
        private const uint GetClientAreaAnimation = 0x1042;

        public static bool ClientAreaAnimationsEnabled()
        {
            bool enabled;
            try
            {
                return NativeMethods.SystemParametersInfo(
                    GetClientAreaAnimation,
                    0,
                    out enabled,
                    0)
                    ? enabled
                    : true;
            }
            catch (EntryPointNotFoundException)
            {
                return true;
            }
        }
    }

    internal enum UserNotificationState
    {
        NotPresent = 1,
        Busy = 2,
        RunningDirect3DFullScreen = 3,
        PresentationMode = 4,
        AcceptsNotifications = 5,
        QuietTime = 6,
        WindowsApp = 7
    }

    internal static class ShellUserState
    {
        public static UserNotificationState Current()
        {
            UserNotificationState state;
            return NativeMethods.SHQueryUserNotificationState(out state) == 0
                ? state
                : UserNotificationState.AcceptsNotifications;
        }

        public static bool IsImmersive(UserNotificationState state)
        {
            return state == UserNotificationState.Busy
                || state == UserNotificationState.RunningDirect3DFullScreen
                || state == UserNotificationState.PresentationMode;
        }

        public static bool AcceptsNotifications(UserNotificationState state)
        {
            return state == UserNotificationState.AcceptsNotifications;
        }
    }

    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct LastInputInfo
        {
            public uint Size;
            public uint Time;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Input
        {
            public uint Type;
            public InputUnion Union;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct InputUnion
        {
            [FieldOffset(0)]
            public MouseInput Mouse;

            [FieldOffset(0)]
            public KeyboardInput Keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MouseInput
        {
            public int Dx;
            public int Dy;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct KeyboardInput
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint SendInput(uint inputCount, [In] Input[] inputs, int inputSize);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetLastInputInfo(ref LastInputInfo info);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        internal static extern int GetSystemMetrics(int index);

        [DllImport("kernel32.dll")]
        internal static extern uint SetThreadExecutionState(uint executionState);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyIcon(IntPtr icon);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SystemParametersInfo(
            uint action,
            uint parameter,
            [MarshalAs(UnmanagedType.Bool)] out bool value,
            uint updateFlags);

        [DllImport("shell32.dll")]
        internal static extern int SHQueryUserNotificationState(
            out UserNotificationState state);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(
            IntPtr window,
            int attribute,
            ref int attributeValue,
            int attributeSize);
    }
}
