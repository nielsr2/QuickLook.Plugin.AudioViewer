using System;
using NAudio.Wave;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>
    /// Per-channel, per-column min/max peaks, computed in a single streaming pass
    /// with bounded memory (safe for long, many-channel files).
    /// </summary>
    public sealed class WaveformData
    {
        /// <summary>Indexed [channel][column].</summary>
        public float[][] Min { get; }
        /// <summary>Indexed [channel][column].</summary>
        public float[][] Max { get; }
        public int ChannelCount => Min.Length;
        public int Columns => Min.Length > 0 ? Min[0].Length : 0;

        private WaveformData(float[][] min, float[][] max)
        {
            Min = min;
            Max = max;
        }

        public static WaveformData Compute(WaveStream stream, int columns = 2000)
        {
            if (columns < 1) columns = 1;

            var provider = stream.ToSampleProvider();
            int channels = Math.Max(1, provider.WaveFormat.Channels);

            // Total per-channel frames from the decoded PCM length; fall back to a
            // single wide column if the reader can't report a length.
            long totalFrames = stream.BlockAlign > 0 ? stream.Length / stream.BlockAlign : 0;
            if (totalFrames <= 0) totalFrames = long.MaxValue;

            long framesPerColumn = Math.Max(1, totalFrames / columns);

            var min = new float[channels][];
            var max = new float[channels][];
            for (int c = 0; c < channels; c++)
            {
                min[c] = new float[columns];
                max[c] = new float[columns];
            }

            var buffer = new float[channels * 4096];
            long frameIndex = 0;
            int read;

            while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i + channels <= read; i += channels)
                {
                    int col = (int)Math.Min(columns - 1, frameIndex / framesPerColumn);
                    for (int c = 0; c < channels; c++)
                    {
                        float v = buffer[i + c];
                        if (v > max[c][col]) max[c][col] = v;
                        if (v < min[c][col]) min[c][col] = v;
                    }
                    frameIndex++;
                }
            }

            return new WaveformData(min, max);
        }
    }
}
