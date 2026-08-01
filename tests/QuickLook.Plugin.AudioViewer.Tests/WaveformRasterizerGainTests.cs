using QuickLook.Plugin.AudioViewer.Audio;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    /// <summary>
    /// Vertical magnification and clip indication. The pre-existing rasterizer tests pin
    /// the un-magnified contract and must keep passing untouched — gain defaults to 1 and
    /// clip indication is off unless a colour is supplied, precisely so they do.
    /// </summary>
    public class WaveformRasterizerGainTests
    {
        private const int Wave = unchecked((int)0xFF4FC3F7);
        private const int Background = unchecked((int)0xFF1E1E1E);
        private const int Clip = unchecked((int)0xFFFFB300);

        private static WaveformData OneChannel(float peak, int columns = 8)
        {
            var min = new float[columns];
            var max = new float[columns];
            for (int i = 0; i < columns; i++) { min[i] = -peak; max[i] = peak; }
            return WaveformData.FromChannels(new[] { min }, new[] { max }, null, null);
        }

        private static int ColouredRows(int[] px, int width, int height, int column, int colour)
        {
            int n = 0;
            for (int y = 0; y < height; y++)
                if (px[y * width + column] == colour) n++;
            return n;
        }

        [Fact]
        public void GainOfOne_IsIdenticalToTheDefault()
        {
            var wf = OneChannel(0.5f);
            var a = WaveformRasterizer.RenderPixels(wf, 20, Wave, Background);
            var b = WaveformRasterizer.RenderPixels(wf, 20, Wave, Background, gain: 1.0);
            Assert.Equal(a, b);
        }

        [Fact]
        public void DoublingGainDoublesTheDrawnHeight()
        {
            var wf = OneChannel(0.25f);
            const int lane = 40;

            int atOne = ColouredRows(WaveformRasterizer.RenderPixels(wf, lane, Wave, Background, 1.0),
                8, lane, 0, Wave);
            int atTwo = ColouredRows(WaveformRasterizer.RenderPixels(wf, lane, Wave, Background, 2.0),
                8, lane, 0, Wave);

            // 0.25 of a half-lane either side at 1x -> ~11 rows; 2x -> ~21.
            Assert.InRange(atTwo, atOne * 2 - 2, atOne * 2 + 2);
        }

        [Fact]
        public void ExcessiveGainClampsWithoutBleedingIntoTheNeighbouringLane()
        {
            const int columns = 4, lane = 20;
            // Channel 0 is loud and heavily magnified; channel 1 is silent.
            var loudMin = new float[columns]; var loudMax = new float[columns];
            var quietMin = new float[columns]; var quietMax = new float[columns];
            for (int i = 0; i < columns; i++) { loudMin[i] = -0.8f; loudMax[i] = 0.8f; }

            var wf = WaveformData.FromChannels(
                new[] { loudMin, quietMin }, new[] { loudMax, quietMax }, null, null);

            var px = WaveformRasterizer.RenderPixels(wf, lane, Wave, Background, gain: 16.0);

            // The silent lane still shows only its own midline.
            int second = ColouredRows(px, columns, lane * 2, 0, Wave);
            Assert.Equal(lane + 1, second);   // 20 rows of lane 0 plus lane 1's single midline
        }

        [Fact]
        public void ClipIndication_IsOffUnlessAColourIsGiven()
        {
            var wf = OneChannel(0.9f);
            var px = WaveformRasterizer.RenderPixels(wf, 20, Wave, Background, gain: 8.0);

            // Only the two supplied constants appear — the pre-existing contract.
            foreach (var p in px)
                Assert.True(p == Wave || p == Background, $"unexpected colour 0x{p:X8}");
        }

        [Fact]
        public void ClipIndication_MarksOnlyTheColumnsActuallyCutOff()
        {
            const int columns = 4, lane = 20;
            var min = new float[columns];
            var max = new float[columns];
            // Column 0 is loud enough to overflow at 4x; the rest stay well inside.
            min[0] = -0.9f; max[0] = 0.9f;
            for (int i = 1; i < columns; i++) { min[i] = -0.05f; max[i] = 0.05f; }

            var wf = WaveformData.FromChannels(new[] { min }, new[] { max }, null, null);
            var px = WaveformRasterizer.RenderPixels(wf, lane, Wave, Background, gain: 4.0, clipArgb: Clip);

            Assert.Equal(Clip, px[0 * columns + 0]);              // top row of the clipped column
            Assert.Equal(Clip, px[(lane - 1) * columns + 0]);     // bottom row
            Assert.NotEqual(Clip, px[0 * columns + 1]);           // a quiet column is untouched
        }

        [Fact]
        public void NonFiniteOrNegativeGainFallsBackToUnity()
        {
            var wf = OneChannel(0.5f);
            var expected = WaveformRasterizer.RenderPixels(wf, 20, Wave, Background);

            Assert.Equal(expected, WaveformRasterizer.RenderPixels(wf, 20, Wave, Background, double.NaN));
            Assert.Equal(expected, WaveformRasterizer.RenderPixels(wf, 20, Wave, Background, -3.0));
            Assert.Equal(expected, WaveformRasterizer.RenderPixels(wf, 20, Wave, Background, 0.0));
        }
    }
}
