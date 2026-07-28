using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace TurzxThemeToolkit.Extractor
{
    /// <summary>
    /// Extracts embedded PNG images from .turtheme files.
    /// .turtheme files are .NET BinaryFormatter-serialized UsbMonitorL.Theme objects.
    /// Bitmap fields (themePic, GraphImage.bitmap, GraphImage.O_bitmap) are serialized
    /// as PNG streams embedded directly in the BinaryFormatter binary data.
    /// </summary>
    public static class ThemeExtractor
    {
        // PNG signature
        private static readonly byte[] PngSignature =
            { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        // IEND chunk type
        private static readonly byte[] IendMarker = { 0x49, 0x45, 0x4E, 0x44 };

        private static readonly string[] ImageExtensions = { ".png", ".bmp", ".jpg", ".jpeg", ".gif", ".webp" };
        private static readonly string[] VideoExtensions = { ".mp4", ".avi", ".mkv", ".h264", ".mov", ".webm", ".ts", ".flv", ".wmv" };

        /// <summary>
        /// Extracts all embedded images from a .turtheme file.
        /// </summary>
        public static List<ExtractedImage> Extract(string filePath)
        {
            var bytes = File.ReadAllBytes(filePath);
            var images = new List<ExtractedImage>();

            // Find all PNG streams
            var pngRanges = FindPngStreams(bytes);
            if (pngRanges.Count == 0)
                return images;

            // Find ImgName strings from the stream (outside PNG data)
            var imgNames = FindImageNameStrings(bytes, pngRanges);

            // Parse IHDR for each PNG to get dimensions
            for (int i = 0; i < pngRanges.Count; i++)
            {
                var (start, end) = pngRanges[i];
                var img = new ExtractedImage
                {
                    Offset = start,
                    Data = new byte[end - start]
                };
                Array.Copy(bytes, start, img.Data, 0, end - start);
                ReadIhdr(img);
                images.Add(img);
            }

            // Classify images by role
            ClassifyImages(images);

            // Assign ImgName values to widget images
            AssignImageNames(images, imgNames);

            return images;
        }

        /// <summary>
        /// Extracts video reference information from a .turtheme file.
        ///
        /// Videos are NOT embedded — only file paths are stored as BinaryFormatter
        /// BinaryObjectString records. The Theme stores up to 4 video-related strings:
        ///   videoPath (Windows abs path), o_videoPath (Linux/device abs path),
        ///   videoTargetPath (optional), videoName (bare filename).
        ///
        /// At runtime TURZX ignores the absolute paths and reconstructs the file
        /// location from: Path.Combine(AppDirPath, "video", resolution, videoName)
        /// i.e. &lt;turzx-root&gt;\video\&lt;resolution&gt;\&lt;videoName&gt;
        ///
        /// The theme file lives in &lt;turzx-root&gt;\theme\&lt;resolution&gt;\,
        /// so the video folder is a sibling of the theme folder.
        /// </summary>
        public static ThemeVideoInfo? ExtractVideoInfo(string themeFilePath)
        {
            var bytes = File.ReadAllBytes(themeFilePath);
            var pngRanges = FindPngStreams(bytes);

            // Collect ALL 0x06 string records outside PNG data, in order.
            var allStrings = ScanAllStrings(bytes, pngRanges);

            // Filter to video-related strings (path contains "video" folder or ends with a video extension)
            var videoStrings = new List<string>();
            foreach (var s in allStrings)
            {
                var lower = s.ToLowerInvariant();
                bool isVideo = false;
                foreach (var ext in VideoExtensions)
                {
                    if (lower.EndsWith(ext, StringComparison.Ordinal))
                    {
                        isVideo = true;
                        break;
                    }
                }
                // Also catch paths that contain "video\" or "/video/" even without a known extension
                if (!isVideo && (lower.Contains("video\\") || lower.Contains("/video/")))
                    isVideo = true;

                if (isVideo)
                    videoStrings.Add(s);
            }

            if (videoStrings.Count == 0)
                return null;

            var info = new ThemeVideoInfo();

            // Classify each string by its shape:
            //  - Starts with "/" → Linux/device path (o_videoPath)
            //  - Contains "\" → Windows path (videoPath)
            //  - No path separator (only filename) → videoName
            //  - Other → treat as videoTargetPath if slot empty, else videoName
            foreach (var s in videoStrings)
            {
                bool hasBackslash = s.Contains('\\');
                bool startsWithSlash = s.StartsWith("/");

                if (startsWithSlash && info.OVideoPath == null)
                    info.OVideoPath = s;
                else if (hasBackslash && info.VideoPath == null)
                    info.VideoPath = s;
                else if (!hasBackslash && !startsWithSlash && info.VideoName == null)
                    info.VideoName = s;
                else if (info.VideoTargetPath == null)
                    info.VideoTargetPath = s;
                else if (info.VideoName == null)
                    info.VideoName = s;
            }

            // Resolve local video file path the same way TURZX does:
            //   Path.Combine(AppDirPath, "video", resolution, videoName)
            // The theme file lives in <root>\theme\<res>\<name>.turtheme
            // The video lives in <root>\video\<res>\<videoName>
            if (!string.IsNullOrEmpty(info.VideoName))
            {
                try
                {
                    var themeDir = Path.GetDirectoryName(themeFilePath);       // <root>\theme\<res>
                    var resDir = Path.GetDirectoryName(themeDir);               // <root>\theme
                    var rootDir = Path.GetDirectoryName(resDir);                // <root>
                    var resName = Path.GetFileName(themeDir);                    // <res>
                    if (rootDir != null && !string.IsNullOrEmpty(resName))
                    {
                        var candidate = Path.Combine(rootDir, "video", resName, info.VideoName);
                        info.LocalVideoPath = candidate;
                        info.ExistsInVideoFolder = File.Exists(candidate);
                    }
                }
                catch { /* ignore path resolution errors */ }
            }

            return info;
        }

        /// <summary>
        /// Saves extracted images to the specified output directory.
        /// Returns the list of saved file paths.
        /// </summary>
        public static List<string> SaveImages(List<ExtractedImage> images, string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            var saved = new List<string>();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var img in images)
            {
                var fileName = SanitizeFileName(img.SuggestedFileName);
                var name = fileName;
                var path = Path.Combine(outputDir, name);

                if (usedNames.Contains(name))
                {
                    var baseName = Path.GetFileNameWithoutExtension(name);
                    var ext = Path.GetExtension(name);
                    int dup = 1;
                    while (usedNames.Contains(baseName + "_" + dup + ext))
                        dup++;
                    name = baseName + "_" + dup + ext;
                    path = Path.Combine(outputDir, name);
                }

                usedNames.Add(name);
                File.WriteAllBytes(path, img.Data);
                saved.Add(path);
            }

            // Write manifest
            WriteManifest(images, saved, outputDir);

            return saved;
        }

        /// <summary>
        /// Writes a manifest file describing the extracted images.
        /// </summary>
        public static void WriteManifest(List<ExtractedImage> images, List<string> savedPaths, string outputDir)
        {
            var sb = new StringBuilder();
            sb.AppendLine("TurzxThemeToolkit — Extraction Manifest");
            sb.AppendLine("========================================");
            sb.AppendLine();
            sb.AppendLine($"Total images: {images.Count}");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("Index | Role                          | Dimensions   | Alpha | Size(KB) | Offset   | ImgName");
            sb.AppendLine("------+-------------------------------+--------------+-------+----------+----------+--------");

            for (int i = 0; i < images.Count; i++)
            {
                var img = images[i];
                var alpha = img.HasAlpha ? "Yes" : "No";
                var sizeKb = (img.ByteLength + 1023) / 1024;
                var offset = img.Offset.ToString("X8");
                var imgName = img.ImgName ?? "";
                sb.AppendLine($"{i,5} | {img.RoleDescription,-29} | {img.Width}x{img.Height,-8} | {alpha,-5} | {sizeKb,8} | {offset} | {imgName}");
            }

            sb.AppendLine();
            sb.AppendLine("Files saved:");
            foreach (var path in savedPaths)
                sb.AppendLine("  " + Path.GetFileName(path));

            var manifestPath = Path.Combine(outputDir, "manifest.txt");
            File.WriteAllText(manifestPath, sb.ToString());
        }

        private static List<(int start, int end)> FindPngStreams(byte[] data)
        {
            var ranges = new List<(int start, int end)>();
            int pos = 0;

            while (pos <= data.Length - PngSignature.Length)
            {
                bool match = true;
                for (int i = 0; i < PngSignature.Length; i++)
                {
                    if (data[pos + i] != PngSignature[i])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    // Find IEND marker
                    int iendPos = -1;
                    for (int j = pos + 8; j <= data.Length - IendMarker.Length; j++)
                    {
                        if (data[j] == 0x49 && data[j + 1] == 0x45 &&
                            data[j + 2] == 0x4E && data[j + 3] == 0x44)
                        {
                            iendPos = j;
                            break;
                        }
                    }

                    if (iendPos >= 0)
                    {
                        // IEND chunk: 4 bytes type + 4 bytes CRC = 8 bytes after "IEND"
                        int end = iendPos + 8;
                        ranges.Add((pos, end));
                        pos = end;
                    }
                    else
                    {
                        pos += PngSignature.Length;
                    }
                }
                else
                {
                    pos++;
                }
            }

            return ranges;
        }

        private static void ReadIhdr(ExtractedImage img)
        {
            // PNG: 8-byte signature, then 4-byte length + 4-byte "IHDR" + 13 bytes data
            // IHDR data: width(4) + height(4) + bitDepth(1) + colorType(1) + ...
            if (img.Data.Length < 24)
                return;

            // Width at offset 16-19 (big-endian)
            img.Width = (img.Data[16] << 24) | (img.Data[17] << 16) | (img.Data[18] << 8) | img.Data[19];
            // Height at offset 20-23 (big-endian)
            img.Height = (img.Data[20] << 24) | (img.Data[21] << 16) | (img.Data[22] << 8) | img.Data[23];
            // Color type at offset 25
            img.ColorType = img.Data[25];
        }

        private static void ClassifyImages(List<ExtractedImage> images)
        {
            if (images.Count == 0)
                return;

            // First image is almost always themePic (background).
            // It's typically RGB (no alpha), unlike GraphImage bitmaps which are usually ARGB.
            images[0].Role = ImageRole.Background;

            if (images.Count == 1)
                return;

            // Remaining images are GraphImage bitmaps and O_bitmaps.
            // BinaryFormatter serializes fields in declaration order:
            //   <bitmap>k__BackingField then <O_bitmap>k__BackingField
            // So PNGs appear in pairs: (bitmap, O_bitmap) per GraphImage widget.
            //
            // O_bitmap is created via new Bitmap(bitmap) and may differ in size:
            //   - If bitmap was resized via the zoom method: O_bitmap retains original
            //     dimensions (typically +1px larger in both W and H)
            //   - If bitmap was NOT resized: O_bitmap = exact copy, same dimensions
            //
            // Strategy: if remaining image count is even, try pairing all consecutively.
            // Validate each pair: the two images should have the same OR +1px dimensions.
            int remaining = images.Count - 1;
            int widgetIndex = 1;
            int i = 1;

            if (remaining % 2 == 0)
            {
                // Validate all consecutive pairs
                bool allPairsValid = true;
                for (int p = 1; p < images.Count; p += 2)
                {
                    var a = images[p];
                    var b = images[p + 1];
                    // Valid if same size, or one is +1px of the other in both dimensions
                    bool sameSize = a.Width == b.Width && a.Height == b.Height;
                    bool bLarger = b.Width == a.Width + 1 && b.Height == a.Height + 1;
                    bool aLarger = a.Width == b.Width + 1 && a.Height == b.Height + 1;
                    if (!sameSize && !bLarger && !aLarger)
                    {
                        allPairsValid = false;
                        break;
                    }
                }

                if (allPairsValid)
                {
                    for (int p = 1; p < images.Count; p += 2)
                    {
                        var first = images[p];
                        var second = images[p + 1];

                        // bitmap is serialized first, O_bitmap second
                        // If second is larger or same → first=bitmap, second=O_bitmap
                        if (second.Width >= first.Width && second.Height >= first.Height)
                        {
                            first.Role = ImageRole.WidgetBitmap;
                            first.WidgetIndex = widgetIndex;
                            second.Role = ImageRole.WidgetOriginal;
                            second.WidgetIndex = widgetIndex;
                        }
                        else
                        {
                            // Unusual: first is larger → first=O_bitmap, second=bitmap
                            first.Role = ImageRole.WidgetOriginal;
                            first.WidgetIndex = widgetIndex;
                            second.Role = ImageRole.WidgetBitmap;
                            second.WidgetIndex = widgetIndex;
                        }
                        widgetIndex++;
                    }
                    return;
                }
            }

            // Fallback: use +1px heuristic for individual pairs
            while (i < images.Count)
            {
                if (i + 1 < images.Count)
                {
                    var cur = images[i];
                    var next = images[i + 1];

                    if (next.Width == cur.Width + 1 && next.Height == cur.Height + 1)
                    {
                        cur.Role = ImageRole.WidgetBitmap;
                        cur.WidgetIndex = widgetIndex;
                        next.Role = ImageRole.WidgetOriginal;
                        next.WidgetIndex = widgetIndex;
                        i += 2;
                        widgetIndex++;
                        continue;
                    }
                }

                // No pair found — treat as standalone bitmap
                images[i].Role = ImageRole.WidgetBitmap;
                images[i].WidgetIndex = widgetIndex;
                i++;
                widgetIndex++;
            }
        }

        /// <summary>
        /// Scans the non-PNG portions of the stream for BinaryFormatter
        /// BinaryObjectString records (record type 0x06) whose value contains
        /// an image filename (e.g., "22-11.png"). These correspond to
        /// GraphImage.ImgName values.
        /// </summary>
        private static List<string> FindImageNameStrings(byte[] data, List<(int start, int end)> pngRanges)
        {
            var results = new List<string>();

            // Build a set of PNG ranges for fast exclusion
            var inPng = new bool[data.Length];
            foreach (var (start, end) in pngRanges)
            {
                for (int j = start; j < end && j < data.Length; j++)
                    inPng[j] = true;
            }

            // Scan for BinaryObjectString records (record type 0x06)
            // Format: 0x06 | 4-byte objectId (LE) | 7-bit-encoded length | UTF-8 string
            for (int i = 0; i < data.Length - 6; i++)
            {
                if (inPng[i])
                    continue;
                if (data[i] != 0x06)
                    continue;

                // Read 7-bit encoded string length (starts at i+5, after 4-byte objectId)
                int p = i + 5;
                int length = 0;
                int shift = 0;
                bool valid = true;

                for (int b = 0; b < 5; b++)
                {
                    if (p >= data.Length) { valid = false; break; }
                    byte bVal = data[p++];
                    length |= (bVal & 0x7F) << shift;
                    shift += 7;
                    if ((bVal & 0x80) == 0) break;
                }

                if (!valid || length < 3 || length > 512)
                    continue;

                int strStart = p;
                if (strStart + length > data.Length)
                    continue;

                // Decode as UTF-8 (ImgName strings may contain CJK prefix from theme name)
                string str;
                try
                {
                    str = Encoding.UTF8.GetString(data, strStart, length);
                }
                catch
                {
                    continue;
                }

                // Check if the string contains a filename ending with an image extension
                var lower = str.ToLowerInvariant();
                bool matched = false;
                foreach (var ext in ImageExtensions)
                {
                    if (lower.EndsWith(ext, StringComparison.Ordinal) &&
                        !lower.Contains("k__backingfield"))
                    {
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                    continue;

                // Extract the filename part (strip any non-ASCII prefix like CJK theme name)
                // Find the ASCII filename substring ending with the extension
                string filename = ExtractFilename(str);

                if (!string.IsNullOrEmpty(filename))
                    results.Add(filename);

                // Skip past the string for efficiency
                i = strStart + length - 1;
            }

            return results;
        }

        /// <summary>
        /// Scans the non-PNG portions of the stream and returns ALL
        /// BinaryObjectString records (record type 0x06), in stream order.
        /// Used to locate video path strings stored in the Theme object.
        /// </summary>
        private static List<string> ScanAllStrings(byte[] data, List<(int start, int end)> pngRanges)
        {
            var results = new List<string>();

            // Build a set of PNG ranges for fast exclusion
            var inPng = new bool[data.Length];
            foreach (var (start, end) in pngRanges)
            {
                for (int j = start; j < end && j < data.Length; j++)
                    inPng[j] = true;
            }

            for (int i = 0; i < data.Length - 6; i++)
            {
                if (inPng[i])
                    continue;
                if (data[i] != 0x06)
                    continue;

                // Read 7-bit encoded string length (starts at i+5, after 4-byte objectId)
                int p = i + 5;
                int length = 0;
                int shift = 0;
                bool valid = true;

                for (int b = 0; b < 5; b++)
                {
                    if (p >= data.Length) { valid = false; break; }
                    byte bVal = data[p++];
                    length |= (bVal & 0x7F) << shift;
                    shift += 7;
                    if ((bVal & 0x80) == 0) break;
                }

                if (!valid || length < 1 || length > 1024)
                    continue;

                int strStart = p;
                if (strStart + length > data.Length)
                    continue;

                string str;
                try
                {
                    str = Encoding.UTF8.GetString(data, strStart, length);
                }
                catch
                {
                    continue;
                }

                results.Add(str);
                i = strStart + length - 1;
            }

            return results;
        }

        /// <summary>
        /// Extracts the ASCII filename portion from a string that may contain
        /// non-ASCII prefix characters (e.g., CJK theme name before "22-11.png").
        /// </summary>
        private static string ExtractFilename(string str)
        {
            // Find the last run of valid filename characters (ASCII letters, digits, -, _, .)
            // that ends at the string's end
            int start = str.Length;
            for (int i = str.Length - 1; i >= 0; i--)
            {
                char c = str[i];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.')
                {
                    start = i;
                }
                else
                {
                    break;
                }
            }

            return start < str.Length ? str.Substring(start) : str;
        }

        private static void AssignImageNames(List<ExtractedImage> images, List<string> imgNames)
        {
            // The first image is themePic (background), which doesn't have an ImgName.
            // The remaining images are GraphImage bitmaps/O_bitmaps.
            // Each GraphImage has one ImgName (shared between bitmap and O_bitmap).
            // So we need one ImgName per widget, and each widget has 1 or 2 images (bitmap, optionally O_bitmap).

            if (imgNames.Count == 0)
                return;

            // Group images by widget index
            var widgetGroups = new Dictionary<int, List<ExtractedImage>>();
            foreach (var img in images)
            {
                if (img.Role == ImageRole.WidgetBitmap || img.Role == ImageRole.WidgetOriginal)
                {
                    if (!widgetGroups.ContainsKey(img.WidgetIndex))
                        widgetGroups[img.WidgetIndex] = new List<ExtractedImage>();
                    widgetGroups[img.WidgetIndex].Add(img);
                }
            }

            // Assign names in order (deduplicate consecutive identical names)
            var sortedWidgets = widgetGroups.Keys.OrderBy(k => k).ToList();
            int nameIdx = 0;

            // Deduplicate consecutive identical names (bitmap and O_bitmap share the same ImgName,
            // but they appear as pairs in the string list)
            var dedupedNames = new List<string>();
            string? last = null;
            foreach (var name in imgNames)
            {
                if (name != last)
                {
                    dedupedNames.Add(name);
                    last = name;
                }
            }

            foreach (var widgetIdx in sortedWidgets)
            {
                if (nameIdx >= dedupedNames.Count)
                    break;
                var name = dedupedNames[nameIdx];
                foreach (var img in widgetGroups[widgetIdx])
                    img.ImgName = name;
                nameIdx++;
            }
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (char c in name)
            {
                if (invalid.Contains(c))
                    sb.Append('_');
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
