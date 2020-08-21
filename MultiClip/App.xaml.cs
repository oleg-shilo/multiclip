using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;

//TODO
// Rebind hotkeys when the config file is updated
//
namespace MultiClip.UI
{
    public partial class App : Application
    {
        Mutex mutex;

        void Application_Startup(object sender, StartupEventArgs e)
        {
            // Debug.Assert(false);

            if (Environment.GetCommandLineArgs().Contains("-kill"))
            {
                var runningGui = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location))
                                        .Where(p => p.Id != Process.GetCurrentProcess().Id);

                foreach (var server in runningGui)
                    try
                    {
                        server.Kill();
                    }
                    catch { }
                ClipboardMonitor.KillAllServers();

                Shutdown();
            }
            else
            {
                Log.WriteLine($"=============== Started =================");
                Log.WriteLine(Assembly.GetExecutingAssembly().Location);
                //new SettingsView().ShowDialog();return;
                //IMPORTANT: Do not release the mutex. OS will release the mutex on app exit automatically.
                mutex = new Mutex(true, "multiclip.history");
                if (mutex.WaitOne(0))
                {
                    StartApp();
                }
                else
                {
                    Operations.MsgBox("Another instance of the application is already running.", "MultiClip");
                    Shutdown();
                }
            }
        }

        void StartApp()
        {
            //The app must be hosted as x86 otherwise the Clipboard some operations can lead to
            //the CLR crash. Shocking!
            //
            //Very tempting to use Caliburn.Micro but for such a simple UI it's a bit overkill.
            //But more importantly the current CB.M depends on .NET v4.5 at least. It also requires
            //System.Windows.Interactivity, which is distributed by MS individually. Thus the deployment pressure
            //is to much for such a simple app as this one.
            //
            //Packing of MultiClip.exe into MultiClip.UI.exe resources is also motivated by the deployment considerations

            //SettingsView.Popup();
            //return;
            try { File.Delete("multiclip.server.exe"); } catch { }

            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            new Bootstrapper().Run();
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
#if DEBUG
            Operations.MsgBox(e.ExceptionObject.ToString(), "MultiClip critical error");
#else
            Debug.Assert(false, e.ToString());
#endif
        }

        public bool MyProperty { get; set; }

        Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("multiclip.server,", StringComparison.InvariantCultureIgnoreCase))
                return Assembly.Load(MultiClip.UI.Properties.Resources.MultiClip_server);
            return null;
        }
    }
}