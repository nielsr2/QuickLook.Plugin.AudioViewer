using System;
using NAudio.Dmo;
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

            return IsPcm16(stream.WaveFormat) ? ComputePcm16(stream, columns) : ComputeFloat(stream, columns);
        }

        /// <summary>
        /// True for 16-bit integer PCM, whether tagged as plain WaveFormatEncoding.Pcm or as
        /// WAVE_FORMAT_EXTENSIBLE with the PCM sub-format (the layout real multichannel audio
        /// interfaces typically write). Anything else — including extensible IEEE-float —
        /// returns false so the caller falls back to the safe, generic float decode path.
        /// </summary>
        private static bool IsPcm16(WaveFormat format)
        {
            if (format.BitsPerSample != 16) return false;
            if (format.Encoding == WaveFormatEncoding.Pcm) return true;
            if (format.Encoding != WaveFormatEncoding.Extensible) return false;

            // A WaveFormatExtensible built in-process (not round-tripped through a file)
            // keeps its SubFormat directly.
            if (format is WaveFormatExtensible ext)
                return ext.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_PCM;

            // NAudio's WaveFileReader parses an on-disk WAVE_FORMAT_EXTENSIBLE fmt chunk into
            // a WaveFormatExtraData, not a WaveFormatExtensible — the cbSize extension bytes
            // (validBitsPerSample:2, channelMask:4, SubFormat GUID:16) are exposed raw via
            // ExtraData instead. Decode the SubFormat GUID from that layout.
            if (format is WaveFormatExtraData extra && format.ExtraSize >= 22)
            {
                var extraData = extra.ExtraData;
                if (extraData != null && extraData.Length >= 22)
                {
                    var subFormatBytes = new byte[16];
                    Array.Copy(extraData, 6, subFormatBytes, 0, 16);
                    return new Guid(subFormatBytes) == AudioMediaSubtypes.MEDIASUBTYPE_PCM;
                }
            }

            return false;
        }

        /// <summary>
        /// Fast path for 16-bit integer PCM: reads raw bytes directly off the stream and
        /// decodes each sample inline, avoiding NAudio's per-sample Pcm16BitToSampleProvider.
        /// </summary>
        private static WaveformData ComputePcm16(WaveStream stream, int columns)
        {
            var format = stream.WaveFormat;
            int channels = Math.Max(1, format.Channels);
            int bytesPerFrame = stream.BlockAlign > 0 ? stream.BlockAlign : channels * 2;

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

            var minAcc = new short[channels];
            var maxAcc = new short[channels];
            ResetPcm16Accumulators(minAcc, maxAcc);

            var buffer = new byte[bytesPerFrame * 4096];
            int remainderBytes = 0;

            int col = 0;
            long framesLeftInColumn = framesPerColumn;
            long framesInColumn = 0;

            int read;
            while ((read = stream.Read(buffer, remainderBytes, buffer.Length - remainderBytes)) > 0)
            {
                int available = remainderBytes + read;
                int usableBytes = available - (available % bytesPerFrame);

                for (int offset = 0; offset < usableBytes; offset += bytesPerFrame)
                {
                    for (int c = 0; c < channels; c++)
                    {
                        int bi = offset + c * 2;
                        short v = (short)(buffer[bi] | (buffer[bi + 1] << 8));
                        if (v > maxAcc[c]) maxAcc[c] = v;
                        if (v < minAcc[c]) minAcc[c] = v;
                    }
                    framesInColumn++;

                    if (col < columns - 1)
                    {
                        framesLeftInColumn--;
                        if (framesLeftInColumn == 0)
                        {
                            FlushPcm16Column(min, max, minAcc, maxAcc, channels, col, framesInColumn);
                            col++;
                            framesLeftInColumn = framesPerColumn;
                            framesInColumn = 0;
                        }
                    }
                }

                remainderBytes = available - usableBytes;
                if (remainderBytes > 0)
                    Buffer.BlockCopy(buffer, usableBytes, buffer, 0, remainderBytes);
            }

            FlushPcm16Column(min, max, minAcc, maxAcc, channels, col, framesInColumn);

            return new WaveformData(min, max);
        }

        private static void ResetPcm16Accumulators(short[] minAcc, short[] maxAcc)
        {
            for (int c = 0; c < minAcc.Length; c++)
            {
                minAcc[c] = short.MaxValue;
                maxAcc[c] = short.MinValue;
            }
        }

        private static void FlushPcm16Column(float[][] min, float[][] max, short[] minAcc, short[] maxAcc,
            int channels, int col, long framesInColumn)
        {
            if (framesInColumn <= 0) return;

            for (int c = 0; c < channels; c++)
            {
                min[c][col] = minAcc[c] / 32768f;
                max[c][col] = maxAcc[c] / 32768f;
            }
            ResetPcm16Accumulators(minAcc, maxAcc);
        }

        /// <summary>
        /// Safe fallback for everything that isn't 16-bit integer PCM (float, extensible
        /// float, 8/24/32-bit int, ADPCM-via-decoder, etc.), decoded generically through
        /// NAudio's ISampleProvider chain.
        /// </summary>
        private static WaveformData ComputeFloat(WaveStream stream, int columns)
        {
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

            var minAcc = new float[channels];
            var maxAcc = new float[channels];
            ResetFloatAccumulators(minAcc, maxAcc);

            var buffer = new float[channels * 4096];
            int col = 0;
            long framesLeftInColumn = framesPerColumn;
            long framesInColumn = 0;
            int read;

            while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i + channels <= read; i += channels)
                {
                    for (int c = 0; c < channels; c++)
                    {
                        float v = buffer[i + c];
                        if (v > maxAcc[c]) maxAcc[c] = v;
                        if (v < minAcc[c]) minAcc[c] = v;
                    }
                    framesInColumn++;

                    if (col < columns - 1)
                    {
                        framesLeftInColumn--;
                        if (framesLeftInColumn == 0)
                        {
                            FlushFloatColumn(min, max, minAcc, maxAcc, channels, col, framesInColumn);
                            col++;
                            framesLeftInColumn = framesPerColumn;
                            framesInColumn = 0;
                        }
                    }
                }
            }

            FlushFloatColumn(min, max, minAcc, maxAcc, channels, col, framesInColumn);

            return new WaveformData(min, max);
        }

        private static void ResetFloatAccumulators(float[] minAcc, float[] maxAcc)
        {
            for (int c = 0; c < minAcc.Length; c++)
            {
                minAcc[c] = float.MaxValue;
                maxAcc[c] = float.MinValue;
            }
        }

        private static void FlushFloatColumn(float[][] min, float[][] max, float[] minAcc, float[] maxAcc,
            int channels, int col, long framesInColumn)
        {
            if (framesInColumn <= 0) return;

            for (int c = 0; c < channels; c++)
            {
                min[c][col] = minAcc[c];
                max[c][col] = maxAcc[c];
            }
            ResetFloatAccumulators(minAcc, maxAcc);
        }
    }
}
