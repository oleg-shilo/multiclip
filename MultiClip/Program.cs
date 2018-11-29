using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MultiClip
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Debug.Assert(false);

            if (args.Contains("-start"))
                Start();

            if (args.Contains("-purge"))
                ClipboardHistory.Purge();

            if (args.Contains("-showdup"))
                ClipboardHistory.Purge(showOnly: true);

            if (args.Contains("-toplaintext"))
                Win32.Clipboard.ToPlainText();

            if (args.Contains("-clearall"))
                ClearAll();

            var loadBuffer = args.FirstOrDefault(x => x.StartsWith("-load:"));

            if (loadBuffer.IsNotEmpty())
            {
                // Debug.Assert(false);
                LoadSnapshot(loadBuffer.Replace("-load:", ""));
            }
        }

        static void KillAll()
        {
            var runningServers = Process.GetProcessesByName("multiclip.svr");
            if (runningServers.Any())
            {
                using (var closeRequest = new EventWaitHandle(false, EventResetMode.ManualReset, Globals.CloseRequestName))
                    closeRequest.Set();

                Parallel.ForEach(runningServers, server =>
                //foreach (var server in runningServers)
                {
                    try
                    {
                        if (server.Id != Process.GetCurrentProcess().Id)
                        {
                            server.WaitForExit(500);
                            if (!server.HasExited)
                                server.Kill();
                        }
                    }
                    catch { }
                }
                );
                Thread.Sleep(200);
            }
        }

        static void ClearAll()
        {
            ClipboardHistory.ClearAll();
        }

        static void LoadSnapshot(string bufferLocation)
        {
            ClipboardHistory.LoadSnapshot(bufferLocation);
        }

        static void Start()
        {
            try
            {
                var monitor = new ClipboardHistory();

                var closeRequest = new EventWaitHandle(false, EventResetMode.ManualReset, Globals.CloseRequestName);
                ClipboardWatcher.OnClipboardChanged = monitor.ScheduleMakeSnapshot;
                ClipboardWatcher.Enabled = true;

                Task.Factory.StartNew(() =>
                {
                    Console.WriteLine("Press 'Enter' to exit");
                    Console.ReadLine();
                    closeRequest.Set();
                });

                closeRequest.WaitOne();
            }
            finally
            {
                ClipboardWatcher.Enabled = false;
            }
        }
    }
}