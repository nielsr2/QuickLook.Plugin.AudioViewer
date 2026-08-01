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
        public void FormatReadout_BoxStyle_LabelsEachRowOnItsOwnLine()
        {
            string s = HoverReadoutFormatter.FormatReadout(
                1.5, 3, 440.0, HoverReadoutMode.MidiNote, 0.5, AmplitudeScale.Decibel,
                HoverReadoutStyle.Box, true, true, true);

            var lines = s.Split('\n');
            Assert.Equal(3, lines.Length);
            Assert.StartsWith("Time", lines[0]);
            Assert.StartsWith("Note", lines[1]);
            Assert.StartsWith("Level", lines[2]);
            Assert.Contains("0:01.500", lines[0]);
            Assert.Contains("A4", lines[1]);
        }

        [Fact]
        public void FormatReadout_PitchLabelTracksTheReadoutMode()
        {
            string note = HoverReadoutFormatter.FormatReadout(0, 0, 440, HoverReadoutMode.MidiNote,
                0.5, AmplitudeScale.Decibel, HoverReadoutStyle.Box, false, true, false);
            string freq = HoverReadoutFormatter.FormatReadout(0, 0, 440, HoverReadoutMode.Frequency,
                0.5, AmplitudeScale.Decibel, HoverReadoutStyle.Box, false, true, false);
            string both = HoverReadoutFormatter.FormatReadout(0, 0, 440, HoverReadoutMode.Both,
                0.5, AmplitudeScale.Decibel, HoverReadoutStyle.Box, false, true, false);

            Assert.StartsWith("Note", note);
            Assert.StartsWith("Freq", freq);
            Assert.StartsWith("Pitch", both);
        }

        [Fact]
        public void FormatReadout_CompactStyle_IsOneLineOfBareValues()
        {
            string s = HoverReadoutFormatter.FormatReadout(
                1.5, 3, 440.0, HoverReadoutMode.MidiNote, 0.5, AmplitudeScale.Decibel,
                HoverReadoutStyle.Compact, true, true, true);

            Assert.DoesNotContain("\n", s);
            Assert.DoesNotContain("Time", s);
            Assert.Contains("·", s);
        }

        [Fact]
        public void FormatReadout_OmitsDeselectedFields()
        {
            string noTime = HoverReadoutFormatter.FormatReadout(
                1.5, 3, 440.0, HoverReadoutMode.MidiNote, 0.5, AmplitudeScale.Decibel,
                HoverReadoutStyle.Box, showTime: false, showPitch: true, showLevel: true);

            Assert.DoesNotContain("Time", noTime);
            Assert.Equal(2, noTime.Split('\n').Length);
        }

        [Fact]
        public void FormatReadout_AllFieldsOff_IsEmptySoTheCallerCanHideIt()
        {
            Assert.Equal(string.Empty, HoverReadoutFormatter.FormatReadout(
                1.5, 3, 440.0, HoverReadoutMode.MidiNote, 0.5, AmplitudeScale.Decibel,
                HoverReadoutStyle.Box, false, false, false));
        }

        [Fact]
        public void PlaceBox_SitsBelowRightOfTheCursorWhenThereIsRoom()
        {
            HoverReadoutFormatter.PlaceBox(100, 100, 80, 40, 800, 600, 12, false, out double x, out double y);
            Assert.Equal(112, x);
            Assert.Equal(112, y);
        }

        /// <summary>
        /// The default: a constant offset from the cursor, even when that means clipping.
        /// Re-anchoring on approach to an edge makes the numbers relocate under a smoothly
        /// moving cursor, which is more distracting than a partly clipped box.
        /// </summary>
        [Fact]
        public void PlaceBox_ByDefaultDoesNotRepositionNearAnEdge()
        {
            // The box overflows the pane (762 + 80 > 800) but most of it is still readable,
            // so it stays put and clips rather than jumping to the other side.
            HoverReadoutFormatter.PlaceBox(750, 550, 80, 40, 800, 600, 12, false, out double x, out double y);
            Assert.Equal(762, x);
            Assert.Equal(562, y);
        }

        [Fact]
        public void PlaceBox_MovesOnlyWhenItWouldBeEffectivelyInvisible()
        {
            // Cursor within 24px of the right edge: none of the box would be readable.
            HoverReadoutFormatter.PlaceBox(795, 300, 80, 40, 800, 600, 12, false, out double x, out double y);
            Assert.Equal(703, x);   // 795 - 12 - 80
            Assert.Equal(312, y);   // vertical had room, so it is untouched
        }

        [Fact]
        public void PlaceBox_KeepInView_FlipsRatherThanSlidingAlongTheEdge()
        {
            HoverReadoutFormatter.PlaceBox(780, 580, 80, 40, 800, 600, 12, true, out double x, out double y);
            Assert.Equal(688, x);   // 780 - 12 - 80
            Assert.Equal(528, y);   // 580 - 12 - 40
        }

        [Fact]
        public void PlaceBox_KeepInView_ClampsWhenItFitsOnNeitherSide()
        {
            // Box wider than the pane: it cannot flip anywhere, so it must still stay inside.
            HoverReadoutFormatter.PlaceBox(50, 50, 500, 40, 400, 600, 12, true, out double x, out double y);
            Assert.Equal(0, x);
            Assert.InRange(y, 0, 560);
        }

        [Fact]
        public void PlaceBox_NeverGoesNegative()
        {
            HoverReadoutFormatter.PlaceBox(5, 5, 200, 100, 210, 110, 12, false, out double x, out double y);
            Assert.True(x >= 0);
            Assert.True(y >= 0);
        }

        [Fact]
        public void HoverReadoutStyle_EnumOrder_IsLoadBearingForTheSettingsCombo()
        {
            Assert.Equal(0, (int)HoverReadoutStyle.Box);
            Assert.Equal(1, (int)HoverReadoutStyle.Compact);
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
