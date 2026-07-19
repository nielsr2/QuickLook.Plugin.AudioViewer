using QuickLook.Plugin.AudioViewer.Audio;
using QuickLook.Plugin.AudioViewer.Settings;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class ColormapsTests
    {
        private static double Lum(byte r, byte g, byte b) => 0.299 * r + 0.587 * g + 0.114 * b;

        [Fact]
        public void Grayscale_Endpoints_BlackToWhite()
        {
            Colormaps.Sample(SpectrogramColormap.Grayscale, 0.0, out var r0, out var g0, out var b0);
            Colormaps.Sample(SpectrogramColormap.Grayscale, 1.0, out var r1, out var g1, out var b1);
            Assert.Equal(0, (int)r0); Assert.Equal(0, (int)g0); Assert.Equal(0, (int)b0);
            Assert.Equal(255, (int)r1); Assert.Equal(255, (int)g1); Assert.Equal(255, (int)b1);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.3)]
        [InlineData(0.7)]
        [InlineData(1.0)]
        public void Grayscale_ChannelsEqual(double t)
        {
            Colormaps.Sample(SpectrogramColormap.Grayscale, t, out var r, out var g, out var b);
            Assert.True(r == g && g == b);
        }

        [Theory]
        [InlineData(SpectrogramColormap.Magma)]
        [InlineData(SpectrogramColormap.Viridis)]
        [InlineData(SpectrogramColormap.Grayscale)]
        [InlineData(SpectrogramColormap.Inferno)]
        [InlineData(SpectrogramColormap.Plasma)]
        [InlineData(SpectrogramColormap.Heat)]
        public void Luminance_IsMonotonic_LowToHigh(SpectrogramColormap map)
        {
            Colormaps.Sample(map, 0.0, out var r0, out var g0, out var b0);
            Colormaps.Sample(map, 0.5, out var r5, out var g5, out var b5);
            Colormaps.Sample(map, 1.0, out var r1, out var g1, out var b1);
            Assert.True(Lum(r0, g0, b0) < Lum(r5, g5, b5));
            Assert.True(Lum(r5, g5, b5) < Lum(r1, g1, b1));
        }

        [Fact]
        public void Magma_Endpoints_MatchStops()
        {
            Colormaps.Sample(SpectrogramColormap.Magma, 0.0, out var r0, out var g0, out var b0);
            Colormaps.Sample(SpectrogramColormap.Magma, 1.0, out var r1, out var g1, out var b1);
            Assert.Equal(0, (int)r0); Assert.Equal(0, (int)g0); Assert.Equal(4, (int)b0);
            Assert.Equal(252, (int)r1); Assert.Equal(253, (int)g1); Assert.Equal(191, (int)b1);
        }

        [Fact]
        public void Inferno_Endpoints_MatchStops()
        {
            Colormaps.Sample(SpectrogramColormap.Inferno, 0.0, out var r0, out var g0, out var b0);
            Colormaps.Sample(SpectrogramColormap.Inferno, 1.0, out var r1, out var g1, out var b1);
            Assert.Equal(0, (int)r0); Assert.Equal(0, (int)g0); Assert.Equal(4, (int)b0);
            Assert.Equal(252, (int)r1); Assert.Equal(255, (int)g1); Assert.Equal(164, (int)b1);
        }

        [Fact]
        public void Plasma_Endpoints_MatchStops()
        {
            Colormaps.Sample(SpectrogramColormap.Plasma, 0.0, out var r0, out var g0, out var b0);
            Colormaps.Sample(SpectrogramColormap.Plasma, 1.0, out var r1, out var g1, out var b1);
            Assert.Equal(13, (int)r0); Assert.Equal(8, (int)g0); Assert.Equal(135, (int)b0);
            Assert.Equal(240, (int)r1); Assert.Equal(249, (int)g1); Assert.Equal(33, (int)b1);
        }

        [Fact]
        public void Turbo_Endpoints_MatchStops()
        {
            Colormaps.Sample(SpectrogramColormap.Turbo, 0.0, out var r0, out var g0, out var b0);
            Colormaps.Sample(SpectrogramColormap.Turbo, 1.0, out var r1, out var g1, out var b1);
            Assert.Equal(48, (int)r0); Assert.Equal(18, (int)g0); Assert.Equal(59, (int)b0);
            Assert.Equal(122, (int)r1); Assert.Equal(4, (int)g1); Assert.Equal(2, (int)b1);
        }

        [Fact]
        public void Heat_Endpoints_MatchStops()
        {
            Colormaps.Sample(SpectrogramColormap.Heat, 0.0, out var r0, out var g0, out var b0);
            Colormaps.Sample(SpectrogramColormap.Heat, 1.0, out var r1, out var g1, out var b1);
            Assert.Equal(0, (int)r0); Assert.Equal(0, (int)g0); Assert.Equal(0, (int)b0);
            Assert.Equal(255, (int)r1); Assert.Equal(255, (int)g1); Assert.Equal(255, (int)b1);
        }

        [Theory]
        [InlineData(SpectrogramColormap.Magma)]
        [InlineData(SpectrogramColormap.Viridis)]
        [InlineData(SpectrogramColormap.Grayscale)]
        [InlineData(SpectrogramColormap.Inferno)]
        [InlineData(SpectrogramColormap.Plasma)]
        [InlineData(SpectrogramColormap.Turbo)]
        [InlineData(SpectrogramColormap.Heat)]
        public void Sample_ClampsOutOfRange(SpectrogramColormap map)
        {
            Colormaps.Sample(map, -1.0, out var rn, out var gn, out var bn);
            Colormaps.Sample(map, 0.0, out var r0, out var g0, out var b0);
            Colormaps.Sample(map, 2.0, out var rp, out var gp, out var bp);
            Colormaps.Sample(map, 1.0, out var r1, out var g1, out var b1);
            Assert.Equal((r0, g0, b0), (rn, gn, bn));
            Assert.Equal((r1, g1, b1), (rp, gp, bp));
        }
    }
}
