using System.IO;
using fiLE=System.IO.File;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MultiClip.UI
{
    /// <summary>
    /// All credit to Thomas Levesque (http://www.thomaslevesque.com/2009/02/05/wpf-paste-an-image-from-the-clipboard/)
    /// </summary>
    class DibHelper
    {
        public static ImageSource ImageFromDIBBytes(byte[] dibBuffer)
        {
            if (dibBuffer == null || dibBuffer.Length == 0)
                return null;

            try
            {
                BITMAPINFOHEADER infoHeader = dibBuffer.MarshalTo<BITMAPINFOHEADER>();

                int fileHeaderSize = Marshal.SizeOf(typeof(BITMAPFILEHEADER));
                int infoHeaderSize = infoHeader.biSize;
                int fileSize = fileHeaderSize + infoHeader.biSize + infoHeader.biSizeImage;

                var fileHeader = new BITMAPFILEHEADER
                {
                    bfType = BITMAPFILEHEADER.BM,
                    bfSize = fileSize,
                    bfReserved1 = 0,
                    bfReserved2 = 0,
                    bfOffBits = fileHeaderSize + infoHeaderSize + infoHeader.biClrUsed * 4
                };

                byte[] fileHeaderBytes = fileHeader.UnmarshalTo();

                //Do not dispose here as BitmapFrame still needs an active stream copy.
                //GC should eventually dispose the stream when the ImageSource is disposed.
                //http://code.logos.com/blog/2008/04/memory_leak_with_bitmapimage_and_memorystream.html
                var msBitmap = new MemoryStream(); 
                msBitmap.Write(fileHeaderBytes, 0, fileHeaderSize);
                msBitmap.Write(dibBuffer, 0, dibBuffer.Length);
                msBitmap.Seek(0, SeekOrigin.Begin);

                return BitmapFrame.Create(msBitmap);
            }
            catch { }
            return null;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct BITMAPFILEHEADER
        {
            public static readonly short BM = 0x4d42; // BM

            public short bfType;
            public int bfSize;
            public short bfReserved1;
            public short bfReserved2;
            public int bfOffBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }
    }
}