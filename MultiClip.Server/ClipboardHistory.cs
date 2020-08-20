using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MultiClip;
using MultiClip.Server;
using Clipboard = Win32.Clipboard;

internal class ClipboardHistory
{
    ManualResetEvent clipboardChanged = new ManualResetEvent(false);

    public string NextItemId()
    {
        return DateTime.Now.ToUniversalTime().Ticks.ToString("X8");
    }

    static public DateTime ToTimestamp(string dir)
    {
        try
        {
            var name = Path.GetFileName(dir);
            var ticks = long.Parse(name, System.Globalization.NumberStyles.HexNumber);
            var date = new DateTime(ticks);
            return date.ToLocalTime();
        }
        catch
        {
            return Directory.GetLastWriteTime(dir);
        }
    }

    public void ScheduleMakeSnapshot()
    {
        Log.WriteLine($"ScheduleMakeSnapshot. async:{Config.AsyncProcessing}");

        if (Config.AsyncProcessing)
        {
            if (false == clipboardChanged.WaitOne(0))
            {
                clipboardChanged.Set();
                Task.Factory.StartNew(() =>
                {
                    Async.Run(MakeSnapshot)
                         .WaitFor(20000, // no need to rush, let convenient debugging, but reset if hangs
                                  onTimeout: () => clipboardChanged.Reset());
                });
            }
        }
        else
        {
            MakeSnapshot();
        }
    }

    internal static Dictionary<string, byte[]> Cache = new Dictionary<string, byte[]>();

    static void ClearCaheHistoryOf(string dir)
    {
        foreach (var file in Directory.GetFiles(dir, "*.cbd"))
            Cache.Remove(file);
        dir.TryDeleteDir();
    }

    public void MakeSnapshot()
    {
        lock (typeof(ClipboardHistory))
        {
            try
            {
                // zos
                //Some applications (e.g. IE) like setting clipboard multiple times to the same content
                Thread.Sleep(600); //dramatically helps with some 'air' in message queue
                string hashFile = SaveSnapshot();

                if (hashFile != null)
                {
                    // lastNotificationTimestamp = Environment.TickCount;

                    string hash = Path.GetFileName(hashFile);

                    var hashFiles = Directory.GetFiles(Globals.DataDir, "*.hash", SearchOption.AllDirectories);

                    if (Config.RemoveDuplicates)
                    {
                        //delete older snapshots with the same content (same hash)
                        var duplicates = hashFiles.Where(file => file.EndsWith(hash) && file != hashFile).ToArray();

                        duplicates.ForEach(file => ClearCaheHistoryOf(Path.GetDirectoryName(file)));

                        hashFiles = hashFiles.Except(duplicates).ToArray();
                    }

                    //purge snapshots history excess
                    var excess = hashFiles.Select(Path.GetDirectoryName)
                                          .OrderByDescending(x => x)
                                          .Skip(Config.MaxHistoryDepth)
                                          .ToArray();

                    var hashFileDirs = hashFiles.Select(Path.GetDirectoryName);

                    var orphantDirs = Directory.GetDirectories(Globals.DataDir, "*", SearchOption.TopDirectoryOnly)
                                               .Where(d => !hashFileDirs.Contains(d))
                                               .ToArray();
                    excess.Concat(orphantDirs)
                          .ForEach(ClearCaheHistoryOf);
                }
            }
            catch { }
            finally
            {
                //Debug.WriteLine("Snapshot End");
                clipboardChanged.Reset();
            }
        }
    }

    public static void ClearAll()
    {
        Directory.GetDirectories(Globals.DataDir, "*", SearchOption.TopDirectoryOnly)
                 .ForEach(ClearCaheHistoryOf);
    }

