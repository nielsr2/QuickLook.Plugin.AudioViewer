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

        /// <summary>Invert the colormap so high magnitudes map to its dark end (and vice versa).</summary>
        public static bool ReverseColormap
        {
            get => SettingHelper.Get("ReverseColormap", false, Domain);
            set => SettingHelper.Set("ReverseColormap", value, Domain);
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
