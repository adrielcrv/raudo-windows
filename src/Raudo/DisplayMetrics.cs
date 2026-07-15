using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Raudo
{
    internal sealed class DpiMetrics
    {
        public const int DefaultDpi = 96;

        private DpiMetrics(int dpi)
        {
            Dpi = Math.Max(DefaultDpi, dpi);
        }

        public int Dpi { get; private set; }

        public float ScaleFactor
        {
            get { return Dpi / (float)DefaultDpi; }
        }

        public static DpiMetrics ForDpi(int dpi)
        {
            return new DpiMetrics(dpi);
        }

        public static DpiMetrics FromControl(Control control)
        {
            if (control == null)
            {
                throw new ArgumentNullException("control");
            }

            return ForDpi(control.IsHandleCreated ? control.DeviceDpi : DefaultDpi);
        }

        public int Scale(int logicalPixels)
        {
            return Math.Max(1, (logicalPixels * Dpi + 48) / DefaultDpi);
        }

        public int ScaleValue(int logicalPixels)
        {
            return (int)Math.Round(logicalPixels * ScaleFactor);
        }

        public Size Scale(Size logicalSize)
        {
            return new Size(Scale(logicalSize.Width), Scale(logicalSize.Height));
        }
    }

    internal static class MainWindowLayout
    {
        public static Rectangle ResolveBounds(
            Point preferredLocation,
            Size desiredSize,
            Rectangle workingArea)
        {
            if (workingArea.Width <= 0 || workingArea.Height <= 0)
            {
                throw new ArgumentException(
                    "The working area must have a positive size.",
                    "workingArea");
            }

            int width = Math.Max(1, Math.Min(desiredSize.Width, workingArea.Width));
            int height = Math.Max(1, Math.Min(desiredSize.Height, workingArea.Height));
            int maximumLeft = workingArea.Right - width;
            int maximumTop = workingArea.Bottom - height;
            return new Rectangle(
                Math.Max(workingArea.Left, Math.Min(maximumLeft, preferredLocation.X)),
                Math.Max(workingArea.Top, Math.Min(maximumTop, preferredLocation.Y)),
                width,
                height);
        }
    }

    internal sealed class DisplaySnapshot
    {
        public DisplaySnapshot(
            string deviceName,
            Rectangle bounds,
            Rectangle workingArea,
            bool primary)
        {
            DeviceName = deviceName ?? string.Empty;
            Bounds = bounds;
            WorkingArea = workingArea;
            IsPrimary = primary;
        }

        public string DeviceName { get; private set; }
        public Rectangle Bounds { get; private set; }
        public Rectangle WorkingArea { get; private set; }
        public bool IsPrimary { get; private set; }

        public static IList<DisplaySnapshot> Capture()
        {
            Screen[] screens = Screen.AllScreens;
            List<DisplaySnapshot> displays = new List<DisplaySnapshot>(screens.Length);
            for (int index = 0; index < screens.Length; index++)
            {
                Screen screen = screens[index];
                displays.Add(new DisplaySnapshot(
                    screen.DeviceName,
                    screen.Bounds,
                    screen.WorkingArea,
                    screen.Primary));
            }

            return displays;
        }
    }

    internal enum MiniDockEdge
    {
        Left = 0,
        Right = 1
    }

    internal sealed class MiniPlacement
    {
        public MiniPlacement(string deviceName, MiniDockEdge edge, double verticalRatio)
        {
            DeviceName = deviceName ?? string.Empty;
            Edge = edge;
            VerticalRatio = Math.Max(0D, Math.Min(1D, verticalRatio));
        }

        public string DeviceName { get; private set; }
        public MiniDockEdge Edge { get; private set; }
        public double VerticalRatio { get; private set; }
    }

    internal static class MiniPlacementResolver
    {
        public static MiniPlacement FromSettings(
            RaudoSettings settings,
            IList<DisplaySnapshot> displays,
            out bool migrated)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            ValidateDisplays(displays);
            migrated = false;
            if (settings.HasSemanticMiniPlacement)
            {
                return new MiniPlacement(
                    settings.MiniMonitorDeviceName,
                    (MiniDockEdge)settings.MiniDockEdge,
                    settings.MiniVerticalRatio);
            }

            MiniPlacement placement;
            if (settings.MiniCenterX >= 0 && settings.MiniCenterY >= 0)
            {
                placement = Capture(
                    new Point(settings.MiniCenterX, settings.MiniCenterY),
                    displays);
            }
            else
            {
                DisplaySnapshot primary = FindPrimary(displays);
                placement = new MiniPlacement(
                    primary.DeviceName,
                    MiniDockEdge.Right,
                    0.92D);
            }

            WriteToSettings(placement, settings);
            migrated = true;
            return placement;
        }

        public static MiniPlacement Capture(Point anchor, IList<DisplaySnapshot> displays)
        {
            ValidateDisplays(displays);
            DisplaySnapshot display = FindForPoint(anchor, displays);
            Rectangle area = display.WorkingArea;
            MiniDockEdge edge = Math.Abs(anchor.X - area.Left)
                    <= Math.Abs(anchor.X - (area.Right - 1))
                ? MiniDockEdge.Left
                : MiniDockEdge.Right;
            double ratio = area.Height <= 0
                ? 0.5D
                : (anchor.Y - area.Top) / (double)area.Height;
            return new MiniPlacement(display.DeviceName, edge, ratio);
        }

        public static Point Resolve(
            MiniPlacement placement,
            IList<DisplaySnapshot> displays,
            int controlHeight,
            int margin,
            out DisplaySnapshot resolvedDisplay)
        {
            if (placement == null)
            {
                throw new ArgumentNullException("placement");
            }

            ValidateDisplays(displays);
            resolvedDisplay = FindByName(placement.DeviceName, displays)
                ?? FindPrimary(displays);
            Rectangle area = resolvedDisplay.WorkingArea;
            int halfHeight = Math.Max(1, controlHeight / 2);
            int verticalMargin = halfHeight + Math.Max(0, margin);
            int minimumY = area.Top + verticalMargin;
            int maximumY = Math.Max(minimumY, area.Bottom - verticalMargin);
            int requestedY = area.Top + (int)Math.Round(
                area.Height * placement.VerticalRatio);
            return new Point(
                placement.Edge == MiniDockEdge.Left ? area.Left : area.Right - 1,
                Math.Max(minimumY, Math.Min(maximumY, requestedY)));
        }

        public static void WriteToSettings(MiniPlacement placement, RaudoSettings settings)
        {
            if (placement == null)
            {
                throw new ArgumentNullException("placement");
            }
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            settings.SchemaVersion = RaudoSettings.CurrentSchemaVersion;
            settings.MiniMonitorDeviceName = placement.DeviceName;
            settings.MiniDockEdge = (int)placement.Edge;
            settings.MiniVerticalRatio = placement.VerticalRatio;
        }

        private static DisplaySnapshot FindByName(
            string deviceName,
            IList<DisplaySnapshot> displays)
        {
            for (int index = 0; index < displays.Count; index++)
            {
                if (string.Equals(
                    displays[index].DeviceName,
                    deviceName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return displays[index];
                }
            }

            return null;
        }

        private static DisplaySnapshot FindForPoint(
            Point point,
            IList<DisplaySnapshot> displays)
        {
            for (int index = 0; index < displays.Count; index++)
            {
                if (displays[index].Bounds.Contains(point)
                    || displays[index].WorkingArea.Contains(point))
                {
                    return displays[index];
                }
            }

            DisplaySnapshot nearest = displays[0];
            long nearestDistance = DistanceSquared(point, nearest.Bounds);
            for (int index = 1; index < displays.Count; index++)
            {
                long distance = DistanceSquared(point, displays[index].Bounds);
                if (distance < nearestDistance)
                {
                    nearest = displays[index];
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private static long DistanceSquared(Point point, Rectangle bounds)
        {
            int x = Math.Max(bounds.Left, Math.Min(bounds.Right - 1, point.X));
            int y = Math.Max(bounds.Top, Math.Min(bounds.Bottom - 1, point.Y));
            long deltaX = point.X - x;
            long deltaY = point.Y - y;
            return (deltaX * deltaX) + (deltaY * deltaY);
        }

        private static DisplaySnapshot FindPrimary(IList<DisplaySnapshot> displays)
        {
            for (int index = 0; index < displays.Count; index++)
            {
                if (displays[index].IsPrimary)
                {
                    return displays[index];
                }
            }

            return displays[0];
        }

        private static void ValidateDisplays(IList<DisplaySnapshot> displays)
        {
            if (displays == null || displays.Count == 0)
            {
                throw new ArgumentException("At least one display is required.", "displays");
            }
        }
    }

    internal static class DpiMessage
    {
        public const int WindowDpiChanged = 0x02E0;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRectangle
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public static int GetDpi(Message message)
        {
            long value = message.WParam.ToInt64();
            return Math.Max(DpiMetrics.DefaultDpi, (int)(value & 0xFFFF));
        }

        public static Rectangle GetSuggestedBounds(Message message)
        {
            if (message.LParam == IntPtr.Zero)
            {
                return Rectangle.Empty;
            }

            NativeRectangle rectangle = (NativeRectangle)Marshal.PtrToStructure(
                message.LParam,
                typeof(NativeRectangle));
            return Rectangle.FromLTRB(
                rectangle.Left,
                rectangle.Top,
                rectangle.Right,
                rectangle.Bottom);
        }
    }
}
