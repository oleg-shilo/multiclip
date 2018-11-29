using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MultiClip.UI.Utils;
using System.Windows;
using Microsoft.Win32;

namespace MultiClip.UI
{
    public class Bootstrapper
    {
        HotKeys hotKeys = HotKeys.Instance;

        public void Run()
        {
            bool justCreated = SettingsView.EnsureDefaults();

            if (!SettingsViewModel.Load().RestoreHistoryAtStartup)
                ClipboardMonitor.Start(clear: true);
            else
                ClipboardMonitor.Start(clear: false);

            TrayIcon.ShowHistory = (s, a) => HistoryView.Popup();
            TrayIcon.ShowSettings = (s, a) => SettingsView.Popup();
            TrayIcon.Rehook = (s, a) => { ClipboardMonitor.Restart(); TrayIcon.RefreshIcon(); };
            TrayIcon.Test = (s, a) => ClipboardMonitor.Test();
            TrayIcon.Exit = (s, a) => this.Close();
            TrayIcon.Init();

            hotKeys.Start();

            HotKeysMapping.EmbeddedHandlers["<MultiClip.Show>"] = HistoryView.Popup;
            HotKeysMapping.EmbeddedHandlers["<MultiClip.ToPlainText>"] = ClipboardMonitor.ToPlainText;
            HotKeysMapping.EmbeddedHandlers["<MultiClip.ShowHotKeys>"] = HotKeysView.Popup;
            HotKeysMapping.EmbeddedHandlers["<MultiClip.Reset>"] = ClipboardMonitor.Restart;

            HotKeysMapping.Bind(hotKeys);

            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Tick += (s, e) => { ClipboardMonitor.Test(); TrayIcon.RefreshIcon(); };
            timer.Interval = TimeSpan.FromMinutes(1);
            timer.Start();

            if (justCreated)
                SettingsView.Popup(); //can pop it up without any side effect only after all messaging is initialized

            SystemEvents.PowerModeChanged += OnPowerChange;
        }

        private void OnPowerChange(object s, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Resume:
                    new Task(ClipboardMonitor.Restart).Start();
                    break;
            }
        }

        void Close()
        {
            try
            {
                ClipboardMonitor.Stop(shutdown: true);
                SettingsView.CloseIfAny();
                HistoryView.CloseIfAny();
                hotKeys.Stop();
                TrayIcon.Close();
            }
            catch { }
        }
    }
}