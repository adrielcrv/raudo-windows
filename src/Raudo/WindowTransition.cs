using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Raudo
{
    internal struct ConnectedTransitionFrame
    {
        public ConnectedTransitionFrame(Rectangle bounds, double opacity)
            : this()
        {
            Bounds = bounds;
            Opacity = opacity;
        }

        public Rectangle Bounds { get; private set; }
        public double Opacity { get; private set; }
    }

    internal static class ConnectedTransitionMath
    {
        public const int DurationMilliseconds = 250;

        public static ConnectedTransitionFrame GetFrame(
            Rectangle source,
            Rectangle target,
            double progress)
        {
            double time = Math.Max(0D, Math.Min(1D, progress));
            double eased = 1D - Math.Pow(1D - time, 3D);
            Rectangle bounds = new Rectangle(
                Interpolate(source.X, target.X, eased),
                Interpolate(source.Y, target.Y, eased),
                Math.Max(1, Interpolate(source.Width, target.Width, eased)),
                Math.Max(1, Interpolate(source.Height, target.Height, eased)));
            double opacity = time < 0.62D
                ? 1D
                : Math.Max(0D, 1D - ((time - 0.62D) / 0.38D));
            return new ConnectedTransitionFrame(bounds, opacity);
        }

        private static int Interpolate(int start, int end, double progress)
        {
            return (int)Math.Round(start + ((end - start) * progress));
        }
    }

    internal sealed class ConnectedMinimizeTransition : Form
    {
        private const uint SetWindowPosNoActivate = 0x0010;
        private const uint SetWindowPosNoZOrder = 0x0004;

        private readonly Bitmap snapshot;
        private readonly Rectangle sourceBounds;
        private readonly Rectangle targetBounds;
        private readonly Timer animationTimer;
        private readonly Stopwatch stopwatch;
        private readonly Action completed;
        private bool completionRaised;

        private ConnectedMinimizeTransition(
            Bitmap currentSnapshot,
            Rectangle source,
            Rectangle target,
            Action completion)
        {
            snapshot = currentSnapshot;
            sourceBounds = source;
            targetBounds = target;
            completed = completion;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            DoubleBuffered = true;
            BackColor = Color.Black;
            Bounds = sourceBounds;

            stopwatch = new Stopwatch();
            animationTimer = new Timer();
            animationTimer.Interval = 15;
            animationTimer.Tick += AnimationTimerTick;
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        public static ConnectedMinimizeTransition Start(
            MainForm source,
            Rectangle miniBounds,
            Action completed)
        {
            if (source == null || source.IsDisposed || !source.Visible)
            {
                return null;
            }

            Bitmap snapshot = new Bitmap(
                Math.Max(1, source.Width),
                Math.Max(1, source.Height));
            ConnectedMinimizeTransition transition = null;
            try
            {
                source.DrawToBitmap(
                    snapshot,
                    new Rectangle(Point.Empty, snapshot.Size));

                Rectangle destination = GetDestination(source.Bounds, miniBounds);
                transition = new ConnectedMinimizeTransition(
                    snapshot,
                    source.Bounds,
                    destination,
                    completed);
                source.HideToTrayImmediately();
                transition.Show();
                DesktopNativeMethods.SetWindowPos(
                    transition.Handle,
                    IntPtr.Zero,
                    transition.Left,
                    transition.Top,
                    transition.Width,
                    transition.Height,
                    SetWindowPosNoZOrder | SetWindowPosNoActivate);
                transition.stopwatch.Start();
                transition.animationTimer.Start();
                return transition;
            }
            catch
            {
                if (transition != null)
                {
                    transition.Dispose();
                }
                else
                {
                    snapshot.Dispose();
                }
                return null;
            }
        }

        public void Cancel()
        {
            Finish(false);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            eventArgs.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            eventArgs.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            eventArgs.Graphics.DrawImage(snapshot, ClientRectangle);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animationTimer.Dispose();
                snapshot.Dispose();
            }

            base.Dispose(disposing);
        }

        private static Rectangle GetDestination(Rectangle source, Rectangle miniBounds)
        {
            int height = Math.Max(1, miniBounds.Height);
            int width = Math.Max(
                miniBounds.Width,
                (int)Math.Round(source.Width * (height / (double)Math.Max(1, source.Height))));
            return new Rectangle(
                miniBounds.Left + ((miniBounds.Width - width) / 2),
                miniBounds.Top + ((miniBounds.Height - height) / 2),
                width,
                height);
        }

        private void AnimationTimerTick(object sender, EventArgs eventArgs)
        {
            double progress = stopwatch.Elapsed.TotalMilliseconds
                / ConnectedTransitionMath.DurationMilliseconds;
            if (progress >= 1D)
            {
                Finish(true);
                return;
            }

            ConnectedTransitionFrame frame = ConnectedTransitionMath.GetFrame(
                sourceBounds,
                targetBounds,
                progress);
            Bounds = frame.Bounds;
            Opacity = Math.Max(0.05D, frame.Opacity);
            UpdateWindowRegion();
            DesktopNativeMethods.SetWindowPos(
                Handle,
                IntPtr.Zero,
                Left,
                Top,
                Width,
                Height,
                SetWindowPosNoZOrder | SetWindowPosNoActivate);
            Invalidate();
        }

        private void UpdateWindowRegion()
        {
            int radius = Math.Max(4, Math.Min(14, Math.Min(Width, Height) / 5));
            using (GraphicsPath path = new GraphicsPath())
            {
                int diameter = radius * 2;
                Rectangle arc = new Rectangle(0, 0, diameter, diameter);
                path.AddArc(arc, 180, 90);
                arc.X = Math.Max(0, Width - diameter);
                path.AddArc(arc, 270, 90);
                arc.Y = Math.Max(0, Height - diameter);
                path.AddArc(arc, 0, 90);
                arc.X = 0;
                path.AddArc(arc, 90, 90);
                path.CloseFigure();
                Region previous = Region;
                Region = new Region(path);
                if (previous != null)
                {
                    previous.Dispose();
                }
            }
        }

        private void Finish(bool notifyCompletion)
        {
            if (completionRaised)
            {
                return;
            }

            completionRaised = true;
            animationTimer.Stop();
            stopwatch.Stop();
            Action completion = completed;
            Hide();
            Close();
            Dispose();
            if (notifyCompletion && completion != null)
            {
                completion();
            }
        }
    }
}
