using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;
using Win32;
using Forms = System.Windows.Forms;

namespace MultiClip.UI
{
    static class Log
    {
        public static bool Enabled = true;

        public static string logFile = Path.Combine(Globals.DataDir, "..", "ui.log");

        public static void WriteLine(string message)
        {
            if (Enabled)
            {
                if (File.Exists(logFile) && new FileInfo(logFile).Length > 100 * 1024) // > 100K
                {
                    if (File.Exists(logFile + ".bak"))
                        File.Delete(logFile + ".bak");
                    File.Move(logFile, logFile + ".bak");
                }

                File.AppendAllText(logFile, $"{DateTime.Now.ToString("s")}: {message}{Environment.NewLine}");
            }
        }
    }

    static class WindowdExtensions
    {
        public static T GetParent<T>(this object obj) where T : class
        {
            var control = (obj as DependencyObject);
            if (control != null)
            {
                var parent = VisualTreeHelper.GetParent(control);
                while (parent != null)
                {
                    if (parent is T)
                        return (parent as T);

                    parent = VisualTreeHelper.GetParent(parent);
                }
            }
            return null;
        }

        public static void CentreOnActiveScreen(this Window window, double horizontalShift = 0, double verticalShift = 0)
        {
            var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);

            var ttt = window.Left;
            var eee = window.Top;
            //if (Forms.Screen.AllScreens.Count() > 1)
            {
                window.Left = screen.Bounds.X + ((screen.Bounds.Width - window.ActualWidth) / 2) + horizontalShift;
                window.Top = screen.Bounds.Y + ((screen.Bounds.Height - window.ActualHeight) / 2) + verticalShift;
            }
        }

        static public IntPtr GetSafeHandle(this Window window) => new WindowInteropHelper(window).Handle;

        [Flags]
        public enum ExtendedWindowStyles
        {
            WS_EX_TOOLWINDOW = 0x00000080,
        }

        public enum GetWindowLongFields
        {
            GWL_EXSTYLE = (-20),
        }

        /// <summary>
        /// Hides the window from taskbar and app switch (Alt+Tab).
        /// Based on: http://stackoverflow.com/questions/357076/best-way-to-hide-a-window-from-the-alt-tab-program-switcher/551847#551847
        /// </summary>
        /// <param name="wnd">The WND.</param>
        public static void MakeInvisible(this Window wnd)
        {
            var wndHelper = new WindowInteropHelper(wnd);

            int exStyle = (int)GetWindowLong(wndHelper.Handle, (int)GetWindowLongFields.GWL_EXSTYLE);

            exStyle |= (int)ExtendedWindowStyles.WS_EX_TOOLWINDOW;
            SetWindowLong(wndHelper.Handle, (int)GetWindowLongFields.GWL_EXSTYLE, (IntPtr)exStyle);
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

        public static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            int error = 0;
            IntPtr result = IntPtr.Zero;
            // Win32 SetWindowLong doesn't clear error on success
            SetLastError(0);

            if (IntPtr.Size == 4)
            {
                // use SetWindowLong
                Int32 tempResult = IntSetWindowLong(hWnd, nIndex, IntPtrToInt32(dwNewLong));
                error = Marshal.GetLastWin32Error();
                result = new IntPtr(tempResult);
            }
            else
            {
                // use SetWindowLongPtr
                result = IntSetWindowLongPtr(hWnd, nIndex, dwNewLong);
                error = Marshal.GetLastWin32Error();
            }

            if ((result == IntPtr.Zero) && (error != 0))
            {
                throw new System.ComponentModel.Win32Exception(error);
            }

            return result;
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr IntSetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern Int32 IntSetWindowLong(IntPtr hWnd, int nIndex, Int32 dwNewLong);

        private static int IntPtrToInt32(IntPtr intPtr)
        {
            return unchecked((int)intPtr.ToInt64());
        }

        [DllImport("kernel32.dll", EntryPoint = "SetLastError")]
        public static extern void SetLastError(int dwErrorCode);
    }

    internal static class GenericExtensions
    {
        public static string GetPath(this Environment.SpecialFolder specialFolder)
            => Environment.GetFolderPath(specialFolder);
    }

    internal static class StringExtensions
    {
        public static Forms.Keys ToKeys(this string keyValue)
        {
            if (keyValue.Length == 1)
                keyValue = keyValue.ToUpper();
            return (Forms.Keys)Enum.Parse(typeof(Forms.Keys), keyValue);
        }

