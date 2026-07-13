using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Raudo
{
    internal enum DesktopDirection
    {
        Left,
        Right
    }

    internal sealed class DesktopWindow
    {
        public DesktopWindow(IntPtr handle, string applicationName, string title)
        {
            Handle = handle;
            ApplicationName = applicationName;
            Title = title;
        }

        public IntPtr Handle { get; private set; }
        public string ApplicationName { get; private set; }
        public string Title { get; private set; }

        public string DisplayName
        {
            get
            {
                string value = string.IsNullOrWhiteSpace(ApplicationName)
                    ? Title
                    : ApplicationName + " — " + Title;
                return value.Length <= 72 ? value : value.Substring(0, 71) + "…";
            }
        }
    }

    internal static class DesktopNavigation
    {
        private const ushort VirtualKeyControl = 0x11;
        private const ushort VirtualKeyLeftWindows = 0x5B;
        private const ushort VirtualKeyLeft = 0x25;
        private const ushort VirtualKeyRight = 0x27;
        private const uint InputKeyboard = 1;
        private const uint KeyEventKeyUp = 0x0002;

        public static bool TrySwitch(DesktopDirection direction, out string error)
        {
            ushort arrow = direction == DesktopDirection.Left
                ? VirtualKeyLeft
                : VirtualKeyRight;
            NativeMethods.Input[] inputs = new[]
            {
                CreateKey(VirtualKeyControl, false),
                CreateKey(VirtualKeyLeftWindows, false),
                CreateKey(arrow, false),
                CreateKey(arrow, true),
                CreateKey(VirtualKeyLeftWindows, true),
                CreateKey(VirtualKeyControl, true)
            };

            uint sent = NativeMethods.SendInput(
                (uint)inputs.Length,
                inputs,
                Marshal.SizeOf(typeof(NativeMethods.Input)));
            if (sent == (uint)inputs.Length)
            {
                error = null;
                return true;
            }

            error = "Windows no pudo cambiar de escritorio.";
            return false;
        }

        private static NativeMethods.Input CreateKey(ushort virtualKey, bool keyUp)
        {
            NativeMethods.Input input = new NativeMethods.Input();
            input.Type = InputKeyboard;
            input.Union.Keyboard.VirtualKey = virtualKey;
            input.Union.Keyboard.Flags = keyUp ? KeyEventKeyUp : 0;
            return input;
        }
    }

    internal sealed class VirtualDesktopService : IDisposable
    {
        private static readonly Guid ManagerClassId =
            new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A");
        private static readonly Guid ImmersiveShellClassId =
            new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239");
        private static readonly Guid InternalManagerServiceId =
            new Guid("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");

        private IVirtualDesktopManager manager;
        private IVirtualDesktopManagerInternal10 internalManager10;
        private IVirtualDesktopManagerInternal11 internalManager11;
        private IApplicationViewCollection applicationViews;
        private readonly int windowsBuild;
        private bool publicInitializationAttempted;
        private bool internalInitializationAttempted;
        private string compatibilityStatus = "No inicializada.";

        public VirtualDesktopService()
        {
            windowsBuild = DesktopNativeMethods.GetWindowsBuild();
        }

        public bool IsAvailable
        {
            get { return windowsBuild >= 10240; }
        }

        public bool CanBringWindows
        {
            get
            {
                InitializeInternalInterfaces();
                return applicationViews != null
                    && (internalManager10 != null || internalManager11 != null);
            }
        }

        public bool TryGetWindowsOutsideCurrentDesktop(
            out IList<DesktopWindow> windows,
            out string error)
        {
            List<DesktopWindow> results = new List<DesktopWindow>();
            windows = results;

            if (!EnsurePublicManager())
            {
                error = "Los escritorios virtuales requieren Windows 10 o posterior.";
                return false;
            }

            uint currentProcessId = (uint)Process.GetCurrentProcess().Id;
            bool enumerated = DesktopNativeMethods.EnumWindows(
                delegate(IntPtr handle, IntPtr parameter)
                {
                    try
                    {
                        DesktopWindow candidate = CreateCandidate(handle, currentProcessId);
                        if (candidate != null)
                        {
                            results.Add(candidate);
                        }
                    }
                    catch (COMException)
                    {
                    }

                    return true;
                },
                IntPtr.Zero);

            if (!enumerated)
            {
                error = "Windows no pudo consultar las ventanas abiertas.";
                return false;
            }

            results.Sort(delegate(DesktopWindow left, DesktopWindow right)
            {
                return string.Compare(
                    left.DisplayName,
                    right.DisplayName,
                    StringComparison.CurrentCultureIgnoreCase);
            });

            error = null;
            return true;
        }

        public bool TryBringHere(IntPtr window, out string error)
        {
            if (!EnsurePublicManager())
            {
                error = "Los escritorios virtuales requieren Windows 10 o posterior.";
                return false;
            }

            if (window == IntPtr.Zero || !DesktopNativeMethods.IsWindow(window))
            {
                error = "La ventana seleccionada ya no está disponible.";
                return false;
            }

            bool alreadyCurrent;
            int result = manager.IsWindowOnCurrentVirtualDesktop(window, out alreadyCurrent);
            if (result < 0)
            {
                error = "Windows no pudo identificar el escritorio de la ventana.";
                return false;
            }

            if (!alreadyCurrent)
            {
                uint processId;
                DesktopNativeMethods.GetWindowThreadProcessId(window, out processId);
                if (processId != (uint)Process.GetCurrentProcess().Id)
                {
                    string diagnostic;
                    if (!TryMoveExternalWindowToCurrent(window, out diagnostic))
                    {
                        error = "Esta versión de Windows no permitió traer la ventana. "
                            + "Puedes moverla manualmente desde Win + Tab.";
                        return false;
                    }
                }
                else
                {
                    using (CurrentDesktopAnchor anchor = new CurrentDesktopAnchor())
                    {
                        Guid desktopId;
                        result = manager.GetWindowDesktopId(anchor.Handle, out desktopId);
                        if (result < 0 || desktopId == Guid.Empty)
                        {
                            error = "Windows no pudo identificar el escritorio actual.";
                            return false;
                        }

                        result = manager.MoveWindowToDesktop(window, ref desktopId);
                        if (result < 0)
                        {
                            error = "La aplicación no permitió mover esa ventana.";
                            return false;
                        }
                    }
                }
            }

            if (DesktopNativeMethods.IsIconic(window))
            {
                DesktopNativeMethods.ShowWindowAsync(window, 9);
            }

            DesktopNativeMethods.SetForegroundWindow(window);
            error = null;
            return true;
        }

        internal bool TryGetDesktopId(IntPtr window, out Guid desktopId)
        {
            desktopId = Guid.Empty;
            return EnsurePublicManager()
                && window != IntPtr.Zero
                && manager.GetWindowDesktopId(window, out desktopId) >= 0
                && desktopId != Guid.Empty;
        }

        public void Dispose()
        {
            if (manager != null)
            {
                Marshal.FinalReleaseComObject(manager);
                manager = null;
            }

            ReleaseComObject(ref applicationViews);
            ReleaseComObject(ref internalManager11);
            ReleaseComObject(ref internalManager10);
        }

        private void InitializeInternalInterfaces()
        {
            if (internalInitializationAttempted)
            {
                return;
            }

            internalInitializationAttempted = true;
            if (windowsBuild < 10240 || windowsBuild > 28000)
            {
                compatibilityStatus = "Build de Windows no compatible: " + windowsBuild + ".";
                return;
            }

            object shellObject = null;
            try
            {
                Type shellType = Type.GetTypeFromCLSID(ImmersiveShellClassId, true);
                shellObject = Activator.CreateInstance(shellType);
                IComServiceProvider provider = (IComServiceProvider)shellObject;

                Guid viewsServiceId = typeof(IApplicationViewCollection).GUID;
                object viewsObject = provider.QueryService(
                    ref viewsServiceId,
                    ref viewsServiceId);
                if (viewsObject == null)
                {
                    compatibilityStatus = "Windows no devolvió IApplicationViewCollection.";
                    return;
                }

                applicationViews = (IApplicationViewCollection)viewsObject;

                object internalObject;
                if (windowsBuild >= 22000)
                {
                    Guid serviceId = InternalManagerServiceId;
                    Guid interfaceId = typeof(IVirtualDesktopManagerInternal11).GUID;
                    internalObject = provider.QueryService(
                        ref serviceId,
                        ref interfaceId);
                    if (internalObject != null)
                    {
                        internalManager11 = (IVirtualDesktopManagerInternal11)internalObject;
                        compatibilityStatus = "Windows 11 disponible.";
                    }
                }
                else
                {
                    Guid serviceId = InternalManagerServiceId;
                    Guid interfaceId = typeof(IVirtualDesktopManagerInternal10).GUID;
                    internalObject = provider.QueryService(
                        ref serviceId,
                        ref interfaceId);
                    if (internalObject != null)
                    {
                        internalManager10 = (IVirtualDesktopManagerInternal10)internalObject;
                        compatibilityStatus = "Windows 10 disponible.";
                    }
                }
            }
            catch (COMException exception)
            {
                compatibilityStatus = string.Format(
                    "Inicialización COM falló con 0x{0:X8}.",
                    exception.ErrorCode);
                ReleaseComObject(ref applicationViews);
                ReleaseComObject(ref internalManager11);
                ReleaseComObject(ref internalManager10);
            }
            catch (InvalidCastException exception)
            {
                compatibilityStatus = "Interfaz COM inesperada: " + exception.Message;
                ReleaseComObject(ref applicationViews);
                ReleaseComObject(ref internalManager11);
                ReleaseComObject(ref internalManager10);
            }
            finally
            {
                if (shellObject != null && Marshal.IsComObject(shellObject))
                {
                    Marshal.FinalReleaseComObject(shellObject);
                }
            }
        }

        private bool TryMoveExternalWindowToCurrent(IntPtr window, out string diagnostic)
        {
            InitializeInternalInterfaces();
            if (applicationViews == null
                || internalManager10 == null && internalManager11 == null)
            {
                diagnostic = compatibilityStatus;
                return false;
            }

            IApplicationView view = null;
            object desktop = null;
            try
            {
                int result = applicationViews.GetViewForHwnd(window, out view);
                if (result < 0 || view == null)
                {
                    diagnostic = string.Format("No se obtuvo la vista (0x{0:X8}).", result);
                    return false;
                }

                if (internalManager11 != null)
                {
                    IVirtualDesktop11 current = internalManager11.GetCurrentDesktop();
                    desktop = current;
                    internalManager11.MoveViewToDesktop(view, current);
                }
                else
                {
                    IVirtualDesktop10 current = internalManager10.GetCurrentDesktop();
                    desktop = current;
                    internalManager10.MoveViewToDesktop(view, current);
                }

                diagnostic = null;
                return true;
            }
            catch (COMException exception)
            {
                diagnostic = string.Format("Windows respondió 0x{0:X8}.", exception.ErrorCode);
                return false;
            }
            finally
            {
                if (view != null && Marshal.IsComObject(view))
                {
                    Marshal.FinalReleaseComObject(view);
                }

                if (desktop != null && Marshal.IsComObject(desktop))
                {
                    Marshal.FinalReleaseComObject(desktop);
                }
            }
        }

        private static void ReleaseComObject<T>(ref T value) where T : class
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.FinalReleaseComObject(value);
            }

            value = null;
        }

        private bool EnsurePublicManager()
        {
            if (manager != null)
            {
                return true;
            }

            if (publicInitializationAttempted || !IsAvailable)
            {
                return false;
            }

            publicInitializationAttempted = true;
            try
            {
                Type managerType = Type.GetTypeFromCLSID(ManagerClassId, true);
                manager = (IVirtualDesktopManager)Activator.CreateInstance(managerType);
            }
            catch (COMException)
            {
                manager = null;
            }
            catch (PlatformNotSupportedException)
            {
                manager = null;
            }

            return manager != null;
        }

        private DesktopWindow CreateCandidate(IntPtr handle, uint currentProcessId)
        {
            if (handle == IntPtr.Zero
                || !DesktopNativeMethods.IsWindowVisible(handle)
                || DesktopNativeMethods.IsIconic(handle) && GetWindowTitle(handle).Length == 0)
            {
                return null;
            }

            uint processId;
            DesktopNativeMethods.GetWindowThreadProcessId(handle, out processId);
            if (processId == 0 || processId == currentProcessId)
            {
                return null;
            }

            string title = GetWindowTitle(handle);
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            int cloaked;
            if (DesktopNativeMethods.DwmGetWindowAttribute(
                handle,
                14,
                out cloaked,
                Marshal.SizeOf(typeof(int))) == 0 && (cloaked & 1) != 0)
            {
                return null;
            }

            bool onCurrentDesktop;
            if (manager.IsWindowOnCurrentVirtualDesktop(handle, out onCurrentDesktop) < 0
                || onCurrentDesktop)
            {
                return null;
            }

            string processName = string.Empty;
            try
            {
                using (Process process = Process.GetProcessById((int)processId))
                {
                    processName = process.ProcessName;
                }
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return new DesktopWindow(handle, FormatApplicationName(processName), title);
        }

        private static string GetWindowTitle(IntPtr handle)
        {
            int length = Math.Min(512, DesktopNativeMethods.GetWindowTextLength(handle));
            if (length <= 0)
            {
                return string.Empty;
            }

            StringBuilder value = new StringBuilder(length + 1);
            DesktopNativeMethods.GetWindowText(handle, value, value.Capacity);
            return value.ToString().Trim();
        }

        private static string FormatApplicationName(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return string.Empty;
            }

            if (string.Equals(processName, "EXCEL", StringComparison.OrdinalIgnoreCase))
            {
                return "Excel";
            }

            if (string.Equals(processName, "WINWORD", StringComparison.OrdinalIgnoreCase))
            {
                return "Word";
            }

            return char.ToUpperInvariant(processName[0]) + processName.Substring(1);
        }

        private sealed class CurrentDesktopAnchor : NativeWindow, IDisposable
        {
            public CurrentDesktopAnchor()
            {
                CreateParams parameters = new CreateParams();
                parameters.Caption = "RaudoDesktopAnchor";
                parameters.X = -32000;
                parameters.Y = -32000;
                parameters.Width = 1;
                parameters.Height = 1;
                parameters.Style = unchecked((int)0x80000000);
                parameters.ExStyle = 0x08000080;
                CreateHandle(parameters);
            }

            public void Dispose()
            {
                DestroyHandle();
            }
        }
    }

    [ComImport]
    [Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVirtualDesktopManager
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop(
            IntPtr topLevelWindow,
            [MarshalAs(UnmanagedType.Bool)] out bool onCurrentDesktop);

        [PreserveSig]
        int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

        [PreserveSig]
        int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }

    [ComImport]
    [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IComServiceProvider
    {
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object QueryService(
            ref Guid serviceId,
            ref Guid interfaceId);
    }

    [ComImport]
    [Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IApplicationViewCollection
    {
        [PreserveSig]
        int GetViews(out object views);

        [PreserveSig]
        int GetViewsByZOrder(out object views);

        [PreserveSig]
        int GetViewsByAppUserModelId(string applicationId, out object views);

        [PreserveSig]
        int GetViewForHwnd(IntPtr window, out IApplicationView view);
    }

    [ComImport]
    [Guid("372E1D3B-38D3-42E4-A15B-8AB2B178F513")]
    [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    internal interface IApplicationView
    {
    }

    [ComImport]
    [Guid("F31574D6-B682-4CDC-BD56-1827860ABEC6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVirtualDesktopManagerInternal10
    {
        int GetCount();
        void MoveViewToDesktop(IApplicationView view, IVirtualDesktop10 desktop);
        bool CanViewMoveDesktops(IApplicationView view);
        IVirtualDesktop10 GetCurrentDesktop();
    }

    [ComImport]
    [Guid("53F5CA0B-158F-4124-900C-057158060B27")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVirtualDesktopManagerInternal11
    {
        int GetCount();
        void MoveViewToDesktop(IApplicationView view, IVirtualDesktop11 desktop);
        bool CanViewMoveDesktops(IApplicationView view);
        IVirtualDesktop11 GetCurrentDesktop();
    }

    [ComImport]
    [Guid("FF72FFDD-BE7E-43FC-9C03-AD81681E88E4")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVirtualDesktop10
    {
    }

    [ComImport]
    [Guid("3F07F4BE-B107-441A-AF0F-39D82529072C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVirtualDesktop11
    {
    }

    internal static class DesktopNativeMethods
    {
        internal delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct OsVersionInfo
        {
            public uint Size;
            public uint Major;
            public uint Minor;
            public uint Build;
            public uint Platform;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string ServicePack;
            public ushort ServicePackMajor;
            public ushort ServicePackMinor;
            public ushort SuiteMask;
            public byte ProductType;
            public byte Reserved;
        }

        internal static int GetWindowsBuild()
        {
            OsVersionInfo version = new OsVersionInfo();
            version.Size = (uint)Marshal.SizeOf(typeof(OsVersionInfo));
            return RtlGetVersion(ref version) == 0 ? (int)version.Build : 0;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsIconic(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(IntPtr window, StringBuilder value, int capacity);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowTextLength(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindowAsync(IntPtr window, int command);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(
            IntPtr window,
            int attribute,
            out int attributeValue,
            int attributeSize);

        [DllImport("ntdll.dll", CharSet = CharSet.Unicode)]
        private static extern int RtlGetVersion(ref OsVersionInfo version);
    }
}
