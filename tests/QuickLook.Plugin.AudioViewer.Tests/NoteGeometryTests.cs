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
    }
}
