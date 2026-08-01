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

        /// <summary>
        /// The level term for one FFT bin, in the units of the active amplitude scale.
        /// </summary>
        /// <remarks>
        /// The magnitude is the raw modulus of a windowed FFT bin — not normalised by the
        /// window energy or the FFT size, and with no reference level — so the dB figures
        /// here are <i>relative</i>, not dBFS. They are also deliberately not clamped to the
        /// contrast floor: the render's black point only governs pixel colour, so a very
        /// quiet bin can legitimately read far below it.
        /// </remarks>
        internal static string FormatLevel(double magnitude, AmplitudeScale scale)
        {
            if (magnitude < 0) magnitude = 0;
            switch (scale)
            {
                case AmplitudeScale.Linear:
                    return magnitude.ToString("0.000");

                case AmplitudeScale.DecibelSquared:
                    // Must match RenderToPixels' half-gain branch, or the readout would
                    // disagree with the pixel the cursor is over.
                    return $"{System.Math.Round(10.0 * System.Math.Log10(magnitude + 1e-12))} dB";

                default:
                    return $"{System.Math.Round(20.0 * System.Math.Log10(magnitude + 1e-12))} dB";
            }
        }
    }
}
