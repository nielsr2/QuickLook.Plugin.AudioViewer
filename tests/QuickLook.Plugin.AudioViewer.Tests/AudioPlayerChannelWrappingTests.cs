using QuickLook.Plugin.AudioViewer.Audio;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class AudioPlayerChannelWrappingTests
    {
        [Fact]
        public void TryWrapForPlayback_MonoSource_ReturnsNull()
        {
            var source = new TestSignals.FixedChannelSampleProvider(44100, new float[] { 0f });
            Assert.Null(AudioPlayer.TryWrapForPlayback(source));
        }

        [Fact]
        public void TryWrapForPlayback_StereoSource_ReturnsNull()
        {
            var source = new TestSignals.FixedChannelSampleProvider(44100, new float[] { 0f, 0f });
            Assert.Null(AudioPlayer.TryWrapForPlayback(source));
        }

        [Fact]
        public void TryWrapForPlayback_MultichannelSource_ReturnsStereoMixerDefaultedToMix()
        {
            var source = new TestSignals.FixedChannelSampleProvider(44100, new float[16]);
            var wrapped = AudioPlayer.TryWrapForPlayback(source);

            Assert.NotNull(wrapped);
            Assert.Equal(2, wrapped.WaveFormat.Channels);
            Assert.Equal(ChannelSelectionMode.Mix, wrapped.Selection.Mode);
        }
    }
}
