using QuickLook.Plugin.AudioViewer.Audio;
using QuickLook.Plugin.AudioViewer.Settings;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    /// <summary>
    /// The frequency ruler and hover readout depend on FrequencyToFraction being the
    /// exact inverse of FractionToFrequency, so ticks line up with the colored rows.
    /// </summary>
    public class FrequencyInverseTests
    {
        [Theory]
        [InlineData(FrequencyScale.Linear)]
        [InlineData(FrequencyScale.Log)]
        [InlineData(FrequencyScale.Mel)]
        public void FractionToFrequency_RoundTrips(FrequencyScale scale)
        {
            double fMin = FrequencyScaleMapper.EffectiveFMin(scale, 1024, 44100);
            double fMax = 22050;
            for (double u = 0.0; u <= 1.0001; u += 0.1)
            {
                double f = FrequencyScaleMapper.FractionToFrequency(u, scale, fMin, fMax);
                double back = FrequencyScaleMapper.FrequencyToFraction(f, scale, fMin, fMax);
                Assert.InRange(back, u - 1e-6, u + 1e-6);
            }
        }

        [Theory]
        [InlineData(FrequencyScale.Linear)]
        [InlineData(FrequencyScale.Log)]
        [InlineData(FrequencyScale.Mel)]
        public void FrequencyToFraction_Endpoints(FrequencyScale scale)
        {
            double fMin = FrequencyScaleMapper.EffectiveFMin(scale, 1024, 44100);
            double fMax = 22050;
            Assert.InRange(FrequencyScaleMapper.FrequencyToFraction(fMin, scale, fMin, fMax), -1e-9, 1e-6);
            Assert.InRange(FrequencyScaleMapper.FrequencyToFraction(fMax, scale, fMin, fMax), 1.0 - 1e-6, 1.0 + 1e-9);
        }

        [Theory]
        [InlineData(0, 0, 22050)]     // full -> Nyquist
        [InlineData(10000, 0, 10000)] // capped below Nyquist
        [InlineData(50000, 0, 22050)] // above Nyquist -> clamped to Nyquist
        public void EffectiveFMax_ClampsToNyquist(double maxFreqHz, double _, double expected)
        {
            Assert.Equal(expected, FrequencyScaleMapper.EffectiveFMax(44100, maxFreqHz), 3);
        }
    }
}
