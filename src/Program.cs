using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using TurzxThemeToolkit.Extractor;
using TurzxThemeToolkit.Gui;

namespace TurzxThemeToolkit
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                LaunchGui();
                return 0;
            }

            var cmd = args[0].ToLowerInvariant();

            switch (cmd)
            {
                case "gui":
                    LaunchGui();
                    return 0;

                case "extract":
                    return RunExtract(args.Skip(1).ToArray());

                case "list":
                    return RunList(args.Skip(1).ToArray());

                case "elements":
                    return RunElements(args.Skip(1).ToArray());

                case "--help":
                case "-h":
                case "help":
                    PrintHelp();
                    return 0;

                default:
                    Console.Error.WriteLine($"Unknown command: {cmd}");
                    Console.Error.WriteLine();
                    PrintHelp();
                    return 1;
            }
        }

        private static void LaunchGui()
        {
            var app = new System.Windows.Application();
            app.Run(new MainWindow());
        }

        private static int RunExtract(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Error: No input path specified.");
                Console.Error.WriteLine("Usage: TurzxThemeToolkit.exe extract <file|directory> [-o <outputDir>] [-r]");
                return 1;
            }

            string inputPath = args[0];
            string? outputDir = null;
            bool recursive = false;

            for (int i = 1; i < args.Length; i++)
            {
                if ((args[i] == "-o" || args[i] == "--output") && i + 1 < args.Length)
                {
                    outputDir = args[++i];
                }
                else if (args[i] == "-r" || args[i] == "--recursive")
                {
                    recursive = true;
                }
            }

            if (Directory.Exists(inputPath))
            {
                return ExtractDirectory(inputPath, outputDir, recursive);
            }
            else if (File.Exists(inputPath))
            {
                return ExtractFile(inputPath, outputDir);
            }
            else
            {
                Console.Error.WriteLine($"Error: Path not found: {inputPath}");
                return 1;
            }
        }

        private static int ExtractFile(string filePath, string? outputDir)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);

                if (string.IsNullOrEmpty(outputDir))
                    outputDir = Path.Combine(Path.GetDirectoryName(filePath)!, fileName + "_extracted");

                Console.WriteLine($"Extracting: {Path.GetFileName(filePath)}");
                var images = ThemeExtractor.Extract(filePath);

                if (images.Count == 0)
                {
                    Console.WriteLine("  No images found.");
                    return 0;
                }

                var saved = ThemeExtractor.SaveImages(images, outputDir!);

                Console.WriteLine($"  Found {images.Count} image(s):");
                for (int i = 0; i < images.Count; i++)
                {
                    var img = images[i];
                    var alpha = img.HasAlpha ? "ARGB" : "RGB";
                    Console.WriteLine($"    [{i}] {img.RoleDescription,-30} {img.Width}x{img.Height} ({alpha}) — {saved[i]}");
                }
                Console.WriteLine($"  Manifest: {Path.Combine(outputDir, "manifest.txt")}");
                Console.WriteLine();

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error extracting {filePath}: {ex.Message}");
                return 1;
            }
        }

        private static int ExtractDirectory(string dirPath, string? outputDir, bool recursive)
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var themeFiles = Directory.GetFiles(dirPath, "*.turtheme", searchOption);

            if (themeFiles.Length == 0)
            {
                Console.Error.WriteLine($"No .turtheme files found in: {dirPath}");
                return 1;
            }

            Console.WriteLine($"Found {themeFiles.Length} .turtheme file(s)\n");

            int failures = 0;

            foreach (var file in themeFiles)
            {
                var perFileDir = string.IsNullOrEmpty(outputDir)
                    ? Path.Combine(dirPath, "_extracted", Path.GetFileNameWithoutExtension(file))
                    : Path.Combine(outputDir, Path.GetFileNameWithoutExtension(file));

                var result = ExtractFile(file, perFileDir);
                if (result != 0)
                    failures++;
            }

            Console.WriteLine($"Done. {themeFiles.Length} file(s) processed, {failures} failure(s).");
            return failures > 0 ? 1 : 0;
        }

        private static int RunList(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Error: No input file specified.");
                Console.Error.WriteLine("Usage: TurzxThemeToolkit.exe list <file>");
                return 1;
            }

            string filePath = args[0];

            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"Error: File not found: {filePath}");
                return 1;
            }

            try
            {
                Console.WriteLine($"File: {Path.GetFileName(filePath)}");
                Console.WriteLine($"Size: {new FileInfo(filePath).Length:N0} bytes");
                Console.WriteLine();

                var images = ThemeExtractor.Extract(filePath);
                var video = ThemeExtractor.ExtractVideoInfo(filePath);

                if (video != null && video.HasVideo)
                {
                    Console.WriteLine("Video reference:");
                    Console.WriteLine($"  Video name:     {video.VideoName ?? "(none)"}");
                    Console.WriteLine($"  Stored Win path:  {video.VideoPath ?? "(none)"}");
                    Console.WriteLine($"  Stored Linux path:{video.OVideoPath ?? "(none)"}");
                    if (video.LocalVideoPath != null)
                    {
                        var status = video.ExistsInVideoFolder ? "FOUND" : "MISSING";
                        Console.WriteLine($"  Local file:     {video.LocalVideoPath}");
                        Console.WriteLine($"  Status:         {status}");
                    }
                    Console.WriteLine();
                }

                if (images.Count == 0 && video == null)
                {
                    Console.WriteLine("No images or videos found.");
                    return 0;
                }

                if (images.Count == 0)
                {
                    Console.WriteLine("No images found.");
                    return 0;
                }

                Console.WriteLine($"Images found: {images.Count}");
                Console.WriteLine();
                Console.WriteLine($"{"Idx",-4} {"Role",-30} {"Dimensions",-12} {"Alpha",-6} {"Size",-10} {"ImgName"}");
                Console.WriteLine(new string('-', 80));

                for (int i = 0; i < images.Count; i++)
                {
                    var img = images[i];
                    var alpha = img.HasAlpha ? "Yes" : "No";
                    var size = $"{img.ByteLength / 1024.0:F1} KB";
                    Console.WriteLine($"{i,-4} {img.RoleDescription,-30} {img.Width + "x" + img.Height,-12} {alpha,-6} {size,-10} {img.ImgName ?? ""}");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private static int RunElements(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Error: No input file specified.");
                Console.Error.WriteLine("Usage: TurzxThemeToolkit.exe elements <file> [--turzx <exe>]");
                return 1;
            }

            string filePath = args[0];
            string? turzxExe = null;

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--turzx" && i + 1 < args.Length)
                    turzxExe = args[++i];
            }

            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"Error: File not found: {filePath}");
                return 1;
            }

            // Auto-detect TURZX.exe from theme path:
            // theme lives in <root>\theme\<res>\<name>.turtheme
            // TURZX.exe lives in <root>\TURZX.exe
            if (turzxExe == null)
                turzxExe = FindTurzxExe(filePath);

            if (turzxExe == null || !File.Exists(turzxExe))
            {
                Console.Error.WriteLine("Error: TURZX.exe not found.");
                Console.Error.WriteLine("The 'elements' command requires TURZX.exe to deserialize the theme.");
                Console.Error.WriteLine("Specify its path with: --turzx \"C:\\path\\to\\TURZX.exe\"");
                return 1;
            }

            try
            {
                Console.WriteLine($"File: {Path.GetFileName(filePath)}");
                Console.WriteLine($"TURZX: {turzxExe}");
                Console.WriteLine();

                var theme = ThemeDeserializer.Deserialize(filePath, turzxExe);
                if (theme == null)
                {
                    Console.Error.WriteLine("Deserialization returned null.");
                    return 1;
                }

                var elements = ThemeElementReader.ReadElements(theme);

                Console.WriteLine($"Elements found: {elements.Count}");
                Console.WriteLine();

                // Group by type category
                var byType = elements.GroupBy(e => e.TypeCategory).OrderBy(g => g.Key);
                foreach (var group in byType)
                {
                    Console.WriteLine($"--- {group.Key} ({group.Count()}) ---");
                    foreach (var elem in group)
                    {
                        Console.WriteLine($"  [{elem.Index}] {elem.DisplayName}");
                        if (elem.FontName != null)
                            Console.WriteLine($"       Font: {elem.FontName} {elem.FontSize}{(elem.FontBold == true ? " Bold" : "")} {elem.FontColor}");
                        Console.WriteLine($"       Pos: ({elem.PosX}, {elem.PosY})");
                        if (elem.HasImage)
                            Console.WriteLine("       Has embedded image");

                        // Print all properties grouped by category
                        var byCat = elem.Properties.GroupBy(p => p.Category);
                        foreach (var cat in byCat)
                        {
                            if (cat.Key == "General") continue; // already printed above
                            Console.WriteLine($"       [{cat.Key}]");
                            foreach (var prop in cat)
                                Console.WriteLine($"         {prop.Name}: {prop.Value}");
                        }
                        Console.WriteLine();
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                    Console.Error.WriteLine($"  Inner: {ex.InnerException.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Auto-detects TURZX.exe from a .turtheme file path.
        /// Theme lives in <root>\theme\<res>\<name>.turtheme
        /// TURZX.exe lives in <root>\TURZX.exe
        /// </summary>
        private static string? FindTurzxExe(string themeFilePath)
        {
            try
            {
                var themeDir = Path.GetDirectoryName(themeFilePath);   // <root>\theme\<res>
                var resDir = Path.GetDirectoryName(themeDir);         // <root>\theme
                var rootDir = Path.GetDirectoryName(resDir);          // <root>
                if (rootDir == null) return null;
                var exePath = Path.Combine(rootDir, "TURZX.exe");
                return File.Exists(exePath) ? exePath : null;
            }
            catch { return null; }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("TurzxThemeToolkit — Extract images and inspect elements from .turtheme files");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  TurzxThemeToolkit.exe                                  Launch GUI");
            Console.WriteLine("  TurzxThemeToolkit.exe gui                              Launch GUI");
            Console.WriteLine("  TurzxThemeToolkit.exe extract <file> [-o <dir>]        Extract images from one .turtheme");
            Console.WriteLine("  TurzxThemeToolkit.exe extract <dir> [-o <dir>] [-r]    Batch extract from a directory");
            Console.WriteLine("  TurzxThemeToolkit.exe list <file>                      List images + video info");
            Console.WriteLine("  TurzxThemeToolkit.exe elements <file> [--turzx <exe>] List all elements with properties");
            Console.WriteLine("  TurzxThemeToolkit.exe --help                           Show this help");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -o, --output <dir>    Output directory (default: <filename>_extracted)");
            Console.WriteLine("  -r, --recursive       Recursively search subdirectories (batch mode)");
            Console.WriteLine("  --turzx <exe>         Path to TURZX.exe (default: auto-detect from theme location)");
            Console.WriteLine();
            Console.WriteLine("About:");
            Console.WriteLine("  .turtheme files are .NET BinaryFormatter-serialized Theme objects.");
            Console.WriteLine("  Images (Bitmap fields) are embedded as PNG streams within the binary data.");
            Console.WriteLine("  'elements' deserializes the full Theme via the TURZX assembly to read all");
            Console.WriteLine("  widget properties — especially font settings the editor doesn't expose.");
        }
    }
}
