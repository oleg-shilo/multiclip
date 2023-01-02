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
internal class ClipboardWatcher : Form
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
        var h = Win32.Desktop.GetForegroundWindow();

        this.InitializeComponent();

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

    void InitializeComponent()
    {
        this.SuspendLayout();
        //
        // ClipboardWatcher
        //
        this.ClientSize = new System.Drawing.Size(284, 195);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
        this.Name = "ClipboardWatcher";
        this.ShowInTaskbar = false;
        this.Text = "MultiClip_ClipboardWatcherWindow";
        this.ResumeLayout(false);
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
        //const int WM_QUERYENDSESSION = 0x11;

        switch (m.Msg)
        {
            //case WM_IDLE:
            //    if (m.WParam == nextClipboardViewer)
            case Globals.WM_MULTICLIPTEST:
                {
                    // Debug.Assert(false);

                    if (ClipboardHistory.lastSnapshopHash != 0 && ClipboardHistory.lastSnapshopHash != Win32.Clipboard.GetClipboard().GetContentHash())
                        m.Result = IntPtr.Zero; // stopped receiving clipboard notifications
                    else
                        m.Result = (IntPtr)Environment.TickCount;

                    break;
                }
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