    static Dictionary<uint, string> uniqunessFormats = (
           // "0000C009.DataObject," +
           // "0000C003.OwnerLink," +
           // "0000C013.Ole Private Data," +
           // "0000C00E.Object Descriptor," +
           // "0000C004.Native," +
           // "00000007.00000010.Locale," +
           "0000000D.UnicodeText," +
           // "0000C00B.Embed Source," +
           "00000008.DeviceIndependentBitmap," +
           "00000001.Text," +
           // "0000C07E.Rich Text Format," +
           // "00000003.MetaFilePict," + //always different even for the virtually same clipboard content
           "00000007.OEMText," +
           "0000C140.HTML Format")
        .Split(',')
        .ToDictionary(x => uint.Parse(x.Split('.').First(), NumberStyles.HexNumber));

    public static void Purge(bool showOnly = false)
    {
        foreach (var dir in Directory.GetDirectories(Globals.DataDir, "???????????????").OrderBy(x => x))
        {
            if (!Directory.GetFiles(dir, "*.hash").Any())
            {
                Console.WriteLine("Deleting hash-less data dir: " + dir);
                if (!showOnly)
                    dir.TryDeleteDir();
            }
        }

        var titles = new Dictionary<string, string>();

        foreach (var file in Directory.GetFiles(Globals.DataDir, "*.hash", SearchOption.AllDirectories).OrderByDescending(x => x))
        {
            var snapshot_dir = Path.GetDirectoryName(file);
            var title = "<binary>";
            var hash = new BytesHash();

            foreach (var data_file in Directory.GetFiles(snapshot_dir, "*.cbd").OrderBy(x => x))
            {
                // For example: 00000001.Text.cbd or 0000000D.UnicodeText.cbd

                string format = Path.GetFileNameWithoutExtension(data_file);
                if (uniqunessFormats.ContainsValue(format))
                {
                    var bytes = new byte[0];
                    try
                    {
                        // File.ReadAllBytes(data_file) will not work because even the same data encrypted
                        // twice (e.g. to different location) will produce different data. Thus need to decrypt it first
                        bytes = ReadPrivateData(data_file);
                        if (showOnly && true)
                        {
                            if (format == "0000000D.UnicodeText")
                            {
                                title = bytes.ToUnicodeTitle(20);
                                Console.WriteLine($"{Path.GetFileName(snapshot_dir)}: {title}");
                            }
                        }
                    }
                    catch { }

                    hash.Add(bytes);
                }
            }

            titles[snapshot_dir] = title;

            foreach (var old_text_ash in Directory.GetFiles(snapshot_dir, "*.text_hash"))
                File.Delete(old_text_ash);

            string crcFile = Path.Combine(snapshot_dir, hash + ".text_hash");
            File.WriteAllText(crcFile, "");
        }

        var duplicates = Directory.GetFiles(Globals.DataDir, "*.text_hash", SearchOption.AllDirectories)
                                  .GroupBy(Path.GetFileName)
                                  .Select(x => new { Hash = x.Key, Files = x.ToArray() })
                                  .ForEach(x =>
                                  {
                                      Debug.WriteLine("");
                                      Debug.WriteLine($"{x.Hash}");
                                      var snapshot = Path.GetDirectoryName(x.Files.First());
                                      if (titles.ContainsKey(snapshot))
                                          Debug.WriteLine($"{titles[snapshot]}");
                                      foreach (var item in x.Files)
                                          Debug.WriteLine("   " + item);
                                  });

        var dirs_to_purge = duplicates.Where(x => x.Files.Count() > 1).ToArray();
        Debug.WriteLine(">>> Duplicates: " + dirs_to_purge.Length);
        foreach (var item in dirs_to_purge)
            item.Files.Skip(1).ForEach(x =>
            {
                var dir = Path.GetDirectoryName(x);
                Console.WriteLine("Deleting " + dir);
                if (titles.ContainsKey(dir))
                    Debug.WriteLine(titles[dir]);
                if (!showOnly)
                    dir.TryDeleteDir();
            });
    }

    const string TestContent = "{825CF33C-83FC-4C7E-813A-1F6FE77B7DC2}";

