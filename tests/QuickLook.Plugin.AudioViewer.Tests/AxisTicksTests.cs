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

        /// <summary>
        /// Reproduces the label-drop the time ruler had: labels were positioned with
        /// Math.Min(width - textWidth, x + 2), so every one past that point collapsed onto
        /// the same x. The first collapsed label set the de-clutter cursor near the right
        /// edge and every later label then tested as overlapping and was dropped — on a
        /// 16-minute file the axis stopped a minute in. De-cluttering must compare the
        /// unclamped positions.
        /// </summary>
        [Fact]
        public void LongFile_LabelsRunTheWholeLength()
        {
            const double duration = 984.682;   // 16:24
            const double width = 12000;        // the timeline width cap
            const double labelWidth = 26;      // roughly "10:00" at 10pt

            double step = AxisTicks.TimeStepSeconds(duration, width, 64);
            var ticks = AxisTicks.TimeTicks(duration, step);
            Assert.True(ticks.Length > 90, $"expected many ticks, got {ticks.Length}");

            // Mirror the ruler's de-clutter, comparing unclamped x as the fixed code does.
            int drawn = 0;
            double lastLabelEnd = double.NegativeInfinity;
            double lastDrawnX = 0;
            foreach (var t in ticks)
            {
                double x = t / duration * width;
                if (x < lastLabelEnd + 4) continue;
                lastLabelEnd = x + labelWidth;
                lastDrawnX = x;
                drawn++;
            }

            // Every tick is far enough apart to keep its label at this step.
            Assert.Equal(ticks.Length, drawn);
            // And the last one really is at the far end, not stranded near the start.
            Assert.True(lastDrawnX > width * 0.95, $"labels stopped at x={lastDrawnX} of {width}");
        }

        /// <summary>
        /// The axis truncation, reproduced against the real files it was reported on.
        /// Whatever the scroll position, the ruler must offer ticks covering the visible
        /// window — the symptom was labels always ending at the same pixel offset, roughly
        /// one viewport in, no matter how far the user scrolled.
        /// </summary>
        [Theory]
        // 1:22 file: 82.368 * 80 = 6589px timeline.
        [InlineData(82.368, 6589, 0)]
        [InlineData(82.368, 6589, 480)]
        [InlineData(82.368, 6589, 3000)]
        [InlineData(82.368, 6589, 5649)]    // scrolled to the very end
        // 4:12 file: 252.906 * 80 = 20232, capped at 12000.
        [InlineData(252.906, 12000, 0)]
        [InlineData(252.906, 12000, 474)]
        [InlineData(252.906, 12000, 6000)]
        [InlineData(252.906, 12000, 11060)]
        public void VisibleTicks_CoverTheWindowAtEveryScrollPosition(
            double duration, double timelineWidth, double scrollOffset)
        {
            const double viewport = 940, widestLabel = 30;

            double step = AxisTicks.TimeStepSeconds(duration, timelineWidth, 64);
            AxisTicks.VisibleTicks(duration, timelineWidth, viewport, scrollOffset, step,
                widestLabel, out long first, out long last, out int stride);

            Assert.True(last >= first, $"empty tick range at offset {scrollOffset}");

            double pxPerSec = timelineWidth / duration;
            double firstX = first * step * pxPerSec - scrollOffset;
            double lastX = last * step * pxPerSec - scrollOffset;

            // The range must start at or before the left edge and reach the right edge —
            // unless the file simply ends inside the window.
            Assert.True(firstX <= 0.001, $"first tick at x={firstX}, leaving a gap on the left");

            double endOfFileX = duration * pxPerSec - scrollOffset;
            if (endOfFileX > viewport)
                Assert.True(lastX >= viewport, $"last tick at x={lastX}, short of the {viewport}px edge");

            Assert.True(stride >= 1);
        }

        [Fact]
        public void VisibleTicks_LabelStrideIsIndependentOfScrollPosition()
        {
            const double duration = 252.906, timelineWidth = 12000, viewport = 940, widest = 30;
            double step = AxisTicks.TimeStepSeconds(duration, timelineWidth, 64);

            AxisTicks.VisibleTicks(duration, timelineWidth, viewport, 0, step, widest,
                out _, out _, out int strideAtStart);
            AxisTicks.VisibleTicks(duration, timelineWidth, viewport, 7000, step, widest,
                out _, out _, out int strideMidway);

            // A stride that moved with the scroll would make labels pop in and out.
            Assert.Equal(strideAtStart, strideMidway);
        }

        [Fact]
        public void VisibleTicks_SkipsLabelsWhenTicksAreTighterThanTheText()
        {
            // 0.1s steps at 80px/s = 8px apart; a 30px label needs every 5th.
            AxisTicks.VisibleTicks(60, 4800, 940, 0, 0.1, 30, out _, out _, out int stride);
            Assert.True(stride >= 5, $"expected to skip labels, got stride {stride}");
        }

        [Fact]
        public void LabelLeft_SitsJustRightOfItsTick()
        {
            Assert.Equal(102.0, AxisTicks.LabelLeft(100, 24, 750, false, double.NegativeInfinity));
        }

        [Fact]
        public void LabelLeft_DoesNotPullAMidFileTickOntoItsNeighbour()
        {
            // The reported overlap: a 74 s file scrolled to show 0:00-0:10. "0:09" sits at
            // x=700 (right edge 726); "0:10"'s tick is just past the 750px edge. It is not the
            // file's end, so it must stay with its tick and clip, not be dragged onto "0:09".
            double? left = AxisTicks.LabelLeft(752, 24, 750, false, 726);
            Assert.Equal(754.0, left);
        }

        [Fact]
        public void LabelLeft_KeepsTheFilesFinalLabelOnScreen()
        {
            Assert.Equal(726.0, AxisTicks.LabelLeft(745, 24, 750, true, 600));
        }

        [Fact]
        public void LabelLeft_DropsTheFinalLabelRatherThanOverlapTheOneBeforeIt()
        {
            // Pulled left to 726, it would sit inside the 6px gap after a label ending at 724.
            Assert.Null(AxisTicks.LabelLeft(745, 24, 750, true, 724));
        }

        [Fact]
        public void VisibleTicks_DegenerateInputsProduceAnEmptyRange()
        {
            AxisTicks.VisibleTicks(0, 6589, 940, 0, 1, 30, out long f1, out long l1, out _);
            Assert.True(l1 < f1);

            AxisTicks.VisibleTicks(82.368, 0, 940, 0, 1, 30, out long f2, out long l2, out _);
            Assert.True(l2 < f2);
        }

        [Fact]
        public void DensityChangesTheStep()
        {
            const double duration = 984.682, width = 12000;
            double sparse = AxisTicks.TimeStepSeconds(duration, width, 110);
            double normal = AxisTicks.TimeStepSeconds(duration, width, 64);
            double dense = AxisTicks.TimeStepSeconds(duration, width, 40);

            Assert.True(sparse >= normal, $"sparse {sparse} should be >= normal {normal}");
            Assert.True(dense <= normal, $"dense {dense} should be <= normal {normal}");
            Assert.True(dense < sparse, "dense and sparse should actually differ");
        }

        [Fact]
        public void DbToFraction_PlacesLevelsWithinTheHalfLane()
        {
            // Unmagnified, 0 dBFS is the lane edge and -6 dB is about half way to it.
            Assert.Equal(1.0, AxisTicks.DbToFraction(0, 1.0), 6);
            Assert.Equal(0.5012, AxisTicks.DbToFraction(-6, 1.0), 3);
            Assert.Equal(0.1, AxisTicks.DbToFraction(-20, 1.0), 6);

            // At 8x the lane edge is -18.06 dB, so -20 dB is still just inside and 0 dB
            // is well past it — which is what the ruler uses to drop off-lane ticks.
            Assert.InRange(AxisTicks.DbToFraction(-20, 8.0), 0.79, 0.81);
            Assert.True(AxisTicks.DbToFraction(0, 8.0) > 1.0);
        }

        [Fact]
        public void FormatDbFs_IsReadable()
        {
            Assert.Equal("0 dB", AxisTicks.FormatDbFs(0));
            Assert.Equal("-12 dB", AxisTicks.FormatDbFs(-12));
            Assert.Equal("-6 dB", AxisTicks.FormatDbFs(-6));
        }

        [Fact]
        public void NiceDbFs_IsDescendingAndStartsAtFullScale()
        {
            Assert.Equal(0, AxisTicks.NiceDbFs[0]);
            for (int i = 1; i < AxisTicks.NiceDbFs.Length; i++)
                Assert.True(AxisTicks.NiceDbFs[i] < AxisTicks.NiceDbFs[i - 1]);
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
