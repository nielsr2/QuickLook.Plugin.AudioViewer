using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    public sealed class AudioPlayer : IDisposable
    {
        private readonly WaveStream _stream;
        private readonly VolumeSampleProvider _volume;
        private WasapiOut _output;
        private bool _disposed;

        public event EventHandler PlaybackStopped;

        public AudioPlayer(WaveStream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            try
            {
                _volume = new VolumeSampleProvider(_stream.ToSampleProvider()) { Volume = 1.0f };
                _output = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 100);
                _output.Init(_volume);
                _output.PlaybackStopped += (s, e) => PlaybackStopped?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                // Ownership rule: once this constructor is called it owns the stream.
                // WasapiOut.Init can throw (e.g. an unsupported shared-mode format); on
                // any failure dispose everything we created — including the stream — so
                // the caller never leaks the file handle (a leaked handle keeps the file
                // locked, blocking the user from deleting or moving it).
                _output?.Dispose();
                _output = null;
                _stream.Dispose();
                throw;
            }
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
