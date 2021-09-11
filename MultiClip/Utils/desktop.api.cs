//css_dbg /t:winexe;
using System;
using System.Drawing;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows;
using System.Reflection;

class Script
{
    [STAThread]
    static public void main(string[] args)
    {
        while (true)
        {
            Desktop.FireKeyInput(Keys.F15, Keys.None);
            Console.WriteLine("fired...");
            Thread.Sleep(1000 * 60 * 5);
        }
    }
}

namespace System.Windows
{
    public class Desktop
    {
        static Desktop()
        {
            AppDomain.CurrentDomain.AssemblyResolve +=
                (sender, args) =>
                {
                    foreach (var dir in Desktop.ProbingDirectories)
                    {
                        var candidate = Path.Combine(dir, args.Name.Split(',')[0] + ".dll");

                        if (File.Exists(candidate))
                            try
                            {
                                return Assembly.LoadFrom(candidate);
                            }
                            catch { }
                    }
                    return null;
                };
        }

        public static IntPtr SetFocus(IntPtr wnd)
        {
            return Win32.SetFocus(wnd);
        }

        public static void SetForegroundWindow(IntPtr wnd)
        {
            Win32.SetForegroundWindow(wnd);
        }

        public static IntPtr GetFocus()
        {
            return Win32.GetFocus();
        }

        public static IntPtr GetActive()
        {
            return Win32.GetActiveWindow();
        }

        public static IntPtr GetProcessForegroundWindow()
        {
            return Win32.GetForegroundWindow();
        }

        public static IntPtr[] GetDesktopWindows()
        {
            var windows = new List<IntPtr>();
            IntPtr hDesktop = IntPtr.Zero; // current desktop
            bool success = Win32.EnumDesktopWindows(
                hDesktop,
                (hWnd, param) =>
                {
                    windows.Add(hWnd);
                    return true;
                },
                IntPtr.Zero);

            return windows.ToArray();
        }

        public static Process GetWindowProcess(IntPtr wnd)
        {
            uint lpdwProcessId;
            Desktop.Win32.GetWindowThreadProcessId(wnd, out lpdwProcessId);
            return Process.GetProcessById((int)lpdwProcessId);
        }

        public static uint GetWindowThread(IntPtr wnd)
        {
            uint lpdwProcessId;
            return Desktop.Win32.GetWindowThreadProcessId(wnd, out lpdwProcessId);
        }

        public static Point GetCursorPos()
        {
            Win32.POINT p;
            Win32.GetCursorPos(out p);
            return new Point(p.X, p.Y);
        }

        public static void SetCursorPos(int x, int y)
        {
            Win32.SetCursorPos(x, y);
        }

        public static IntPtr GetWindowFromPoint(int x, int y)
        {
            return Win32.WindowFromPoint(new Win32.POINT(x, y));
        }

        public static IntPtr GetWindow(string className, string windowName)
        {
            return Win32.FindWindow(className, windowName);
        }

        public static IntPtr GetChildWindowFromPoint(IntPtr parent, int x, int y)
        {
            System.Drawing.Rectangle r = Desktop.GetWindowRect(parent);
            return GetWindowFromPoint(r.X + x, r.Y + y);
        }

        public static bool IsParentProcess(int parentId, int childId)
        {
            var process = Process.GetProcessById(childId);

            while (process != null)
            {
                for (int i = 0; i < Process.GetProcessesByName(process.ProcessName).Length; i++)
                {
                    var suffix = (i == 0 ? "" : "#" + i);

                    int procID = (int)new PerformanceCounter("Process", "ID Process", process.ProcessName + suffix).NextValue();

                    if (procID == 0) //Idle
                        return false;

                    if (procID == process.Id)
                    {
                        int parent = (int)new PerformanceCounter("Process", "Creating Process ID", process.ProcessName + suffix).NextValue();

                        if (parent == parentId)
                            return true;
                        else
                        {
                            process = null;
                            try
                            {
                                process = Process.GetProcessById(parent);
                            }
                            catch { }
                            break;
                        }
                    }
                }
            }
            return false;
        }

