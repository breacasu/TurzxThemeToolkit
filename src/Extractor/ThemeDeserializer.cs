using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;

namespace TurzxThemeToolkit.Extractor
{
    /// <summary>
    /// Deserializes a .turtheme file by loading the TURZX assembly and using
    /// the real BinaryFormatter to reconstruct the Theme object graph.
    /// This gives access to ALL element properties (font, position, colors, etc.)
    /// that are not available from the signature-scanning approach.
    /// </summary>
    public static class ThemeDeserializer
    {
        /// <summary>
        /// Loads the TURZX assembly, deserializes the theme, and returns the
        /// deserialized Theme object (typed as object since we reference TURZX
        /// only via reflection).
        ///
        /// Set up an AssemblyResolve handler BEFORE calling this if the theme
        /// references a different version of UsbMonitorL than the one in TURZX.exe.
        /// </summary>
        public static object? Deserialize(string themeFilePath, string turzxExePath)
        {
            // Load the TURZX assembly (contains UsbMonitorL types)
            var turzxAssembly = Assembly.LoadFrom(turzxExePath);
            var themeType = turzxAssembly.GetType("UsbMonitorL.Theme");
            if (themeType == null)
                throw new InvalidOperationException("UsbMonitorL.Theme type not found in TURZX.exe");

            // Hook up assembly resolution so any version of UsbMonitorL resolves
            // to the loaded TURZX assembly (themes may reference a specific version).
            AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
            _resolveTarget = turzxAssembly;
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            // Deserialize
            var formatter = new BinaryFormatter();
            using var fs = File.OpenRead(themeFilePath);
            return formatter.Deserialize(fs);
        }

        private static Assembly? _resolveTarget;

        private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            if (_resolveTarget == null) return null;
            // Match by simple name (strip version/culture/token from the full name)
            var simpleName = args.Name.Split(',')[0];
            var targetName = _resolveTarget.GetName().Name;
            if (string.Equals(simpleName, targetName, StringComparison.OrdinalIgnoreCase))
                return _resolveTarget;
            // Also resolve System.Drawing, WindowsBase etc. from the TURZX directory
            // (these are usually already loaded, but just in case)
            return null;
        }
    }
}
