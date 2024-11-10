using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using MultiClip.UI.Utils;

namespace MultiClip.UI
{
    public class Bootstrapper
    {
        private HotKeys hotKeys = HotKeys.Instance;

        public void Run()
        {
            bool justCreated = SettingsView.EnsureDefaults();

            Func<bool> clearAtStartup = () => !SettingsViewModel.Load().RestoreHistoryAtStartup;
            ClipboardMonitor.Start(clearAtStartup);

            TrayIcon.ShowHistory = (s, a) => HistoryView.Popup();
            TrayIcon.ShowSettings = (s, a) => SettingsView.Popup();
            TrayIcon.Rehook = (s, a) => { ClipboardMonitor.Restart(); TrayIcon.RefreshIcon(); };
            TrayIcon.Test = (s, a) => ClipboardMonitor.Test();
            TrayIcon.Exit = (s, a) => this.Close();
            TrayIcon.Init();

            hotKeys.Start();

            HotKeysMapping.EmbeddedHandlers[HistoryView.PopupActionName] = HistoryView.Popup;
            HotKeysMapping.EmbeddedHandlers[ClipboardMonitor.ToPlainTextActionName] = ClipboardMonitor.ToPlainText;
            HotKeysMapping.EmbeddedHandlers[HotKeysView.PopupActionName] = HotKeysView.Popup;
            HotKeysMapping.EmbeddedHandlers[ClipboardMonitor.RestartActionName] = () => ClipboardMonitor.Restart();

            HotKeysMapping.Bind(hotKeys, TrayIcon.InvokeMenu);

            var timer = new System.Windows.Threading.DispatcherTimer();
            var lastCheck = DateTime.Now;

            timer.Tick += (s, e) =>
            {
                ClipboardMonitor.Test();

                if ((DateTime.Now - lastCheck) > TimeSpan.FromMinutes(2)) // to ensure that after a long sleep we are restarting
                {
                    ClipboardMonitor.Restart(true);
                }

                if (restartingIsEnabled && ClipboardMonitor.HowLongRunning() > 3 * 60 * 1000) // restart every 3 minutes
                {
                    ClipboardMonitor.Restart(true);
                }

                lastCheck = DateTime.Now;

                // refreshing the icon works but I am not convinced it is beneficial enough to be released
                // it also creates a short flickering effect every minute.
                // TrayIcon.RefreshIcon();
            };

            timer.Interval = TimeSpan.FromMinutes(1);

            timer.Start();

            if (justCreated)
                SettingsView.Popup(); //can pop it up without any side effect only after all messaging is initialized

            SystemEvents.PowerModeChanged += OnPowerChange;
        }

        bool restartingIsEnabled => !File.Exists(Path.Combine(Globals.DataDir, "..", "disable-reset"));

        private void OnPowerChange(object s, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Resume:
                    new Task(() => ClipboardMonitor.Restart()).Start();
                    break;
            }
        }

        private void Close()
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