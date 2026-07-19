using System;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>
    /// Pure helper mapping a frequency to the nearest standard MIDI note under
    /// 12-tone equal temperament (A4 = MIDI 69 = 440 Hz, middle C = C4 = MIDI 60).
    /// Used by the spectrogram hover readout to show a musical pitch instead of a
    /// raw frequency. Kept free of WPF so it can be unit-tested.
    /// </summary>
    internal static class MidiNote
    {
        // Pitch classes with sharps, indexed by (midi % 12); C is 0.
        private static readonly string[] PitchClasses =
            { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        /// <summary>Nearest MIDI note number for a frequency, clamped to [0,127]; -1 for freq &lt;= 0.</summary>
        internal static int NearestNoteNumber(double freq)
        {
            if (freq <= 0) return -1;
            int midi = (int)Math.Round(69 + 12 * Math.Log(freq / 440.0, 2));
            if (midi < 0) midi = 0; else if (midi > 127) midi = 127;
            return midi;
        }

        /// <summary>Exact frequency (Hz) of a MIDI note under 12-TET.</summary>
        internal static double NoteFrequency(int midi) => 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);

        /// <summary>Note name + octave, e.g. midi 69 -&gt; "A4", 60 -&gt; "C4", 61 -&gt; "C#4".</summary>
        internal static string NoteName(int midi) => PitchClasses[midi % 12] + ((midi / 12) - 1);

        /// <summary>Cents deviation of a frequency from the given MIDI note (negative carries its own sign).</summary>
        internal static int Cents(double freq, int midi) =>
            (int)Math.Round(1200 * Math.Log(freq / NoteFrequency(midi), 2));

        /// <summary>Format a frequency as the nearest note plus cents deviation, e.g. "E7 -14¢"; "—" for freq &lt;= 0.</summary>
        internal static string Format(double freq)
        {
            if (freq <= 0) return "—";
            int n = NearestNoteNumber(freq);
            int c = Cents(freq, n);
            return c == 0 ? NoteName(n) : NoteName(n) + " " + (c > 0 ? "+" : "") + c + "¢";
        }
    }
}
