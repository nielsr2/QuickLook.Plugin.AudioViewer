using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using NAudio.Wave;
using QuickLook.Common.Plugin;
using QuickLook.Plugin.AudioViewer.Audio;
using QuickLook.Plugin.AudioViewer.Settings;

namespace QuickLook.Plugin.AudioViewer
{
    public partial class AudioViewerControl : UserControl
    {
        private readonly string _path;
        private readonly ContextObject _context;
        private AudioPlayer _player;
        private DispatcherTimer _timer;
        private bool _scrubbing;
        private bool _showingSpectrogram;
        private bool _spectrogramBuilt;

        public AudioViewerControl(string path, ContextObject context)
        {
            InitializeComponent();
            _path = path;
            _context = context;
            AutoPlayCheck.IsChecked = PluginSettings.AutoPlay;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            try
            {
                WaveStream playbackStream = null;
                WaveformData waveform = null;

                await Task.Run(() =>
                {
                    playbackStream = AudioReaderFactory.Create(_path);
                    using (var scan = AudioReaderFactory.Create(_path))
                        waveform = WaveformData.Compute(scan);
                });

                _player = new AudioPlayer(playbackStream);
                _player.PlaybackStopped += (s, a) => Dispatcher.Invoke(OnPlaybackStopped);
                _player.Volume = (float)VolumeSlider.Value;

                Waveform.Waveform = waveform;
                StatusText.Visibility = Visibility.Collapsed;
                UpdateTimeText();

                _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
                _timer.Tick += (s, a) => Tick();
                _timer.Start();

                if (PluginSettings.AutoPlay) TogglePlay(true);
            }
            catch (AudioFormatException ex)
            {
                ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                ShowError("Could not play this audio file. " + ex.Message);
            }
        }

        private void ShowError(string message)
        {
            StatusText.Text = message;
            StatusText.Visibility = Visibility.Visible;
            PlayPauseButton.IsEnabled = false;
            SeekSlider.IsEnabled = false;
            SpectrogramButton.IsEnabled = false;
        }

        private void Tick()
        {
            if (_player == null || _scrubbing) return;
            double dur = _player.Duration.TotalSeconds;
            double pos = _player.Position.TotalSeconds;
            double frac = dur > 0 ? Math.Min(1.0, pos / dur) : 0;
            SeekSlider.Value = frac;
            Waveform.PlayheadFraction = frac;
            UpdateSpectrogramPlayhead(frac);
            UpdateTimeText();
        }

        private void UpdateTimeText()
        {
            if (_player == null) return;
            TimeText.Text = $"{Fmt(_player.Position)} / {Fmt(_player.Duration)}";
        }

        private static string Fmt(TimeSpan t) => $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e) => TogglePlay(!_player?.IsPlaying ?? false);

        private void TogglePlay(bool play)
        {
            if (_player == null) return;
            if (play) { _player.Play(); PlayPauseButton.Content = "⏸"; }
            else { _player.Pause(); PlayPauseButton.Content = "▶"; }
        }

        private void OnPlaybackStopped()
        {
            // reached end (or stopped): reset button glyph
            if (_player != null && _player.Position >= _player.Duration - TimeSpan.FromMilliseconds(100))
                PlayPauseButton.Content = "▶";
        }

        private void SeekSlider_DragStarted(object sender, DragStartedEventArgs e) => _scrubbing = true;

        private void SeekSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _scrubbing = false;
            SeekTo(SeekSlider.Value);
        }

        private void SeekTo(double fraction)
        {
            if (_player == null) return;
            _player.Seek(TimeSpan.FromSeconds(fraction * _player.Duration.TotalSeconds));
            Waveform.PlayheadFraction = fraction;
            UpdateSpectrogramPlayhead(fraction);
            UpdateTimeText();
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_player != null) _player.Volume = (float)e.NewValue;
        }

        private void AutoPlayCheck_Changed(object sender, RoutedEventArgs e)
        {
            PluginSettings.AutoPlay = AutoPlayCheck.IsChecked == true;
        }

        private void SpectrogramButton_Click(object sender, RoutedEventArgs e)
        {
            _showingSpectrogram = !_showingSpectrogram;
            if (_showingSpectrogram && !_spectrogramBuilt) BuildSpectrogram();

            Waveform.Visibility = _showingSpectrogram ? Visibility.Collapsed : Visibility.Visible;
            SpectrogramImage.Visibility = _showingSpectrogram ? Visibility.Visible : Visibility.Collapsed;
            SpectrogramPlayhead.Visibility = _showingSpectrogram && _spectrogramBuilt
                ? Visibility.Visible : Visibility.Collapsed;
            SpectrogramButton.Content = _showingSpectrogram ? "◂ Waveform" : "Spectrogram ▸";
        }

        private async void BuildSpectrogram()
        {
            SpectrogramButton.IsEnabled = false;
            StatusText.Text = "Generating spectrogram…";
            StatusText.Visibility = Visibility.Visible;

            int w = Math.Max(1, (int)Waveform.ActualWidth);
            int h = Math.Max(1, (int)Waveform.ActualHeight);
            System.Windows.Media.Imaging.WriteableBitmap bmp = null;

            bool capped = false;
            await Task.Run(() =>
            {
                using (var stream = AudioReaderFactory.Create(_path))
                {
                    var mags = SpectrogramGenerator.ComputeMagnitudes(stream.ToSampleProvider(),
                        fftSize: 1024, hop: 512, maxFrames: 4000, out capped);
                    bmp = SpectrogramGenerator.Render(mags, w, h);
                    bmp.Freeze();
                }
            });

            SpectrogramImage.Source = bmp;
            _spectrogramBuilt = true;
            StatusText.Visibility = capped ? Visibility.Visible : Visibility.Collapsed;
            if (capped) StatusText.Text = "Spectrogram (time-truncated for length)";
            SpectrogramButton.IsEnabled = true;
        }

        private void UpdateSpectrogramPlayhead(double frac)
        {
            if (!_showingSpectrogram || !_spectrogramBuilt) return;
            SpectrogramPlayhead.Margin = new Thickness(frac * SpectrogramImage.ActualWidth, 0, 0, 0);
        }

        public void Unload()
        {
            _timer?.Stop();
            _player?.Dispose();
            _player = null;
        }
    }
}
