using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using NClipboard = System.Windows.Forms.Clipboard;

namespace Win32
{
    public class Desktop
    {
        [DllImport("user32.dll")]
        public static extern IntPtr SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
    }

    public class Clipboard
    {
        [DllImport("user32.dll")]
        static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        static extern bool EmptyClipboard();

        [DllImport("user32.dll")]
        static extern uint EnumClipboardFormats(uint format);

        [DllImport("user32.dll")]
        static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll")]
        static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll")]
        static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll")]
        static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        static extern IntPtr GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        static extern IntPtr GlobalFree(IntPtr hMem);

        [DllImport("kernel32.dll")]
        static extern UIntPtr GlobalSize(IntPtr hMem);

        const uint GMEM_DDESHARE = 0x2000;
        const uint GMEM_MOVEABLE = 0x2;

        public static void WithFormats(Action<uint> handler)
        {
            uint num = 0u;
            while ((num = EnumClipboardFormats(num)) != 0)
            {
                handler(num);
            }
        }

        public class LastSessionErrorDetectedException : Exception
        {
            public LastSessionErrorDetectedException()
            {
            }

            public LastSessionErrorDetectedException(string message) : base(message)
            {
            }
        }

        // UI will use this static class for browsing history and `GetExecutingAssembly` may return invalid path (GAC)
        // but because server is always invoked as a process then it is safe to use `GetEntryAssembly`.
        public static string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MultiClip.History", "Data");

        static string ErrorLog = Path.Combine(DataDir, @"..\reading.log");

        static void BackupLog(string logFile)
        {
            if (File.Exists(logFile))
            {
                bool hasReadingErrors = File.ReadAllLines(logFile)
                                            .Any(x => x.EndsWith("started -> ")
                                                 || x.EndsWith("Error"));

                if (hasReadingErrors)
                {
                    PurgeErrors(logFile);

                    var lastErrorLog = logFile + Guid.NewGuid() + ".error.log";
                    File.Move(logFile, lastErrorLog);

                    throw new LastSessionErrorDetectedException(lastErrorLog);
                }

                if (!ErrorLogIsPurged)
                {
                    ErrorLogIsPurged = true;
                    PurgeErrors(logFile);
                }
            }
        }

        static void PurgeErrors(string logFile)
        {
            try
            {
                foreach (var oldLog in Directory
                    .GetFiles(Path.GetDirectoryName(logFile), "*.error.log")
                    .OrderByDescending(x => x)
                    .Skip(10))
                {
                    try
                    {
                        File.Delete(oldLog);
                    }
                    catch { }
                }
            }
            catch { }
        }

        static bool ErrorLogIsPurged = false;

        static uint[] ignoreCipboardFormats = new uint[]
        {
            49466, // (InShellDragLoop)
            50417, // (PowerPoint 12.0 Internal Theme)
            50418, // (PowerPoint 12.0 Internal Color Scheme)
            50416, // (Art::Text ClipFormat)
            50378, // (Art::Table ClipFormat)
            49171, // (Ole Private Data) // not sure if not having this one is 100% acceptable
            14,    // (EnhancedMetafile)
            3,     // CF_METAFILEPICT - upsets Excel
        };

        public static Dictionary<uint, byte[]> GetClipboard()
        {
            BackupLog(ErrorLog);

            using (var readingLog = new StreamWriter(ErrorLog))
            {
                readingLog.WriteLine(ErrorLog);

                var result = new Dictionary<uint, byte[]>();
                try
                {
                    if (OpenClipboard(ClipboardWatcher.WindowHandle))
                    {
                        try
                        {
                            WithFormats(delegate (uint format)
                            {
                                // zos
                                // skipping nasty formats as well as delaying the making snapshot (ClipboardHistory.cs:78)
                                // seems to help with unhanded Win32 exceptions
                                if (ignoreCipboardFormats.Contains(format))
                                    return;

                                try
                                {
                                    readingLog.Write($"Reading {format} ({format.ToFormatName()}): started -> ");
                                    readingLog.Flush();
                                    byte[] bytes = GetBytes(format);
                                    if (bytes != null)
                                    {
                                        result[format] = bytes;
                                    }
                                    readingLog.WriteLine("OK");
                                }
                                catch
                                {
                                    readingLog.WriteLine("Error");
                                }
                                try { readingLog.Flush(); } catch { }
                            });
                        }
                        finally
                        {
                            CloseClipboard();
                            try { readingLog.Flush(); } catch { }
                        }
                    }
                }
                catch
                {
                }

                return result;
            }
        }

        public static string[] GetDropFiles(byte[] bytes)
        {
            var result = new List<string>();

            var buf = new StringBuilder(bytes.Length);

            IntPtr mem = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, mem, bytes.Length);

                var count = DragQueryFile(mem, uint.MaxValue, null, 0);
                for (uint i = 0; i < count; i++)
                    if (0 < DragQueryFile(mem, i, buf, buf.Capacity))
                        result.Add(buf.ToString());
            }
            finally
            {
                Marshal.FreeHGlobal(mem);
            }
            return result.ToArray();
        }

        public static byte[] GetBytes(uint format)
        {
            IntPtr pos = IntPtr.Zero;
            try
            {
                pos = GetClipboardData(format);
                if (pos != IntPtr.Zero)
                {
                    IntPtr gLock = GlobalLock(pos);
                    if (gLock == IntPtr.Zero)
                        return null;

                    var length = (int)GlobalSize(pos);
                    if (length > 0)
                    {
                        var buffer = new byte[length];
                        Marshal.Copy(gLock, buffer, 0, length);
                        return buffer;
                    }
                }
                return null;
            }
            finally
            {
                if (pos != IntPtr.Zero)
                    try { GlobalUnlock(pos); }
                    catch { }
            }
        }

        public static IEnumerable<uint> GetFormats()
        {
            var result = new List<uint>();
            uint format = 0;
            while ((format = EnumClipboardFormats(format)) != 0)
                result.Add(format);
            return result;
        }

        static public void SetBytes(uint format, byte[] data)
        {
            if (data.Length > 0)
            {
                IntPtr alloc = GlobalAlloc(GMEM_MOVEABLE | GMEM_DDESHARE, (UIntPtr)data.Length);
                if (alloc != IntPtr.Zero)
                {
                    IntPtr gLock = GlobalLock(alloc);
                    if (gLock != IntPtr.Zero)
                    {
                        Marshal.Copy(data, 0, gLock, data.Length);
                        GlobalUnlock(alloc);

                        SetClipboardData(format, alloc);

                        GlobalFree(alloc);
                    }
                }
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder lpszFile, int cch);

        static public void SetText(string text)
        {
            try
            {
                NClipboard.SetText(text);
            }
            catch { }
        }

        static public void ToPlainText()
        {
            try
            {
                if (NClipboard.ContainsText())
                {
                    string text = NClipboard.GetText();
                    NClipboard.SetText(text); //all formatting (e.g. RTF) will be removed
                }
            }
            catch { }
        }

        static public void SetClipboard(Dictionary<uint, byte[]> data)
        {
            try
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    EmptyClipboard();
                    foreach (var format in data.Keys)
                        SetBytes(format, data[format]);

                    CloseClipboard();
                }
            }
            catch { }
        }
    }
}