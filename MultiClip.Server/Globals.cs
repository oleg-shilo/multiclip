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

    class Config
    {
        public static int MaxHistoryDepth = 35;
        public static bool RestoreHistoryAtStartup = true;
        internal static bool EncryptData = true;
        internal static int CacheEncryptDataMinSize = 1024 * 5;
        internal static bool RemoveDuplicates = true;
        internal static bool AsyncProcessing = true;
    }
}