        public static Modifiers ToModifiers(this IEnumerable<string> keyValues)
        {
            Modifiers modifiers = 0;
            foreach (string m in keyValues)
                modifiers |= (Modifiers)Enum.Parse(typeof(Modifiers), m);
            return modifiers;
        }
    }

    internal static class Operations
    {
        static public void SetClipboardTo(string bufferLocation)
        {
            ClipboardMonitor.LoadSnapshot(bufferLocation);
        }

        static public void ConfigAppStartup(bool start)
        {
            try
            {
                var shortcutFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "MultiClip.lnk");

                if (start)
                {
                    CreateShortcut(shortcutFile, Assembly.GetEntryAssembly().Location);
                }
                else
                {
                    if (File.Exists(shortcutFile))
                        File.Delete(shortcutFile);
                }
            }
            catch { } //doesn't matter why we failed, just ignore and continue
        }

        public static void MsgBox(string message, string caption)
        {
            if (Environment.GetEnvironmentVariable("UNDER_CHOCO").IsEmpty())
                MessageBox.Show(message, caption);
        }

        private static string CreateShortcut(string destFile, string appPath, string args = null)
        {
            // Debug.Assert(destFile.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase));

            dynamic shell = null;
            dynamic lnk = null;

            try
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);

                shell = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8")));
                lnk = shell.CreateShortcut(destFile);
                lnk.WorkingDirectory = Path.GetDirectoryName(appPath);
                lnk.TargetPath = appPath;
                lnk.Arguments = args;
                lnk.Save();

