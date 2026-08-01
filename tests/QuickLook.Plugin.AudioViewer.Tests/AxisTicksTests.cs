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
