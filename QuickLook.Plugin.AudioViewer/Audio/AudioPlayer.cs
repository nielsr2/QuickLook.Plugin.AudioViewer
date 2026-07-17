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
            _volume = new VolumeSampleProvider(_stream.ToSampleProvider()) { Volume = 1.0f };
            _output = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 100);
            _output.Init(_volume);
            _output.PlaybackStopped += (s, e) => PlaybackStopped?.Invoke(this, EventArgs.Empty);
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
            var clamped = position < TimeSpan.Zero ? TimeSpan.Zero
                : position > _stream.TotalTime ? _stream.TotalTime : position;
            _stream.CurrentTime = clamped;
        }

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
