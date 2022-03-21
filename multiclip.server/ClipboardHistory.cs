using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
                //Some applications (e.g. IE) like setting clipboard multiple times to the same content
                // Thread.Sleep(300); //dramatically helps with some 'air' in message queue
                string hashFile = SaveSnapshot();

                if (hashFile != null)
                {
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
            if (Environment.GetEnvironmentVariable("MULTICLIP_SHOW_ERRORS") != null)
            {
                var newThread = new Thread(() =>
                {
                    try
                    {
                        Thread.Sleep(1000);
                        Clipboard.SetText($"MultiClip Error: {ex.Message}");
                    }
                    catch { }
                });
                newThread.IsBackground = true;
                newThread.SetApartmentState(ApartmentState.STA);
                newThread.Start();
            }
        }
        catch
        {
        }
        return null;
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
}