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
            : this(handle, applicationName, title, false)
        {
        }

        public DesktopWindow(
            IntPtr handle,
            string applicationName,
            string title,
            bool isOnCurrentDesktop)
        {
            Handle = handle;
            ApplicationName = applicationName;
            Title = title;
            IsOnCurrentDesktop = isOnCurrentDesktop;
        }

        public IntPtr Handle { get; private set; }
        public string ApplicationName { get; private set; }
        public string Title { get; private set; }
        public bool IsOnCurrentDesktop { get; private set; }

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
        private const ushort VirtualKeyD = 0x44;
        private const ushort VirtualKeyTab = 0x09;
        private const ushort VirtualKeyLeft = 0x25;
        private const ushort VirtualKeyRight = 0x27;
        private const uint InputKeyboard = 1;
        private const uint KeyEventKeyUp = 0x0002;

        public static bool TrySwitch(DesktopDirection direction, out string error)
        {
            ushort arrow = direction == DesktopDirection.Left
                ? VirtualKeyLeft
                : VirtualKeyRight;
            return TrySendChord(
                new[] { VirtualKeyControl, VirtualKeyLeftWindows, arrow },
                "Windows no pudo cambiar de escritorio.",
                out error);
        }

        public static bool TryCreate(out string error)
        {
            return TrySendChord(
                new[] { VirtualKeyLeftWindows, VirtualKeyControl, VirtualKeyD },
                "Windows no pudo crear un escritorio nuevo.",
                out error);
        }

        public static bool TryOpenOverview(out string error)
        {
            return TrySendChord(
                new[] { VirtualKeyLeftWindows, VirtualKeyTab },
                "Windows no pudo abrir la vista de escritorios.",
                out error);
        }

        private static bool TrySendChord(
            ushort[] keys,
            string failureMessage,
            out string error)
        {
            List<NativeMethods.Input> inputs = new List<NativeMethods.Input>(keys.Length * 2);
            for (int index = 0; index < keys.Length; index++)
            {
                inputs.Add(CreateKey(keys[index], false));
            }

            for (int index = keys.Length - 1; index >= 0; index--)
            {
                inputs.Add(CreateKey(keys[index], true));
            }

            uint sent = NativeMethods.SendInput(
                (uint)inputs.Count,
                inputs.ToArray(),
                Marshal.SizeOf(typeof(NativeMethods.Input)));
            if (sent == (uint)inputs.Count)
            {
                error = null;
                return true;
            }

            error = failureMessage;
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
        private static readonly Guid PinnedAppsServiceId =
            new Guid("B5A399E7-1C87-46B8-88E9-FC5747B171BD");

        private IVirtualDesktopManager manager;
        private IVirtualDesktopManagerInternal10 internalManager10;
        private IVirtualDesktopManagerInternal11 internalManager11;
        private IApplicationViewCollection applicationViews;
        private IVirtualDesktopPinnedApps pinnedApps;
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

        public bool TryKeepWindowVisibleAcrossDesktops(IntPtr window, out string error)
        {
            if (window == IntPtr.Zero || !DesktopNativeMethods.IsWindow(window))
            {
                error = "La ventana de Raudo todavía no está disponible.";
                return false;
            }

            if (!internalInitializationAttempted)
            {
                return TryKeepWindowVisibleTransient(window, out error);
            }

            InitializeInternalInterfaces();
            if (applicationViews == null || pinnedApps == null)
            {
                error = "Windows no permitió mantener Raudo en todos los escritorios.";
                return false;
            }

            return TryPinView(window, applicationViews, pinnedApps, out error);
        }

        private bool TryKeepWindowVisibleTransient(IntPtr window, out string error)
        {
            object shellObject = null;
            IApplicationViewCollection views = null;
            IVirtualDesktopPinnedApps pins = null;
            try
            {
                if (windowsBuild < 10240 || windowsBuild > 28000)
                {
                    error = "Windows no permitió mantener Raudo en todos los escritorios.";
                    return false;
                }

                Type shellType = Type.GetTypeFromCLSID(ImmersiveShellClassId, true);
                shellObject = Activator.CreateInstance(shellType);
                IComServiceProvider provider = (IComServiceProvider)shellObject;

                Guid viewsId = typeof(IApplicationViewCollection).GUID;
                object viewsObject = provider.QueryService(ref viewsId, ref viewsId);
                views = viewsObject as IApplicationViewCollection;

                Guid serviceId = PinnedAppsServiceId;
                Guid interfaceId = typeof(IVirtualDesktopPinnedApps).GUID;
                object pinsObject = provider.QueryService(ref serviceId, ref interfaceId);
                pins = pinsObject as IVirtualDesktopPinnedApps;
                if (views == null || pins == null)
                {
                    error = "Windows no permitió mantener Raudo en todos los escritorios.";
                    return false;
                }

                return TryPinView(window, views, pins, out error);
            }
            catch (COMException)
            {
                error = "Windows no permitió mantener Raudo en todos los escritorios.";
                return false;
            }
            catch (InvalidCastException)
            {
                error = "Windows no permitió mantener Raudo en todos los escritorios.";
                return false;
            }
            finally
            {
                ReleaseComObject(ref pins);
                ReleaseComObject(ref views);
                if (shellObject != null && Marshal.IsComObject(shellObject))
                {
                    Marshal.FinalReleaseComObject(shellObject);
                }
            }
        }

        private static bool TryPinView(
            IntPtr window,
            IApplicationViewCollection views,
            IVirtualDesktopPinnedApps pins,
            out string error)
        {

            IApplicationView view = null;
            try
            {
                int result = views.GetViewForHwnd(window, out view);
                if (result < 0 || view == null)
                {
                    error = "Windows no pudo identificar la ventana de Raudo.";
                    return false;
                }

                if (!pins.IsViewPinned(view))
                {
                    pins.PinView(view);
                }

                error = null;
                return true;
            }
            catch (COMException)
            {
                error = "Windows no permitió mantener Raudo en todos los escritorios.";
                return false;
            }
            finally
            {
                if (view != null && Marshal.IsComObject(view))
                {
                    Marshal.FinalReleaseComObject(view);
                }
            }
        }

        public bool TryGetNavigationAvailability(
            out bool canNavigateLeft,
            out bool canNavigateRight)
        {
            canNavigateLeft = false;
            canNavigateRight = false;
            InitializeInternalInterfaces();

            if (internalManager10 == null && internalManager11 == null)
            {
                return false;
            }

            object current = null;
            object left = null;
            object right = null;
            try
            {
                if (internalManager11 != null)
                {
                    IVirtualDesktop11 currentDesktop = internalManager11.GetCurrentDesktop();
                    current = currentDesktop;

                    IVirtualDesktop11 leftDesktop;
                    int leftResult = internalManager11.GetAdjacentDesktop(
                        currentDesktop,
                        3,
                        out leftDesktop);
                    left = leftDesktop;
                    canNavigateLeft = leftResult >= 0 && leftDesktop != null;

                    IVirtualDesktop11 rightDesktop;
                    int rightResult = internalManager11.GetAdjacentDesktop(
                        currentDesktop,
                        4,
                        out rightDesktop);
                    right = rightDesktop;
                    canNavigateRight = rightResult >= 0 && rightDesktop != null;
                }
                else
                {
                    IVirtualDesktop10 currentDesktop = internalManager10.GetCurrentDesktop();
                    current = currentDesktop;

                    IVirtualDesktop10 leftDesktop;
                    int leftResult = internalManager10.GetAdjacentDesktop(
                        currentDesktop,
                        3,
                        out leftDesktop);
                    left = leftDesktop;
                    canNavigateLeft = leftResult >= 0 && leftDesktop != null;

                    IVirtualDesktop10 rightDesktop;
                    int rightResult = internalManager10.GetAdjacentDesktop(
                        currentDesktop,
                        4,
                        out rightDesktop);
                    right = rightDesktop;
                    canNavigateRight = rightResult >= 0 && rightDesktop != null;
                }

                return true;
            }
            catch (COMException)
            {
                canNavigateLeft = false;
                canNavigateRight = false;
                return false;
            }
            finally
            {
                ReleaseComObject(ref right);
                ReleaseComObject(ref left);
                ReleaseComObject(ref current);
            }
        }

        public bool TryGetWindowsOutsideCurrentDesktop(
            out IList<DesktopWindow> windows,
            out string error)
        {
            return TryGetWindows(false, out windows, out error);
        }

        public bool TryGetOpenWindows(
            out IList<DesktopWindow> windows,
            out string error)
        {
            return TryGetWindows(true, out windows, out error);
        }

        private bool TryGetWindows(
            bool includeCurrentDesktop,
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
                        DesktopWindow candidate = CreateCandidate(
                            handle,
                            currentProcessId,
                            includeCurrentDesktop);
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
                    Guid desktopId;
                    if (!TryGetCurrentDesktopId(out desktopId))
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

            if (DesktopNativeMethods.IsIconic(window))
            {
                if (!DesktopNativeMethods.ShowWindowAsync(window, 9))
                {
                    error = "Windows no pudo restaurar la ventana seleccionada.";
                    return false;
                }
            }

            if (!DesktopNativeMethods.SetForegroundWindow(window))
            {
                error = "La ventana está disponible, pero Windows no permitió activarla.";
                return false;
            }

            error = null;
            return true;
        }

        public bool TryMoveWindowToCurrentDesktop(IntPtr window, out string error)
        {
            if (!EnsurePublicManager())
            {
                error = "Los escritorios virtuales requieren Windows 10 o posterior.";
                return false;
            }

            if (window == IntPtr.Zero || !DesktopNativeMethods.IsWindow(window))
            {
                error = "La ventana de Raudo todavía no está disponible.";
                return false;
            }

            uint processId;
            DesktopNativeMethods.GetWindowThreadProcessId(window, out processId);
            if (processId != (uint)Process.GetCurrentProcess().Id)
            {
                error = "Raudo solo traslada sus propias ventanas con esta acción.";
                return false;
            }

            bool alreadyCurrent;
            int result = manager.IsWindowOnCurrentVirtualDesktop(window, out alreadyCurrent);
            if (result < 0)
            {
                error = "Windows no pudo identificar el escritorio actual.";
                return false;
            }

            if (alreadyCurrent)
            {
                error = null;
                return true;
            }

            Guid desktopId;
            if (!TryGetCurrentDesktopId(out desktopId))
            {
                error = "Windows no pudo identificar el escritorio actual.";
                return false;
            }

            result = manager.MoveWindowToDesktop(window, ref desktopId);
            if (result < 0)
            {
                error = "Windows no permitió mover Raudo al escritorio actual.";
                return false;
            }

            error = null;
            return true;
        }

        private bool TryGetCurrentDesktopId(out Guid desktopId)
        {
            desktopId = Guid.Empty;
            InitializeInternalInterfaces();
            object current = null;
            try
            {
                if (internalManager11 != null)
                {
                    IVirtualDesktop11 desktop = internalManager11.GetCurrentDesktop();
                    current = desktop;
                    desktopId = desktop == null ? Guid.Empty : desktop.GetId();
                }
                else if (internalManager10 != null)
                {
                    IVirtualDesktop10 desktop = internalManager10.GetCurrentDesktop();
                    current = desktop;
                    desktopId = desktop == null ? Guid.Empty : desktop.GetId();
                }

                if (desktopId != Guid.Empty)
                {
                    return true;
                }
            }
            catch (COMException)
            {
                desktopId = Guid.Empty;
            }
            finally
            {
                ReleaseComObject(ref current);
            }

            using (CurrentDesktopAnchor anchor = new CurrentDesktopAnchor())
            {
                return EnsurePublicManager()
                    && manager.GetWindowDesktopId(anchor.Handle, out desktopId) >= 0
                    && desktopId != Guid.Empty;
            }
        }

        internal bool TryGetDesktopId(IntPtr window, out Guid desktopId)
        {
            desktopId = Guid.Empty;
            return EnsurePublicManager()
                && window != IntPtr.Zero
                && manager.GetWindowDesktopId(window, out desktopId) >= 0
                && desktopId != Guid.Empty;
        }

        internal bool TryIsWindowOnCurrentDesktop(IntPtr window, out bool isCurrent)
        {
            isCurrent = false;
            return EnsurePublicManager()
                && window != IntPtr.Zero
                && manager.IsWindowOnCurrentVirtualDesktop(window, out isCurrent) >= 0;
        }

        public void Dispose()
        {
            if (manager != null)
            {
                Marshal.FinalReleaseComObject(manager);
                manager = null;
            }

            ReleaseComObject(ref applicationViews);
            ReleaseComObject(ref pinnedApps);
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

                try
                {
                    Guid serviceId = PinnedAppsServiceId;
                    Guid interfaceId = typeof(IVirtualDesktopPinnedApps).GUID;
                    object pinnedObject = provider.QueryService(
                        ref serviceId,
                        ref interfaceId);
                    if (pinnedObject != null)
                    {
                        pinnedApps = (IVirtualDesktopPinnedApps)pinnedObject;
                    }
                }
                catch (COMException)
                {
                    pinnedApps = null;
                }
                catch (InvalidCastException)
                {
                    pinnedApps = null;
                }
            }
            catch (COMException exception)
            {
                compatibilityStatus = string.Format(
                    "Inicialización COM falló con 0x{0:X8}.",
                    exception.ErrorCode);
                ReleaseComObject(ref applicationViews);
                ReleaseComObject(ref pinnedApps);
                ReleaseComObject(ref internalManager11);
                ReleaseComObject(ref internalManager10);
            }
            catch (InvalidCastException exception)
            {
                compatibilityStatus = "Interfaz COM inesperada: " + exception.Message;
                ReleaseComObject(ref applicationViews);
                ReleaseComObject(ref pinnedApps);
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

        private DesktopWindow CreateCandidate(
            IntPtr handle,
            uint currentProcessId,
            bool includeCurrentDesktop)
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
                || !includeCurrentDesktop && onCurrentDesktop)
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

            return new DesktopWindow(
                handle,
                FormatApplicationName(processName),
                title,
                onCurrentDesktop);
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
        void GetDesktops(out IObjectArray desktops);
        [PreserveSig]
        int GetAdjacentDesktop(
            IVirtualDesktop10 from,
            int direction,
            out IVirtualDesktop10 desktop);
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
        void GetDesktops(out IObjectArray desktops);
        [PreserveSig]
        int GetAdjacentDesktop(
            IVirtualDesktop11 from,
            int direction,
            out IVirtualDesktop11 desktop);
    }

    [ComImport]
    [Guid("4CE81583-1E4C-4632-A621-07A53543148F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVirtualDesktopPinnedApps
    {
        [return: MarshalAs(UnmanagedType.Bool)]
        bool IsAppIdPinned([MarshalAs(UnmanagedType.LPWStr)] string applicationId);
        void PinAppID([MarshalAs(UnmanagedType.LPWStr)] string applicationId);
        void UnpinAppID([MarshalAs(UnmanagedType.LPWStr)] string applicationId);
        [return: MarshalAs(UnmanagedType.Bool)]
        bool IsViewPinned(IApplicationView applicationView);
        void PinView(IApplicationView applicationView);
        void UnpinView(IApplicationView applicationView);
    }

    [ComImport]
    [Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IObjectArray
    {
        void GetCount(out int count);
        void GetAt(
            int index,
            ref Guid interfaceId,
            [MarshalAs(UnmanagedType.Interface)] out object value);
    }

    [ComImport]
    [Guid("FF72FFDD-BE7E-43FC-9C03-AD81681E88E4")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVirtualDesktop10
    {
        [return: MarshalAs(UnmanagedType.Bool)]
        bool IsViewVisible(IApplicationView applicationView);
        Guid GetId();
    }

    [ComImport]
    [Guid("3F07F4BE-B107-441A-AF0F-39D82529072C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVirtualDesktop11
    {
        [return: MarshalAs(UnmanagedType.Bool)]
        bool IsViewVisible(IApplicationView applicationView);
        Guid GetId();
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

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(
            IntPtr window,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

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
