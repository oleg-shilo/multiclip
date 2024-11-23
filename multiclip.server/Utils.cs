using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace MultiClip.Server
{
    static class Log
    {
        static string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MultiClip.History", "Data");
        static string logFile = Path.Combine(DataDir, @"..\server.log");

        public static void WriteLine(string message)
        {
            if (File.Exists(logFile) && new FileInfo(logFile).Length > 100 * 1024) // > 100K
            {
                if (File.Exists(logFile + ".bak"))
                    File.Delete(logFile + ".bak");
                File.Move(logFile, logFile + ".bak");
            }

            File.AppendAllText(logFile, $"{DateTime.Now.ToString("s")}: {message}{Environment.NewLine}");
        }
    }

    static class Operations
    {
        public static void MsgBox(string message, string caption)
        {
            if (Environment.GetEnvironmentVariable("UNDER_CHOCO").IsEmpty())
                System.Windows.Forms.MessageBox.Show(message, caption);
        }
    }
}

class BytesHash
{
    public BytesHash()
    {
        unchecked
        {
            hash = (int)2166136261;
        }
    }

    int hash;
    const int p = 16777619;

    public BytesHash Add(params byte[] data)
    {
        unchecked
        {
            for (int i = 0; i < data.Length; i++)
                hash = (hash ^ data[i]) * p;
        }
        return this;
    }

    public int HashCode
    {
        get
        {
            hash += hash << 13;
            hash ^= hash >> 7;
            hash += hash << 3;
            hash ^= hash >> 17;
            hash += hash << 5;
            return hash;
        }
    }

    public override string ToString()
    {
        return HashCode.ToString();
    }
}

//Thread based task that can be canceled without the task action/body processing the cancellation token
public class Async
{
    public Thread thread;

    static public Async Run(ThreadStart action)
    {
        var result = new Async { thread = new Thread(action) };
        result.thread.Start();
        return result;
    }

    public Async WaitFor(int timeout, Action onTimeout = null)
    {
        if (!thread.Join(timeout))
        {
            try
            {
                thread.Abort();
            }
            catch
            {
                onTimeout?.Invoke();
            }
        }
        return this;
    }
}

public static class ClipboardExtensions
{
    public static ulong GetContentHash(this Dictionary<uint, byte[]> data)
    {
        ulong checksum = 0;

        unchecked
        {
            foreach (uint i in data.Keys)
                checksum = checksum ^ i;

            foreach (byte[] value in data.Values)
                for (int i = 0; i < value.Length; i++)
                    checksum = checksum ^ value[i];
            return checksum;
        }
    }

    static public IEnumerable<T> ForEach<T>(this IEnumerable<T> collection, Action<T> action)
    {
        foreach (var item in collection)
        {
            action(item);
        }

        return collection;
    }

    public static void TryDeleteDir(this string directory)
    {
        try
        {
            Directory.Delete(directory, true);
        }
        catch { }
    }

    public static bool HasInvalidPathCharacters(this string path)
    {
        return (!string.IsNullOrEmpty(path) && path.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0);
    }

    public static string ToAsciiTitle(this byte[] bytes, int max_length = 300)
    {
        var title = Encoding.ASCII.GetString(bytes.TrimAsciiEnd());
        if (title.Length > max_length)
            title = title.Substring(0, max_length) + "...";

        title = title.Replace("\n", "").Replace("\r", "");
        return title;
    }

    public static string ToUnicodeTitle(this byte[] bytes, int max_length = 300)
    {
        var title = Encoding.Unicode.GetString(bytes.TrimUnicodeEnd());
        if (title.Length > max_length)
            title = title.Substring(0, max_length) + "...";

        title = title.Replace("\n", "").Replace("\r", "");
        return title;
    }

    public static byte[] TrimUnicodeEnd(this byte[] bytes)
    {
        if (bytes.Length > 4 &&
            bytes[bytes.Length - 1 - 1] == 0 &&
            bytes[bytes.Length - 1 - 2] == 0 &&
            bytes[bytes.Length - 1 - 3] == 0 &&
            bytes[bytes.Length - 1 - 4] == 0)
            return bytes.Take(bytes.Length - 4).ToArray();
        else if (bytes.Length > 2 &&
            bytes[bytes.Length - 1 - 1] == 0 &&
            bytes[bytes.Length - 1 - 2] == 0)
            return bytes.Take(bytes.Length - 2).ToArray();
        else
            return bytes;
    }

    public static byte[] TrimAsciiEnd(this byte[] bytes)
    {
        if (bytes.Length > 2 &&
            bytes[bytes.Length - 1 - 1] == 0 &&
            bytes[bytes.Length - 1 - 2] == 0)
            return bytes.Take(bytes.Length - 2).ToArray();
        else
            return bytes;
    }

    public static string ToReadableHotKey(this string text)
    {
        if (text.IsEmpty())
            return text;
        else
            return text.Replace("Control", "Ctrl")
                       .Replace("PrintScreen", "PrtScr")
                       .Replace("Oem3", "Tilde")
                       .Replace("Oemtilde", "Tilde");
    }

    public static string ToMachineHotKey(this string text)
    {
        if (text.IsEmpty())
            return text;
        else
            return text.Replace("Ctrl", "Control")
                   .Replace("PrtScr", "PrintScreen")
                   .Replace("Tilde", "Oemtilde"); //don't bother with Oem3
    }

    public static bool IsEmpty(this string text)
    {
        return string.IsNullOrWhiteSpace(text);
    }
    public static string EscapePath(this string text)
    {
        Path.GetInvalidFileNameChars().ForEach(c => text = text.Replace(c.ToString(), "_"));
        return text;
    }

    public static bool IsNotEmpty(this string text)
    {
        return !string.IsNullOrWhiteSpace(text);
    }

    public static void DeleteIfDiExists(this string path)
    {
        if (path.IsNotEmpty() && Directory.Exists(path))
            Directory.Delete(path, true);
    }

    public static bool SameAs(this string text, string text2, bool ignoreCase = false)
    {
        return string.Equals(text, text2, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.CurrentCulture);
    }

    public static string FormatWith(this string text, params object[] args)
    {
        return string.Format(text, args);
    }

    public static int GetHash(this byte[] bytes)
    {
        return new BytesHash().Add(bytes)
                              .HashCode;
    }

    public static string ToFormatName(this uint format)
    {
        return System.Windows.Forms.DataFormats.GetFormat((int)format).Name;
    }
}