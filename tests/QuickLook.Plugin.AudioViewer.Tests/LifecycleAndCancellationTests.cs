using System;
using System.IO;
using System.Threading;
using NAudio.Wave;
using QuickLook.Plugin.AudioViewer.Audio;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    /// <summary>
    /// Guards the ship-safety fixes: in-flight decode aborts on cancellation, and
    /// readers release their file handle on dispose (so a previewed file can be
    /// deleted/moved immediately — the failure mode of the close-during-load leak).
    /// </summary>
    public class LifecycleAndCancellationTests
    {
        [Fact]
        public void WaveformCompute_PreCancelledToken_Throws()
        {
            var wav = TestSignals.WriteSineWav(Path.GetTempPath(), seconds: 1.0);
            try
            {
                using (var cts = new CancellationTokenSource())
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    cts.Cancel();
                    Assert.Throws<OperationCanceledException>(
                        () => WaveformData.Compute(stream, 800, cts.Token));
                }
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void ComputeMagnitudes_PreCancelledToken_Throws()
        {
            var wav = TestSignals.WriteSineWav(Path.GetTempPath(), seconds: 1.0);
            try
            {
                using (var cts = new CancellationTokenSource())
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    cts.Cancel();
                    Assert.Throws<OperationCanceledException>(
                        () => SpectrogramGenerator.ComputeMagnitudes(
                            stream.ToSampleProvider(), 1024, 512, 100, out _, cts.Token));
                }
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void Reader_AfterDispose_ReleasesFileHandle()
        {
            var wav = TestSignals.WriteSineWav(Path.GetTempPath(), seconds: 0.5);
            try
            {
                var stream = AudioReaderFactory.Create(wav);
                stream.Dispose();
                // If the reader leaked the handle, this throws IOException (file in use).
                File.Delete(wav);
                Assert.False(File.Exists(wav));
            }
            finally { if (File.Exists(wav)) File.Delete(wav); }
        }
    }
}
