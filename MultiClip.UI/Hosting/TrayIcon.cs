using System;
using System.Drawing;
using System.Reflection;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using MultiClip.UI.Properties;
using System.Windows.Forms;

namespace MultiClip.UI
{
    class TrayIcon
    {
        static public EventHandler Rehook;
        static public EventHandler Test;
        static public EventHandler ShowSettings;
        static public EventHandler ShowHistory;
        static public EventHandler Exit;

        static NotifyIcon ni;

        static public void RefreshIcon()
        {
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