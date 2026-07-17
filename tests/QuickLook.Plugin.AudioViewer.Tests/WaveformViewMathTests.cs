using QuickLook.Plugin.AudioViewer.Controls;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class WaveformViewMathTests
    {
        [Theory]
        [InlineData(0, 200, 0.0)]
        [InlineData(100, 200, 0.5)]
        [InlineData(200, 200, 1.0)]
        [InlineData(-10, 200, 0.0)]   // clamp low
        [InlineData(500, 200, 1.0)]   // clamp high
        public void FractionFromX_MapsAndClamps(double x, double width, double expected)
        {
            Assert.Equal(expected, WaveformView.FractionFromX(x, width), 3);
        }
    }
}
