using System.Windows;
using System.Windows.Media;
using QuickLook.Common.Helpers;
using QuickLook.Common.Plugin;

namespace QuickLook.Plugin.AudioViewer.Theming
{
    /// <summary>
    /// Resolves the light/dark palette for the viewer chrome. QuickLook may report a
    /// concrete theme; when it reports <see cref="Themes.None"/> we fall back to the OS
    /// app theme, and to dark if even that is unavailable (the historical look).
    ///
    /// Only the chrome, text, waveform, playhead and info overlay are themed. The
    /// spectrogram image is left as-is: its colormaps (Magma/Viridis) are conventionally
    /// dark-based and read fine on either background, and the image covers its pane.
    /// </summary>
    internal static class ThemePalette
    {
        internal static bool ResolveIsDark(Themes theme)
        {
            if (theme == Themes.Light) return false;
            if (theme == Themes.Dark) return true;
            try { return OSThemeHelper.AppsUseDarkTheme(); }
            catch { return true; }
        }

        /// <summary>Populate the brush resource keys referenced by the XAML via DynamicResource.</summary>
        internal static void Apply(ResourceDictionary res, bool isDark)
        {
            void Set(string key, string hex) => res[key] = Frozen(hex);
            if (isDark)
            {
                Set("AppBackground", "#FF1E1E1E");
                Set("PrimaryText", "#FFDDDDDD");
                Set("SecondaryText", "#FFBBBBBB");
                Set("PopupBackground", "#FF2A2A2A");
                Set("PopupBorder", "#FF565656");
                Set("OverlayBackground", "#CC000000");
                Set("OverlayBorder", "#40FFFFFF");
                Set("OverlayText", "#FFEDEDED");
                Set("OverlayClose", "#FFCCCCCC");
                Set("Playhead", "#FFFF5252");
            }
            else
            {
                Set("AppBackground", "#FFF5F5F5");
                Set("PrimaryText", "#FF1B1B1B");
                Set("SecondaryText", "#FF555555");
                Set("PopupBackground", "#FFFDFDFD");
                Set("PopupBorder", "#FFCCCCCC");
                Set("OverlayBackground", "#E6FFFFFF");
                Set("OverlayBorder", "#40000000");
                Set("OverlayText", "#FF1B1B1B");
                Set("OverlayClose", "#FF555555");
                Set("Playhead", "#FFD32F2F");
            }
        }

        // Colors consumed directly by non-XAML drawing (WaveformView, and the axis
        // controls in Item 9). Kept here so the palette stays single-sourced.
        internal static Color WaveformWave(bool isDark) => Hex(isDark ? "#FF4FC3F7" : "#FF0277BD");
        internal static Color Playhead(bool isDark) => Hex(isDark ? "#FFFF5252" : "#FFD32F2F");
        internal static Color WaveformBackground(bool isDark) => Hex(isDark ? "#FF1E1E1E" : "#FFF5F5F5");
        internal static Color AxisText(bool isDark) => Hex(isDark ? "#FFBBBBBB" : "#FF555555");
        internal static Color AxisLine(bool isDark) => Hex(isDark ? "#FF6E6E6E" : "#FFAAAAAA");

        private static SolidColorBrush Frozen(string hex)
        {
            var b = new SolidColorBrush(Hex(hex));
            b.Freeze();
            return b;
        }

        private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    }
}
