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

        /// <summary>Which part of a note's horizontal extent a pixel x falls in.</summary>
        public enum NoteEdge { Outside, Left, Body, Right }

        /// <summary>
        /// Classify a pixel x against a note drawn from <paramref name="leftX"/>..<paramref name="rightX"/>,
        /// with an <paramref name="edgePx"/> grab tolerance (assumes the y is already known to be inside
        /// the note's vertical band). Edges win over the body so a resize is reachable even on a narrow
        /// note; the same tolerance pads the outside so thin notes and their edges stay grabbable.
        /// </summary>
        public static NoteEdge HitEdge(double px, double leftX, double rightX, double edgePx)
        {
            if (px < leftX - edgePx || px > rightX + edgePx) return NoteEdge.Outside;
            if (Math.Abs(px - leftX) <= edgePx) return NoteEdge.Left;
            if (Math.Abs(px - rightX) <= edgePx) return NoteEdge.Right;
            if (px >= leftX && px <= rightX) return NoteEdge.Body;
            return NoteEdge.Outside;
        }

        /// <summary>
        /// Resolve a draw drag into a start time + duration: the fixed <paramref name="anchorSeconds"/>
        /// press point and the current cursor time bound the span, supporting a leftward drag (start is
        /// the smaller of the two), clamped to a minimum duration so a bare click still makes a note.
        /// </summary>
        public static void DrawSpan(double anchorSeconds, double currentSeconds, double minDurationSeconds,
            out double startSeconds, out double durationSeconds)
        {
            double start = Math.Min(anchorSeconds, currentSeconds);
            double end = Math.Max(anchorSeconds, currentSeconds);
            if (start < 0) start = 0;
            double dur = end - start;
            if (dur < minDurationSeconds) dur = minDurationSeconds;
            startSeconds = start;
            durationSeconds = dur;
        }

        /// <summary>
        /// Move a note's left edge to <paramref name="newStartSeconds"/> while keeping
        /// <paramref name="fixedEndSeconds"/> pinned, honoring a minimum duration and a 0 s floor.
        /// </summary>
        public static void ResizeLeft(double fixedEndSeconds, double newStartSeconds, double minDurationSeconds,
            out double startSeconds, out double durationSeconds)
        {
            double maxStart = fixedEndSeconds - minDurationSeconds;
            double start = newStartSeconds;
            if (start > maxStart) start = maxStart;
            if (start < 0) start = 0;
            startSeconds = start;
            durationSeconds = fixedEndSeconds - start;
        }

        /// <summary>
        /// New duration when a note's right edge is dragged to <paramref name="newEndSeconds"/> while its
        /// <paramref name="fixedStartSeconds"/> stays put, clamped to a minimum duration.
        /// </summary>
        public static double ResizeRight(double fixedStartSeconds, double newEndSeconds, double minDurationSeconds)
        {
            double dur = newEndSeconds - fixedStartSeconds;
            return dur < minDurationSeconds ? minDurationSeconds : dur;
        }

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
