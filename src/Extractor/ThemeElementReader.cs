using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;

namespace TurzxThemeToolkit.Extractor
{
    /// <summary>
    /// Reads the deserialized TURZX Theme object (loaded via reflection) and
    /// extracts a flat, display-friendly list of all widget elements and their
    /// properties — especially font information which the TURZX editor doesn't
    /// expose for existing elements.
    /// </summary>
    public static class ThemeElementReader
    {
        /// <summary>
        /// Reads all GraphList elements from a deserialized Theme object.
        /// Returns a list of ThemeElement with flattened properties.
        /// </summary>
        public static List<ThemeElement> ReadElements(object theme)
        {
            var elements = new List<ThemeElement>();
            if (theme == null) return elements;

            var themeType = theme.GetType();
            var graphListProp = themeType.GetProperty("GraphList");
            if (graphListProp == null) return elements;

            var graphList = graphListProp.GetValue(theme) as IEnumerable;
            if (graphList == null) return elements;

            int index = 0;
            foreach (var item in graphList)
            {
                if (item == null) continue;
                var element = ReadElement(item, index);
                elements.Add(element);
                index++;
            }

            return elements;
        }

        private static ThemeElement ReadElement(object graphItem, int index)
        {
            var itemType = graphItem.GetType();
            var element = new ThemeElement
            {
                Index = index,
                TypeName = itemType.Name,
                TypeCategory = GetFriendlyType(itemType.Name),
            };

            // Read common GraphItem properties
            element.DisplayName = GetStringValue(graphItem, "DisplayName") ?? "";
            element.Enabled = GetBoolValue(graphItem, "enabled", true);
            element.Hidden = GetBoolValue(graphItem, "hide", false);
            element.PosX = GetIntValue(graphItem, "posX");
            element.PosY = GetIntValue(graphItem, "posY");

            // Detect images
            var bitmapProp = itemType.GetProperty("bitmap");
            var oBitmapProp = itemType.GetProperty("O_bitmap");
            element.HasImage = bitmapProp != null && bitmapProp.GetValue(graphItem) != null;

            // Read fontConfig (the key feature — font info)
            var fontConfigProp = itemType.GetProperty("fontConfig");
            if (fontConfigProp != null)
            {
                var fontConfig = fontConfigProp.GetValue(graphItem);
                if (fontConfig != null)
                {
                    element.FontName = GetStringValue(fontConfig, "name");
                    element.FontSize = GetIntValueOrNull(fontConfig, "size");
                    element.FontBold = GetBoolValueOrNull(fontConfig, "isBold");

                    var color = GetColorValue(fontConfig, "color");
                    if (color.HasValue)
                        element.FontColor = ColorToHex(color.Value);
                }
            }

            // Build flat property list, grouped by category
            ReadCommonProperties(graphItem, element);
            ReadTypeSpecificProperties(graphItem, element);

            return element;
        }

