using QuickLook.Plugin.AudioViewer.Controls;
using QuickLook.Plugin.AudioViewer.Settings;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>
    /// Composes the pitch term of the spectrogram hover readout from the user's
    /// <see cref="HoverReadoutMode"/>. Pure composition over <see cref="MidiNote.Format"/>
    /// and <see cref="AxisTicks.FormatHz"/> — no pitch math of its own. Kept free of WPF
    /// so it can be unit-tested.
    /// </summary>
    internal static class HoverReadoutFormatter
    {
        /// <summary>Separator between the note and frequency terms, matching the readout's own " · " style.</summary>
        private const string Separator = "  ·  ";

        /// <summary>
        /// The pitch term for a cursor frequency: the nearest note, the raw frequency, or both.
        /// </summary>
        internal static string FormatPitch(double freq, HoverReadoutMode mode)
        {
            switch (mode)
            {
                case HoverReadoutMode.MidiNote:
                    return MidiNote.Format(freq);

                case HoverReadoutMode.Frequency:
                    return AxisTicks.FormatHz(freq);

                default:
                    // At freq <= 0 (the DC bin at the bottom of a linear-scale plot) there is
                    // no note to name, so show the frequency alone rather than a dangling "—".
                    string hz = AxisTicks.FormatHz(freq);
                    return freq <= 0 ? hz : MidiNote.Format(freq) + Separator + hz;
            }
        }
    }
}
