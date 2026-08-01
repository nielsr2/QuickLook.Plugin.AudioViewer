using System;
using QuickLook.Plugin.AudioViewer.Controls;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class AxisTicksTests
    {
        [Fact]
        public void NiceFrequencies_AreInRangeAndAscending()
        {
            var ticks = AxisTicks.NiceFrequencies(20, 20000);
            Assert.NotEmpty(ticks);
            for (int i = 0; i < ticks.Length; i++)
            {
                Assert.InRange(ticks[i], 20, 20000);
                if (i > 0) Assert.True(ticks[i] > ticks[i - 1]);
            }
            Assert.Contains(1000.0, ticks);
            Assert.Contains(10000.0, ticks);
        }

        [Fact]
        public void TimeStepSeconds_GrowsWhenLabelsWouldCrowd()
        {
            // 600 s across 600 px with 60 px min spacing => ~1 label / 60 px => 60 s step.
            double step = AxisTicks.TimeStepSeconds(600, 600, 60);
            Assert.True(step >= 30, $"expected a coarse step for a long file, got {step}");

            // Plenty of room => a fine step.
            double fine = AxisTicks.TimeStepSeconds(10, 1000, 60);
            Assert.True(fine <= 2, $"expected a fine step for a short wide view, got {fine}");
        }

        [Fact]
        public void TimeTicks_StartAtZeroAndStayWithinDuration()
        {
            var ticks = AxisTicks.TimeTicks(10, 2);
            Assert.Equal(0.0, ticks[0]);
            Assert.All(ticks, t => Assert.InRange(t, 0, 10));
        }

        [Theory]
        [InlineData(500, "500 Hz")]
        [InlineData(1000, "1 kHz")]
        [InlineData(2500, "2.5 kHz")]
        [InlineData(10000, "10 kHz")]
        public void FormatHz_IsReadable(double hz, string expected)
            => Assert.Equal(expected, AxisTicks.FormatHz(hz));

        [Theory]
        [InlineData(5, "0:05")]
        [InlineData(65, "1:05")]
        [InlineData(3661, "1:01:01")]
        public void FormatTime_IsReadable(double seconds, string expected)
            => Assert.Equal(expected, AxisTicks.FormatTime(seconds));

        [Fact]
        public void DecimalsForStep_MatchesTheStepResolution()
        {
            Assert.Equal(0, AxisTicks.DecimalsForStep(1));
            Assert.Equal(0, AxisTicks.DecimalsForStep(30));
            Assert.Equal(1, AxisTicks.DecimalsForStep(0.5));
            Assert.Equal(2, AxisTicks.DecimalsForStep(0.05));
            Assert.Equal(3, AxisTicks.DecimalsForStep(0.002));
        }

        [Fact]
        public void FormatTime_WithDecimals_ShowsSubSecondDetail()
        {
            Assert.Equal("0:05", AxisTicks.FormatTime(5));
            Assert.Equal("0:05.5", AxisTicks.FormatTime(5.5, 1));
            Assert.Equal("0:05.25", AxisTicks.FormatTime(5.25, 2));
            Assert.Equal("0:05.125", AxisTicks.FormatTime(5.125, 3));
            Assert.Equal("1:01:01.500", AxisTicks.FormatTime(3661.5, 3));
        }

        [Fact]
        public void FormatTime_RoundingCarriesIntoTheSecondsField()
        {
            // 1.9995 s at 3 dp is 2.000 s — the carry must reach seconds rather than
            // producing "0:01.1000" or "0:01.000".
            Assert.Equal("0:02.000", AxisTicks.FormatTime(1.9995, 3));
            Assert.Equal("0:01.0", AxisTicks.FormatTime(0.96, 1));
        }

        /// <summary>
        /// The reported symptom: a 2 s file rendered "0:00, 0:00, 0:01, 0:01, 0:02" because
        /// the step ladder bottomed out at 0.5 s while labels stayed at whole seconds.
        /// </summary>
        [Fact]
        public void ShortFile_ProducesDistinctTickLabels()
        {
            double step = AxisTicks.TimeStepSeconds(2.0, 900, 64);
            Assert.True(step < 1.0, $"expected a sub-second step for a 2 s file, got {step}");

            int decimals = AxisTicks.DecimalsForStep(step);
            var labels = new System.Collections.Generic.List<string>();
            foreach (var t in AxisTicks.TimeTicks(2.0, step))
                labels.Add(AxisTicks.FormatTime(t, decimals));

            Assert.Equal(labels.Count, new System.Collections.Generic.HashSet<string>(labels).Count);
        }

        [Fact]
        public void FormatMagnitude_KeepsResolutionAcrossTheWholeRange()
        {
            Assert.Equal("0", AxisTicks.FormatMagnitude(0));
            Assert.Equal("1.234", AxisTicks.FormatMagnitude(1.2344));
            Assert.Equal("0.01580", AxisTicks.FormatMagnitude(0.0158));
            Assert.Equal("0.00316", AxisTicks.FormatMagnitude(0.00316));
            Assert.Equal("3.16e-5", AxisTicks.FormatMagnitude(3.16e-5));
        }

        /// <summary>
        /// The colorbar's three ticks are Black, the midpoint, and Ceiling. Linear mode
        /// derives Black as Ceiling * 10^(DbFloor/20), so at the default -90 dB floor the
        /// bottom tick is ~31 600x smaller than the top. The previous "0.00" format
        /// rendered all three identically on quiet material, which is what made the
        /// legend read "0.00 / 0.00 / 0.00".
        /// </summary>
        [Theory]
        [InlineData(0.004)]     // quiet file
        [InlineData(0.9)]       // loud file
        public void FormatMagnitude_ColorbarTicksAreDistinct(double ceiling)
        {
            double black = ceiling * Math.Pow(10.0, -90.0 / 20.0);
            string lo = AxisTicks.FormatMagnitude(black);
            string mid = AxisTicks.FormatMagnitude(black + 0.5 * (ceiling - black));
            string hi = AxisTicks.FormatMagnitude(ceiling);

            Assert.NotEqual(lo, mid);
            Assert.NotEqual(mid, hi);
            Assert.NotEqual(lo, hi);
        }
    }
}