        private static void ReadCommonProperties(object graphItem, ThemeElement element)
        {
            var type = graphItem.GetType();

            element.Properties.Add(new ElementProperty
            {
                Category = "General",
                Name = "Type",
                Value = element.TypeName
            });

            AddProp(element, "General", "DisplayName", element.DisplayName);
            AddProp(element, "General", "Enabled", element.Enabled.ToString());
            AddProp(element, "General", "Hidden", element.Hidden.ToString());
            AddProp(element, "General", "Position", $"({element.PosX}, {element.PosY})");

            var typeNameField = GetStringValue(graphItem, "TypeName");
            if (!string.IsNullOrEmpty(typeNameField))
                AddProp(element, "General", "TypeName", typeNameField);

            var subTypeName = GetStringValue(graphItem, "SubTypeName");
            if (!string.IsNullOrEmpty(subTypeName))
                AddProp(element, "General", "SubTypeName", subTypeName);

            // AcceptDataList
            var acceptDataListProp = type.GetProperty("AcceptDataList");
            if (acceptDataListProp != null)
            {
                var list = acceptDataListProp.GetValue(graphItem) as IEnumerable;
                if (list != null)
                {
                    var items = new List<string>();
                    foreach (var s in list)
                        items.Add(s?.ToString() ?? "");
                    AddProp(element, "General", "AcceptedDataSources", string.Join(", ", items));
                }
            }

            // Font configuration
            var fontConfigProp = type.GetProperty("fontConfig");
            if (fontConfigProp != null)
            {
                var fontConfig = fontConfigProp.GetValue(graphItem);
                if (fontConfig != null)
                {
                    var cat = "Font";
                    var name = GetStringValue(fontConfig, "name");
                    if (name != null)
                    {
                        element.Properties.Add(new ElementProperty
                        {
                            Category = cat, Name = "FontName", Value = name, IsFont = true
                        });
                    }
                    AddProp(element, cat, "FontSize", GetIntValue(fontConfig, "size").ToString());
                    AddProp(element, cat, "Bold", GetBoolValue(fontConfig, "isBold", false).ToString());
                    AddProp(element, cat, "Interval", GetFloatValue(fontConfig, "interval").ToString("F1"));

                    var color = GetColorValue(fontConfig, "color");
                    if (color.HasValue)
                        AddProp(element, cat, "Color", ColorToHex(color.Value));

                    var grColor = GetColorValue(fontConfig, "GrColor");
                    if (grColor.HasValue)
                        AddProp(element, cat, "GradientColor", ColorToHex(grColor.Value));

                    AddProp(element, cat, "GradientDirection", GetIntValue(fontConfig, "GrDirection").ToString());

                    // Alignment
                    var alignmentProp = fontConfig.GetType().GetProperty("alignment");
                    if (alignmentProp != null)
                    {
                        var alignment = alignmentProp.GetValue(fontConfig);
                        if (alignment != null)
                        {
                            var alignIndex = GetIntValue(alignment, "index");
                            var alignDisplay = GetStringValue(alignment, "displayName");
                            var alignName = alignIndex switch
                            {
                                0 => "Left",
                                1 => "Center",
                                2 => "Right",
                                _ => alignDisplay ?? alignIndex.ToString()
                            };
                            AddProp(element, cat, "Alignment", alignName);
                        }
                    }
                }
            }

            // M_Data (sensor binding)
            var mDataProp = type.GetProperty("m_data");
            if (mDataProp != null)
            {
                var mData = mDataProp.GetValue(graphItem);
                if (mData != null)
                {
                    var cat = "DataBinding";
                    AddProp(element, cat, "DataName", GetStringValue(mData, "DataName") ?? "");
                    AddProp(element, cat, "DisplayName", GetStringValue(mData, "DisplayName") ?? "");
                    AddProp(element, cat, "SubName", GetStringValue(mData, "SubName") ?? "");
                    AddProp(element, cat, "ShowUnit", GetBoolValue(mData, "ShowUnit", false).ToString());
                    AddProp(element, cat, "QueueLength", GetIntValue(mData, "queueLen").ToString());
                }
            }
        }

        private static void ReadTypeSpecificProperties(object graphItem, ThemeElement element)
        {
            var type = graphItem.GetType();
            var typeName = type.Name;

            switch (typeName)
            {
                case "GraphImage":
                case "GraphClock":
                    ReadImageProperties(graphItem, element);
                    if (typeName == "GraphClock")
                        ReadClockProperties(graphItem, element);
                    break;
                case "GraphAnimation":
                    ReadAnimationProperties(graphItem, element);
                    break;
                case "GraphStatuBar":
                case "GraphDynamicBar":
                    ReadStatuBarProperties(graphItem, element);
                    break;
                case "GraphArchBar":
                    ReadArchBarProperties(graphItem, element);
                    break;
                case "GraphLine":
                    ReadLineProperties(graphItem, element);
                    break;
            }

            // For GraphDynamicBar (inherits GraphStatuBar), also read the extra field
            if (typeName == "GraphDynamicBar")
            {
                AddProp(element, "DynamicBar", "InnerCircleRadius",
                    GetIntValue(graphItem, "InnerCircleRadius").ToString());
            }
        }

        private static void ReadImageProperties(object graphItem, ThemeElement element)
        {
            var cat = "Image";
            var imgName = GetStringValue(graphItem, "ImgName");
            if (!string.IsNullOrEmpty(imgName))
                AddProp(element, cat, "ImageName", imgName);

            var zoomRate = GetDoubleValue(graphItem, "zoom_rate");
            if (zoomRate.HasValue)
                AddProp(element, cat, "ZoomRate", zoomRate.Value.ToString("F2"));

            var bitmap = graphItem.GetType().GetProperty("bitmap")?.GetValue(graphItem) as Bitmap;
            if (bitmap != null)
                AddProp(element, cat, "BitmapSize", $"{bitmap.Width}x{bitmap.Height}");

            var oBitmap = graphItem.GetType().GetProperty("O_bitmap")?.GetValue(graphItem) as Bitmap;
            if (oBitmap != null)
                AddProp(element, cat, "OriginalSize", $"{oBitmap.Width}x{oBitmap.Height}");
        }

