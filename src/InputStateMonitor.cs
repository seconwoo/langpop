using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanguageIndicator
{
    /// <summary>Snapshot of the foreground input state.</summary>
    internal struct InputState
    {
        public int LangId;
        public bool Native;   // IME is open and in native (e.g. Chinese) conversion mode
        public bool Caps;

        public bool SameAs(InputState o)
        {
            return LangId == o.LangId && Native == o.Native && Caps == o.Caps;
        }

        public string Glyph
        {
            get
            {
                int primary = LangId & 0x3FF;
                switch (primary)
                {
                    case 0x04: return Native ? "中" : "英";   // Chinese
                    case 0x11: return Native ? "あ" : "A";    // Japanese
                    case 0x12: return Native ? "한" : "A";    // Korean
                    case 0x09: return "EN";
                }
                return Culture != null ? Culture.TwoLetterISOLanguageName.ToUpperInvariant() : "??";
            }
        }

        public string Caption
        {
            get
            {
                string text;
                int primary = LangId & 0x3FF;
                if (primary == 0x04) text = Native ? "中文" : "英文模式";
                else if (primary == 0x09) text = "English";
                else text = Culture != null ? Culture.NativeName : "Unknown";
                return Caps ? text + "  ·  CAPS" : text;
            }
        }

        /// <summary>True when the layout is a non-Latin language actively producing native text.</summary>
        public bool IsAccent
        {
            get
            {
                int primary = LangId & 0x3FF;
                if (primary == 0x04 || primary == 0x11 || primary == 0x12) return Native;
                return primary != 0x09;
            }
        }

        private CultureInfo Culture
        {
            get
            {
                try { return new CultureInfo(LangId); } catch { return null; }
            }
        }
    }

    /// <summary>
    /// Watches the foreground window's keyboard layout and IME conversion mode.
    /// Polls periodically and also re-checks right after modifier/switch keys are released.
    /// </summary>
    internal sealed class InputStateMonitor : IDisposable
    {
        public event Action<InputState, IntPtr> StateChanged;
        public InputState Current { get { return _state; } }

        private readonly Timer _poll = new Timer { Interval = 120 };
        private readonly Timer _kick = new Timer { Interval = 50 };
        private readonly Native.LowLevelKeyboardProc _hookProc; // kept alive to avoid GC
        private IntPtr _hook;
        private InputState _state;
        private IntPtr _lastForeground;
        private int _lastKeyTick;
        private bool _initialized;

        public InputStateMonitor()
        {
            _hookProc = HookCallback;
            _poll.Tick += delegate { Check(); };
            _kick.Tick += delegate { _kick.Stop(); Check(); };
        }

        public void Start()
        {
            _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _hookProc, Native.GetModuleHandle(null), 0);
            Check();
            _poll.Start();
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP)
                {
                    var kb = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                    if (IsSwitchKey(kb.vkCode))
                    {
                        _lastKeyTick = Environment.TickCount;
                        _kick.Stop();
                        _kick.Start();
                    }
                }
            }
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private static bool IsSwitchKey(int vk)
        {
            switch (vk)
            {
                case 0x10: case 0xA0: case 0xA1: // Shift
                case 0x11: case 0xA2: case 0xA3: // Ctrl
                case 0x12: case 0xA4: case 0xA5: // Alt
                case 0x5B: case 0x5C:            // Win
                case 0x20:                       // Space
                case 0x14:                       // Caps Lock
                case 0x15: case 0x19:            // Kana/Hangul, Kanji/Hanja
                    return true;
            }
            return false;
        }

        private void Check()
        {
            IntPtr fg = Native.GetForegroundWindow();
            if (fg == IntPtr.Zero) return;

            uint pid;
            uint tid = Native.GetWindowThreadProcessId(fg, out pid);
            var s = new InputState();
            s.LangId = (int)(Native.GetKeyboardLayout(tid).ToInt64() & 0xFFFF);
            s.Caps = (Native.GetKeyState(Native.VK_CAPITAL) & 1) != 0;

            int primary = s.LangId & 0x3FF;
            if (primary == 0x04 || primary == 0x11 || primary == 0x12)
                s.Native = QueryImeNative(fg);

            bool foregroundChanged = fg != _lastForeground;
            _lastForeground = fg;

            if (_initialized && s.SameAs(_state)) return;

            bool first = !_initialized;
            _state = s;
            _initialized = true;
            if (first) return;

            // Switching windows can change the state too; only announce that when the user just
            // pressed a switch key (e.g. Win+Space whose flyout briefly takes focus).
            bool recentKey = unchecked(Environment.TickCount - _lastKeyTick) < 1500;
            if (foregroundChanged && !recentKey) return;

            var handler = StateChanged;
            if (handler != null) handler(s, fg);
        }

        private static bool QueryImeNative(IntPtr hwnd)
        {
            IntPtr ime = Native.ImmGetDefaultIMEWnd(hwnd);
            if (ime == IntPtr.Zero) return false;

            IntPtr open, mode;
            if (Native.SendMessageTimeout(ime, Native.WM_IME_CONTROL, (IntPtr)Native.IMC_GETOPENSTATUS, IntPtr.Zero,
                    Native.SMTO_ABORTIFHUNG, 50, out open) == IntPtr.Zero)
                return false;
            if (open == IntPtr.Zero) return false;

            if (Native.SendMessageTimeout(ime, Native.WM_IME_CONTROL, (IntPtr)Native.IMC_GETCONVERSIONMODE, IntPtr.Zero,
                    Native.SMTO_ABORTIFHUNG, 50, out mode) == IntPtr.Zero)
                return false;
            return (mode.ToInt64() & Native.IME_CMODE_NATIVE) != 0;
        }

        public void Dispose()
        {
            _poll.Dispose();
            _kick.Dispose();
            if (_hook != IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }
    }
}
