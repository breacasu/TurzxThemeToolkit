# TurzxThemeToolkit — Technical Documentation

For a user-friendly overview, see [README.md](README.md).

## Overview

`.turtheme` files are `.NET BinaryFormatter`-serialized `UsbMonitorL.Theme` objects. Bitmap fields are embedded as PNG streams. All other element properties (font, position, colors, dimensions, data bindings) are stored as serialized .NET objects.

The toolkit uses **two approaches**:
- **Signature scanning** (no TURZX required) — extracts embedded PNG images and video path references by scanning the binary stream
- **Full deserialization** (requires `TURZX.exe`) — loads the TURZX assembly and deserializes the complete Theme object to read all widget element properties via reflection

## Image Extraction (Signature Scanning)

`.turtheme` files contain PNG streams embedded directly in the BinaryFormatter binary data. The tool scans for PNG signatures (`\x89PNG\r\n\x1a\n` through `IEND`) and extracts each embedded image without requiring the TURZX assembly. It also scans for `BinaryObjectString` records (record type `0x06`) to recover original image filenames (`ImgName`).

### Image Classification

Extracted images are classified into roles:
- **Background** (`themePic`) — the theme's main background image
- **WidgetBitmap** — the current widget overlay bitmap
- **WidgetOriginal** (`O_bitmap`) — the original unmodified widget bitmap

WidgetBitmap and WidgetOriginal are paired: if the widget bitmap differs from the original by exactly 1 pixel or is the same size, they are paired as modified/original.

## Video References

Videos are **not embedded** in `.turtheme` files — only their file paths are stored as strings. The Theme stores up to 4 video-related fields: `videoPath` (Windows absolute path), `o_videoPath` (Linux/device path), `videoTargetPath`, and `videoName` (bare filename).

At runtime, TURZX **ignores the stored absolute paths** and reconstructs the file location from:
```
Path.Combine(AppDirPath, "video", resolution, videoName)
```
i.e. `<turzx-root>\video\<resolution>\<videoName>`. The tool replicates this resolution to check if the video file exists locally.

## Element Inspection (Full Deserialization)

The `elements` command loads `TURZX.exe` via `Assembly.LoadFrom`, sets up an `AssemblyResolve` handler (so any version of `UsbMonitorL` resolves to the loaded assembly), and uses `BinaryFormatter.Deserialize` to reconstruct the complete Theme object graph.

The deserialized `Theme.GraphList` contains `GraphItem` subclasses:
- **GraphImage** — image widget (has `bitmap`, `O_bitmap`, `ImgName`, `zoom_rate`)
- **GraphAnimation** — animation/video widget (has `FilePath`, `videoName`, `potritMode`)
- **GraphStatuBar** — status bar (has `direction`, `width`, `height`, `radius`, `FrontColor`, etc.)
- **GraphDynamicBar** — dynamic bar (inherits GraphStatuBar, adds `InnerCircleRadius`)
- **GraphArchBar** — arch/curved bar (has `diameter`, `archWidth`, `totalAngel`, etc.)
- **GraphLine** — line graph (has `LineColor`, `FillColor`, `maxValue`, etc.)
- **GraphClock** — clock (inherits GraphImage, adds `centerX`, `centerY`, `angle`, etc.)

Each element also inherits common properties from `GraphItem`: `TypeName`, `DisplayName`, `posX`, `posY`, `enabled`, `hide`, `m_data` (sensor binding), `fontConfig` (font settings), `AcceptDataList` (allowed data sources).

The tool reads all these via reflection (`ThemeElementReader.cs`) and flattens them into a display-friendly property list, grouped by category (Font, Position, DataBinding, TypeSpecific).

## Building from Source

```powershell
git clone https://github.com/breacasu/TurzxThemeToolkit.git
cd TurzxThemeToolkit
dotnet build TurzxThemeToolkit.sln -c Release
```

Output: `src\bin\Release\net48\TurzxThemeToolkit.exe`

## Project Structure

```
TurzxThemeToolkit/
├── src/
│   ├── TurzxThemeToolkit.csproj    # net48, WPF + WinForms, x64, ApplicationIcon
│   ├── Program.cs                   # CLI entry point + GUI launcher
│   ├── Extractor/
│   │   ├── ThemeExtractor.cs        # PNG signature scanning + string extraction
│   │   ├── ThemeDeserializer.cs     # Loads TURZX.exe, deserializes Theme via BinaryFormatter
│   │   ├── ThemeElementReader.cs    # Reads deserialized elements via reflection → flat property list
│   │   ├── ExtractedImage.cs        # Image model (data, dimensions, role, naming)
│   │   ├── ThemeElement.cs          # Element model (type, font, position, properties)
│   │   ├── ThemeVideoInfo.cs        # Video reference model
│   │   └── ImageRole.cs             # Enum: Background, WidgetBitmap, WidgetOriginal
│   └── Gui/
│       ├── MainWindow.xaml          # WPF GUI: Images tab + Elements tab
│       └── MainWindow.xaml.cs
├── assets/icons/icon.svg
├── .github/workflows/release.yml
├── TurzxThemeToolkit.sln
├── README.md, TECHNICAL.md, LICENSE, .gitignore
```