        private static void ReadClockProperties(object graphItem, ThemeElement element)
        {
            var cat = "Clock";
            AddProp(element, cat, "CenterX", GetIntValue(graphItem, "centerX").ToString());
            AddProp(element, cat, "CenterY", GetIntValue(graphItem, "centerY").ToString());
            AddProp(element, cat, "Angle", GetIntValue(graphItem, "angle").ToString());
            AddProp(element, cat, "EndAngle", GetIntValue(graphItem, "endAngle").ToString());
            AddProp(element, cat, "AngleOffset", GetFloatValue(graphItem, "AngleOffset").ToString("F1"));
            AddProp(element, cat, "Offset", GetFloatValue(graphItem, "offset").ToString("F1"));
            AddProp(element, cat, "MoveOriginPoint", GetBoolValue(graphItem, "moveOpoint", false).ToString());
        }

        private static void ReadAnimationProperties(object graphItem, ThemeElement element)
        {
            var cat = "Animation";
            var videoName = GetStringValue(graphItem, "videoName");
            if (!string.IsNullOrEmpty(videoName))
                AddProp(element, cat, "VideoName", videoName);

            var filePath = GetStringValue(graphItem, "FilePath");
            if (!string.IsNullOrEmpty(filePath))
                AddProp(element, cat, "FilePath", filePath);

            AddProp(element, cat, "PortraitMode", GetBoolValue(graphItem, "potritMode", false).ToString());
            AddProp(element, cat, "Ration", GetIntValue(graphItem, "ration").ToString());
        }

        private static void ReadStatuBarProperties(object graphItem, ThemeElement element)
        {
            var cat = "StatusBar";
            AddProp(element, cat, "Direction", GetIntValue(graphItem, "direction").ToString());
            AddProp(element, cat, "TransparentBackground", GetBoolValue(graphItem, "trBack", false).ToString());
            AddProp(element, cat, "FillBackground", GetBoolValue(graphItem, "fillBack", false).ToString());
            AddProp(element, cat, "UseGradient", GetBoolValue(graphItem, "useGradient", false).ToString());
            AddProp(element, cat, "LineWidth", GetIntValue(graphItem, "lineWidth").ToString());
            AddProp(element, cat, "Width", GetIntValue(graphItem, "width").ToString());
            AddProp(element, cat, "Height", GetIntValue(graphItem, "height").ToString());
            AddProp(element, cat, "Radius", GetIntValue(graphItem, "radius").ToString());

            AddColorProp(element, cat, "FrontColor", graphItem, "FrontColor");
            AddProp(element, cat, "FrontAlpha", GetByteValue(graphItem, "FrontAlpha").ToString());
            AddColorProp(element, cat, "BackColor", graphItem, "BackColor");
            AddProp(element, cat, "BackAlpha", GetByteValue(graphItem, "BackAlpha").ToString());
            AddColorProp(element, cat, "GradientColor", graphItem, "GradientColor");

            AddProp(element, cat, "UseSubsection", GetBoolValue(graphItem, "useSubsection", false).ToString());
            AddProp(element, cat, "SplitBlockWidth", GetIntValue(graphItem, "SplitBlockWidth").ToString());
            AddProp(element, cat, "SplitBlankWidth", GetIntValue(graphItem, "SplitBlankWidth").ToString());
        }

        private static void ReadArchBarProperties(object graphItem, ThemeElement element)
        {
            var cat = "ArchBar";
            AddProp(element, cat, "UseBlock", GetBoolValue(graphItem, "useBlock", false).ToString());
            AddProp(element, cat, "Round", GetBoolValue(graphItem, "round", false).ToString());
            AddProp(element, cat, "TransparentBackground", GetBoolValue(graphItem, "trBack", false).ToString());
            AddProp(element, cat, "FillBackground", GetBoolValue(graphItem, "fillBack", false).ToString());
            AddProp(element, cat, "ArchWidth", GetIntValue(graphItem, "archWidth").ToString());
            AddProp(element, cat, "LineWidth", GetIntValue(graphItem, "lineWidth").ToString());
            AddProp(element, cat, "Diameter", GetIntValue(graphItem, "diameter").ToString());
            AddProp(element, cat, "Height", GetIntValue(graphItem, "height").ToString());
            AddProp(element, cat, "StartPercent", GetIntValue(graphItem, "startPer").ToString());
            AddProp(element, cat, "TotalAngle", GetIntValue(graphItem, "totalAngel").ToString());
            AddProp(element, cat, "HasRingBorder", GetBoolValue(graphItem, "HasRingBorder", false).ToString());

            AddColorProp(element, cat, "FrontColor", graphItem, "FrontColor");
            AddProp(element, cat, "FrontAlpha", GetByteValue(graphItem, "FrontAlpha").ToString());
            AddColorProp(element, cat, "BackColor", graphItem, "BackColor");
            AddProp(element, cat, "BackAlpha", GetByteValue(graphItem, "BackAlpha").ToString());
            AddColorProp(element, cat, "GradientColor", graphItem, "GradientColor");
            AddProp(element, cat, "SplitBlockWidth", GetIntValue(graphItem, "SplitBlockWidth").ToString());
            AddProp(element, cat, "SplitBlankWidth", GetIntValue(graphItem, "SplitBlankWidth").ToString());
        }

