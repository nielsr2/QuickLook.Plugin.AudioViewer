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

        /// <summary>Spectrogram hover readout: nearest MIDI note (default) vs. raw frequency.</summary>
        public static HoverReadoutMode HoverReadoutMode
        {
            get => ParseEnum("HoverReadoutMode", HoverReadoutMode.MidiNote);
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

        private static TEnum ParseEnum<TEnum>(string key, TEnum failsafe) where TEnum : struct
        {
            var stored = SettingHelper.Get(key, failsafe.ToString(), Domain);
            return Enum.TryParse(stored, true, out TEnum value) ? value : failsafe;
        }
    }
}
