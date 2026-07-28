using System.Collections.Generic;

namespace TurzxThemeToolkit.Extractor
{
    /// <summary>
    /// A single property of a deserialized theme element, captured via reflection.
    /// Values are stored as strings for display; nested objects are flattened.
    /// </summary>
    public class ElementProperty
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
        public string Category { get; set; } = "";
        public bool IsFont { get; set; }
    }

    /// <summary>
    /// A single widget/element extracted from a deserialized Theme.GraphList.
    /// Holds the element's type, display name, and all its properties in a
    /// flat, display-friendly list grouped by category.
    /// </summary>
    public class ThemeElement
    {
        /// <summary>CLR type name (e.g. "GraphImage", "GraphStatuBar").</summary>
        public string TypeName { get; set; } = "";

        /// <summary>Friendly type for grouping/filtering (e.g. "Image", "Status Bar").</summary>
        public string TypeCategory { get; set; } = "";

        /// <summary>Display name from the element itself (DisplayName field).</summary>
        public string DisplayName { get; set; } = "";

        /// <summary>Index in the GraphList.</summary>
        public int Index { get; set; }

        /// <summary>All properties, grouped for display.</summary>
        public List<ElementProperty> Properties { get; set; } = new();

        /// <summary>True if this element has an associated embedded image (bitmap/O_bitmap).</summary>
        public bool HasImage { get; set; }

        /// <summary>True if this element is enabled (visible).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>True if this element is hidden.</summary>
        public bool Hidden { get; set; }

        /// <summary>Position X in theme pixels.</summary>
        public int PosX { get; set; }

        /// <summary>Position Y in theme pixels.</summary>
        public int PosY { get; set; }

        /// <summary>Font name if the element has a fontConfig (null otherwise).</summary>
        public string? FontName { get; set; }

        /// <summary>Font size if the element has a fontConfig (null otherwise).</summary>
        public int? FontSize { get; set; }

        /// <summary>True if the font is bold.</summary>
        public bool? FontBold { get; set; }

        /// <summary>Font color as hex (#RRGGBB) if the element has a fontConfig.</summary>
        public string? FontColor { get; set; }

        /// <summary>One-line summary for list display.</summary>
        public string Summary
        {
            get
            {
                var parts = new List<string> { TypeCategory };
                if (!string.IsNullOrEmpty(DisplayName))
                    parts.Add(DisplayName);
                if (HasImage)
                    parts.Add("[image]");
                if (FontName != null)
                    parts.Add($"{FontName} {FontSize}");
                return string.Join(" — ", parts);
            }
        }
    }
}
