using System;
using System.Diagnostics;
using Win32;

namespace MultiClip.UI
{
    class HistoryItemViewModel
    {
        public static HistoryItemViewModel LoadFrom(string dir)
        {
            var model = new ClipboardView(dir);

            return new HistoryItemViewModel
            {
                Location = model.Location,
                Title = model.Title,
                Timestamp = model.Timestamp,
                ViewFormat = model.MainFormat,
                PreviewText = model.PreviewText,
                PreviewImageData = model.PreviewImage
            };
        }

        public ClipboardView.ViewFormat ViewFormat { get; set; }
        public DateTime Timestamp { get; set; }
        public string Location { get; set; }
        public string Title { get; set; }

        byte[] PreviewImageData;
        object previewImage;

        public object PreviewImage
        {
            get
            {
                if (previewImage == null && PreviewImageData != null)
                {
                    previewImage = DibHelper.ImageFromDIBBytes(PreviewImageData);
                    if (previewImage == null)
                        PreviewImageData = null;
                }
                return previewImage;
            }

        }
        public object PreviewText { get; set; }
    }
}