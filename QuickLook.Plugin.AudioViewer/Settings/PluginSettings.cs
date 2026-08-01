using System;
using QuickLook.Common.Helpers;

namespace QuickLook.Plugin.AudioViewer.Settings
{
    public static class PluginSettings
    {
        public const string Domain = "QuickLook.Plugin.AudioViewer";

        public static bool AutoPlay
        {
            get => SettingHelper.Get("AutoPlay", false, Domain);
            set => SettingHelper.Set("AutoPlay", value, Domain);
        }

        /// <summary>Show the file-info overlay (format, channels, bitrate, …).</summary>
        /// <remarks>Defaults to true: the default HoverIcon style is an unobtrusive
        /// info pill, so the overlay is available out of the box without getting in the way.</remarks>
        public static bool ShowFileInfo
        {
            get => SettingHelper.Get("ShowFileInfo", true, Domain);
            set => SettingHelper.Set("ShowFileInfo", value, Domain);
        }

        /// <summary>Remembered file-info overlay offset (px) from its bottom-right anchor.</summary>
        public static int InfoPosX
        {
            get => SettingHelper.Get("InfoPosX", 0, Domain);
            set => SettingHelper.Set("InfoPosX", value, Domain);
        }

        public static int InfoPosY
        {
            get => SettingHelper.Get("InfoPosY", 0, Domain);
            set => SettingHelper.Set("InfoPosY", value, Domain);
        }

        /// <summary>
        /// Remembered size of the resizable settings popup. Always re-clamped through
        /// <see cref="SettingsPaneMetrics"/> on load: the stored value may have come from
        /// a larger monitor than the one currently in use.
        /// </summary>
        public static int SettingsPaneWidth
        {
            get => SettingHelper.Get("SettingsPaneWidth", (int)SettingsPaneMetrics.DefaultWidth, Domain);
            set => SettingHelper.Set("SettingsPaneWidth", value, Domain);
        }

        public static int SettingsPaneHeight
        {
            get => SettingHelper.Get("SettingsPaneHeight", 0, Domain); // 0 = "fit the screen"
            set => SettingHelper.Set("SettingsPaneHeight", value, Domain);
        }

        // Enum settings are stored as their string name: SettingHelper round-trips
        // primitives via Convert.ChangeType, which cannot parse enum names (it would
        // silently fall back to the default). Enum.TryParse on the stored name restores
        // them. Property names differ from the enum type names to avoid C#'s
        // "Color Color" resolution ambiguity.

        public static LayoutMode Layout
        {
            get => ParseEnum("Layout", LayoutMode.Stacked);
            set => SettingHelper.Set("Layout", value.ToString(), Domain);
        }

        public static FrequencyScale FreqScale
        {
            get => ParseEnum("FreqScale", FrequencyScale.Linear);
            set => SettingHelper.Set("FreqScale", value.ToString(), Domain);
        }

        public static AmplitudeScale AmpScale
        {
            get => ParseEnum("AmpScale", AmplitudeScale.Decibel);
            set => SettingHelper.Set("AmpScale", value.ToString(), Domain);
        }

        public static SpectrogramColormap Colormap
        {
            get => ParseEnum("Colormap", SpectrogramColormap.Magma);
            set => SettingHelper.Set("Colormap", value.ToString(), Domain);
        }

        /// <summary>How the file-info overlay is presented (hover pill vs. draggable box).</summary>
        public static FileInfoDisplay FileInfoDisplay
        {
            get => ParseEnum("FileInfoDisplay", FileInfoDisplay.HoverIcon);
            set => SettingHelper.Set("FileInfoDisplay", value.ToString(), Domain);
        }

        /// <summary>How a stereo file's waveform is drawn: two lanes, mono-average (default), or peak-union.
        /// Named differently from its enum type to avoid the "Color Color" resolution ambiguity.</summary>
        public static StereoWaveformMode StereoMode
        {
            get => ParseEnum("StereoMode", StereoWaveformMode.MonoAverage);
            set => SettingHelper.Set("StereoMode", value.ToString(), Domain);
        }

