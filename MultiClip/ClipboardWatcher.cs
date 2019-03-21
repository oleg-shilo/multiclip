using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using MultiClip;

/// <summary>
/// it has to be  window (Form) in order to allow access to the WinProc
/// </summary>
class ClipboardWatcher : Form
{
    IntPtr nextClipboardViewer;
    static public Action OnClipboardChanged;

    static ClipboardWatcher dialog;

    static public void InUiThread(Action action)
    {
        if (dialog == null)
            action();
        else
            dialog.Invoke(action);
    }

    static bool enabled;

    static new public bool Enabled
    {
        get
        {
            return enabled;
        }

        set
        {
            if (enabled != value)
            {
                enabled = value;
                if (enabled)
                    Start();
                else
                    Stop();
            }
        }
    }

    public static IntPtr WindowHandle;

    static bool started = false;

    static void Start()
    {
        lock (typeof(ClipboardWatcher))
        {
            if (!started)
            {
                started = true;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        Debug.Assert(dialog == null);
                        dialog = new ClipboardWatcher();
                        dialog.Activated += delegate
                        {
                            WindowHandle = dialog.Handle;
                        };
                        dialog.ShowDialog();
                    }
                    catch { }
                });

                //Thread newThread = new Thread(new ThreadStart(()=>
                //{
                //    try
                //    {
                //        Debug.Assert(dialog == null);
                //        dialog = new ClipboardWatcher();
                //        dialog.ShowDialog();
                //    }
                //    catch { }
                //}));
                //newThread.SetApartmentState(ApartmentState.STA);
                //newThread.Start();
            }
        }
    }

    static void Stop()
    {
        lock (typeof(ClipboardWatcher))
        {
            try
            {
                if (started && dialog != null)
                {
                    InUiThread(dialog.Close);
                    dialog = null;
                    started = false;
                }
            }
            catch { }
        }
    }

    public ClipboardWatcher()
    {
        this.InitializeComponent();
    }

    [DllImport("User32.dll", CharSet = CharSet.Auto)]
    public static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

    static internal int ChangesCount = 0;
    static internal bool IsTestingMode = false;

    void NotifyChanged()
    {
        try
        {
            ChangesCount++;
            Console.WriteLine("OnClipboardChanged");
            if (!IsTestingMode && OnClipboardChanged != null)
                OnClipboardChanged();
        }
        catch
        {
            //Debug.Assert(false);
        }
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            ChangeClipboardChain(base.Handle, this.nextClipboardViewer);
            Console.WriteLine("Exited");
            base.Dispose(disposing);
        }
        catch { }
    }

    // Remove from Alt+Tab dialog
    //protected override CreateParams CreateParams
    //{
    //    get
    //    {
    //        var @params = base.CreateParams;
    //        @params.ExStyle |= 0x80;
    //        return @params;
    //    }
    //}

    System.Windows.Forms.Timer healthCheckTimer;

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    void InitializeComponent()
    {
        this.healthCheckTimer = new System.Windows.Forms.Timer();

        var h = Win32.Desktop.GetForegroundWindow();

        this.Text = Globals.ClipboardWatcherWindow;
        this.ShowInTaskbar = false;
        this.FormBorderStyle = FormBorderStyle.FixedToolWindow;

        //Cannot start minimized as it will be visible floating over the taskbar.
        //Need to start normal but this in turn will steal the current focus.
        //Thus need to restore focus in the GotFocus handler
        //this.WindowState = FormWindowState.Minimized;

        this.healthCheckTimer.Interval = 1000 * 15;
        this.healthCheckTimer.Enabled = true;

        this.Load += (s, e) =>
        {
            Left = -8200;
            Init();
        };

        this.GotFocus += (s, e) =>
        {
            Win32.Desktop.SetForegroundWindow(h); //it is important to return the focus back to the desktop window as this one always steals it at startup
        };

        this.FormClosed += (s, e) =>
        {
            Uninit();
        };
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("User32.dll")]
    protected static extern int SetClipboardViewer(int hWndNewViewer);

    void Init()
    {
        nextClipboardViewer = (IntPtr)SetClipboardViewer((int)Handle);
    }

    void Uninit()
    {
        ChangeClipboardChain(Handle, nextClipboardViewer);
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_DRAWCLIPBOARD = 0x308;
        const int WM_CHANGECBCHAIN = 0x030D;
        const int WM_ENDSESSION = 0x16;
        const int WM_QUERYENDSESSION = 0x11;

        switch (m.Msg)
        {
            //case WM_IDLE:
            //    if (m.WParam == nextClipboardViewer)
            case Globals.WM_MULTICLIPTEST:
                m.Result = (IntPtr)Environment.TickCount;
                break;

            case WM_ENDSESSION:
                Application.Exit();
                break;

            case WM_DRAWCLIPBOARD:
                NotifyChanged();
                SendMessage(nextClipboardViewer, m.Msg, m.WParam, m.LParam);
                break;

            case WM_CHANGECBCHAIN:
                if (m.WParam == nextClipboardViewer)
                {
                    nextClipboardViewer = m.LParam;
                }
                else
                {
                    SendMessage(nextClipboardViewer, m.Msg, m.WParam, m.LParam);
                }
                break;

            default:
                base.WndProc(ref m);
                break;
        }
    }
}