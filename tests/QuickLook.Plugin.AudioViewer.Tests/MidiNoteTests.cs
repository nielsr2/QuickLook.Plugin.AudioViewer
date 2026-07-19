using System;
using QuickLook.Plugin.AudioViewer.Audio;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    /// <summary>
    /// The spectrogram hover readout can show the nearest 12-TET MIDI note instead of a
    /// raw frequency (A4 = 440 Hz, middle C = C4). These pin the pitch math.
    /// </summary>
    public class MidiNoteTests
    {
        [Fact]
        public void A440_MapsToA4()
        {
            Assert.Equal(69, MidiNote.NearestNoteNumber(440.0));
            Assert.Equal("A4", MidiNote.NoteName(69));
            Assert.InRange(MidiNote.Cents(440.0, 69), -1, 1);
            Assert.Equal("A4", MidiNote.Format(440.0));
        }

        [Fact]
        public void MiddleC_IsC4()
        {
            int n = MidiNote.NearestNoteNumber(261.6256);
            Assert.Equal(60, n);
            Assert.Equal("C4", MidiNote.NoteName(n));
        }

        [Fact]
        public void ASharp4_IsNamedWithSharp()
        {
            int n = MidiNote.NearestNoteNumber(466.1638);
            Assert.Equal(70, n);
            Assert.Equal("A#4", MidiNote.NoteName(n));
        }

        [Fact]
        public void NoteFrequency_OfA4_Is440()
        {
            Assert.Equal(440.0, MidiNote.NoteFrequency(69), 6);
        }

        [Fact]
        public void SharpDetunedA4_ReadsPositiveCentsAndFormatsWithPlus()
        {
            // Sharpen A4 by ~40 cents (well inside A4's rounding zone; a full +50 cents
            // is the exact A4/A#4 midpoint and rounds up to A#4, so it reads -50 there).
            double freq = 440.0 * Math.Pow(2.0, 0.40 / 12.0); // ~450.2 Hz
            Assert.Equal(69, MidiNote.NearestNoteNumber(freq)); // still A4
            int c = MidiNote.Cents(freq, MidiNote.NearestNoteNumber(freq));
            Assert.InRange(c, 38, 42);

            string formatted = MidiNote.Format(freq);
            Assert.Contains("A4", formatted);
            Assert.Contains("+", formatted);
        }

        [Fact]
        public void NonPositiveFrequency_FormatsAsDash()
        {
            Assert.Equal("—", MidiNote.Format(0));
            Assert.Equal("—", MidiNote.Format(-100));
            Assert.Equal(-1, MidiNote.NearestNoteNumber(0));
        }

        [Theory]
        [InlineData(60)]   // C4
        [InlineData(61)]   // C#4
        [InlineData(70)]   // A#4
        public void NoteName_KnownValues(int midi)
        {
            string expected = midi == 60 ? "C4" : midi == 61 ? "C#4" : "A#4";
            Assert.Equal(expected, MidiNote.NoteName(midi));
        }

        [Theory]
        [InlineData(82.41)]    // E2
        [InlineData(261.63)]   // C4
        [InlineData(440.0)]    // A4
        [InlineData(1975.5)]   // B6
        [InlineData(3000.0)]
        public void RoundTrip_NearestNoteFrequency_WithinSemitone(double freq)
        {
            int n = MidiNote.NearestNoteNumber(freq);
            double back = MidiNote.NoteFrequency(n);
            // Within a semitone ratio (2^(1/12) ≈ 1.0595) in either direction.
            double ratio = back / freq;
            double semitone = Math.Pow(2.0, 1.0 / 12.0);
            Assert.InRange(ratio, 1.0 / semitone, semitone);
        }
    }
}
