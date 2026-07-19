using System.IO;
using QuickLook.Plugin.AudioViewer.Audio;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    /// <summary>
    /// BuildFileInfo runs before the AudioPlayer exists and its throw would leak the
    /// playback stream, so it must never throw for normal mono/stereo input and must
    /// format the core fields sanely.
    /// </summary>
    public class BuildFileInfoTests
    {
        [Theory]
        [InlineData(1, "Mono")]
        [InlineData(2, "Stereo")]
        public void BuildFileInfo_MonoAndStereo_DoesNotThrowAndReportsFields(int channels, string channelLabel)
        {
            var wav = TestSignals.WriteSineWav(Path.GetTempPath(), 44100, 1.0, 440, 0.5, channels);
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var info = AudioViewerControl.BuildFileInfo(wav, stream);

                    Assert.Contains("Sample rate", info);
                    Assert.Contains("44,100 Hz", info);
                    Assert.Contains(channelLabel, info);
                    Assert.Contains("Duration", info);
                    Assert.Contains("Bitrate", info);
                }
            }
            finally { File.Delete(wav); }
        }
    }
}
