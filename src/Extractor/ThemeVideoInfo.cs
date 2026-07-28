namespace TurzxThemeToolkit.Extractor
{
    /// <summary>
    /// Information about a video referenced by a .turtheme file.
    ///
    /// Videos are NOT embedded in the .turtheme — only file paths are stored
    /// as BinaryFormatter BinaryObjectString records.
    ///
    /// The Theme object stores up to 4 video-related string fields:
    ///   - videoPath      — original absolute Windows path (e.g. "D:\8.8\video\AMD.mp4")
    ///   - o_videoPath    — original absolute Linux/device path (e.g. "/mnt/UDISK/video/AMD.mp4")
    ///   - videoTargetPath — optional install target path
    ///   - videoName      — bare filename (e.g. "AMD.mp4")
    ///
    /// At runtime, TURZX IGNORES the stored absolute paths and reconstructs
    /// the actual file location from:
    ///   Path.Combine(AppDirPath, "video", resolution, videoName)
    /// i.e. &lt;turzx-root&gt;\video\&lt;resolution&gt;\&lt;videoName&gt;
    /// (see Monitor.cs AppVideoPath and ꮳ.cs line 3699).
    /// </summary>
    public class ThemeVideoInfo
    {
        /// <summary>Original absolute Windows path stored in the theme (not used by TURZX at runtime).</summary>
        public string? VideoPath { get; set; }

        /// <summary>Original absolute Linux/device path stored in the theme (not used by TURZX at runtime).</summary>
        public string? OVideoPath { get; set; }

        /// <summary>Optional target path used during theme installation.</summary>
        public string? VideoTargetPath { get; set; }

        /// <summary>Bare filename (e.g. "AMD.mp4") — this is the only field TURZX actually uses at runtime.</summary>
        public string? VideoName { get; set; }

        /// <summary>True if a video file is referenced by this theme.</summary>
        public bool HasVideo => !string.IsNullOrEmpty(VideoName) ||
                                !string.IsNullOrEmpty(VideoPath) ||
                                !string.IsNullOrEmpty(OVideoPath);

        /// <summary>True if the referenced video file exists in the local video\&lt;res&gt; folder.</summary>
        public bool ExistsInVideoFolder { get; set; }

        /// <summary>Full path to the video file in the local TURZX installation, as TURZX would resolve it.</summary>
        public string? LocalVideoPath { get; set; }

        /// <summary>Frame rate stored in the Theme (0 if not set / not a video theme).</summary>
        public int FrameRate { get; set; }

        /// <summary>Human-readable summary for display.</summary>
        public string Summary
        {
            get
            {
                if (!HasVideo)
                    return "No video";
                var parts = new List<string>();
                if (VideoName != null)
                    parts.Add(VideoName);
                if (ExistsInVideoFolder)
                    parts.Add("found");
                else if (LocalVideoPath != null)
                    parts.Add("missing");
                return string.Join(" — ", parts);
            }
        }
    }
}
