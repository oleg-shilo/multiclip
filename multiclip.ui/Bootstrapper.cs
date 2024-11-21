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
            TrayIcon.Test = (s, a) => ClipboardMonitor.RestartIfFaulty();
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

                try
                {
                    // to test the clipboard monitor
                    var restarted = ClipboardMonitor.RestartIfFaulty();

                    if (!restarted)
                    {
                        // keeping all three checks even though some of them overlap with each other
                        var needToRestart = false;

                        // if there was no keyboard input for a long time, we can assume that the user is not using the app
                        // ideally it should be any key press input but at the moment it's actually the time since the last hot key registered
                        if (HotKeys.Instance.LastKeyInputTime.IntervalFromNow() > TimeSpan.FromMinutes(3))
                        {
                            needToRestart = true;
                        }

                        // ensure that after a long sleep we are restarting
                        if (lastCheck.IntervalFromNow() > TimeSpan.FromMinutes(2))
                        {
                            needToRestart = true;
                        }

                        // restart every 3 minutes because... why not? :o)
                        // this is a bit of a hack to ensure that the app is running
                        if (Config.RestartingIsEnabled && ClipboardMonitor.LastRestart.IntervalFromNow() > TimeSpan.FromMinutes(3))
                        {
                            needToRestart = true;
                        }

                        if (needToRestart)
                        {
                            ClipboardMonitor.Restart(true);
                        }

                        // refreshing the icon works but I am not convinced it is beneficial enough to be released
                        // it also creates a short flickering effect every minute.
                        // TrayIcon.RefreshIcon();
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteLine(ex.ToString());
                }
                finally
                {
                    lastCheck = DateTime.Now;
                }

            };

            timer.Interval = TimeSpan.FromSeconds(30);

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
                    new Task(() =>
                    {
                        Thread.Sleep(5000);
                        ClipboardMonitor.Restart();
                    }).Start();
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