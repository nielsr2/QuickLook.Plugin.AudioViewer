using QuickLook.Plugin.AudioViewer.Audio;
using QuickLook.Plugin.AudioViewer.Settings;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class NoteGeometryTests
    {
        [Fact]
        public void TimeToX_And_XToTime_RoundTrip()
        {
            double dur = 12.0, width = 800.0;
            for (double t = 0; t <= dur; t += 1.5)
            {
                double x = NoteGeometry.TimeToX(t, dur, width);
                Assert.InRange(x, 0, width);
                Assert.Equal(t, NoteGeometry.XToTime(x, width, dur), 6);
            }
        }

        [Theory]
        [InlineData(FrequencyScale.Linear)]
        [InlineData(FrequencyScale.Log)]
        [InlineData(FrequencyScale.Mel)]
        public void MidiToY_Then_YToMidi_RecoversTheNote(FrequencyScale scale)
        {
            double fMin = FrequencyScaleMapper.EffectiveFMin(scale, 1024, 44100);
            double fMax = 8000, height = 512;
            // Notes comfortably inside the visible band on all scales.
            foreach (int midi in new[] { 40, 52, 60, 69, 76, 84 })
            {
                double y = NoteGeometry.MidiToY(midi, scale, fMin, fMax, height);
                Assert.Equal(midi, NoteGeometry.YToMidi(y, scale, fMin, fMax, height));
            }
        }

        [Theory]
        [InlineData(FrequencyScale.Linear)]
        [InlineData(FrequencyScale.Log)]
        [InlineData(FrequencyScale.Mel)]
        public void NoteYBounds_TopAboveBottom_AndContainsCenter(FrequencyScale scale)
        {
            double fMin = FrequencyScaleMapper.EffectiveFMin(scale, 1024, 44100);
            double fMax = 8000, height = 512;
            int midi = 60;
            NoteGeometry.NoteYBounds(midi, scale, fMin, fMax, height, out double yTop, out double yBottom);
            double center = NoteGeometry.MidiToY(midi, scale, fMin, fMax, height);
            Assert.True(yTop < yBottom, "top pixel should be above (smaller than) bottom");
            Assert.InRange(center, yTop, yBottom);
        }

        [Fact]
        public void NoteYBounds_LogScale_GivesConstantRowHeight()
        {
            var scale = FrequencyScale.Log;
            double fMin = FrequencyScaleMapper.EffectiveFMin(scale, 1024, 44100);
            double fMax = 8000, height = 512;
            NoteGeometry.NoteYBounds(48, scale, fMin, fMax, height, out double t1, out double b1);
            NoteGeometry.NoteYBounds(72, scale, fMin, fMax, height, out double t2, out double b2);
            // On a log scale each semitone occupies an equal vertical span.
            Assert.Equal(b1 - t1, b2 - t2, 3);
        }

        // NoteEdge is a public enum nested in the internal NoteGeometry, so its effective
        // accessibility is internal — it can't appear in a public xUnit method signature. Pass the
        // expectation as the enum's int value (Outside=0, Left=1, Body=2, Right=3) and cast.
        [Theory]
        [InlineData(50, (int)NoteGeometry.NoteEdge.Left)]   // within 4px of leftX=52
        [InlineData(52, (int)NoteGeometry.NoteEdge.Left)]   // exactly on the left edge
        [InlineData(100, (int)NoteGeometry.NoteEdge.Body)]  // interior
        [InlineData(148, (int)NoteGeometry.NoteEdge.Right)] // exactly on the right edge
        [InlineData(151, (int)NoteGeometry.NoteEdge.Right)] // within 4px of rightX=148
        [InlineData(10, (int)NoteGeometry.NoteEdge.Outside)]
        [InlineData(200, (int)NoteGeometry.NoteEdge.Outside)]
        public void HitEdge_ClassifiesEdgesBodyAndOutside(double px, int expected)
        {
            Assert.Equal(expected, (int)NoteGeometry.HitEdge(px, leftX: 52, rightX: 148, edgePx: 4));
        }

        [Fact]
        public void HitEdge_LeftWinsOverRight_OnThinNotes()
        {
            // When both edges are within tolerance, the left edge is reported first.
            Assert.Equal(NoteGeometry.NoteEdge.Left, NoteGeometry.HitEdge(100, leftX: 100, rightX: 102, edgePx: 4));
        }

        [Fact]
        public void DrawSpan_RightwardDrag_UsesAnchorAsStart()
        {
            NoteGeometry.DrawSpan(1.0, 2.5, 0.03, out double start, out double dur);
            Assert.Equal(1.0, start, 6);
            Assert.Equal(1.5, dur, 6);
        }

        [Fact]
        public void DrawSpan_LeftwardDrag_SwapsSoStartIsTheSmaller()
        {
            NoteGeometry.DrawSpan(2.5, 1.0, 0.03, out double start, out double dur);
            Assert.Equal(1.0, start, 6);
            Assert.Equal(1.5, dur, 6);
        }

        [Fact]
        public void DrawSpan_BareClick_ClampsToMinDuration_AndFloorsStart()
        {
            NoteGeometry.DrawSpan(-0.2, -0.2, 0.03, out double start, out double dur);
            Assert.Equal(0.0, start, 6);   // negative anchor floored to 0
            Assert.Equal(0.03, dur, 6);    // no movement -> min duration
        }

        [Fact]
        public void ResizeRight_ClampsToMinDuration()
        {
            Assert.Equal(2.0, NoteGeometry.ResizeRight(1.0, 3.0, 0.03), 6);  // 3.0 - 1.0
            Assert.Equal(0.03, NoteGeometry.ResizeRight(1.0, 0.5, 0.03), 6); // end before start -> min
        }

        [Fact]
        public void ResizeLeft_KeepsEndFixed_AndHonorsMinDurationAndFloor()
        {
            NoteGeometry.ResizeLeft(fixedEndSeconds: 3.0, newStartSeconds: 1.0, minDurationSeconds: 0.03,
                out double start, out double dur);
            Assert.Equal(1.0, start, 6);
            Assert.Equal(2.0, dur, 6);

            // Dragging the left edge past the end pins it to end - minDuration.
            NoteGeometry.ResizeLeft(3.0, 3.5, 0.03, out start, out dur);
            Assert.Equal(2.97, start, 6);
            Assert.Equal(0.03, dur, 6);

            // Negative start is floored to 0 (duration grows to the fixed end).
            NoteGeometry.ResizeLeft(3.0, -1.0, 0.03, out start, out dur);
            Assert.Equal(0.0, start, 6);
            Assert.Equal(3.0, dur, 6);
        }
    }
}
