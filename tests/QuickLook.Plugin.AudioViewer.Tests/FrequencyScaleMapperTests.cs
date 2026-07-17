using QuickLook.Plugin.AudioViewer.Audio;
using QuickLook.Plugin.AudioViewer.Settings;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class FrequencyScaleMapperTests
    {
        [Fact]
        public void HzToMel_Zero_IsZero()
        {
            Assert.Equal(0.0, FrequencyScaleMapper.HzToMel(0), 6);
        }

        [Fact]
        public void HzToMel_1000Hz_IsAbout1000Mel()
        {
            // The 2595/700 constants make mel(1000 Hz) ≈ 1000 by construction.
            Assert.InRange(FrequencyScaleMapper.HzToMel(1000), 998.0, 1002.0);
        }

        [Theory]
        [InlineData(50)]
        [InlineData(440)]
        [InlineData(1000)]
        [InlineData(8000)]
        public void MelToHz_RoundTrips(double hz)
        {
            double back = FrequencyScaleMapper.MelToHz(FrequencyScaleMapper.HzToMel(hz));
            Assert.InRange(back, hz - 0.01, hz + 0.01);
        }

        [Theory]
        [InlineData(FrequencyScale.Linear, 0.0, 22050.0)]
        [InlineData(FrequencyScale.Mel, 0.0, 22050.0)]
        [InlineData(FrequencyScale.Log, 20.0, 20000.0)]
        public void FractionToFrequency_Endpoints_HitFMinAndFMax(FrequencyScale scale, double fMin, double fMax)
        {
            Assert.InRange(FrequencyScaleMapper.FractionToFrequency(0.0, scale, fMin, fMax), fMin - 0.01, fMin + 0.01);
            Assert.InRange(FrequencyScaleMapper.FractionToFrequency(1.0, scale, fMin, fMax), fMax - 0.01, fMax + 0.01);
        }

        [Fact]
        public void FractionToFrequency_Linear_MidpointIsArithmeticMean()
        {
            Assert.InRange(FrequencyScaleMapper.FractionToFrequency(0.5, FrequencyScale.Linear, 0, 22050), 11024.0, 11026.0);
        }

        [Fact]
        public void FractionToFrequency_Log_MidpointIsGeometricMean()
        {
            // sqrt(20 * 20000) = 632.4555
            Assert.InRange(FrequencyScaleMapper.FractionToFrequency(0.5, FrequencyScale.Log, 20, 20000), 631.0, 634.0);
        }

        [Theory]
        [InlineData(FrequencyScale.Linear, 0.0, 22050.0)]
        [InlineData(FrequencyScale.Mel, 0.0, 22050.0)]
        [InlineData(FrequencyScale.Log, 20.0, 20000.0)]
        public void FractionToFrequency_IsMonotonicIncreasing(FrequencyScale scale, double fMin, double fMax)
        {
            double prev = double.MinValue;
            foreach (var u in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
            {
                double f = FrequencyScaleMapper.FractionToFrequency(u, scale, fMin, fMax);
                Assert.True(f > prev, $"not increasing at u={u}: {f} <= {prev}");
                prev = f;
            }
        }

        [Fact]
        public void BuildRowBins_LinearFullRange_CoversZeroToMaxBin()
        {
            var rows = FrequencyScaleMapper.BuildRowBins(64, FrequencyScale.Linear, fftSize: 1024, sampleRate: 44100, fMaxHz: 0);
            Assert.Equal(64, rows.Length);
            Assert.Equal(0, rows[0].Lo);
            Assert.Equal(511, rows[63].Hi); // fftSize/2 - 1
            // monotonic non-decreasing
            for (int i = 1; i < rows.Length; i++)
                Assert.True(rows[i].Lo >= rows[i - 1].Lo && rows[i].Hi >= rows[i - 1].Hi);
            // every row spans at least one valid bin
            foreach (var r in rows)
                Assert.True(r.Hi >= r.Lo && r.Lo >= 0 && r.Hi <= 511);
        }

        [Fact]
        public void BuildRowBins_MaxFrequencyCap_LimitsTopBin()
        {
            // 5000 Hz -> bin ≈ 5000 * 1024 / 44100 ≈ 116
            var rows = FrequencyScaleMapper.BuildRowBins(64, FrequencyScale.Linear, 1024, 44100, fMaxHz: 5000);
            Assert.InRange(rows[63].Hi, 112, 120);
        }
    }
}
