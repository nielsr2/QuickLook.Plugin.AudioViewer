using System;
using QuickLook.Plugin.AudioViewer.Settings;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>
    /// Pure mapping between note (time, pitch) and overlay pixels. Uses the SAME
    /// <see cref="FrequencyScaleMapper"/> as the spectrogram image and frequency ruler,
    /// so notes line up on any scale (Linear/Log/Mel) and any max-frequency. WPF-free
    /// (returns plain doubles) so it is unit-tested.
    /// </summary>
    internal static class NoteGeometry
    {
        // A note occupies the semitone band [f*2^(-1/24), f*2^(+1/24)] around its pitch.
        private const double SemitoneHalfExponent = 1.0 / 24.0;

        /// <summary>Start/end second -> x pixel across the full timeline width.</summary>
        public static double TimeToX(double seconds, double durationSeconds, double width)
            => durationSeconds <= 0 ? 0 : Clamp01(seconds / durationSeconds) * width;

        /// <summary>x pixel -> time in seconds.</summary>
        public static double XToTime(double x, double width, double durationSeconds)
            => width <= 0 ? 0 : Clamp01(x / width) * durationSeconds;

        /// <summary>y pixel -> nearest MIDI note under the current vertical scale.</summary>
        public static int YToMidi(double y, FrequencyScale scale, double fMin, double fMax, double height)
        {
            double frac = height <= 0 ? 0 : Clamp01(1.0 - y / height); // y=0 (top) is the highest frequency
            double freq = FrequencyScaleMapper.FractionToFrequency(frac, scale, fMin, fMax);
            return MidiNote.NearestNoteNumber(freq);
        }

        /// <summary>Center y pixel of a MIDI note under the current vertical scale.</summary>
        public static double MidiToY(int midi, FrequencyScale scale, double fMin, double fMax, double height)
        {
            double u = FrequencyScaleMapper.FrequencyToFraction(MidiNote.NoteFrequency(midi), scale, fMin, fMax);
            return (1.0 - u) * height;
        }

        /// <summary>
        /// Vertical pixel band [yTop, yBottom] a note's semitone occupies. Constant height on
        /// the Log scale (even piano-roll rows); grows with pitch on Linear — always matching
        /// the spectrogram underneath.
        /// </summary>
        public static void NoteYBounds(int midi, FrequencyScale scale, double fMin, double fMax, double height,
            out double yTop, out double yBottom)
        {
            double f = MidiNote.NoteFrequency(midi);
            double fLo = f * Math.Pow(2.0, -SemitoneHalfExponent);
            double fHi = f * Math.Pow(2.0, +SemitoneHalfExponent);
            double uLo = FrequencyScaleMapper.FrequencyToFraction(fLo, scale, fMin, fMax);
            double uHi = FrequencyScaleMapper.FrequencyToFraction(fHi, scale, fMin, fMax);
            yTop = (1.0 - uHi) * height;    // higher frequency -> smaller y
            yBottom = (1.0 - uLo) * height;
        }

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
