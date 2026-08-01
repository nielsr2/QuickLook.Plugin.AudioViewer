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

        [Fact]
        public void FormatLevel_DecibelSquared_IsHalfThePlainDecibelValue()
        {
            // A bin at 1e-4 reads -80 dB on the plain scale but -40 on the half-gain one,
            // which is the whole point: against the same contrast floor it sits far higher
            // up the colour ramp instead of being crushed toward black.
            Assert.Equal("-80 dB", HoverReadoutFormatter.FormatLevel(1e-4, AmplitudeScale.Decibel));
            Assert.Equal("-40 dB", HoverReadoutFormatter.FormatLevel(1e-4, AmplitudeScale.DecibelSquared));
        }

        [Fact]
        public void FormatLevel_LinearShowsRawMagnitude()
        {
            Assert.Equal("0.250", HoverReadoutFormatter.FormatLevel(0.25, AmplitudeScale.Linear));
        }

        [Fact]
        public void FormatLevel_FullScaleIsZeroOnBothDecibelScales()
        {
            Assert.Equal("0 dB", HoverReadoutFormatter.FormatLevel(1.0, AmplitudeScale.Decibel));
            Assert.Equal("0 dB", HoverReadoutFormatter.FormatLevel(1.0, AmplitudeScale.DecibelSquared));
        }

        [Fact]
        public void FormatLevel_SilenceDoesNotProduceInfinityOrNaN()
        {
            foreach (AmplitudeScale s in System.Enum.GetValues(typeof(AmplitudeScale)))
            {
                string t = HoverReadoutFormatter.FormatLevel(0.0, s);
                Assert.DoesNotContain("∞", t);
                Assert.DoesNotContain("NaN", t);
            }
        }

        [Fact]
        public void AmplitudeScale_EnumOrder_IsLoadBearingForTheSettingsCombo()
        {
            // AmpCombo_Changed casts SelectedIndex straight to this enum, so the XAML
            // item order (Decibel / Linear / dB²) must match these values.
            Assert.Equal(0, (int)AmplitudeScale.Decibel);
            Assert.Equal(1, (int)AmplitudeScale.Linear);
            Assert.Equal(2, (int)AmplitudeScale.DecibelSquared);
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
