using System.IO;
using QuickLook.Plugin.AudioViewer.Audio;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class WaveformDataTests
    {
        [Fact]
        public void Compute_FullScaleSine_PeaksNearUnity()
        {
            var wav = TestSignals.WriteSineWav(Path.GetTempPath(), seconds: 1.0, amplitude: 1.0, channels: 1);
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var wf = WaveformData.Compute(stream, columns: 500);
                    Assert.Equal(1, wf.ChannelCount);
                    Assert.Equal(500, wf.Columns);
                    float globalMax = float.MinValue, globalMin = float.MaxValue;
                    for (int i = 0; i < wf.Columns; i++)
                    {
                        if (wf.Max[0][i] > globalMax) globalMax = wf.Max[0][i];
                        if (wf.Min[0][i] < globalMin) globalMin = wf.Min[0][i];
                    }
                    Assert.InRange(globalMax, 0.9f, 1.01f);
                    Assert.InRange(globalMin, -1.01f, -0.9f);
                }
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void Compute_Silence_PeaksNearZero()
        {
            var wav = TestSignals.WriteSilenceWav(Path.GetTempPath(), seconds: 1.0, channels: 1);
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var wf = WaveformData.Compute(stream, columns: 200);
                    for (int i = 0; i < wf.Columns; i++)
                    {
                        Assert.InRange(wf.Max[0][i], -0.001f, 0.001f);
                        Assert.InRange(wf.Min[0][i], -0.001f, 0.001f);
                    }
                }
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void Compute_MultiChannel_TracksPerChannelIndependently()
        {
            var wav = TestSignals.WriteMultiToneWav(Path.GetTempPath(), 44100, 1.0,
                new[] { 1.0, 0.0, 0.5 });
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var wf = WaveformData.Compute(stream, columns: 200);
                    Assert.Equal(3, wf.ChannelCount);

                    Assert.InRange(MaxOf(wf.Max[0]), 0.9f, 1.01f);   // full-scale channel
                    Assert.InRange(MaxOf(wf.Max[1]), -0.001f, 0.001f); // silent channel
                    Assert.InRange(MaxOf(wf.Max[2]), 0.4f, 0.6f);    // half-scale channel
                }
            }
            finally { File.Delete(wav); }
        }

        private static float MaxOf(float[] values)
        {
            float m = float.MinValue;
            foreach (var v in values) if (v > m) m = v;
            return m;
        }
    }
}