    public bool TestClipboardWatcher(bool interactive = true)
    {
        MakeSnapshot();

        try
        {
            string latestNonTestSnapshot = Directory.GetDirectories(Globals.DataDir).Reverse().FirstOrDefault();

            ClipboardWatcher.IsTestingMode = true;
            int countBefore = ClipboardWatcher.ChangesCount;
            Win32.Clipboard.SetText(TestContent);
            Thread.Sleep(200);
            int countAfter = ClipboardWatcher.ChangesCount;

            if (countBefore != countAfter)
            {
                if (latestNonTestSnapshot.IsNotEmpty())
                {
                    LoadSnapshot(latestNonTestSnapshot);

                    if (TestContent == Win32.Clipboard.GetText())
                    {
                        Thread.Sleep(300);
                        LoadSnapshot(latestNonTestSnapshot); //try again one more time
                    }
                }

                if (interactive)
                    MessageBox.Show("The clipboard monitor is OK.", "MultiClip");
                return true;
            }
            else
            {
                if (interactive)
                    MessageBox.Show("The clipboard monitor is faulty.", "MultiClip");
                return false;
            }
        }
        finally
        {
            ClipboardWatcher.IsTestingMode = false;
        }
    }

    public bool TestClipboardWatcherFull(bool interactive = true)
    {
        MakeSnapshot();

        string latestNonTestSnapshot = Directory.GetDirectories(Globals.DataDir).Reverse().FirstOrDefault();

        Win32.Clipboard.SetText(TestContent);
        Thread.Sleep(200);

        string latestSnapshot = Directory.GetDirectories(Globals.DataDir).Reverse().FirstOrDefault();

        int totalWait = 200;
        int checkPause = 200;
        while (latestSnapshot == latestNonTestSnapshot && totalWait < 1600)
        {
            Thread.Sleep(checkPause);
            totalWait += checkPause;
            latestSnapshot = Directory.GetDirectories(Globals.DataDir).Reverse().FirstOrDefault();
        }

        if (latestSnapshot != latestNonTestSnapshot)
        {
            if (interactive)
                MessageBox.Show($"The clipboard monitor is OK.\nChecking time: {totalWait}", "MultiClip");

            latestSnapshot.TryDeleteDir();

            if (latestNonTestSnapshot.IsNotEmpty())
                LoadSnapshot(latestNonTestSnapshot);
            return true;
        }
        else
        {
            if (interactive)
                MessageBox.Show("The clipboard monitor is faulty.", "MultiClip");
            return false;
        }
    }

    int countLastCheckCount;

    public void DoHealthCheck()
    {
        lock (typeof(ClipboardHistory))
        {
            if (countLastCheckCount != ClipboardWatcher.ChangesCount)
            {
                countLastCheckCount = ClipboardWatcher.ChangesCount;
                return; //no need to check as watcher detected some changes since last checking, meaning the watcher works
            }

            try
            {
                if (!TestClipboardWatcher(false))
                {
                    ClipboardWatcher.Enabled = false;
                    ClipboardWatcher.Enabled = true;

                    if (TestClipboardWatcher(false))
                        Debug.WriteLine("MultiClip has recovered.");
                    else
                        Debug.WriteLine("ultiClip could not recover.");
                }
                countLastCheckCount = ClipboardWatcher.ChangesCount;
            }
            catch { }
        }
    }

    public string SaveSnapshot()
    {
        try
        {
            Log.WriteLine($"SaveSnapshot");

            Dictionary<uint, byte[]> clipboard = Win32.Clipboard.GetClipboard();

            if (clipboard?.Any() == true)
            {
                string snapshotDir = Path.Combine(Globals.DataDir, NextItemId());

                Directory.CreateDirectory(snapshotDir);

                var bytesHash = new BytesHash();

                foreach (uint item in clipboard.Keys.OrderBy(x => x))
                {
                    string formatFile = Path.Combine(snapshotDir, $"{item:X8}.{item.ToFormatName()}.cbd");

                    var array = new byte[0];
                    try
                    {
                        array = clipboard[item];
                    }
                    catch { }

                    if (array.Any())
                    {
                        if (uniqunessFormats.ContainsKey(item))
                            bytesHash.Add(array);

                        if (Config.EncryptData && Config.CacheEncryptDataMinSize < array.Length)
                            Cache[formatFile] = array;

                        WritePrivateData(formatFile, array);
                    }
                }

                string shapshotHashFile = Path.Combine(snapshotDir, bytesHash + ".hash");
                File.WriteAllText(shapshotHashFile, "");
                return shapshotHashFile;
            }
        }
        catch (Clipboard.LastSessionErrorDetectedException ex)
        {
            var newThread = new Thread(() =>
            {
                try
                {
                    Thread.Sleep(1000);
                    //Debug.Assert(false);
                    Clipboard.SetText($"MultiClip Error: {ex.Message}");
                }
                catch { }
            });
            newThread.IsBackground = true;
            newThread.SetApartmentState(ApartmentState.STA);
            newThread.Start();
        }
        catch
        {
        }
        return null;
    }

