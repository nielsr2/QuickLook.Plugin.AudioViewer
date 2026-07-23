using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    public sealed class AudioPlayer : IDisposable
    {
        private readonly WaveStream _stream;
        private readonly VolumeSampleProvider _volume;
        private readonly ChannelMixSampleProvider _channelMix; // null when the source has <= 2 channels
        private WasapiOut _output;
        private bool _disposed;

        public event EventHandler PlaybackStopped;

        public AudioPlayer(WaveStream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            try
            {
                // Multichannel sources (>2ch) are downmixed to stereo for playback; mono/stereo
                // pass through unmodified (_channelMix stays null).
                var native = _stream.ToSampleProvider();
                _channelMix = TryWrapForPlayback(native);
                ISampleProvider playbackSource = _channelMix ?? native;

                _volume = new VolumeSampleProvider(playbackSource) { Volume = 1.0f };
                _output = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 100);
                _output.Init(_volume);
                _output.PlaybackStopped += (s, e) => PlaybackStopped?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                // Ownership rule: once this constructor is called it owns the stream.
                // WasapiOut.Init can throw (e.g. an unsupported shared-mode format), as can
                // the mix/provider setup; on any failure dispose everything we created —
                // including the stream — so the caller never leaks the file handle (a leaked
                // handle keeps the file locked, blocking the user from deleting or moving it).
                _output?.Dispose();
                _output = null;
                _stream.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Wraps a native sample provider with a stereo-downmixing <see cref="ChannelMixSampleProvider"/>
        /// when it has more channels than a typical WASAPI shared-mode endpoint accepts (2);
        /// returns null for mono/stereo sources, which play back unmodified.
        /// </summary>
        public static ChannelMixSampleProvider TryWrapForPlayback(ISampleProvider native) =>
            native.WaveFormat.Channels > 2 ? new ChannelMixSampleProvider(native, ChannelSelection.Mix) : null;

        /// <summary>True if this player's source has more than 2 channels and can be re-mixed live via <see cref="SetChannelSelection"/>.</summary>
        public bool SupportsChannelSelection => _channelMix != null;

        /// <summary>Change which channel(s) play, for multichannel sources. No-op for mono/stereo sources.</summary>
        public void SetChannelSelection(ChannelSelection selection)
        {
            if (_channelMix != null) _channelMix.Selection = selection;
        }

        public TimeSpan Duration => _stream.TotalTime;

        public TimeSpan Position => _stream.CurrentTime;

        public bool IsPlaying => _output != null && _output.PlaybackState == PlaybackState.Playing;

        public float Volume
        {
            get => _volume.Volume;
            set => _volume.Volume = Math.Max(0f, Math.Min(1f, value));
        }

        public void Play() => _output?.Play();

        public void Pause() => _output?.Pause();

        public void Stop()
        {
            _output?.Stop();
            _stream.CurrentTime = TimeSpan.Zero;
        }

        public void Seek(TimeSpan position)
        {
            _stream.CurrentTime = ClampSeek(position, _stream.TotalTime);
        }

        /// <summary>Clamp a seek target into [0, total]. Pure, so it is unit-tested.</summary>
        internal static TimeSpan ClampSeek(TimeSpan position, TimeSpan total)
            => position < TimeSpan.Zero ? TimeSpan.Zero
                : position > total ? total : position;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _output?.Stop(); } catch { }
            _output?.Dispose();
            _output = null;
            _stream?.Dispose();
        }
    }
}
