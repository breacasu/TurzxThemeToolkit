# TurzxThemeToolkit

**Extract images and font settings from TURZX theme files**

## Why This Tool?

With custom sensors working in the TURZX editor, the next step was customizing an existing theme — just a few changes: swap logos in the background image (e.g. Intel to AMD), replace irrelevant sensor values (GPU fan, network traffic) with water-cooling sensors, and remove categories that don't apply to the current setup.

Two problems stood in the way:

**1. No image export:** The TURZX editor offers no way to export background images from an existing theme. To swap just a logo, you somehow have to extract the image from the theme file first.

**2. Font settings not visible:** When adding new elements, it was extremely frustrating that the font in use wasn't shown in the font picker — even if it was already used elsewhere in the theme. With dozens of fonts available, matching the new element to the existing ones was a long trial-and-error process.

TurzxThemeToolkit solves both: it extracts all images from `.turtheme` files and displays all element properties — especially the font settings (name, size, bold, color, alignment) of existing elements.

## Features

- **Image extraction** — background images, widget overlays, and original copies as PNG files, with smart classification and original filename recovery
- **Video reference detection** — shows whether referenced video files exist in the local `video\` folder
- **Element inspector** — lists all widget elements with their complete properties:
  - **Font settings** (name, size, bold, color, gradient, alignment) — the key feature for matching fonts on new widgets
  - Position, enabled/hidden state
  - Data source bindings (sensor name, display name, sub-name)
  - Type-specific properties (status bar dimensions/colors, arch bar angles, line parameters, etc.)
- **Type filtering** — group elements by type (Image, Status Bar, Arch Bar, Line, Animation, Clock, Text)
- **GUI + CLI** — WPF GUI for interactive use, CLI for batch processing

## Requirements

- **Windows** (x64)
- **.NET Framework 4.8** (included with Windows 10/11)
- **TURZX.exe** — required only for the element inspector; image extraction works without it

## Usage

### GUI

Launch `TurzxThemeToolkit.exe` (double-click or run without arguments):

1. **Browse** — select a `.turtheme` file
2. **Images tab** — view thumbnails, preview, extract all or save individual images
3. **Elements tab** — filter by type, click an element to see all properties (including font settings)

### CLI — Extract images

```
TurzxThemeToolkit.exe extract "theme\4801920\AMD.turtheme"
TurzxThemeToolkit.exe extract "theme\4801920" -o "C:\output" -r
```

### CLI — List images + video info

```
TurzxThemeToolkit.exe list "theme\4801920\AMD.turtheme"
```

### CLI — Inspect elements (requires TURZX.exe)

```
TurzxThemeToolkit.exe elements "theme\4801920\Simple Future Purple Vertical.turtheme"
```

TURZX.exe is auto-detected from the theme file's location (`<root>\TURZX.exe`). To specify manually:

```
TurzxThemeToolkit.exe elements "theme.turtheme" --turzx "C:\path\to\TURZX.exe"
```

### Help

```
TurzxThemeToolkit.exe --help
```

## Related Projects

- **[TurzxPatcher](https://github.com/breacasu/TurzxPatcher)** — TURZX patch loader and plugin host
- **[TurzxSensorBridge](https://github.com/breacasu/TurzxSensorBridge)** — Custom hardware sensors for TURZX
- **[TURZX](https://www.turzx.com/)** — Universal Screen Themes for Windows

## Version History

### v1.0.0 (current)
- Initial release
- Image extraction via PNG signature scanning (no TURZX.exe required)
- Image classification: Background, WidgetBitmap, WidgetOriginal with original filename recovery
- Video reference detection with local file existence check
- Element inspector via full deserialization (requires TURZX.exe)
- All GraphItem subclass properties read via reflection: FontConfig (name, size, bold, color, gradient, alignment), M_Data (DataName, DisplayName, SubName, ShowUnit), position, type-specific properties (ArchBar, StatuBar, Line, Animation, Clock, Image)
- GUI with Images tab + Elements tab (type filter, element list, property details, preview)
- CLI commands: `extract`, `list`, `elements`, `--help`

## License

MIT — see [LICENSE](LICENSE).

For technical details, build instructions, and architecture see [TECHNICAL.md](TECHNICAL.md).

---

**Made with ❤️ by breacasu and AI**
