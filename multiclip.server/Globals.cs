using System;
using System.IO;
using System.Linq;

namespace MultiClip
{
    class Globals
    {
        static Globals()
        {
            Directory.CreateDirectory(DataDir);
        }

        public static string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MultiClip.History", "Data");
        public static string CloseRequestName = "multiclip_CloseRequest";
        public static string ClipboardWatcherWindow = "MultiClip_ClipboardWatcherWindow";
        public const int WM_USER = 0x0400;
        public const int WM_MULTICLIPTEST = WM_USER + 100;
    }

    // hardcodded configuration (not persisted)
    class Config
    {
        public static int MaxHistoryDepth = 35;
        public static bool RestoreHistoryAtStartup = true;
        public static bool EncryptData = true;
        public static int CacheEncryptDataMinSize = 1024 * 5;
        public static bool AsyncProcessing = true;
        public static bool RestartingIsEnabled => !File.Exists(Path.Combine(Globals.DataDir, "..", "disable-reset")); // can be a property getter at it will be checked only every 3 mins
        public static bool RemoveDuplicates = !File.Exists(Path.Combine(Globals.DataDir, "..", "disable-remove-duplicates")); // has to be initialized once as it is checked only every time clipboard is changed
    }
}