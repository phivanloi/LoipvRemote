using System;
using System.IO;
using WeifenLuo.WinFormsUI.Docking;
using System.Linq;
using System.Runtime.Versioning;

namespace LoipvRemote.Themes
{
    [SupportedOSPlatform("windows")]
    public static class ThemeSerializer
    {
        /// <summary>
        /// Save the theme to file, name property is used as filename
        /// The baseTheme is used as a template, by copy that file and rewrite the extpalette values
        /// </summary>
        /// <param name="themeToSave"></param>
        /// <param name="baseTheme"></param>
        public static void SaveToXmlFile(ThemeInfo themeToSave, ThemeInfo baseTheme)
        {
            if (string.IsNullOrWhiteSpace(baseTheme.URI) || baseTheme.URI.Contains("../") || baseTheme.URI.Contains(@"..\"))
                throw new ArgumentException("Invalid file path");
            if (themeToSave.Name == null || themeToSave.Name.Contains("../") || themeToSave.Name.Contains(@"..\"))
                throw new ArgumentException("Invalid file path");
            string oldURI = baseTheme.URI;
            string? directoryName = Path.GetDirectoryName(oldURI);
            if (string.IsNullOrEmpty(directoryName))
                throw new ArgumentException("Invalid file path");
            string toSaveURI = Path.Combine(directoryName, $"{themeToSave.Name}.vstheme");
            File.Copy(baseTheme.URI, toSaveURI);
            themeToSave.URI = toSaveURI;
        }

        public static void DeleteFile(ThemeInfo themeToDelete)
        {
            if (themeToDelete.URI == null || themeToDelete.URI.Contains("../") || themeToDelete.URI.Contains(@"..\"))
                throw new ArgumentException("Invalid file path");
            File.Delete(themeToDelete.URI);
        }

        /// <summary>
        /// Takes a theme in memory and update the color values that the user might have changed
        /// </summary>
        /// <param name="themeToUpdate"></param>
        public static void UpdateThemeXMLValues(ThemeInfo themeToUpdate)
        {
            if (themeToUpdate.URI == null || themeToUpdate.URI.Contains("../") || themeToUpdate.URI.Contains(@"..\"))
                throw new ArgumentException("Invalid file path");
            byte[] bytesIn = File.ReadAllBytes(themeToUpdate.URI);
            LoipvRemotePaletteManipulator manipulator = new(bytesIn, themeToUpdate.ExtendedPalette);
            byte[] bytesOut = manipulator.mergePalette(themeToUpdate.ExtendedPalette);
            File.WriteAllBytes(themeToUpdate.URI, bytesOut);
        }

        /// <summary>
        /// Load a theme form an xml file
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="defaultTheme"></param>
        /// <returns></returns>
        public static ThemeInfo LoadFromXmlFile(string filename, ThemeInfo? defaultTheme = null)
        {
            if (filename == null || filename.Contains("../") || filename.Contains(@"..\"))
                throw new ArgumentException("Invalid file path");
            byte[] bytes = File.ReadAllBytes(filename);
            //Load the dockpanel part
            LoipvRemoteThemeBase themeBaseLoad = new(bytes);
            //Cause we cannot default the theme for the default theme
            LoipvRemotePaletteManipulator extColorLoader = new(bytes, defaultTheme?.ExtendedPalette);
            ThemeInfo loadedTheme = new(Path.GetFileNameWithoutExtension(filename), themeBaseLoad, filename,
                                            VisualStudioToolStripExtender.VsVersion.Vs2015, extColorLoader.getColors());
            if (new[] { "darcula", "vs2015blue", "vs2015dark", "vs2015light" }.Contains(
                                                                                      Path
                                                                                          .GetFileNameWithoutExtension(filename))
            )
            {
                loadedTheme.IsThemeBase = true;
            }

            loadedTheme.IsExtendable = true;
            return loadedTheme;
        }

    }
}