        /// <summary>Spectrogram hover readout: nearest MIDI note, raw frequency, or both (default).</summary>
        public static HoverReadoutMode HoverReadoutMode
        {
            get => ParseEnum("HoverReadoutMode", HoverReadoutMode.Both);
            set => SettingHelper.Set("HoverReadoutMode", value.ToString(), Domain);
        }

        /// <summary>
        /// Show milliseconds in the transport readout (00:12.345 rather than 00:12).
        /// On by default — a preview is often used to find an exact moment, and whole
        /// seconds are too coarse for that on short files.
        /// </summary>
        public static bool ShowMilliseconds
        {
            get => SettingHelper.Get("ShowMilliseconds", true, Domain);
            set => SettingHelper.Set("ShowMilliseconds", value, Domain);
        }

        /// <summary>FFT window length (power of two): 512 / 1024 / 2048 / 4096.</summary>
        public static int FftSize
        {
            get => SettingHelper.Get("FftSize", 1024, Domain);
            set => SettingHelper.Set("FftSize", value, Domain);
        }

        /// <summary>Top of the frequency axis in Hz; 0 means "full" (Nyquist).</summary>
        public static int MaxFrequencyHz
        {
            get => SettingHelper.Get("MaxFrequencyHz", 0, Domain);
            set => SettingHelper.Set("MaxFrequencyHz", value, Domain);
        }

        /// <summary>Contrast floor in whole dB below the peak (negative), e.g. -90.</summary>
        public static int DbFloor
        {
            get => SettingHelper.Get("DbFloor", -90, Domain);
            set => SettingHelper.Set("DbFloor", value, Domain);
        }

        /// <summary>When the colormap is inverted: never, always, or only in light mode.</summary>
        public static ReverseColormapMode ReverseMode
        {
            get
            {
                // Carry the pre-1.6 bool forward: someone who had Reverse ticked keeps an
                // inverted map rather than silently reverting to Never on upgrade.
                var fallback = SettingHelper.Get("ReverseColormap", false, Domain)
                    ? ReverseColormapMode.Always
                    : ReverseColormapMode.Never;
                return ParseEnum("ReverseMode", fallback);
            }
            set => SettingHelper.Set("ReverseMode", value.ToString(), Domain);
        }

        /// <summary>
        /// Magnify a quiet file's waveform to fill its lane on load. On by default: a flat
        /// line that hides real content is a worse first impression than a correctly scaled
        /// waveform, and the on-screen badge says when it is active.
        /// </summary>
        /// <remarks>
        /// Only the policy is persisted, never the gain value. A gain of 18x belongs to one
        /// quiet file; carrying it to the next normally-levelled file would render it
        /// clipped and broken with no obvious cause. Per-file storage is not practical
        /// either — SettingHelper names each XML element after its key, and a file path
        /// cannot be an element name.
        /// </remarks>
        public static bool WaveformAutoFit
        {
            get => SettingHelper.Get("WaveformAutoFit", true, Domain);
            set => SettingHelper.Set("WaveformAutoFit", value, Domain);
        }

        /// <summary>Mark waveform columns that the lane edge cut off.</summary>
        public static bool WaveformShowClipping
        {
            get => SettingHelper.Get("WaveformShowClipping", true, Domain);
            set => SettingHelper.Set("WaveformShowClipping", value, Domain);
        }

        /// <summary>How densely the time axis is marked.</summary>
        public static TimeAxisDensity TimeAxisDensity
        {
            get => ParseEnum("TimeAxisDensity", TimeAxisDensity.Normal);
            set => SettingHelper.Set("TimeAxisDensity", value.ToString(), Domain);
        }

        /// <summary>Minimum pixels between time-axis labels, which selects the tick step.</summary>
        public static double TimeAxisMinPxPerLabel
        {
            get
            {
                switch (TimeAxisDensity)
                {
                    case TimeAxisDensity.Sparse: return 110;
                    case TimeAxisDensity.Dense: return 40;
                    default: return 64;
                }
            }
        }

