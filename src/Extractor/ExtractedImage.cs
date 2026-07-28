using System.Drawing;
using System.IO;

namespace TurzxThemeToolkit.Extractor
{
    public class ExtractedImage
    {
        public byte[] Data { get; set; } = new byte[0];
        public int Offset { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int ColorType { get; set; }
        public bool HasAlpha => ColorType == 4 || ColorType == 6;
        public ImageRole Role { get; set; } = ImageRole.Unknown;
        public string? ImgName { get; set; }
        public int WidgetIndex { get; set; } = -1;
        public long ByteLength => Data.Length;

        public string RoleDescription => Role switch
        {
            ImageRole.Background => "Background (themePic)",
            ImageRole.WidgetBitmap => $"Widget {WidgetIndex} — bitmap",
            ImageRole.WidgetOriginal => $"Widget {WidgetIndex} — original (O_bitmap)",
            _ => "Unknown"
        };

        public string SuggestedFileName
        {
            get
            {
                string baseName = Role switch
                {
                    ImageRole.Background => "background",
                    ImageRole.WidgetBitmap => $"widget_{WidgetIndex}",
                    ImageRole.WidgetOriginal => $"widget_{WidgetIndex}_original",
                    _ => $"image_{Offset}"
                };

                // When an ImgName is present, use it as the filename
                if (ImgName != null)
                {
                    if (Role == ImageRole.WidgetOriginal)
                    {
                        // Append "_original" before the extension to distinguish from bitmap
                        var nameNoExt = Path.GetFileNameWithoutExtension(ImgName);
                        var ext = Path.GetExtension(ImgName);
                        if (string.IsNullOrEmpty(ext)) ext = ".png";
                        return nameNoExt + "_original" + ext;
                    }
                    return ImgName;
                }

                return baseName + ".png";
            }
        }

        public Bitmap? ToBitmap()
        {
            try
            {
                using var ms = new System.IO.MemoryStream(Data);
                return new Bitmap(ms);
            }
            catch { return null; }
        }
    }
}
