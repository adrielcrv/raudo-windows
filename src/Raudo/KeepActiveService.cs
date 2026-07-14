using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Raudo
{
    internal enum KeepActivePhase
    {
        Inactive,
        Active,
        EndingSoon,
        Critical,
        Completed
    }

    internal sealed class KeepActiveAttentionEventArgs : EventArgs
    {
        public KeepActiveAttentionEventArgs(KeepActivePhase phase)
        {
            Phase = phase;
        }

        public KeepActivePhase Phase { get; private set; }
    }

    internal sealed class KeepActiveService : IDisposable
    {
        internal const int IdleThresholdMilliseconds = 45000;
        internal const int EndingSoonMinutes = 15;
        internal const int CriticalMinutes = 5;
        private readonly System.Windows.Forms.Timer executionTimer;

        private DateTime activeUntilUtc;
        private bool keepAwakeGranted;
        private bool disposed;

        public KeepActiveService()
        {
            executionTimer = new System.Windows.Forms.Timer();
            executionTimer.Tick += ExecutionTimerTick;
            StatusMessage = "Pulso listo";
        }

        public event EventHandler StateChanged;
        public event EventHandler<KeepActiveAttentionEventArgs> AttentionRequired;

        public bool IsActive { get; private set; }
        public KeepActivePhase Phase { get; private set; }
        public int PulseCount { get; private set; }
        public string StatusMessage { get; private set; }
        public DateTime? ActiveUntilUtc
        {
            get { return IsActive ? activeUntilUtc : (DateTime?)null; }
        }

        public void Start(int durationMinutes)
        {
            ThrowIfDisposed();
            if (!DurationOption.IsSupported(durationMinutes))
            {
                throw new ArgumentOutOfRangeException("durationMinutes");
            }

            if (IsActive)
            {
                Stop("Reiniciado");
            }

            ActivateUntil(DateTime.UtcNow.AddMinutes(durationMinutes));
        }

        public bool TryResume(DateTime expirationUtc)
        {
            ThrowIfDisposed();

            if (expirationUtc.Kind != DateTimeKind.Utc)
            {
                expirationUtc = expirationUtc.ToUniversalTime();
            }

            DateTime restorableExpiration;
            if (!PulseSessionState.TryGetRestorableExpiration(
                expirationUtc.Ticks,
                DateTime.UtcNow,
                out restorableExpiration))
            {
                return false;
            }

            if (IsActive)
            {
                Stop("Reiniciado");
            }

            ActivateUntil(restorableExpiration);
            return true;
        }

        private void ActivateUntil(DateTime expirationUtc)
        {
            keepAwakeGranted = PowerState.TryKeepAwake();
            activeUntilUtc = expirationUtc;
            PulseCount = 0;
            StatusMessage = keepAwakeGranted
                ? "Activo"
                : "Activo; Windows rechazó la solicitud adicional de energía";
            IsActive = true;
            Phase = DeterminePhase(activeUntilUtc - DateTime.UtcNow);
            ScheduleNextCheck(IdleThresholdMilliseconds);
            RaiseStateChanged();
        }

        public void Stop(string reason)
        {
            executionTimer.Stop();

            if (keepAwakeGranted)
            {
                PowerState.Release();
            }

            bool changed = IsActive || keepAwakeGranted;
            IsActive = false;
            Phase = KeepActivePhase.Inactive;
            keepAwakeGranted = false;
            activeUntilUtc = DateTime.MinValue;
            StatusMessage = reason;

            if (changed)
            {
                RaiseStateChanged();
            }
        }

        public TimeSpan? GetRemaining()
        {
            if (!IsActive)
            {
                return null;
            }

            TimeSpan remaining = activeUntilUtc - DateTime.UtcNow;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            executionTimer.Stop();
            if (keepAwakeGranted)
            {
                PowerState.Release();
            }

            executionTimer.Dispose();
            disposed = true;
        }

        private void ExecutionTimerTick(object sender, EventArgs e)
        {
            executionTimer.Stop();
            if (!IsActive)
            {
                return;
            }

            if (DateTime.UtcNow >= activeUntilUtc)
            {
                Complete();
                return;
            }

            KeepActivePhase nextPhase = DeterminePhase(activeUntilUtc - DateTime.UtcNow);
            bool phaseChanged = nextPhase != Phase;
            if (phaseChanged)
            {
                Phase = nextPhase;
                RaiseStateChanged();
                RaiseAttentionRequired(Phase);
            }

            uint idleMilliseconds;
            int nextDelay = IdleThresholdMilliseconds;

            if (NativeInput.TryGetIdleMilliseconds(out idleMilliseconds))
            {
                if (idleMilliseconds >= IdleThresholdMilliseconds)
                {
                    string error;
                    if (!NativeInput.TryPulse(out error))
                    {
                        Stop("Pulso se detuvo: " + error);
                        return;
                    }

                    PulseCount++;
                    StatusMessage = "Activo";
                    if (!phaseChanged)
                    {
                        RaiseStateChanged();
                    }
                }
                else
                {
                    nextDelay = Math.Max(1000, IdleThresholdMilliseconds - (int)idleMilliseconds);
                }
            }

            ScheduleNextCheck(nextDelay);
        }

        private void ScheduleNextCheck(int requestedDelayMilliseconds)
        {
            if (!IsActive)
            {
                return;
            }

            double untilExpiration = (activeUntilUtc - DateTime.UtcNow).TotalMilliseconds;
            if (untilExpiration <= 0)
            {
                Stop("Pulso finalizó");
                return;
            }

            int delay = Math.Max(1000, requestedDelayMilliseconds);
            delay = Math.Min(delay, Math.Max(1000, (int)Math.Min(int.MaxValue, untilExpiration)));
            double untilThreshold = GetMillisecondsUntilNextThreshold(untilExpiration);
            if (untilThreshold > 0)
            {
                delay = Math.Min(
                    delay,
                    Math.Max(1000, (int)Math.Min(int.MaxValue, untilThreshold)));
            }
            executionTimer.Interval = delay;
            executionTimer.Start();
        }

        internal static KeepActivePhase DeterminePhase(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero)
            {
                return KeepActivePhase.Completed;
            }

            if (remaining <= TimeSpan.FromMinutes(CriticalMinutes))
            {
                return KeepActivePhase.Critical;
            }

            if (remaining <= TimeSpan.FromMinutes(EndingSoonMinutes))
            {
                return KeepActivePhase.EndingSoon;
            }

            return KeepActivePhase.Active;
        }

        private double GetMillisecondsUntilNextThreshold(double untilExpiration)
        {
            if (Phase == KeepActivePhase.Active)
            {
                return untilExpiration - TimeSpan.FromMinutes(EndingSoonMinutes).TotalMilliseconds;
            }

            if (Phase == KeepActivePhase.EndingSoon)
            {
                return untilExpiration - TimeSpan.FromMinutes(CriticalMinutes).TotalMilliseconds;
            }

            return untilExpiration;
        }

        private void Complete()
        {
            executionTimer.Stop();
            if (keepAwakeGranted)
            {
                PowerState.Release();
            }

            IsActive = false;
            keepAwakeGranted = false;
            activeUntilUtc = DateTime.MinValue;
            Phase = KeepActivePhase.Completed;
            StatusMessage = "Pulso finalizó";
            RaiseStateChanged();
            RaiseAttentionRequired(Phase);
        }

        private void RaiseStateChanged()
        {
            EventHandler handler = StateChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void RaiseAttentionRequired(KeepActivePhase phase)
        {
            EventHandler<KeepActiveAttentionEventArgs> handler = AttentionRequired;
            if (handler != null)
            {
                handler(this, new KeepActiveAttentionEventArgs(phase));
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException("KeepActiveService");
            }
        }
    }

    internal static class PowerState
    {
        private const uint EsSystemRequired = 0x00000001;
        private const uint EsDisplayRequired = 0x00000002;
        private const uint EsContinuous = 0x80000000;

        public static bool TryKeepAwake()
        {
            return NativeMethods.SetThreadExecutionState(
                EsContinuous | EsSystemRequired | EsDisplayRequired) != 0;
        }

        public static void Release()
        {
            NativeMethods.SetThreadExecutionState(EsContinuous);
        }
    }

    internal static class NativeInput
    {
        private const uint InputMouse = 0;
        private const uint MouseEventMove = 0x0001;
        private const uint MouseEventVirtualDesk = 0x4000;
        private const uint MouseEventAbsolute = 0x8000;
        private const int SmXVirtualScreen = 76;
        private const int SmYVirtualScreen = 77;
        private const int SmCxVirtualScreen = 78;
        private const int SmCyVirtualScreen = 79;

        public static bool TryGetIdleMilliseconds(out uint idleMilliseconds)
        {
            NativeMethods.LastInputInfo info = new NativeMethods.LastInputInfo();
            info.Size = (uint)Marshal.SizeOf(info);
            if (!NativeMethods.GetLastInputInfo(ref info))
            {
                idleMilliseconds = 0;
                return false;
            }

            uint now = unchecked((uint)Environment.TickCount);
            idleMilliseconds = now - info.Time;
            return true;
        }

        public static bool TryPulse(out string error)
        {
            NativeMethods.Point cursor;
            if (!NativeMethods.GetCursorPos(out cursor))
            {
                error = "Windows no pudo leer la posición del cursor.";
                return false;
            }

            int virtualLeft = NativeMethods.GetSystemMetrics(SmXVirtualScreen);
            int virtualTop = NativeMethods.GetSystemMetrics(SmYVirtualScreen);
            int virtualWidth = NativeMethods.GetSystemMetrics(SmCxVirtualScreen);
            int virtualHeight = NativeMethods.GetSystemMetrics(SmCyVirtualScreen);
            if (virtualWidth <= 1 || virtualHeight <= 1)
            {
                error = "Windows devolvió dimensiones de escritorio no válidas.";
                return false;
            }

            int virtualRight = virtualLeft + virtualWidth - 1;
            int direction = cursor.X < virtualRight ? 1 : -1;

            NativeMethods.Input[] inputs = new NativeMethods.Input[3];
            inputs[0] = CreateMouseMove(direction);
            inputs[1] = CreateMouseMove(-direction);
            inputs[2] = CreateAbsoluteMove(cursor, virtualLeft, virtualTop, virtualWidth, virtualHeight);

            uint sent = NativeMethods.SendInput(
                (uint)inputs.Length,
                inputs,
                Marshal.SizeOf(typeof(NativeMethods.Input)));

            if (sent != inputs.Length)
            {
                int win32Error = Marshal.GetLastWin32Error();
                error = win32Error == 0
                    ? "Windows bloqueó la entrada solicitada."
                    : new Win32Exception(win32Error).Message;
                return false;
            }

            error = null;
            return true;
        }

        private static NativeMethods.Input CreateMouseMove(int deltaX)
        {
            NativeMethods.Input input = new NativeMethods.Input();
            input.Type = InputMouse;
            input.Union.Mouse = new NativeMethods.MouseInput();
            input.Union.Mouse.Dx = deltaX;
            input.Union.Mouse.Flags = MouseEventMove;
            return input;
        }

        private static NativeMethods.Input CreateAbsoluteMove(
            NativeMethods.Point point,
            int virtualLeft,
            int virtualTop,
            int virtualWidth,
            int virtualHeight)
        {
            double normalizedX = (point.X - virtualLeft) * 65535D / (virtualWidth - 1D);
            double normalizedY = (point.Y - virtualTop) * 65535D / (virtualHeight - 1D);

            NativeMethods.Input input = new NativeMethods.Input();
            input.Type = InputMouse;
            input.Union.Mouse = new NativeMethods.MouseInput();
            input.Union.Mouse.Dx = (int)Math.Round(Math.Max(0D, Math.Min(65535D, normalizedX)));
            input.Union.Mouse.Dy = (int)Math.Round(Math.Max(0D, Math.Min(65535D, normalizedY)));
            input.Union.Mouse.Flags = MouseEventMove | MouseEventAbsolute | MouseEventVirtualDesk;
            return input;
        }
    }
}
