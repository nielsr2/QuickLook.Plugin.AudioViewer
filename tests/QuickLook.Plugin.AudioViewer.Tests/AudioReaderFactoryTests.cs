using System;
using System.IO;
using QuickLook.Plugin.AudioViewer.Audio;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class AudioReaderFactoryTests
    {
        [Fact]
        public void Create_WavFile_ReportsCorrectFormat()
        {
            var dir = Path.GetTempPath();
            var wav = TestSignals.WriteSineWav(dir, sampleRate: 44100, seconds: 2.0, channels: 2);
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    Assert.Equal(44100, stream.WaveFormat.SampleRate);
                    Assert.Equal(2, stream.WaveFormat.Channels);
                    Assert.InRange(stream.TotalTime.TotalSeconds, 1.9, 2.1);
                }
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void Create_SixteenChannelWav_OpensAndStreamsCorrectly()
        {
            var dir = Path.GetTempPath();
            var wav = TestSignals.WriteSineWav(dir, sampleRate: 44100, seconds: 0.5, channels: 16);
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    Assert.Equal(16, stream.WaveFormat.Channels);
                    Assert.InRange(stream.TotalTime.TotalSeconds, 0.45, 0.55);
                }
                using (var scan = AudioReaderFactory.Create(wav))
                {
                    var wf = QuickLook.Plugin.AudioViewer.Audio.WaveformData.Compute(scan, columns: 100);
                    Assert.Equal(16, wf.ChannelCount);
                    Assert.Equal(100, wf.Columns);
                }
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void Create_UnsupportedOrCorrupt_ThrowsAudioFormatException()
        {
            var dir = Path.GetTempPath();
            var bogus = Path.Combine(dir, $"bogus_{Guid.NewGuid():N}.wav");
            File.WriteAllText(bogus, "this is not audio");
            try
            {
                Assert.Throws<AudioFormatException>(() => AudioReaderFactory.Create(bogus));
            }
            finally { File.Delete(bogus); }
        }
    }
}
