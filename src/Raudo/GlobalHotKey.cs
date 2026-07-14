using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Raudo
{
    [Flags]
    internal enum HotKeyModifiers : uint
    {
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        NoRepeat = 0x4000
    }

    internal sealed class GlobalHotKey : NativeWindow, IDisposable
    {
        private const int HotKeyMessage = 0x0312;
        private const int HotKeyId = 0x5241;
        private bool disposed;

        public GlobalHotKey(HotKeyModifiers modifiers, Keys key)
        {
            CreateParams parameters = new CreateParams();
            parameters.Caption = "Raudo Hotkey";
            parameters.Parent = new IntPtr(-3);
            CreateHandle(parameters);

            IsRegistered = NativeMethods.RegisterHotKey(
                Handle,
                HotKeyId,
                (uint)modifiers,
                (uint)key);
            if (!IsRegistered)
            {
                RegistrationError = Marshal.GetLastWin32Error();
            }
        }

        public event EventHandler Pressed;

        public bool IsRegistered { get; private set; }
        public int RegistrationError { get; private set; }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == HotKeyMessage && message.WParam.ToInt32() == HotKeyId)
            {
                EventHandler handler = Pressed;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }

            base.WndProc(ref message);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (IsRegistered)
            {
                NativeMethods.UnregisterHotKey(Handle, HotKeyId);
                IsRegistered = false;
            }

            DestroyHandle();
        }
    }
}
