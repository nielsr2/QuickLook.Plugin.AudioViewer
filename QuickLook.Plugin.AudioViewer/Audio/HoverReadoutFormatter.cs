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

        /// <summary>Row label for the pitch term, which changes with the readout mode.</summary>
        private static string PitchLabel(HoverReadoutMode mode)
        {
            switch (mode)
            {
                case HoverReadoutMode.MidiNote: return "Note";
                case HoverReadoutMode.Frequency: return "Freq";
                default: return "Pitch";
            }
        }

        /// <summary>
        /// The whole hover readout. In <see cref="HoverReadoutStyle.Box"/> each value gets a
        /// label on its own line; in Compact they run together on one line as bare values.
        /// Returns an empty string when every field is switched off, which the caller treats
        /// as "hide the readout".
        /// </summary>
        /// <remarks>
        /// "Level" is a relative figure, not dBFS — see <see cref="FormatLevel"/>.
        /// </remarks>
        internal static string FormatReadout(
            double timeSeconds, int timeDecimals,
            double freq, HoverReadoutMode pitchMode,
            double magnitude, AmplitudeScale ampScale,
            HoverReadoutStyle style,
            bool showTime, bool showPitch, bool showLevel)
        {
            var labels = new System.Collections.Generic.List<string>(3);
            var values = new System.Collections.Generic.List<string>(3);

            if (showTime)
            {
                labels.Add("Time");
                values.Add(AxisTicks.FormatTime(timeSeconds, timeDecimals));
            }
            if (showPitch)
            {
                labels.Add(PitchLabel(pitchMode));
                values.Add(FormatPitch(freq, pitchMode));
            }
            if (showLevel)
            {
                labels.Add("Level");
                values.Add(FormatLevel(magnitude, ampScale));
            }

            if (values.Count == 0) return string.Empty;
            if (style == HoverReadoutStyle.Compact) return string.Join(Separator, values);

            // Labels are padded to a fixed width so the values line up in the monospace
            // font, matching how the file-info overlay lays out its rows.
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < labels.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(labels[i].PadRight(6)).Append(values[i]);
            }
            return sb.ToString();
        }

        /// <summary>Below this much of the box still on-screen, it is moved regardless.</summary>
        private const double MinVisible = 24;

        /// <summary>
        /// Place the readout next to the cursor.
        /// </summary>
        /// <param name="keepInView">
        /// When true, the box flips to the other side of the cursor as soon as it would
        /// overflow, and is clamped if it fits on neither side.
        ///
        /// When false (the default) it simply keeps a constant offset from the cursor and
        /// is left to clip at the pane edge. Constantly re-anchoring is more distracting
        /// than a partly clipped box: the readout jumps sides while the cursor is moving
        /// smoothly, so the numbers you are reading relocate under you. It still steps in
        /// once the box would be effectively invisible.
        /// </param>
        internal static void PlaceBox(
            double cursorX, double cursorY, double boxW, double boxH,
            double paneW, double paneH, double gap, bool keepInView,
            out double x, out double y)
        {
            x = cursorX + gap;
            y = cursorY + gap;

            if (keepInView)
            {
                if (x + boxW > paneW && cursorX - gap - boxW >= 0) x = cursorX - gap - boxW;
                if (y + boxH > paneH && cursorY - gap - boxH >= 0) y = cursorY - gap - boxH;
                if (x + boxW > paneW) x = paneW - boxW;
                if (y + boxH > paneH) y = paneH - boxH;
            }
            else
            {
                if (x > paneW - MinVisible) x = cursorX - gap - boxW;
                if (y > paneH - MinVisible) y = cursorY - gap - boxH;
            }

            if (x < 0) x = 0;
            if (y < 0) y = 0;
        }
    }
}
