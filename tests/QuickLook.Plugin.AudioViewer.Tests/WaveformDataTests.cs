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
                    Assert.Equal(500, wf.Columns);
                    // Somewhere the positive peak is ~+1 and the negative peak is ~-1.
                    float globalMax = float.MinValue, globalMin = float.MaxValue;
                    for (int i = 0; i < wf.Columns; i++)
                    {
                        if (wf.Max[i] > globalMax) globalMax = wf.Max[i];
                        if (wf.Min[i] < globalMin) globalMin = wf.Min[i];
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
                        Assert.InRange(wf.Max[i], -0.001f, 0.001f);
                        Assert.InRange(wf.Min[i], -0.001f, 0.001f);
                    }
                }
            }
            finally { File.Delete(wav); }
        }
    }
}
