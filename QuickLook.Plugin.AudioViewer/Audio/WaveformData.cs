using System;
using System.Threading;
using NAudio.Wave;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>
    /// Per-column min/max peaks of a mono-mixed signal, computed in a single
    /// streaming pass with bounded memory (safe for long files).
    /// </summary>
    public sealed class WaveformData
    {
        public float[] Min { get; }
        public float[] Max { get; }
        public int Columns => Min.Length;

        private WaveformData(float[] min, float[] max)
        {
            Min = min;
            Max = max;
        }

        public static WaveformData Compute(WaveStream stream, int columns = 2000,
            CancellationToken token = default)
        {
            if (columns < 1) columns = 1;

            var provider = stream.ToSampleProvider();
            int channels = provider.WaveFormat.Channels;

            // Total mono frames from the decoded PCM length; fall back to a
            // single wide column if the reader can't report a length.
            long totalFrames = stream.BlockAlign > 0 ? stream.Length / stream.BlockAlign : 0;
            if (totalFrames <= 0) totalFrames = long.MaxValue;

            long framesPerColumn = Math.Max(1, totalFrames / columns);

            var min = new float[columns];
            var max = new float[columns];
            for (int i = 0; i < columns; i++)
            {
                min[i] = 0f;
                max[i] = 0f;
            }

            var buffer = new float[channels * 4096];
            long frameIndex = 0;
            int read;
            bool anyData = false;

            while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested(); // abort promptly if the preview closed
                anyData = true;
                for (int i = 0; i + channels <= read; i += channels)
                {
                    float mono = 0f;
                    for (int c = 0; c < channels; c++)
                        mono += buffer[i + c];
                    mono /= channels;

                    int col = (int)Math.Min(columns - 1, frameIndex / framesPerColumn);
                    if (mono > max[col]) max[col] = mono;
                    if (mono < min[col]) min[col] = mono;
                    frameIndex++;
                }
            }

            if (!anyData)
            {
                // empty stream: leave zeroed columns
            }

            return new WaveformData(min, max);
        }
    }
}
