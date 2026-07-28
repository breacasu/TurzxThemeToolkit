using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using TurzxThemeToolkit.Extractor;

namespace TurzxThemeToolkit.Gui
{
    public class ImageListItem
    {
        public ExtractedImage Image { get; set; } = new ExtractedImage();
        public BitmapImage? Thumbnail { get; set; }
        public string Title => Image.RoleDescription;
        public string Subtitle
        {
            get
            {
                var alpha = Image.HasAlpha ? "ARGB" : "RGB";
                var name = Image.ImgName != null ? $" [{Image.ImgName}]" : "";
                return $"{Image.Width}x{Image.Height} {alpha} {Image.ByteLength / 1024.0:F1} KB{name}";
            }
        }
    }

    public class ElementListItem
    {
        public ThemeElement Element { get; set; } = new ThemeElement();
        public string Summary => Element.Summary;
        public string DetailLine
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                parts.Add($"pos=({Element.PosX},{Element.PosY})");
                if (Element.HasImage) parts.Add("has image");
                return string.Join(", ", parts);
            }
        }
    }

    public partial class MainWindow : Window
    {
        private List<ExtractedImage> _images = new();
        private List<ThemeElement> _elements = new();
        private string? _currentFilePath;
        private string? _turzxExePath;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select a .turtheme file",
                Filter = "TURZX Theme (*.turtheme)|*.turtheme|All files (*.*)|*.*",
                Multiselect = false
            };

            if (dlg.ShowDialog() == true)
            {
                LoadThemeFile(dlg.FileName);
            }
        }

        private void LoadThemeFile(string path)
        {
            _currentFilePath = path;
            FilePathBox.Text = path;

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Extract images (signature scanning — always works)
                _images = ThemeExtractor.Extract(path);

                // Extract video info
                var video = ThemeExtractor.ExtractVideoInfo(path);

                // Load elements (requires TURZX.exe for deserialization)
                _turzxExePath = FindTurzxExe(path);
                _elements = new List<ThemeElement>();
                if (_turzxExePath != null)
                {
                    try
                    {
                        var theme = ThemeDeserializer.Deserialize(path, _turzxExePath);
                        if (theme != null)
                            _elements = ThemeElementReader.ReadElements(theme);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(
                            $"Could not load element details (requires TURZX.exe):\n{ex.Message}",
                            "Elements unavailable",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                sw.Stop();

                // Populate images tab
                ImageList.Items.Clear();
                PreviewImage.Source = null;
                PreviewPlaceholder.Visibility = Visibility.Visible;

                for (int i = 0; i < _images.Count; i++)
                {
                    var img = _images[i];
                    var item = new ImageListItem { Image = img };
                    item.Thumbnail = CreateThumbnail(img);
                    ImageList.Items.Add(item);
                }

                // Populate elements tab
                ElementList.Items.Clear();
                ElementDetailsPanel.Children.Clear();
                var placeholder = new TextBlock
                {
                    Text = "Select an element to inspect its properties",
                    Foreground = System.Windows.Media.Brushes.Gray,
                    FontSize = 13,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 40, 0, 0)
                };
                ElementDetailsPanel.Children.Add(placeholder);
                ElementPreviewImage.Visibility = Visibility.Collapsed;
                PopulateElementTypeFilter();
                RefreshElementList();

                // Status
                var fileName = Path.GetFileName(path);
                var fileSize = new FileInfo(path).Length;
                var videoInfo = video != null && video.HasVideo ? $", video: {video.VideoName}" : "";
                var elemInfo = _elements.Count > 0 ? $", {_elements.Count} elements" : "";
                StatusText.Text = $"Loaded {fileName} — {_images.Count} image(s){videoInfo}{elemInfo} in {sw.ElapsedMilliseconds}ms ({fileSize / 1024.0:F1} KB)";
                DetailText.Text = _elements.Count > 0
                    ? $"{_elements.Count} elements loaded via TURZX.exe"
                    : (_turzxExePath == null ? "TURZX.exe not found — element details unavailable" : "");

                ExtractBtn.IsEnabled = _images.Count > 0;
                SaveAsBtn.IsEnabled = false;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to load {path}:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = $"Error: {ex.Message}";
            }
        }

        private void PopulateElementTypeFilter()
        {
            ElementTypeFilter.Items.Clear();
            ElementTypeFilter.Items.Add("All types");
            var types = _elements.Select(e => e.TypeCategory).Distinct().OrderBy(t => t);
            foreach (var t in types)
                ElementTypeFilter.Items.Add(t);
            ElementTypeFilter.SelectedIndex = 0;
        }

        private void RefreshElementList()
        {
            if (_elements.Count == 0) return;

            var selected = ElementTypeFilter.SelectedItem?.ToString() ?? "All types";
            ElementList.Items.Clear();

            foreach (var elem in _elements)
            {
                if (selected == "All types" || elem.TypeCategory == selected)
                    ElementList.Items.Add(new ElementListItem { Element = elem });
            }

            ElementCount.Text = $"{ElementList.Items.Count} element(s)";
        }

        private void ElementTypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ElementTypeFilter.Items.Count == 0) return;
            RefreshElementList();
        }

        private void ElementList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ElementList.SelectedItem is not ElementListItem item)
            {
                ElementPreviewImage.Visibility = Visibility.Collapsed;
                return;
            }

            var elem = item.Element;

            // Clear previous details (keep the preview image control)
            ElementDetailsPanel.Children.Clear();
            ElementPreviewImage.Visibility = Visibility.Collapsed;

            // Header
            AddDetailHeader(elem);

            // Show preview for image elements
            if (elem.HasImage && _turzxExePath != null)
            {
                ShowElementPreview(elem);
            }

            // Group properties by category
            var byCat = elem.Properties.GroupBy(p => p.Category);
            foreach (var cat in byCat)
            {
                var group = new System.Windows.Controls.GroupBox
                {
                    Header = cat.Key,
                    Margin = new Thickness(0, 8, 0, 0),
                    Padding = new Thickness(4)
                };
                var sp = new StackPanel();
                foreach (var prop in cat)
                {
                    var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var nameText = new TextBlock
                    {
                        Text = prop.Name,
                        Foreground = System.Windows.Media.Brushes.Gray,
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    var valueText = new TextBlock
                    {
                        Text = prop.Value,
                        FontWeight = prop.IsFont ? FontWeights.Bold : FontWeights.Normal,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    Grid.SetColumn(nameText, 0);
                    Grid.SetColumn(valueText, 1);
                    row.Children.Add(nameText);
                    row.Children.Add(valueText);
                    sp.Children.Add(row);
                }
                group.Content = sp;
                ElementDetailsPanel.Children.Add(group);
            }
        }

        private void AddDetailHeader(ThemeElement elem)
        {
            var header = new TextBlock
            {
                Text = $"{elem.TypeCategory} [{elem.Index}]",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            ElementDetailsPanel.Children.Add(header);

            if (!string.IsNullOrEmpty(elem.DisplayName))
            {
                var name = new TextBlock
                {
                    Text = elem.DisplayName,
                    FontSize = 14,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                ElementDetailsPanel.Children.Add(name);
            }

            // Font summary line
            if (elem.FontName != null)
            {
                var fontLine = $"Font: {elem.FontName} {elem.FontSize}" +
                               (elem.FontBold == true ? " Bold" : "") +
                               (elem.FontColor != null ? $" {elem.FontColor}" : "");
                var fontText = new TextBlock
                {
                    Text = fontLine,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                ElementDetailsPanel.Children.Add(fontText);
            }
        }

        private void ShowElementPreview(ThemeElement elem)
        {
            // Find the corresponding extracted image by matching the element index
            // Image elements are typically first in GraphList and first in our extracted list
            // We match by widget index (Background is index 0, widget 1 = images[1], etc.)
            // This is a best-effort match since both lists are in serialization order.
            var idx = elem.Index;
            if (idx >= 0 && idx < _images.Count)
            {
                var bmp = CreateFullPreview(_images[idx]);
                if (bmp != null)
                {
                    ElementPreviewImage.Source = bmp;
                    ElementPreviewImage.Visibility = Visibility.Visible;
                }
            }
        }

        private BitmapImage? CreateThumbnail(ExtractedImage img)
        {
            try
            {
                using var ms = new MemoryStream(img.Data);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 96;
                bmp.DecodePixelHeight = 96;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        private BitmapImage? CreateFullPreview(ExtractedImage img)
        {
            try
            {
                using var ms = new MemoryStream(img.Data);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        private void ImageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ImageList.SelectedItem is ImageListItem item)
            {
                var preview = CreateFullPreview(item.Image);
                if (preview != null)
                {
                    PreviewImage.Source = preview;
                    PreviewPlaceholder.Visibility = Visibility.Collapsed;
                    DetailText.Text = $"{item.Title} — {item.Image.Width}x{item.Image.Height} {item.Subtitle}";
                    SaveAsBtn.IsEnabled = true;
                }
            }
            else
            {
                PreviewImage.Source = null;
                PreviewPlaceholder.Visibility = Visibility.Visible;
                SaveAsBtn.IsEnabled = false;
            }
        }

        private void Extract_Click(object sender, RoutedEventArgs e)
        {
            if (_images.Count == 0 || _currentFilePath == null)
                return;

            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select output directory for extracted images",
                ShowNewFolderButton = true
            };

            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            try
            {
                var saved = ThemeExtractor.SaveImages(_images, dlg.SelectedPath);
                StatusText.Text = $"Extracted {saved.Count} image(s) to {dlg.SelectedPath}";

                System.Windows.MessageBox.Show(
                    $"Extracted {saved.Count} image(s) to:\n{dlg.SelectedPath}",
                    "Extraction Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Extraction failed:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveSelected_Click(object sender, RoutedEventArgs e)
        {
            if (ImageList.SelectedItem is not ImageListItem item)
                return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save image",
                Filter = "PNG Image (*.png)|*.png|All files (*.*)|*.*",
                FileName = item.Image.SuggestedFileName
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllBytes(dlg.FileName, item.Image.Data);
                    StatusText.Text = $"Saved: {dlg.FileName}";
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Save failed:\n\n{ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private string? FindTurzxExe(string themeFilePath)
        {
            try
            {
                var themeDir = Path.GetDirectoryName(themeFilePath);
                var resDir = Path.GetDirectoryName(themeDir);
                var rootDir = Path.GetDirectoryName(resDir);
                if (rootDir == null) return null;
                var exePath = Path.Combine(rootDir, "TURZX.exe");
                return File.Exists(exePath) ? exePath : null;
            }
            catch { return null; }
        }
    }
}