                return destFile;
            }
            finally
            {
                if (lnk != null) Marshal.FinalReleaseComObject(lnk);
                if (shell != null) Marshal.FinalReleaseComObject(shell);
            }
        }
    }

    [ValueConversion(typeof(ClipboardView.ViewFormat), typeof(string))]
    public class FormatToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ClipboardView.ViewFormat)
            {
                string icon = null;
                string color = null;

                switch ((ClipboardView.ViewFormat)value)
                {
                    case ClipboardView.ViewFormat.Custom:
                        {
                            icon = "M342,0L684,342 342,684 0,342z";
                            color = "#FF68217A";
                            break;
                        }
                    case ClipboardView.ViewFormat.PlainText:
                        {
                            icon = "M0,57.653999L67.001999,57.653999 67.001999,64 0,64z M0,42.504999L67.001999,42.504999 67.001999,48.849998 0,48.849998z M0,28.292999L67.001999,28.292999 67.001999,34.638 0,34.638z M0,14.143L67.001999,14.143 67.001999,20.487999 0,20.487999z M0,0L67.001999,0 67.001999,6.3449993 0,6.3449993z";
                            color = "#FF0D0E0D";
                            break;
                        }
                    case ClipboardView.ViewFormat.UnicodeText:
                        {
                            icon = "M10.00997,48.16L38.009968,48.16 38.009968,52.212002 10.00997,52.212002z M19.459968,38.040001L38.014969,38.040001 38.014969,42.088001 19.459968,42.088001z M6.7500123,5.3901367C6.0100211,5.3901367,5.4000343,6,5.4000338,6.7402344L5.4000338,57.25C5.4000343,58,6.0100211,58.600098,6.7500123,58.600098L44.529987,58.600098C45.279987,58.600098,45.890098,58,45.890098,57.25L45.890098,24.42041 35.820011,24.380371C27.22002,25.060059,26.99004,17.260254,26.99004,17.260254L26.99004,5.3901367z M6.7500123,0L29.480034,0 34.400085,5.1904297 34.470032,5.1904297 46.370079,17.810059 46.380089,17.830078 51.279999,23 51.279999,57.25C51.279999,60.97998,48.260097,64,44.529987,64L6.7500123,64C3.0299131,64,3.9343649E-07,60.97998,0,57.25L0,6.7402344C3.9343649E-07,3.0200195,3.0299131,0,6.7500123,0z";
                            color = "#FF004D00";
                            break;
                        }
                    case ClipboardView.ViewFormat.Files:
                        {
                            icon = "M0,5.4450354L21,5.4450354 19.666702,18.667036 1.3333017,18.667036z M2.1115479,0L10.499994,0 10.499994,1.4449959 20.111004,1.4449959 20.111004,2.8889923 0.4449985,2.8889923 0.4449985,1.4449959 2.1115479,1.4449959z";
                            color = "#FFB4966A";
                            break;
                        }
                    case ClipboardView.ViewFormat.Image:
                        {
                            icon = "M19.127866,25.310001L23.765831,30.044474 26.567812,28.49755 37.869933,39.801126 45.692677,35.840065 56.513001,41.153945 56.513001,51.201002 7.2449999,51.201002 7.2449999,36.998981z M41.072999,23.409999L42.377999,23.409999 41.992444,25.68 41.461236,25.68z M46.198105,21.301L47.529,23.179733 47.154015,23.555999 45.275998,22.224118z M37.4299,21.301L38.352,22.224118 36.473973,23.555999 36.099,23.179733z M47.239998,17.146999L49.510998,17.533812 49.510998,18.065188 47.239998,18.451999z M36.227,17.146999L36.227,18.451999 33.956,18.065188 33.956,17.533812z M41.635785,13.912C43.771012,13.912 45.5,15.640979 45.5,17.77481 45.5,19.90871 43.771012,21.639 41.635785,21.638999 39.503125,21.639 37.773999,19.90871 37.773999,17.77481 37.773999,15.640979 39.503125,13.912 41.635785,13.912z M47.008332,12.172L47.385998,12.547014 46.05272,14.426 45.131001,13.504083z M36.328124,12.172L38.209,13.504083 37.286812,14.426 35.953,12.547014z M41.492733,10.047L42.023705,10.047 42.409001,12.318 41.105,12.318z M4.445323,4.7018394L4.445323,53.5182 58.929883,53.5182 58.929883,4.7018394z M0,0L63.758,0 63.758,63.999998 0,63.999998z";
                            color = "#FF1979CA";
                            break;
                        }
                }

                if (parameter != null && parameter.Equals("color"))
                    return color;
                else
                    return icon;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(object), typeof(Visibility))]
    public class ObjectToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = (value != null);
            if (string.Compare("reverse", parameter as string) == 0)
                isVisible = !isVisible;

            if (isVisible)
                return Visibility.Visible;
            else
                return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(DateTime), typeof(string))]
    public class TimestampToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime)
            {
                var age = (DateTime.Now - (DateTime)value).Duration().TotalMinutes;

                if (age > 20)
                    return Colors.Black.ToString();
                else if (age > 17)
                    return Colors.Violet.ToString();
                else if (age > 14)
                    return Colors.Blue.ToString();
                else if (age > 11)
                    return Colors.Aqua.ToString();
                else if (age > 8)
                    return Colors.Green.ToString();
                else if (age > 5)
                    return Colors.Yellow.ToString();
                else if (age > 2)
                    return Colors.Orange.ToString();
                else if (age > 1)
                    return Colors.OrangeRed.ToString();
                else
                    return Colors.Red.ToString();
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public static class BinaryStructConverter
    {
        public static T MarshalTo<T>(this byte[] bytes) where T : struct
        {
            IntPtr ptr = IntPtr.Zero;
            try
            {
                int size = Marshal.SizeOf(typeof(T));
                ptr = Marshal.AllocHGlobal(size);
                Marshal.Copy(bytes, 0, ptr, size);
                object obj = Marshal.PtrToStructure(ptr, typeof(T));
                return (T)obj;
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }

        public static byte[] UnmarshalTo<T>(this T obj) where T : struct
        {
            IntPtr ptr = IntPtr.Zero;
            try
            {
                int size = Marshal.SizeOf(typeof(T));
                ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(obj, ptr, true);
                byte[] bytes = new byte[size];
                Marshal.Copy(ptr, bytes, 0, size);
                return bytes;
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }
    }

    public static class Serializer
    {
        public static void Serialize<T>(this T obj, string file)
        {
            var dir = Path.GetDirectoryName(file);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (var writer = new StreamWriter(file))
            {
                var serializer = new XmlSerializer(typeof(T));
                serializer.Serialize(writer, obj);
            }
        }

        public static T Deserialize<T>(string file)
        {
            using (var reader = new StreamReader(file))
            {
                var serializer = new XmlSerializer(typeof(T));
                return (T)serializer.Deserialize(reader);
            }
        }
    }
}