        public static IntPtr GetChild(IntPtr parent, string text)
        {
            IntPtr retval = IntPtr.Zero;

            Win32.EnumChildWindows(parent, delegate (IntPtr hWnd, IntPtr param)
            {
                if (GetWindowText(hWnd) == text)
                {
                    retval = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            return retval;
        }

        public static IntPtr GetParent(IntPtr child)
        {
            return Win32.GetParent(child);
        }

        public static IntPtr GetTopParent(IntPtr child)
        {
            var potentialPartent = Win32.GetParent(child);

            while (potentialPartent != IntPtr.Zero)
            {
                if (Win32.GetParent(child) != IntPtr.Zero)
                    potentialPartent = Win32.GetParent(child);
                else
                    return potentialPartent;
            }
            return IntPtr.Zero;
        }

        public static IntPtr GetThreadTopVisibleWindow(uint threadId)
        {
            foreach (var window in Desktop.GetVisibleWindows().Keys)
            {
                if (threadId == Desktop.GetWindowThread(window))
                {
                    var parent = Desktop.GetTopParent(window);

                    if (parent == IntPtr.Zero)
                        return window;
                    else
                        return parent;
                }
            }
            return IntPtr.Zero;
        }

        public static IntPtr WaitForThreadTopVisibleWindow(uint threadId)
        {
            return WaitForThreadTopVisibleWindow(threadId, -1);
        }

        public static IntPtr WaitForThreadTopVisibleWindow(uint threadId, int timeout)
        {
            var counter = new Stopwatch();
            counter.Start();
            var retval = IntPtr.Zero;

            while ((retval = GetThreadTopVisibleWindow(threadId)) == IntPtr.Zero)
            {
                if (timeout == -1 || counter.ElapsedMilliseconds < timeout)
                    Thread.Sleep(50);
                else
                    break;
            }
            return retval;
        }

        public class AssemblyExecutionInfo
        {
            public uint RawThreadId { get; set; }
            public Thread Thread { get; set; }
            public Process Process { get; set; }
        }

        public static List<string> ProbingDirectories = new List<string>();

        public static AssemblyExecutionInfo StartUIAssemblyLocalExecution(string path, params string[] args)
        {
            var retval = new AssemblyExecutionInfo { Process = Process.GetCurrentProcess() };

            retval.Thread = new Thread(delegate ()
                                        {
                                            retval.RawThreadId = Win32.GetCurrentThreadId();
                                            AppDomain.CurrentDomain.ExecuteAssembly(path, args);
                                        });

            retval.Thread.SetApartmentState(ApartmentState.STA);
            retval.Thread.Start();

            WaitWhile(() => retval.RawThreadId == 0);

            return retval;
        }

        public static void WaitWhile(Func<bool> condition)
        {
            while (condition())
                Thread.Sleep(50);
        }

        public static IntPtr GetTopMostParent(IntPtr child)
        {
            IntPtr retval = GetParent(child);

            while (GetParent(retval) != IntPtr.Zero)
                retval = GetParent(retval);

            return retval;
        }

        public static string GetWindowText(IntPtr wnd)
        {
            int length = Win32.GetWindowTextLength(wnd);
            StringBuilder sb = new StringBuilder(length + 1);
            Win32.GetWindowText(wnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static void SetWindowText(IntPtr wnd, string text)
        {
            Win32.SetWindowText(wnd, text);
        }

        public static System.Drawing.Rectangle GetWindowRect(IntPtr wnd)
        {
            Win32.RECT r;
            Win32.GetWindowRect(wnd, out r);
            return r;
        }

        public static void FireMouseClick(int x, int y)
        {
            Win32.SetCursorPos(x, y);
            Win32.mouse_event((uint)Win32.MouseEventFlags.LEFTDOWN, 0, 0, 0, 0);
            Win32.mouse_event((uint)Win32.MouseEventFlags.LEFTUP, 0, 0, 0, 0);
        }

        public static void FireMouseDoubluClick(int x, int y)
        {
            Win32.SetCursorPos(x, y);
            Win32.mouse_event((uint)Win32.MouseEventFlags.LEFTDOWN, 0, 0, 0, 0);
            Win32.mouse_event((uint)Win32.MouseEventFlags.LEFTUP, 0, 0, 0, 0);
            Win32.mouse_event((uint)Win32.MouseEventFlags.LEFTDOWN, 0, 0, 0, 0);
            Win32.mouse_event((uint)Win32.MouseEventFlags.LEFTUP, 0, 0, 0, 0);
        }

        public static void FireKeyInput(Keys key, params Keys[] modifiers)
        {
            foreach (Keys k in modifiers)
                Win32.keybd_event((byte)k, 0x45, 0, UIntPtr.Zero);

            Win32.keybd_event((byte)key, 0x45, 0, UIntPtr.Zero);

            Thread.Sleep(10);

            Win32.keybd_event((byte)key, 0x45, Win32.KEYEVENTF_KEYUP, UIntPtr.Zero);

            foreach (Keys k in modifiers)
                Win32.keybd_event((byte)k, 0x45, Win32.KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static void FireKeyInput(string text)
        {
            foreach (char c in text.ToCharArray())
            {
                string chStr = c.ToString();
                bool upper = (chStr == chStr.ToUpper());

                if (chStr == chStr.ToLower())
                    upper = false;

                char ch = chStr.ToCharArray()[0];
                short vk = Win32.VkKeyScan(ch);

                Keys key = (Keys)Enum.Parse(typeof(Keys), Win32.VkKeyScan(ch).ToString());

                if (chStr == ":")
                {
                    FireKeyInput(key, Keys.LShiftKey);
                }
                else
                {
                    FireKeyInput(key, upper ? Keys.LShiftKey : Keys.None);
                }
            }
        }

        public static Dictionary<IntPtr, string> GetVisibleWindows()
        {
            Dictionary<IntPtr, string> retval = new Dictionary<IntPtr, string>();

            // Get the desktopwindow handle
            IntPtr nDeshWndHandle = Win32.GetDesktopWindow();

            IntPtr nChildHandle = Win32.GetWindow(nDeshWndHandle, Win32.GW_CHILD);

            while (nChildHandle != IntPtr.Zero)
            {
                if (Win32.IsWindowVisible(nChildHandle) != 0)
                {
                    StringBuilder sbTitle = new StringBuilder(1024);

                    Win32.GetWindowText(nChildHandle, sbTitle, sbTitle.Capacity);
                    String sWinTitle = sbTitle.ToString();

                    if (sWinTitle.Length > 0)
                    {
                        retval.Add(nChildHandle, sWinTitle);
                    }
                }
                nChildHandle = Win32.GetWindow(nChildHandle, Win32.GW_HWNDNEXT);
            }

            return retval;
        }

        #region WIN32

        internal class Win32
        {
            internal const int KEYEVENTF_KEYUP = 0x02;
            internal const int GW_CHILD = 5;
            internal const int GW_HWNDNEXT = 2;

            [StructLayout(LayoutKind.Sequential)]
            internal struct POINT
            {
                public int X;
                public int Y;
                public POINT(int x, int y)
                {
                    this.X = x;
                    this.Y = y;
                }
                public static implicit operator System.Drawing.Point(POINT p)
                {
                    return new System.Drawing.Point(p.X, p.Y);
                }
                public static implicit operator POINT(System.Drawing.Point p)
                {
                    return new POINT(p.X, p.Y);
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
                public static implicit operator System.Drawing.Rectangle(RECT r)
                {
                    return new System.Drawing.Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
                }
            }

            [Flags]
            public enum MouseEventFlags
            {
                LEFTDOWN = 0x00000002,
                LEFTUP = 0x00000004,
                MIDDLEDOWN = 0x00000020,
                MIDDLEUP = 0x00000040,
                MOVE = 0x00000001,
                ABSOLUTE = 0x00008000,
                RIGHTDOWN = 0x00000008,
                RIGHTUP = 0x00000010
            }

            public struct KEYBDINPUT
            {
                public ushort wVk;
                public ushort wScan;
                public uint dwFlags;
                public long time;
                public uint dwExtraInfo;
            };

            [StructLayout(LayoutKind.Explicit, Size = 28)]
            public struct INPUT
            {
                [FieldOffset(0)]
                public uint type;
                [FieldOffset(4)]
                public KEYBDINPUT ki;
            };

            //SendInput work better than keybd_event when there is a neeed to hande modifiers (e.g. Alt, Ctrl)
            // [DllImport("user32.dll")]
            public static extern uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);

            public static void SendInput(Keys key, bool keyDown)
            {
                INPUT structInput = new INPUT();
                structInput.type = 1;
                structInput.ki.wVk = (ushort)key;
                if (!keyDown)
                    structInput.ki.dwFlags = KEYEVENTF_KEYUP;
                SendInput(1, ref structInput, Marshal.SizeOf(structInput));
            }

            public static void SendInput(char key, bool keyDown)
            {
                INPUT structInput = new INPUT();
                structInput.type = 1;
                structInput.ki.wVk = (ushort)VkKeyScan(key);
                if (!keyDown)
                    structInput.ki.dwFlags = KEYEVENTF_KEYUP;
                SendInput(1, ref structInput, Marshal.SizeOf(structInput));
            }

            // [DllImport("kernel32.dll")]
            public static extern uint GetCurrentThreadId();

            // [DllImport("user32.dll")]
            internal static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

            [DllImport("user32.dll")]
            internal static extern IntPtr SetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            internal static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll")]
            internal static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

            [DllImport("user32.dll")]
            internal static extern IntPtr SetFocus(IntPtr hWnd);

            [DllImport("user32.dll")]
            internal static extern IntPtr GetFocus();

            [DllImport("user32.dll")]
            internal static extern IntPtr GetActiveWindow();

            [DllImport("user32.dll")]
            internal static extern IntPtr WindowFromPoint(POINT Point);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

            [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            internal static extern int GetWindowTextLength(IntPtr hWnd);

            [DllImport("user32.dll")]
            internal static extern bool SetWindowText(IntPtr hWnd, string lpString);

            [DllImport("user32.dll")]
            internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

            [DllImport("user32.dll")]
            internal static extern bool SetCursorPos(int X, int Y);

            [DllImport("user32.dll")]
            internal static extern bool GetCursorPos(out POINT lpPoint);

            [DllImport("user32.dll")]
            internal static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

            [DllImport("user32.dll")]
            internal static extern short VkKeyScan(char ch);

            [DllImport("user32")]
            public static extern IntPtr GetParent(IntPtr hwnd);

            [DllImport("user32")]
            public static extern IntPtr GetWindow(IntPtr hwnd, int wCmd);

            [DllImport("user32")]
            public static extern int IsWindowVisible(IntPtr hwnd);

            [DllImport("user32")]
            public static extern IntPtr GetDesktopWindow();

            public delegate bool EnumWindowProc(IntPtr hWnd, IntPtr parameter);

            [DllImport("user32")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool EnumChildWindows(IntPtr window, EnumWindowProc callback, IntPtr i);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

            [DllImport("user32.dll")]
            public static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumWindowProc lpfn, IntPtr lParam);
        }

        #endregion WIN32
    }
}