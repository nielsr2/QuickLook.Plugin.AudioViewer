using System;
using QuickLook.Plugin.AudioViewer.Audio;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class AudioPlayerMathTests
    {
        [Fact]
        public void ClampSeek_BelowZero_ClampsToZero()
        {
            var total = TimeSpan.FromSeconds(10);
            Assert.Equal(TimeSpan.Zero, AudioPlayer.ClampSeek(TimeSpan.FromSeconds(-5), total));
        }

        [Fact]
        public void ClampSeek_BeyondTotal_ClampsToTotal()
        {
            var total = TimeSpan.FromSeconds(10);
            Assert.Equal(total, AudioPlayer.ClampSeek(TimeSpan.FromSeconds(20), total));
        }

        [Fact]
        public void ClampSeek_WithinBounds_Unchanged()
        {
            var total = TimeSpan.FromSeconds(10);
            var mid = TimeSpan.FromSeconds(4.2);
            Assert.Equal(mid, AudioPlayer.ClampSeek(mid, total));
        }
    }
}
