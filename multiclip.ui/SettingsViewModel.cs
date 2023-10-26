using MultiClip.UI.Properties;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;

namespace MultiClip.UI
{
    public class SettingsViewModel : NotifyPropertyChangedBase
    {
        bool darkTheme;

        [XmlIgnore]
        public bool LightTheme { get; set; }

        public bool DarkTheme { get { return darkTheme; } set { darkTheme = value; Process(); } }
        public int MaxHistoryItems { get; set; }
        string hotKeysView;

        public string HotKeysView
        {
            get { return hotKeysView; }

            set
            {
                hotKeysView = value;
                OnPropertyChanged(() => HotKeysView);
            }
        }

        public bool RestoreHistoryAtStartup { get; set; }
        public bool PasteAfterSelection { get; set; }

        public bool StartWithWindows { get; set; }

        public SettingsViewModel()
        {
            HotKeysView = HotKeysMapping.ToView();
            MaxHistoryItems = 40;
            DarkTheme = true;
            LightTheme = false;
            RestoreHistoryAtStartup = true;
            PasteAfterSelection = true;
            StartWithWindows = true;
        }

        static public string SettingsFile = Path.Combine(Globals.DataDir, @"..\multiclip.config");

        public static bool EnsureDefaults()
        {
            var settings = Load();
            return !File.Exists(SettingsFile);
        }

        public void Save()
        {
            this.Serialize(SettingsFile);
        }

        public static SettingsViewModel Load()
        {
            var result = new SettingsViewModel();

            try
            {
                if (File.Exists(SettingsFile))
                    result = Serializer.Deserialize<SettingsViewModel>(SettingsFile);
            }
            catch { }

            result.Process();

            return result;
        }

        public void Test()
        {
        }

        public void Process()
        {
            HotKeysView = HotKeysMapping.ToView();

            LightTheme = !DarkTheme;

            TrayIcon.SetIcon(DarkTheme ? AppResources.tray_icon : AppResources.tray_icon_black);
            MultiClip.Config.MaxHistoryDepth = MaxHistoryItems;
            Operations.ConfigAppStartup(StartWithWindows);
        }

        public void RefreshHotKeysView()
        {
            HotKeysView = HotKeysMapping.ToView();
        }

        public void MaxHistoryItemsUp()
        {
            MaxHistoryItems++;
            OnPropertyChanged(() => MaxHistoryItems);
        }

        public void MaxHistoryItemsDown()
        {
            MaxHistoryItems--;
            OnPropertyChanged(() => MaxHistoryItems);
        }

        public void ClearHistory()
        {
            ClipboardMonitor.ClearAll();
        }

        public void PurgeHistory()
        {
            ClipboardMonitor.ClearDuplicates();
        }

        public void ReadingLog()
        {
            try
            {
                var logFile = Path.Combine(Globals.DataDir, @"..\reading.log");
                System.Diagnostics.Process.Start("notepad.exe", logFile);

                System.Diagnostics.Process.Start("explorer.exe", logFile + @"\..\");
            }
            catch
            {
            }
        }

        public void About()
        {
            Operations.MsgBox("Multi-item clipboard buffer.\n\n" +
                "Version: " + Assembly.GetExecutingAssembly().GetName().Version + "\n" +
                "Copyright © Oleg Shilo 2015-2023", "Multiclip");
        }

        public void EditHotKeys()
        {
            Task.Factory.StartNew(HotKeysMapping.Edit);
        }
    }
}