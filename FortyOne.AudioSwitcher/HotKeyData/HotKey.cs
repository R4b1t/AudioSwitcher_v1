using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AudioSwitcher.AudioApi;
using FortyOne.AudioSwitcher.Helpers;

namespace FortyOne.AudioSwitcher.HotKeyData
{
    public class HotKey : IDisposable
    {
        public bool IsRegistered;

        private Guid _deviceId;
        private IDevice _cachedDevice;
        private bool _deviceCacheValid;

        public HotKey()
        {
            Modifiers = Modifiers.None;
            Key = Keys.None;
        }

        public Guid DeviceId
        {
            get { return _deviceId; }
            set
            {
                if (_deviceId == value)
                    return;
                _deviceId = value;
                InvalidateDeviceCache();
            }
        }

        public IDevice Device
        {
            get
            {
                if (_deviceCacheValid)
                    return _cachedDevice;

                try
                {
                    // Include NotPresent so temporarily unavailable devices still resolve
                    _cachedDevice = AudioDeviceManager.Controller.GetDevice(DeviceId, DeviceState.All);
                }
                catch (Exception ex)
                {
                    AppLog.Warn("GetDevice failed for " + DeviceId + ": " + ex.Message);
                    _cachedDevice = null;
                }

                _deviceCacheValid = true;
                return _cachedDevice;
            }
        }

        public void InvalidateDeviceCache()
        {
            _deviceCacheValid = false;
            _cachedDevice = null;
        }

        public string DeviceName
        {
            get
            {
                var device = Device;
                if (device == null)
                    return "Unknown Device";
                return device.FullName;
            }
        }

        public string HotKeyString
        {
            get
            {
                var keystring = "";
                if ((Modifiers & Modifiers.Alt) > 0)
                    keystring += "Alt+";
                if ((Modifiers & Modifiers.Control) > 0)
                    keystring += "Ctrl+";
                if ((Modifiers & Modifiers.Shift) > 0)
                    keystring += "Shift+";
                if ((Modifiers & Modifiers.Win) > 0)
                    keystring += "Win+";
                keystring += Key.ToString();
                return keystring;
            }
        }

        private HotKeyNativeWindow HotKeyWindow { get; set; }

        public Modifiers Modifiers { get; set; }

        public Keys Key { get; set; }

        public void Dispose()
        {
            if (HotKeyWindow != null)
                HotKeyWindow.UnregisterHotkey();
        }

        public event EventHandler HotKeyPressed;

        public bool RegisterHotkey()
        {
            if (HotKeyWindow == null)
                HotKeyWindow = new HotKeyNativeWindow(this);

            try
            {
                if (Key != Keys.None)
                {
                    HotKeyWindow.RegisterHotkey();
                }
                else
                {
                    if (HotKeyWindow.Handle != IntPtr.Zero)
                        HotKeyWindow.DestroyHandle();
                    HotKeyWindow = null;
                }

                IsRegistered = true;
            }
            catch (Exception ex)
            {
                AppLog.Warn("Hotkey registration failed (" + HotKeyString + "): " + ex.Message);
                if (HotKeyWindow != null && HotKeyWindow.Handle != IntPtr.Zero)
                    HotKeyWindow.DestroyHandle();
                HotKeyWindow = null;
                IsRegistered = false;
            }

            return IsRegistered;
        }

        public void RegisterHotkey(Modifiers modifiers, Keys key)
        {
            Modifiers = modifiers;
            Key = key;
            RegisterHotkey();
        }

        public void UnregisterHotkey()
        {
            if (IsRegistered && HotKeyWindow != null)
                HotKeyWindow.UnregisterHotkey();

            IsRegistered = false;
        }

        public void ActivateWindow(IntPtr hWnd)
        {
            var hForeground = NativeMethods.GetForegroundWindow();
            if (hWnd != hForeground)
            {
                var hForegroundThread = NativeMethods.GetWindowThreadProcessId(hForeground, IntPtr.Zero);
                var hCurrentThread = NativeMethods.GetWindowThreadProcessId(hWnd, IntPtr.Zero);

                if (hForegroundThread != hCurrentThread)
                {
                    NativeMethods.AttachThreadInput(hForegroundThread, hCurrentThread, true);
                    NativeMethods.SetForegroundWindow(hWnd);
                    NativeMethods.AttachThreadInput(hForegroundThread, hCurrentThread, false);
                }
                else
                {
                    NativeMethods.SetForegroundWindow(hWnd);
                }

                if (NativeMethods.IsIconic(hWnd))
                    NativeMethods.ShowWindow(hWnd, NativeMethods.ShowWindowCommand.SW_RESTORE);
                else
                    NativeMethods.ShowWindow(hWnd, NativeMethods.ShowWindowCommand.SW_SHOW);
            }
        }

        protected virtual void OnHotKey()
        {
            if (HotKeyPressed != null)
                HotKeyPressed(this, new EventArgs());
        }

        private class HotKeyNativeWindow : NativeWindow
        {
            private const int WM_HOTKEY = 0x312;
            private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

            public HotKeyNativeWindow(HotKey owner)
            {
                Owner = owner;
            }

            private HotKey Owner { get; set; }
            private short HotKeyID { get; set; }

            public IntPtr WindowHandle
            {
                get { return Handle; }
            }

            ~HotKeyNativeWindow()
            {
                try
                {
                    UnregisterHotkey();
                }
                catch
                {
                }
            }

            public override void DestroyHandle()
            {
                UnregisterHotkey();
                base.DestroyHandle();
            }

            public override void ReleaseHandle()
            {
                UnregisterHotkey();
                base.ReleaseHandle();
            }

            public void RegisterHotkey()
            {
                if (HandleCreated() && Owner.Key != Keys.None)
                {
                    if (HotKeyID == 0)
                        HotKeyID = NativeMethods.GlobalAddAtom(Guid.NewGuid().ToString("N"));

                    if (!NativeMethods.RegisterHotKey(Handle, HotKeyID, (int)Owner.Modifiers, (int)Owner.Key))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }

            public void UnregisterHotkey()
            {
                if (Handle != IntPtr.Zero && HotKeyID != 0)
                {
                    NativeMethods.UnregisterHotKey(Handle, HotKeyID);
                    NativeMethods.GlobalDeleteAtom(HotKeyID);
                    HotKeyID = 0;
                }
            }

            private bool HandleCreated()
            {
                if (Handle == IntPtr.Zero)
                {
                    var createParams = new CreateParams
                    {
                        Caption = Guid.NewGuid().ToString("N"),
                        Style = 0,
                        ExStyle = 0,
                        ClassStyle = 0
                    };
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                        createParams.Parent = HWND_MESSAGE;
                    CreateHandle(createParams);
                }
                return Handle != IntPtr.Zero;
            }

            protected override void WndProc(ref Message m)
            {
                // Fire the switch only — do not unregister/re-simulate the keystroke.
                // That dance added latency and could leave hotkeys permanently unregistered.
                if (m.Msg == WM_HOTKEY)
                    Owner.OnHotKey();

                base.WndProc(ref m);
            }
        }
    }
}
