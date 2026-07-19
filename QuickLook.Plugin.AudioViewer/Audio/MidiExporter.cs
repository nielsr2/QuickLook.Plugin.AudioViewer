using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Midi;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>
    /// Writes notes to a Standard MIDI File via NAudio.Midi (already shipped with NAudio —
    /// no extra dependency). Audio has no inherent tempo; MIDI timing is tick-based and
    /// anchored by a single TempoEvent, so wall-clock timing is preserved for any BPM —
    /// the BPM only affects how a DAW labels bars/beats.
    /// </summary>
    internal static class MidiExporter
    {
        public const int DefaultBpm = 120;
        public const int DefaultPpq = 480; // ticks per quarter note

        /// <summary>Seconds -&gt; MIDI ticks for the given tempo/resolution.</summary>
        public static long SecondsToTicks(double seconds, int bpm, int ppq)
            => (long)Math.Round(seconds * ppq * bpm / 60.0);

        public static MidiEventCollection BuildCollection(IEnumerable<NoteModel> notes,
            int bpm = DefaultBpm, int ppq = DefaultPpq)
        {
            if (bpm <= 0) bpm = DefaultBpm;
            if (ppq <= 0) ppq = DefaultPpq;

            var events = new MidiEventCollection(0, ppq); // type 0 = single track
            int microsecondsPerQuarter = (int)(60_000_000.0 / bpm);
            events.AddEvent(new TempoEvent(microsecondsPerQuarter, 0), 0);

            foreach (var n in notes.OrderBy(x => x.StartSeconds))
            {
                int midi = Clamp(n.Midi, 0, 127);
                int velocity = Clamp(n.Velocity, 1, 127);
                long onTick = SecondsToTicks(n.StartSeconds, bpm, ppq);
                long offTick = SecondsToTicks(n.StartSeconds + Math.Max(0.0, n.DurationSeconds), bpm, ppq);
                int durationTicks = (int)Math.Max(1, offTick - onTick);

                var noteOn = new NoteOnEvent(onTick, 1, midi, velocity, durationTicks); // channel 1 (1-based)
                events.AddEvent(noteOn, 0);
                events.AddEvent(noteOn.OffEvent, 0); // NAudio's paired NoteOff at on+duration
            }

            events.PrepareForExport(); // sorts events, adds End-Of-Track
            return events;
        }

        public static void WriteMidi(string path, IEnumerable<NoteModel> notes,
            int bpm = DefaultBpm, int ppq = DefaultPpq)
            => MidiFile.Export(path, BuildCollection(notes, bpm, ppq));

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
