using System;
using NAudio.Wave;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>
    /// Downmixes an N-channel <see cref="ISampleProvider"/> to stereo per a live-swappable
    /// <see cref="ChannelSelection"/>. Shared by playback (WASAPI shared mode typically only
    /// accepts mono/stereo) and the spectrogram (to analyze a single soloed channel).
    /// </summary>
    public sealed class ChannelMixSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly int _sourceChannels;
        private float[] _readBuffer = new float[0];

        public ChannelMixSampleProvider(ISampleProvider source, ChannelSelection initialSelection)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _sourceChannels = source.WaveFormat.Channels;
            if (_sourceChannels < 1)
                throw new ArgumentException("Source must have at least one channel.", nameof(source));
            Selection = initialSelection;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 2);
        }

        /// <summary>Mutable so the UI can switch what's playing/analyzed without rebuilding the pipeline.</summary>
        public ChannelSelection Selection { get; set; }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int framesRequested = count / 2;
            int sourceCount = framesRequested * _sourceChannels;
            if (_readBuffer.Length < sourceCount) _readBuffer = new float[sourceCount];

            int sourceRead = _source.Read(_readBuffer, 0, sourceCount);
            int framesRead = sourceRead / _sourceChannels;

            var sel = Selection;
            for (int f = 0; f < framesRead; f++)
            {
                int baseIdx = f * _sourceChannels;
                float left, right;
                switch (sel.Mode)
                {
                    case ChannelSelectionMode.Solo:
                        left = right = _readBuffer[baseIdx + Clamp(sel.ChannelA)];
                        break;
                    case ChannelSelectionMode.SoloPair:
                        left = _readBuffer[baseIdx + Clamp(sel.ChannelA)];
                        right = _readBuffer[baseIdx + Clamp(sel.ChannelB)];
                        break;
                    default: // Mix
                        float sum = 0f;
                        for (int c = 0; c < _sourceChannels; c++) sum += _readBuffer[baseIdx + c];
                        left = right = sum / _sourceChannels;
                        break;
                }
                buffer[offset + f * 2] = left;
                buffer[offset + f * 2 + 1] = right;
            }
            return framesRead * 2;
        }

        private int Clamp(int channel) =>
            channel < 0 ? 0 : channel >= _sourceChannels ? _sourceChannels - 1 : channel;
    }
}
