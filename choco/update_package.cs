//css_dir %WINDOWS_DESKTOP_APP%
///css_ac

using System.IO;
using System.Net;
using System.Text;
using System.Diagnostics;
using System;

//void main()
//{
Console.WriteLine("Starting...");

ServicePointManager.Expect100Continue = true;
ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

var url = "https://github.com/oleg-shilo/multiclip/releases/download/v1.4.5.0/multiclip.v1.4.5.0.7z";

var installScript = @"tools\chocolateyInstall.ps1";

var checksum = calcChecksum(url);
// var cheksum = "E1809AD6433A91B2FF4803E7F4B15AE0FA88905A28949EAC5590F7D9FD9BE9C3";
Console.WriteLine(checksum);

var code = File.ReadAllText(installScript + ".template")
               .Replace("$url = ???", "$url = '" + url + "'")
               .Replace("$checksum = ???", "$checksum = '" + checksum + "'");

File.WriteAllText(installScript, code);
Console.WriteLine("--------------");
Console.WriteLine(code);
Console.WriteLine("--------------");
Console.WriteLine();
Console.WriteLine("Done...");
//}

string calcChecksum(string url)
{
    var file = "multiclip.7z";
    DownloadBinary(url, file, (step, total) => Console.Write("\r{0}%\r", (int)(step * 100.0 / total)));
    Console.WriteLine();

    var checksum = run(@"C:\ProgramData\chocolatey\tools\checksum.exe", "-t sha256 -f \"" + file + "\"", echo: false).Trim();
    return checksum;
}

void DownloadBinary(string url, string destinationPath, Action<long, long> onProgress = null)
{
    var sb = new StringBuilder();
    byte[] buf = new byte[1024 * 4];

    var request = WebRequest.Create(url);
    var response = (HttpWebResponse)request.GetResponse();

    if (File.Exists(destinationPath))
        File.Delete(destinationPath);

    using (var destStream = new FileStream(destinationPath, FileMode.CreateNew))
    using (var resStream = response.GetResponseStream())
    {
        int totalCount = 0;
        int count = 0;

        while (0 < (count = resStream.Read(buf, 0, buf.Length)))
        {
            destStream.Write(buf, 0, count);

            totalCount += count;
            if (onProgress != null)
                onProgress(totalCount, response.ContentLength);
        }
    }

    if (File.ReadAllText(destinationPath).Contains("Error 404"))
        throw new Exception($"Resource {url} cannot be downloaded.");
}

string run(string app, string args, bool echo = true)
{
    StringBuilder sb = new StringBuilder();
    Process myProcess = new Process();
    myProcess.StartInfo.FileName = app;
    myProcess.StartInfo.Arguments = args;
    myProcess.StartInfo.UseShellExecute = false;
    myProcess.StartInfo.RedirectStandardOutput = true;
    myProcess.StartInfo.CreateNoWindow = true;
    myProcess.Start();

    string line = null;

    while (null != (line = myProcess.StandardOutput.ReadLine()))
    {
        Console.WriteLine(line);
        sb.Append(line);
    }
    myProcess.WaitForExit();
    return sb.ToString();
}