using QuickLook.Plugin.AudioViewer.Audio;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class ChannelMixSampleProviderTests
    {
        [Fact]
        public void Read_MixMode_AveragesAllChannels()
        {
            var source = new TestSignals.FixedChannelSampleProvider(44100, new float[] { 1f, 0f, -1f, 0.5f });
            var mixer = new ChannelMixSampleProvider(source, ChannelSelection.Mix);
            var buffer = new float[2];

            int read = mixer.Read(buffer, 0, 2);

            Assert.Equal(2, read);
            Assert.Equal(0.125f, buffer[0], 3); // (1 + 0 - 1 + 0.5) / 4
            Assert.Equal(0.125f, buffer[1], 3);
        }

        [Fact]
        public void Read_SoloMode_PassesOneChannelToBothOutputs()
        {
            var source = new TestSignals.FixedChannelSampleProvider(44100, new float[] { 1f, 0f, -1f, 0.5f });
            var mixer = new ChannelMixSampleProvider(source, ChannelSelection.Solo(2)); // 0-indexed -> -1f
            var buffer = new float[2];

            mixer.Read(buffer, 0, 2);

            Assert.Equal(-1f, buffer[0], 3);
            Assert.Equal(-1f, buffer[1], 3);
        }

        [Fact]
        public void Read_SoloPairMode_MapsChannelsToLeftAndRight()
        {
            var source = new TestSignals.FixedChannelSampleProvider(44100, new float[] { 1f, 0f, -1f, 0.5f });
            var mixer = new ChannelMixSampleProvider(source, ChannelSelection.SoloPair(0, 3));
            var buffer = new float[2];

            mixer.Read(buffer, 0, 2);

            Assert.Equal(1f, buffer[0], 3);
            Assert.Equal(0.5f, buffer[1], 3);
        }

        [Fact]
        public void WaveFormat_IsAlwaysStereoAtSourceSampleRate()
        {
            var source = new TestSignals.FixedChannelSampleProvider(48000, new float[6]);
            var mixer = new ChannelMixSampleProvider(source, ChannelSelection.Mix);

            Assert.Equal(2, mixer.WaveFormat.Channels);
            Assert.Equal(48000, mixer.WaveFormat.SampleRate);
        }

        [Fact]
        public void Selection_CanBeSwappedLiveAfterConstruction()
        {
            var source = new TestSignals.FixedChannelSampleProvider(44100, new float[] { 10f, 20f });
            var mixer = new ChannelMixSampleProvider(source, ChannelSelection.Solo(0));
            var buffer = new float[2];

            mixer.Selection = ChannelSelection.Solo(1);
            mixer.Read(buffer, 0, 2);

            Assert.Equal(20f, buffer[0], 3);
            Assert.Equal(20f, buffer[1], 3);
        }
    }
}
