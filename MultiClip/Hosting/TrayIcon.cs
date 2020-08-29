using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using MultiClip.UI.Properties;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using NotifyIcon = System.Windows.Forms.NotifyIcon;

namespace MultiClip.UI
{
    class TrayIcon
    {
        static public EventHandler Rehook;
        static public EventHandler Test;
        static public EventHandler ShowSettings;
        static public EventHandler ShowHistory;
        static public EventHandler Exit;
        static public ToolStripMenuItem InvokeMenu;

        static NotifyIcon ni;

        static public void RefreshIcon()
        {
            // works but interferes with the mouse cursor and menus
            var icon = ni.Icon;
            ni.Icon = null;
            Application.DoEvents();
            ni.Icon = icon;
        }

        static public void SetIcon(Icon icon)
        {
            if (ni != null)
                ni.Icon = icon;
        }

        static public void Close()
        {
            if (ni != null)
            {
                ni.Visible = false;
                ni.Dispose();
            }
        }

        static public void Init()
        {
            ni = new NotifyIcon();

            ni.Text = "MultiClip";
            ni.Visible = true;
            ni.DoubleClick += ShowHistory;

            ni.ContextMenuStrip = new ContextMenuStrip();
            ni.ContextMenuStrip.Items.Add("Open", null, ShowHistory);
            ni.ContextMenuStrip.Items.Add("Settings", null, ShowSettings);
            ni.ContextMenuStrip.Items.Add("-");
            InvokeMenu = (ToolStripMenuItem)ni.ContextMenuStrip.Items.Add("Invoke");
            // invoke.DropDownItems.Add();
            // #if DEBUG
            ni.ContextMenuStrip.Items.Add("-");
            ni.ContextMenuStrip.Items.Add("Reset", null, Rehook);
            //ni.ContextMenuStrip.Items.Add("Test", null, Test);
            // #endif
            ni.ContextMenuStrip.Items.Add("-");
            ni.ContextMenuStrip.Items.Add("Exit", null, Exit);

            ni.ContextMenuStrip.Items[0].Font = new Font(ni.ContextMenuStrip.Items[0].Font, FontStyle.Bold);

            SetIcon(Resources.tray_icon);
        }
    }
}