using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace LangPop
{
    /// <summary>Finds the on-screen text caret of the foreground window, falling back to the mouse.</summary>
    internal static class CaretLocator
    {
        /// <summary>Returns the caret rectangle in screen pixels, or a 0x0 rect at the mouse position.</summary>
        public static Rectangle Locate(IntPtr foreground, out bool isCaret)
        {
            isCaret = true;
            uint pid;
            uint tid = Native.GetWindowThreadProcessId(foreground, out pid);

            var info = new Native.GUITHREADINFO();
            info.cbSize = Marshal.SizeOf(typeof(Native.GUITHREADINFO));
            if (Native.GetGUIThreadInfo(tid, ref info))
            {
                // Classic Win32 caret (Notepad, Office, most edit controls).
                if (info.hwndCaret != IntPtr.Zero)
                {
                    var tl = new Native.POINT(info.rcCaret.Left, info.rcCaret.Top);
                    if (Native.ClientToScreen(info.hwndCaret, ref tl))
                    {
                        int h = Math.Max(1, info.rcCaret.Bottom - info.rcCaret.Top);
                        if (tl.X != 0 || tl.Y != 0)
                            return new Rectangle(tl.X, tl.Y, 1, h);
                    }
                }

                // Accessibility caret (Chromium, Electron, WPF, UWP ...).
                IntPtr target = info.hwndFocus != IntPtr.Zero ? info.hwndFocus : foreground;
                Rectangle r;
                if (TryAccessibleCaret(target, out r)) return r;
            }

            isCaret = false;
            Native.POINT p;
            Native.GetCursorPos(out p);
            return new Rectangle(p.X, p.Y, 0, 0);
        }

        private static bool TryAccessibleCaret(IntPtr hwnd, out Rectangle rect)
        {
            rect = Rectangle.Empty;
            object obj = null;
            try
            {
                if (Native.AccessibleObjectFromWindow(hwnd, Native.OBJID_CARET, ref Native.IID_IAccessible, out obj) != 0
                    || obj == null)
                    return false;
                var acc = obj as Accessibility.IAccessible;
                if (acc == null) return false;
                int l, t, w, h;
                acc.accLocation(out l, out t, out w, out h, 0);
                if (l == 0 && t == 0 && w == 0 && h == 0) return false;
                rect = new Rectangle(l, t, Math.Max(1, w), Math.Max(1, h));
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (obj != null && Marshal.IsComObject(obj)) Marshal.ReleaseComObject(obj);
            }
        }
    }
}