        /// <summary>
        /// Reposition the hover readout so it stays inside the pane. Off by default: the
        /// constant offset from the cursor is easier to read than a box that jumps sides,
        /// and it is only moved when it would otherwise be entirely out of view.
        /// </summary>
        public static bool HoverKeepInView
        {
            get => SettingHelper.Get("HoverKeepInView", false, Domain);
            set => SettingHelper.Set("HoverKeepInView", value, Domain);
        }

        /// <summary>Show an on-screen slider for the waveform's vertical zoom.</summary>
        public static bool ShowGainSlider
        {
            get => SettingHelper.Get("ShowGainSlider", false, Domain);
            set => SettingHelper.Set("ShowGainSlider", value, Domain);
        }

        /// <summary>Hover readout layout: labelled rows (default) or a single compact line.</summary>
        public static HoverReadoutStyle HoverStyle
        {
            get => ParseEnum("HoverStyle", HoverReadoutStyle.Box);
            set => SettingHelper.Set("HoverStyle", value.ToString(), Domain);
        }

        /// <summary>Include the time row in the hover readout.</summary>
        public static bool HoverShowTime
        {
            get => SettingHelper.Get("HoverShowTime", true, Domain);
            set => SettingHelper.Set("HoverShowTime", value, Domain);
        }

        /// <summary>Include the note/frequency row in the hover readout.</summary>
        public static bool HoverShowPitch
        {
            get => SettingHelper.Get("HoverShowPitch", true, Domain);
            set => SettingHelper.Set("HoverShowPitch", value, Domain);
        }

        /// <summary>Include the level row in the hover readout.</summary>
        public static bool HoverShowLevel
        {
            get => SettingHelper.Get("HoverShowLevel", true, Domain);
            set => SettingHelper.Set("HoverShowLevel", value, Domain);
        }

        /// <summary>Light/dark palette selection; defaults to following QuickLook.</summary>
        public static ThemeMode ThemeMode
        {
            get => ParseEnum("ThemeMode", ThemeMode.FollowHost);
            set => SettingHelper.Set("ThemeMode", value.ToString(), Domain);
        }

        /// <summary>
        /// Waveform colour as an ARGB hex string, or "" to follow the theme's own blue.
        /// Stored as text because SettingHelper round-trips through Convert.ChangeType,
        /// which cannot handle a Color; never store null (Set would throw on GetType).
        /// </summary>
        public static string WaveColorHex
        {
            get => SettingHelper.Get("WaveColorHex", string.Empty, Domain) ?? string.Empty;
            set => SettingHelper.Set("WaveColorHex", value ?? string.Empty, Domain);
        }

        /// <summary>Gamma applied to the normalized magnitude before coloring; &lt;1 lifts quiet detail.</summary>
        public static double Gamma
        {
            get => SettingHelper.Get("Gamma", 1.0, Domain);
            set => SettingHelper.Set("Gamma", value, Domain);
        }

        /// <summary>Show the interactive MIDI-note overlay on the spectrogram (draw/edit/export).</summary>
        public static bool NoteLayerEnabled
        {
            get => SettingHelper.Get("NoteLayerEnabled", false, Domain);
            set => SettingHelper.Set("NoteLayerEnabled", value, Domain);
        }

        /// <summary>Tempo (BPM) written into exported MIDI; only affects a DAW's bar/beat labeling.</summary>
        public static int NoteExportBpm
        {
            get => SettingHelper.Get("NoteExportBpm", 120, Domain);
            set => SettingHelper.Set("NoteExportBpm", value, Domain);
        }

        private static TEnum ParseEnum<TEnum>(string key, TEnum failsafe) where TEnum : struct
        {
            var stored = SettingHelper.Get(key, failsafe.ToString(), Domain);
            return Enum.TryParse(stored, true, out TEnum value) ? value : failsafe;
        }
    }
}