    public static string GetSnapshotsInfo(string dir)
    {
        var result = new StringBuilder();
        foreach (var item in Directory.GetDirectories(dir))
            result.AppendLine(GetSnapshotInfo(item));
        return result.ToString();
    }

    public static string GetSnapshotInfo(string dir)
    {
        var result = new StringBuilder();

        var totalCrc = new BytesHash();
        foreach (string file in Directory.GetFiles(dir, "*.cbd").OrderBy(x => x))
        {
            var fileCrc = new BytesHash();
            byte[] data = null;
            try
            {
                if (Cache.ContainsKey(file))
                    data = Cache[file];
                else
                    data = ReadPrivateData(file);

                fileCrc.Add(data);
                totalCrc.Add(data);

                var val = Encoding.ASCII.GetString(data);
            }
            catch { }
            result.AppendFormat("    {0} - crc:{1}\n", Path.GetFileName(file).Split('.')[1], fileCrc);
        }

        result.Insert(0, string.Format("--> dir:{0} - crc:{1}\n", Path.GetFileName(dir), totalCrc));

        return result.ToString();
    }

    public static void LoadSnapshot(string dir)
    {
        Log.WriteLine(nameof(LoadSnapshot));

        lock (typeof(ClipboardHistory))
        {
            try
            {
                var data = new Dictionary<uint, byte[]>();
                foreach (string file in Directory.GetFiles(dir, "*.cbd"))
                {
                    try
                    {
                        uint format = uint.Parse(Path.GetFileName(file).Split('.').First(), NumberStyles.HexNumber);
                        var bytes = new byte[0];

                        try
                        {
                            if (Cache.ContainsKey(file))
                                bytes = Cache[file];
                            else
                                bytes = ReadPrivateData(file);
                        }
                        catch { }

                        if (bytes.Any())
                            data[format] = bytes;
                    }
                    catch { }
                }

                if (data.Any())
                    Win32.Clipboard.SetClipboard(data);
            }
            catch { }
        }
    }

    static byte[] entropy = Encoding.Unicode.GetBytes("MultiClip");

    static internal byte[] ReadPrivateData(string file)
    {
        if (Cache.ContainsKey(file))
        {
            return Cache[file];
        }
        else
        {
            var bytes = File.ReadAllBytes(file);
            if (Config.EncryptData)
            {
                bytes = ProtectedData.Unprotect(bytes, entropy, DataProtectionScope.CurrentUser);

                if (Config.CacheEncryptDataMinSize < bytes.Length)
                    Cache[file] = bytes;
            }
            return bytes;
        }
    }

    static void WritePrivateData(string file, byte[] data)
    {
        if (Config.EncryptData)
        {
            var bytes = ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(file, bytes);
        }
        else
            File.WriteAllBytes(file, data);
    }

    public static byte[] ReadFormatData(string dir, int format)
    {
        var file = Directory.GetFiles(dir, "{0:X8}.*.cbd".FormatWith(format)).FirstOrDefault();
        if (file != null)
            return ReadPrivateData(file);
        else
            return null;
    }

    public static void DecryptAll()
    {
        foreach (var file in Directory.GetFiles(Globals.DataDir, " *.cbd", SearchOption.AllDirectories))
        {
            var bytes = ClipboardHistory.ReadPrivateData(file);
            File.WriteAllBytes(file, bytes);
        }
    }
}