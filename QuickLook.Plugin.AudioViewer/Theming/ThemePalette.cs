using System.Windows;
using System.Windows.Media;
using QuickLook.Common.Helpers;
using QuickLook.Common.Plugin;
using QuickLook.Plugin.AudioViewer.Settings;

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

        /// <summary>
        /// Resolve the effective palette, letting an explicit user choice override the host.
        /// </summary>
        internal static bool ResolveIsDark(ThemeMode mode, Themes hostTheme)
        {
            switch (mode)
            {
                case ThemeMode.Light: return false;
                case ThemeMode.Dark: return true;
                default: return ResolveIsDark(hostTheme);
            }
        }

        /// <summary>Whether the colormap should be inverted for this mode and palette.</summary>
        internal static bool ResolveReverse(ReverseColormapMode mode, bool isDark)
        {
            switch (mode)
            {
                case ReverseColormapMode.Always: return true;
                case ReverseColormapMode.AutoLight: return !isDark;
                default: return false;
            }
        }

        /// <summary>
        /// The waveform colour choices offered in settings. A null hex means "follow the
        /// theme", i.e. <see cref="WaveformWave"/>; the rest are absolute and stay put in
        /// either palette, which is what makes them a deliberate user choice.
        /// Deliberately excludes red — it would be mistaken for the playhead.
        /// </summary>
        internal static readonly (string Name, string Hex)[] WavePalette =
        {
            ("Theme default", null),
            ("Blue",          "#FF4FC3F7"),
            ("Teal",          "#FF26C6DA"),
            ("Green",         "#FF66BB6A"),
            ("Lime",          "#FFAEEA00"),
            ("Amber",         "#FFFFB300"),
            ("Orange",        "#FFFF8A3D"),
            ("Purple",        "#FFAB47BC"),
            ("Magenta",       "#FFEC407A"),
            ("Gray",          "#FF9E9E9E")
        };

        /// <summary>
        /// The waveform colour to draw with: the user's pick if one is set and parses,
        /// otherwise the theme's own. A corrupt or hand-edited value falls back rather
        /// than throwing, since this runs during control construction.
        /// </summary>
        internal static Color ResolveWaveColor(string hex, bool isDark)
        {
            if (!string.IsNullOrWhiteSpace(hex))
            {
                try { return Hex(hex); }
                catch { /* fall through to the theme default */ }
            }
            return WaveformWave(isDark);
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

        // Note-overlay colors. A cyan hue (distinct from the warm Magma/Viridis colormaps) so
        // notes read on either map; the fill carries ~0x80 alpha so the spectrogram shows through.
        // The selected note switches to a contrasting amber for an unmistakable focus cue.
        internal static Color NoteFill(bool isDark) => Hex(isDark ? "#8022D3EE" : "#800E7490");
        internal static Color NoteBorder(bool isDark) => Hex(isDark ? "#FF67E8F9" : "#FF0E7490");
        internal static Color NoteSelected(bool isDark) => Hex(isDark ? "#FFFDE047" : "#FFB45309");

        private static SolidColorBrush Frozen(string hex)
        {
            var b = new SolidColorBrush(Hex(hex));
            b.Freeze();
            return b;
        }

        private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    }
}
