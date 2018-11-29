using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace Win32
{
    public class ClipboardView
    {
        public enum ViewFormat : int
        {
            Custom = 0,
            PlainText = 1,
            UnicodeText = 13,
            Files = 15,
            Image = 8,
        }

        public ViewFormat MainFormat;
        public string Title;
        public string Location;
        public byte[] PreviewImage;
        public string PreviewText;
        public DateTime Timestamp;

        public ClipboardView(string location)
        {
            Location = location;
            Title = "<custom>";
            PreviewText = "<Custom Data>";
            try
            {
                Timestamp = ClipboardHistory.ToTimestamp(location);

                byte[] data = null;
                if ((data = ClipboardHistory.ReadFormatData(location, (int)ViewFormat.Image)) != null)
                {
                    MainFormat = ViewFormat.Image;
                    Title = "<Image>";
                    PreviewImage = data;
                    PreviewText = "<Image>";
                }
                else if ((data = ClipboardHistory.ReadFormatData(location, (int)ViewFormat.Files)) != null)
                {
                    MainFormat = ViewFormat.Files;

                    string[] files = Clipboard.GetDropFiles(data);
                    Title = files.FirstOrDefault() ?? "<File/Directory>";
                    PreviewText = string.Join("\n", files);
                }
                else if ((data = ClipboardHistory.ReadFormatData(location, (int)ViewFormat.UnicodeText)) != null)
                {
                    MainFormat = ViewFormat.UnicodeText;

                    Title =
                    PreviewText = data.ToUnicodeTitle();
                }
                else if ((data = ClipboardHistory.ReadFormatData(location, (int)ViewFormat.PlainText)) != null)
                {
                    MainFormat = ViewFormat.PlainText;

                    Title =
                    PreviewText = data.ToAsciiTitle();
                }
            }
            catch { }

            Title = Title.Replace("\r\n", " ")
                         .Replace("\r\n", " ")
                         .Trim();

            int titleMaxLength = 100;
            if (Title.Length > titleMaxLength)
                Title = Title.Substring(0, titleMaxLength) + "...";
        }
    }
}