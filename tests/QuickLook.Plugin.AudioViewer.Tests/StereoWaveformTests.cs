using QuickLook.Plugin.AudioViewer.Audio;
using QuickLook.Plugin.AudioViewer.Settings;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class StereoWaveformTests
    {
        // A 2-channel fixture with distinct, easy-to-reason-about column peaks, plus a
        // known mono envelope that is deliberately NOT the union/average of the peaks
        // (so tests can tell which branch produced the output).
        private static WaveformData Stereo()
        {
            var min = new[]
            {
                new[] { -0.2f, -0.9f, -0.1f },  // L
                new[] { -0.5f, -0.3f, -0.4f },  // R
            };
            var max = new[]
            {
                new[] { 0.8f, 0.2f, 0.6f },     // L
                new[] { 0.3f, 0.7f, 0.5f },     // R
            };
            var combinedMin = new[] { -0.11f, -0.22f, -0.33f };
            var combinedMax = new[] { 0.44f, 0.55f, 0.66f };
            return WaveformData.FromChannels(min, max, combinedMin, combinedMax);
        }

        [Fact]
        public void PeakUnion_CollapsesToOneLane_UnionOfPeaks()
        {
            var result = StereoWaveform.Apply(Stereo(), StereoWaveformMode.PeakUnion);

            Assert.Equal(1, result.ChannelCount);
            Assert.Equal(3, result.Columns);
            // max = max of both channels' maxes; min = min of both channels' mins.
            Assert.Equal(new[] { 0.8f, 0.7f, 0.6f }, result.Max[0]);
            Assert.Equal(new[] { -0.5f, -0.9f, -0.4f }, result.Min[0]);
        }

        [Fact]
        public void MonoAverage_ReturnsTheScannedCombinedEnvelope()
        {
            var result = StereoWaveform.Apply(Stereo(), StereoWaveformMode.MonoAverage);

            Assert.Equal(1, result.ChannelCount);
            // Uses the sample-exact combined arrays, NOT a peak union/average of the columns.
            Assert.Equal(new[] { 0.44f, 0.55f, 0.66f }, result.Max[0]);
            Assert.Equal(new[] { -0.11f, -0.22f, -0.33f }, result.Min[0]);
        }

        [Fact]
        public void MonoAverage_FallsBackToPeakUnion_WhenCombinedMissing()
        {
            // A stereo scan always fills the combined arrays; this is the defensive path.
            var noCombined = WaveformData.FromChannels(
                new[] { new[] { -0.2f }, new[] { -0.5f } },
                new[] { new[] { 0.8f }, new[] { 0.3f } });

            var result = StereoWaveform.Apply(noCombined, StereoWaveformMode.MonoAverage);

            Assert.Equal(1, result.ChannelCount);
            Assert.Equal(0.8f, result.Max[0][0]);   // union, not (null) combined
            Assert.Equal(-0.5f, result.Min[0][0]);
        }

        [Fact]
        public void Split_ReturnsInputUnchanged()
        {
            var raw = Stereo();
            var result = StereoWaveform.Apply(raw, StereoWaveformMode.Split);
            Assert.Same(raw, result);
        }

        [Theory]
        [InlineData(StereoWaveformMode.Split)]
        [InlineData(StereoWaveformMode.MonoAverage)]
        [InlineData(StereoWaveformMode.PeakUnion)]
        public void Mono_ReturnsInputUnchanged(StereoWaveformMode mode)
        {
            var mono = WaveformData.FromChannels(
                new[] { new[] { -0.3f, -0.1f } },
                new[] { new[] { 0.5f, 0.2f } });
            Assert.Same(mono, StereoWaveform.Apply(mono, mode));
        }

        [Theory]
        [InlineData(StereoWaveformMode.MonoAverage)]
        [InlineData(StereoWaveformMode.PeakUnion)]
        public void ThreeChannel_ReturnsInputUnchanged(StereoWaveformMode mode)
        {
            var three = WaveformData.FromChannels(
                new[] { new[] { -0.3f }, new[] { -0.1f }, new[] { -0.2f } },
                new[] { new[] { 0.5f }, new[] { 0.2f }, new[] { 0.4f } });
            Assert.Same(three, StereoWaveform.Apply(three, mode));
        }

        [Fact]
        public void Null_ReturnsNull()
        {
            Assert.Null(StereoWaveform.Apply(null, StereoWaveformMode.MonoAverage));
        }
    }
}
