using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace MultiClip.UI
{
    internal class ClipboardMonitor
    {
        private static string multiClipServerExe = Path.Combine(Path.GetDirectoryName(Globals.DataDir), "multiclip.svr.exe");

        static ClipboardMonitor()
        {
            SystemEvents.PowerModeChanged += OnPowerChange;
            SystemEvents.SessionEnded += SystemEvents_SessionEnded;
            SystemEvents.SessionEnding += SystemEvents_SessionEnding;

            try
            {
                Stop();
                File.WriteAllBytes(multiClipServerExe, Properties.Resources.MultiClip);
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

        public static void Start(bool clear = false)
        {
            KillAllServers();
            Thread.Sleep(1000);
            Task.Factory.StartNew(() =>
            {
                while (!stopping && !shutdownRequested)
                {
                    try
                    {
                        StartServer("-start " + (clear ? "-clearall" : "")).WaitForExit();

                        Log.WriteLine($"Unexpected server exit.");

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

        static void KillAllServers()
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

        public static void Restart()
        {
            lastRestart = Environment.TickCount;
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

        private static int lastRestart = Environment.TickCount;
        static double threshold = TimeSpan.FromMinutes(6).TotalMilliseconds;

        public static void Test()
        {
            
            var serverRunningPeriod = (Environment.TickCount - lastRestart);
            if (serverRunningPeriod > threshold)
            {
                Restart();
                Thread.Sleep(1500);
            }

            var wnd = FindWindow(null, Globals.ClipboardWatcherWindow);
            if (wnd != null)
            {
                bool success = TestClipboard();

                if (!success)
                    Restart();
            }
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