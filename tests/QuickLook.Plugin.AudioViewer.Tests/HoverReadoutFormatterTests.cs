using QuickLook.Plugin.AudioViewer.Audio;
using QuickLook.Plugin.AudioViewer.Controls;
using QuickLook.Plugin.AudioViewer.Settings;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    /// <summary>
    /// The spectrogram hover readout can show the nearest note, the raw frequency, or
    /// both (the default). These pin the composition; the underlying pitch and Hz math
    /// is covered by <see cref="MidiNoteTests"/> and <see cref="AxisTicksTests"/>.
    /// </summary>
    public class HoverReadoutFormatterTests
    {
        [Fact]
        public void MidiNoteMode_ShowsNoteOnly()
        {
            string s = HoverReadoutFormatter.FormatPitch(440.0, HoverReadoutMode.MidiNote);
            Assert.Equal("A4", s);
            Assert.DoesNotContain("Hz", s);
        }

        [Fact]
        public void FrequencyMode_ShowsHzOnly()
        {
            string s = HoverReadoutFormatter.FormatPitch(440.0, HoverReadoutMode.Frequency);
            Assert.Equal("440 Hz", s);
            Assert.DoesNotContain("A4", s);
        }

        [Fact]
        public void BothMode_ShowsNoteAndHzSeparated()
        {
            Assert.Equal("A4  ·  440 Hz", HoverReadoutFormatter.FormatPitch(440.0, HoverReadoutMode.Both));
        }

        [Fact]
        public void BothMode_DelegatesToTheExistingHelpers()
        {
            // Composition, not reimplementation: both terms must be byte-identical to
            // what the note/Hz helpers already produce.
            const double freq = 2500.0;
            string s = HoverReadoutFormatter.FormatPitch(freq, HoverReadoutMode.Both);
            Assert.Contains(MidiNote.Format(freq), s);
            Assert.Contains(AxisTicks.FormatHz(freq), s);
        }

        [Fact]
        public void BothMode_CarriesCentsDeviation()
        {
            // A detuned pitch keeps its cents term alongside the frequency.
            string s = HoverReadoutFormatter.FormatPitch(452.0, HoverReadoutMode.Both);
            Assert.Contains("A4", s);
            Assert.Contains("¢", s);
            Assert.Contains("452 Hz", s);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-100.0)]
        public void NonPositiveFrequency_KeepsPerModeBehaviour(double freq)
        {
            // MIDI alone has no note to name; frequency alone is still meaningful (DC bin).
            Assert.Equal("—", HoverReadoutFormatter.FormatPitch(freq, HoverReadoutMode.MidiNote));
            Assert.Equal(AxisTicks.FormatHz(freq), HoverReadoutFormatter.FormatPitch(freq, HoverReadoutMode.Frequency));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-100.0)]
        public void BothMode_DropsTheDashWhenThereIsNoNote(double freq)
        {
            // Avoids a dangling "—  ·  0 Hz" at the bottom edge of a linear-scale plot.
            string s = HoverReadoutFormatter.FormatPitch(freq, HoverReadoutMode.Both);
            Assert.DoesNotContain("—", s);
            Assert.Equal(AxisTicks.FormatHz(freq), s);
        }

        [Fact]
        public void EnumOrder_IsLoadBearingForTheSettingsCombo()
        {
            // The settings ComboBox casts SelectedIndex straight to this enum, so the
            // XAML item order (MIDI note / Frequency / Note + frequency) must match.
            Assert.Equal(0, (int)HoverReadoutMode.MidiNote);
            Assert.Equal(1, (int)HoverReadoutMode.Frequency);
            Assert.Equal(2, (int)HoverReadoutMode.Both);
        }
    }
}
