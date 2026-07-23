using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Midi;
using QuickLook.Plugin.AudioViewer.Audio;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class MidiExporterTests
    {
        [Fact]
        public void SecondsToTicks_ScalesWithTempoAndResolution()
        {
            // At 120 BPM a quarter note is 0.5 s and equals ppq ticks.
            Assert.Equal(0, MidiExporter.SecondsToTicks(0, 120, 480));
            Assert.Equal(480, MidiExporter.SecondsToTicks(0.5, 120, 480));
            Assert.Equal(960, MidiExporter.SecondsToTicks(1.0, 120, 480));
        }

        [Fact]
        public void BuildCollection_IncludesTempoAndNotePairs()
        {
            var notes = new[]
            {
                new NoteModel { Midi = 69, StartSeconds = 0, DurationSeconds = 1, Velocity = 80 },
            };
            var events = MidiExporter.BuildCollection(notes, 120, 480);
            var flat = events.SelectMany(t => t).ToList();

            var tempo = flat.OfType<TempoEvent>().FirstOrDefault();
            Assert.NotNull(tempo);
            Assert.Equal(120, (int)Math.Round(tempo.Tempo)); // TempoEvent.Tempo is BPM

            Assert.Single(flat.OfType<NoteOnEvent>(), e => e.Velocity > 0);
        }

        [Fact]
        public void WriteMidi_RoundTrips_NoteCountNumbersAndTiming()
        {
            var notes = new List<NoteModel>
            {
                new NoteModel { Midi = 60, StartSeconds = 0.0, DurationSeconds = 0.5, Velocity = 80 },
                new NoteModel { Midi = 64, StartSeconds = 0.5, DurationSeconds = 0.25, Velocity = 90 },
                new NoteModel { Midi = 67, StartSeconds = 1.0, DurationSeconds = 1.0, Velocity = 100 },
            };
            var path = Path.Combine(Path.GetTempPath(), $"notes_{Guid.NewGuid():N}.mid");
            try
            {
                MidiExporter.WriteMidi(path, notes, 120, 480);
                Assert.True(File.Exists(path));

                var mf = new MidiFile(path, false);
                var noteOns = mf.Events.SelectMany(t => t)
                    .OfType<NoteOnEvent>()
                    .Where(e => e.Velocity > 0)
                    .OrderBy(e => e.AbsoluteTime)
                    .ToList();

                Assert.Equal(3, noteOns.Count);
                Assert.Equal(new[] { 60, 64, 67 }, noteOns.Select(e => e.NoteNumber).ToArray());
                Assert.Equal(0, noteOns[0].AbsoluteTime);
                Assert.Equal(480, noteOns[0].NoteLength); // 0.5 s at 120 BPM / 480 ppq
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
