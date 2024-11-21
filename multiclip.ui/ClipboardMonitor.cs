using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MultiClip.UI
{
    internal class ClipboardMonitor
    {
        private static string multiClipServerExe = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "multiclip.server.exe");

        static ClipboardMonitor()
        {
            SystemEvents.PowerModeChanged += OnPowerChange;
            SystemEvents.SessionEnded += SystemEvents_SessionEnded;
            SystemEvents.SessionEnding += SystemEvents_SessionEnding;

            try
            {
                Stop();
            }
            catch { }
        }

        private static void SystemEvents_SessionEnded(object sender, SessionEndedEventArgs e)
        {
            Log.WriteLine("SystemEvents_SessionEnded");
            OnSysShutdown(null, null);
        }

        private static void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            Log.WriteLine("SystemEvents_SessionEnding");
            OnSysShutdown(null, null);
        }

        public static int ServerRecoveryDelay = 3000;

        static bool firstRun = true;

        public static void Start(Func<bool> clear)
        {
            KillAllServers();
            Thread.Sleep(1000);
            Task.Factory.StartNew(() =>
            {
                while (!stopping && !shutdownRequested)
                {
                    try
                    {
                        if (firstRun)
                        {
                            firstRun = false;
                            var clearHistory = clear();
                            Debug.Assert(clearHistory == false, "Multiclip UI is requesting clearing the history.");
                            StartServer("-start " + (clear() ? "-clearall" : "")).WaitForExit();
                        }
                        else
                        {
                            StartServer("-start").WaitForExit();
                        }

                        if (IsScheduledRestart)
                        {
                            Log.WriteLine($"Restart is requested.");
                            IsScheduledRestart = false;
                        }
                        else
                        {
                            Log.WriteLine($"Unexpected server exit.");
                        }

                        // KillAllServers();

                        // if the server exited because of the system shutdown
                        // let some time so UI also processes shutdown event.
                        Thread.Sleep(ServerRecoveryDelay);

                        //it crashed or was killed so resurrect it in the next loop
                    }
                    catch { }
                }
            });
        }

        private static Process StartServer(string args)
        {
            Log.WriteLine($"Starting server");
            var p = new Process();

            p.StartInfo.FileName = multiClipServerExe;
            p.StartInfo.Arguments = args;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.CreateNoWindow = true;
            p.Start();

            return p;
        }

        private static bool shutdownRequested = false;
        private static bool stopping = false;

        public static void Stop(bool shutdown = false)
        {
            stopping = true;
            shutdownRequested = shutdown;

            Log.WriteLine($"Stop(shutdown: {shutdown})");

            if (shutdown)
                SystemEvents.PowerModeChanged -= OnPowerChange;

            var runningServers = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(multiClipServerExe));
            if (runningServers.Any())
            {
                using (var closeRequest = new EventWaitHandle(false, EventResetMode.ManualReset, Globals.CloseRequestName))
                    closeRequest.Set();

                Parallel.ForEach(runningServers, server =>
                {
                    try
                    {
                        server.WaitForExit(200);
                        if (!server.HasExited)
                            server.Kill();
                    }
                    catch { }
                });

                Thread.Sleep(200);
            }
            stopping = false;
        }

        public static void KillAllServers()
        {
            var runningServers = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(multiClipServerExe));
            if (runningServers.Any())
                foreach (var server in runningServers)
                {
                    try
                    {
                        using (var closeRequest = new EventWaitHandle(false, EventResetMode.ManualReset, Globals.CloseRequestName))
                            closeRequest.Set();

                        server.WaitForExit(200);
                        if (!server.HasExited)
                            server.Kill();
                    }
                    catch { }
                }
        }

        public static string ToPlainTextActionName = "<MultiClip.ToPlainText>";
        public static string RestartActionName = "<MultiClip.Reset>";

        public static void ToPlainText()
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    StartServer("-toplaintext");
                }
                catch { }
            });
        }

        internal static bool IsScheduledRestart = false;

        public static void Restart(bool isScheduledRestart = false)
        {
            IsScheduledRestart = isScheduledRestart;
            LastRestart = DateTime.Now;
            try
            {
                Log.Enabled = false;
                // it will kill any active instance and the monitor loop will
                // restart the server automatically.
                KillAllServers();

                Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(ServerRecoveryDelay + 1000);
                    Log.Enabled = true;
                });
            }
            catch { }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

        public static DateTime LastRestart = DateTime.Now;

        public static bool RestartIfFaulty()
        {
            bool restarted = false;

            var wnd = FindWindow(null, Globals.ClipboardWatcherWindow);
            if (wnd != null)
            {
                // prime the clipboard monitor channel with a test content
                bool success = TestClipboard(wnd);

                if (!success)
                {
                    Restart();
                    restarted = true;
                }
            }

            return restarted;
        }

        private static bool TestClipboard(IntPtr wnd)
        {
            var success = (0 != SendMessage(wnd, Globals.WM_MULTICLIPTEST, IntPtr.Zero, IntPtr.Zero));
            // or do some test clipboard read/write
            return success;
        }

        public static void ClearAll()
        {
            Directory.GetDirectories(Globals.DataDir, "*", SearchOption.TopDirectoryOnly)
                     .ForEach(dir => dir.TryDeleteDir());
        }

        public static void ClearDuplicates()
        {
            try
            {
                StartServer("-purge");
            }
            catch { }
        }

        public static void LoadSnapshot(string bufferLocation)
        {
            try
            {
                StartServer($"\"-load:{bufferLocation}").WaitForExit();

                if (SettingsViewModel.Load().PasteAfterSelection ||
                    System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl))
                    Task.Run(() =>
                    {
                        Thread.Sleep(100);

                        Desktop.FireKeyInput(System.Windows.Forms.Keys.V, System.Windows.Forms.Keys.ControlKey);
                    });
            }
            catch
            {
            }
        }

        private static void OnPowerChange(object s, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Resume:
                    Restart();
                    break;
            }
        }

        private static void OnSysShutdown(object s, SessionEndedEventArgs e)
        {
            Log.WriteLine($"System shutdown detected. Stopping the server.");
            Stop(shutdown: true);
        }
    }
}

class Desktop
{
    [DllImport("user32.dll")]
    internal static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    internal const int KEYEVENTF_KEYUP = 0x02;
    internal const int KEYEVENTF_KEYDOWN = 0x00;

    public static void FireKeyInput(Keys key, params Keys[] modifiers)
    {
        foreach (Keys k in modifiers)
            keybd_event((byte)k, 0x45, KEYEVENTF_KEYDOWN, UIntPtr.Zero);

        keybd_event((byte)key, 0x45, KEYEVENTF_KEYDOWN, UIntPtr.Zero);

        Thread.Sleep(10);

        keybd_event((byte)key, 0x45, KEYEVENTF_KEYUP, UIntPtr.Zero);

        foreach (Keys k in modifiers)
            keybd_event((byte)k, 0x45, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}