        private static void ReadLineProperties(object graphItem, ThemeElement element)
        {
            var cat = "Line";
            AddColorProp(element, cat, "LineColor", graphItem, "LineColor");
            AddColorProp(element, cat, "FillColor", graphItem, "FillColor");
            AddColorProp(element, cat, "BorderColor", graphItem, "BorderColor");
            AddProp(element, cat, "LineWidth", GetIntValue(graphItem, "lineWidth").ToString());
            AddProp(element, cat, "BorderWidth", GetIntValue(graphItem, "borderWidth").ToString());
            AddProp(element, cat, "ColumnWidth", GetIntValue(graphItem, "columnWidth").ToString());
            AddProp(element, cat, "RollDirection", GetBoolValue(graphItem, "rollDirection", false).ToString());
            AddProp(element, cat, "MaxValue", GetFloatValue(graphItem, "maxValue").ToString("F1"));
        }

        // --- Reflection helpers ---

        private static string? GetStringValue(object obj, string propName)
        {
            var prop = obj.GetType().GetProperty(propName);
            return prop?.GetValue(obj)?.ToString();
        }

        private static int GetIntValue(object obj, string propName)
        {
            var prop = obj.GetType().GetProperty(propName);
            if (prop == null) return 0;
            var val = prop.GetValue(obj);
            if (val is int i) return i;
            if (val != null) return Convert.ToInt32(val);
            return 0;
        }

        private static int? GetIntValueOrNull(object obj, string propName)
        {
            var prop = obj.GetType().GetProperty(propName);
            if (prop == null) return null;
            var val = prop.GetValue(obj);
            if (val is int i) return i;
            if (val != null) return Convert.ToInt32(val);
            return null;
        }

        private static bool GetBoolValue(object obj, string propName, bool defaultValue)
        {
            var prop = obj.GetType().GetProperty(propName);
            if (prop == null) return defaultValue;
            return prop.GetValue(obj) is bool b && b;
        }

        private static bool? GetBoolValueOrNull(object obj, string propName)
        {
            var prop = obj.GetType().GetProperty(propName);
            if (prop == null) return null;
            return prop.GetValue(obj) is bool b ? b : null;
        }

        private static float GetFloatValue(object obj, string propName)
        {
            var prop = obj.GetType().GetProperty(propName);
            if (prop == null) return 0f;
            var val = prop.GetValue(obj);
            if (val is float f) return f;
            if (val != null) return Convert.ToSingle(val);
            return 0f;
        }

        private static double? GetDoubleValue(object obj, string propName)
        {
            var prop = obj.GetType().GetProperty(propName);
            if (prop == null) return null;
            var val = prop.GetValue(obj);
            if (val is double d) return d;
            if (val != null) return Convert.ToDouble(val);
            return null;
        }

        private static byte GetByteValue(object obj, string propName)
        {
            var prop = obj.GetType().GetProperty(propName);
            if (prop == null) return 0;
            var val = prop.GetValue(obj);
            if (val is byte b) return b;
            if (val != null) return Convert.ToByte(val);
            return 0;
        }

        private static Color? GetColorValue(object obj, string propName)
        {
            var prop = obj.GetType().GetProperty(propName);
            if (prop == null) return null;
            return prop.GetValue(obj) as Color?;
        }

        private static void AddColorProp(ThemeElement element, string category, string label,
            object obj, string propName)
        {
            var color = GetColorValue(obj, propName);
            if (color.HasValue)
                AddProp(element, category, label, ColorToHex(color.Value));
        }

        private static void AddProp(ThemeElement element, string category, string name, string? value)
        {
            element.Properties.Add(new ElementProperty
            {
                Category = category,
                Name = name,
                Value = value ?? ""
            });
        }

        private static string ColorToHex(Color c) =>
            $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        private static string GetFriendlyType(string typeName) => typeName switch
        {
            "GraphImage" => "Image",
            "GraphAnimation" => "Animation",
            "GraphStatuBar" => "Status Bar",
            "GraphDynamicBar" => "Dynamic Bar",
            "GraphArchBar" => "Arch Bar",
            "GraphLine" => "Line",
            "GraphClock" => "Clock",
            _ => typeName
        };
    }
}
