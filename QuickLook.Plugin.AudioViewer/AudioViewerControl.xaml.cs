using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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

        // Cached linear STFT frames so setting changes re-render without re-running
        // the FFT; only an FFT-size change forces a recompute.
        private float[][] _spectrogramFrames;
        private int _spectrogramSampleRate;
        private int _spectrogramFftSize = -1;

        private ChannelSelection _channelSelection = ChannelSelection.Mix;
        private ChannelSelection _spectrogramChannelSelection = ChannelSelection.Mix;

        private bool _initializingSettings;
        private int _renderGeneration;

        private bool _infoDragging;
        private Point _infoDragStart;
        private double _infoStartX, _infoStartY;

        private static readonly int[] FftSizes = { 512, 1024, 2048, 4096 };
        private static readonly int[] MaxFreqs = { 0, 20000, 10000, 5000 };

        // Timeline sizing: the whole file is rendered at ~PxPerSec, bounded by
        // MaxColumns (caps bitmap width/memory and STFT frame count). Short files
        // fill the pane (MinWidth binding); long files scroll horizontally.
        private const double TimelinePxPerSec = 80;
        private const int MinTimelineColumns = 800;
        private const int MaxTimelineColumns = 12000;

        private static int TimelineColumns(double seconds)
        {
            int cols = (int)(seconds * TimelinePxPerSec);
            if (cols < MinTimelineColumns) cols = MinTimelineColumns;
            if (cols > MaxTimelineColumns) cols = MaxTimelineColumns;
            return cols;
        }

        public AudioViewerControl(string path, ContextObject context)
        {
            InitializeComponent();
            _path = path;
            _context = context;
            AutoPlayCheck.IsChecked = PluginSettings.AutoPlay;
            InitSettingsControls();
            Loaded += OnLoaded;
        }

        private void InitSettingsControls()
        {
            _initializingSettings = true;
            FileInfoCheck.IsChecked = PluginSettings.ShowFileInfo;
            LayoutCombo.SelectedIndex = (int)PluginSettings.Layout;
            FreqCombo.SelectedIndex = (int)PluginSettings.FreqScale;
            AmpCombo.SelectedIndex = (int)PluginSettings.AmpScale;
            ColorCombo.SelectedIndex = (int)PluginSettings.Colormap;
            FftCombo.SelectedIndex = Math.Max(0, Array.IndexOf(FftSizes, PluginSettings.FftSize));
            MaxFreqCombo.SelectedIndex = Math.Max(0, Array.IndexOf(MaxFreqs, PluginSettings.MaxFrequencyHz));
            ContrastSlider.Value = PluginSettings.DbFloor;
            _initializingSettings = false;
        }

        /// <summary>Show the channel selector only for files with more than 2 channels, and populate it.</summary>
        private void InitChannelCombo(int channels)
        {
            _channelSelection = ChannelSelection.Mix;
            if (channels <= 2)
            {
                ChannelCombo.Visibility = Visibility.Collapsed;
                return;
            }

            ChannelCombo.Items.Clear();
            ChannelCombo.Items.Add($"Mix (all {channels})");
            for (int c = 1; c <= channels; c++)
                ChannelCombo.Items.Add($"Channel {c}");
            ChannelCombo.SelectionChanged -= ChannelCombo_Changed; // avoid firing while pre-selecting
            ChannelCombo.SelectedIndex = 0;
            ChannelCombo.SelectionChanged += ChannelCombo_Changed;
            ChannelCombo.Visibility = Visibility.Visible;
        }

        private void ChannelCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (ChannelCombo.SelectedIndex < 0) return;
            _channelSelection = ChannelCombo.SelectedIndex == 0
                ? ChannelSelection.Mix
                : ChannelSelection.Solo(ChannelCombo.SelectedIndex - 1);
            _player?.SetChannelSelection(_channelSelection);
            // Only recompute now if the spectrogram is actually visible (Stacked layout, or
            // Single layout with the spectrogram toggled in) — _showingSpectrogram tracks
            // that in both layouts (ApplyLayout sets it true for Stacked). Otherwise skip the
            // full-file FFT; ShowSpectrogramAsync's own selectionChanged check forces the
            // recompute later when the user reveals the spectrogram (SpectrogramButton_Click
            // / switching to Stacked both call ShowSpectrogramAsync(false), which still
            // recomputes because _spectrogramChannelSelection is now stale).
            if (_showingSpectrogram) ShowSpectrogramAsync(true);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            WaveStream playbackStream = null;
            try
            {
                string infoText = null;

                // Phase 1 (fast): open the playback stream and build the file-info text
                // only. The slow full-file waveform scan is deferred to Phase 2 so info,
                // transport, and audio appear almost immediately instead of waiting on it.
                await Task.Run(() =>
                {
                    playbackStream = AudioReaderFactory.Create(_path);
                    infoText = BuildFileInfo(_path, playbackStream);
                });

                // File info, transport, and the player are set up independently of the
                // waveform (Phase 2) and even of playback: they show even if AudioPlayer
                // below fails to init (e.g. a many-channel WAV a stereo output device
                // rejects in WASAPI shared mode).
                SetTimelineWidth(playbackStream.TotalTime.TotalSeconds);
                FileInfoText.Text = infoText;
                FileInfoTranslate.X = PluginSettings.InfoPosX;
                FileInfoTranslate.Y = PluginSettings.InfoPosY;
                FileInfoPanel.Visibility = PluginSettings.ShowFileInfo ? Visibility.Visible : Visibility.Collapsed;
                // Keep the status line up while the background waveform scan runs (Phase 2
                // clears it). The exact text is also the guard Phase 2 checks before
                // collapsing, so it doesn't stomp the Stacked spectrogram path below.
                StatusText.Text = "Analyzing waveform…";
                StatusText.Visibility = Visibility.Visible;
                InitChannelCombo(playbackStream.WaveFormat.Channels);

                // Many-channel files (>2ch) always open showing just the waveform lane
                // stack, regardless of the persisted Layout setting: computing/showing the
                // spectrogram immediately for e.g. a 16-channel file is expensive and rarely
                // wanted on open. This is a per-file presentation default only — it does not
                // read or change PluginSettings.Layout, so the setting (and the flyout's
                // Layout combo, which still reflects it) is unaffected; SpectrogramButton
                // still brings the spectrogram up on demand via the normal Single-layout path.
                // Mono/stereo files keep the persisted layout exactly as before.
                if (playbackStream.WaveFormat.Channels > 2)
                {
                    ApplyLayout(LayoutMode.Single);
                }
                else
                {
                    ApplyLayout(PluginSettings.Layout);
                    if (PluginSettings.Layout == LayoutMode.Stacked)
                        ShowSpectrogramAsync(false);
                }

                try
                {
                    _player = new AudioPlayer(playbackStream);
                }
                catch (Exception ex)
                {
                    playbackStream.Dispose();
                    TimeText.Text = "Playback unavailable: " + ex.Message;
                    PlayPauseButton.IsEnabled = false;
                    SeekSlider.IsEnabled = false;
                    VolumeSlider.IsEnabled = false;
                    return;
                }

                _player.PlaybackStopped += (s, a) => Dispatcher.Invoke(OnPlaybackStopped);
                _player.Volume = (float)VolumeSlider.Value;
                UpdateTimeText();

                _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
                _timer.Tick += (s, a) => Tick();
                _timer.Start();

                if (PluginSettings.AutoPlay) TogglePlay(true);

                // Phase 2 (background): the slow full-file waveform scan. Its own stream
                // (disposed by the using) — playbackStream is owned by _player now. This
                // gets its own try/catch so a waveform failure never tears down the
                // already-running player.
                try
                {
                    WaveformData result = null;
                    // One-pending throttle: the scan thread fires ~10 progressive snapshots, but
                    // a slow UI thread must not accumulate a backlog. While a dispatched snapshot
                    // is still pending we skip delivering another; the final full result below
                    // always lands regardless of whether the last snapshot was skipped.
                    bool snapshotPending = false;
                    // Progressive snapshots post at Background priority, but the scan-completion
                    // continuation below resumes at Normal priority — and WPF's dispatcher drains
                    // by priority, not FIFO. So a snapshot queued just before the (now fast) scan
                    // finishes can run AFTER the final assignment and clobber the complete waveform
                    // with a partial one. Once the final result is shown, this flag blocks any late
                    // snapshot from regressing it. Both run on the single UI thread, so it's race-free.
                    bool waveformFinalized = false;
                    await Task.Run(() =>
                    {
                        using (var scan = AudioReaderFactory.Create(_path))
                            result = WaveformData.Compute(
                                scan,
                                TimelineColumns(scan.TotalTime.TotalSeconds),
                                onProgress: snapshot =>
                                {
                                    // Runs on the scan thread — all UI access via the dispatcher only.
                                    if (snapshotPending) return;
                                    snapshotPending = true;
                                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                                    {
                                        snapshotPending = false;
                                        if (waveformFinalized) return; // final result already shown; never regress to a partial
                                        if (!IsLoaded) return; // unloaded mid-scan; don't touch UI
                                        Waveform.Waveform = snapshot;
                                    }));
                                });
                    });

                    if (!IsLoaded) return; // control was unloaded mid-scan; don't touch UI
                    waveformFinalized = true;
                    Waveform.Waveform = result;
                    // Only collapse the status line if it still shows our Phase-1 message —
                    // the Stacked spectrogram path may have replaced it with its own status,
                    // which we must not stomp.
                    if (StatusText.Text == "Analyzing waveform…")
                        StatusText.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    if (!IsLoaded) return; // unloaded mid-scan; nothing to show
                    StatusText.Text = "Waveform unavailable: " + ex.Message;
                    StatusText.Visibility = Visibility.Visible;
                }
            }
            catch (AudioFormatException ex)
            {
                if (_player == null) playbackStream?.Dispose();
                ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                if (_player == null) playbackStream?.Dispose();
                ShowError("Could not open this audio file. " + ex.Message);
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
            // Called every ~33ms timer tick; the string only changes about once a second
            // (seconds resolution), so skip the property write when it's identical to avoid
            // needless TextBlock invalidation.
            string text = $"{Fmt(_player.Position)} / {Fmt(_player.Duration)}";
            if (TimeText.Text != text) TimeText.Text = text;
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
            ScrollPlayheadIntoView(fraction);
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

        private void FileInfoCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializingSettings) return;
            PluginSettings.ShowFileInfo = FileInfoCheck.IsChecked == true;
            FileInfoPanel.Visibility = PluginSettings.ShowFileInfo ? Visibility.Visible : Visibility.Collapsed;
        }

        private void FileInfoClose_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // don't also start a drag
            // Dismiss for this file only — leave the "Show file info" setting on, so a
            // new file shows it again (unless the user unticks it in settings).
            FileInfoPanel.Visibility = Visibility.Collapsed;
        }

        private void FileInfoPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _infoDragging = true;
            _infoDragStart = e.GetPosition(ViewPaneGrid);
            _infoStartX = FileInfoTranslate.X;
            _infoStartY = FileInfoTranslate.Y;
            FileInfoPanel.CaptureMouse();
            e.Handled = true;
        }

        private void FileInfoPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_infoDragging) return;
            var p = e.GetPosition(ViewPaneGrid);
            double nx = _infoStartX + (p.X - _infoDragStart.X);
            double ny = _infoStartY + (p.Y - _infoDragStart.Y);
            // Base anchor is bottom-right; clamp so the panel stays inside the pane.
            double minX = -Math.Max(0, ViewPaneGrid.ActualWidth - FileInfoPanel.ActualWidth - 12);
            double minY = -Math.Max(0, ViewPaneGrid.ActualHeight - FileInfoPanel.ActualHeight - 12);
            FileInfoTranslate.X = Math.Min(0, Math.Max(minX, nx));
            FileInfoTranslate.Y = Math.Min(0, Math.Max(minY, ny));
        }

        private void FileInfoPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_infoDragging) return;
            _infoDragging = false;
            FileInfoPanel.ReleaseMouseCapture();
            PluginSettings.InfoPosX = (int)Math.Round(FileInfoTranslate.X);
            PluginSettings.InfoPosY = (int)Math.Round(FileInfoTranslate.Y);
        }

        private void LayoutCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initializingSettings || LayoutCombo.SelectedIndex < 0) return;
            var mode = (LayoutMode)LayoutCombo.SelectedIndex;
            PluginSettings.Layout = mode;
            ApplyLayout(mode);
            if (mode == LayoutMode.Stacked) ShowSpectrogramAsync(false);
        }

        private void FreqCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initializingSettings || FreqCombo.SelectedIndex < 0) return;
            PluginSettings.FreqScale = (FrequencyScale)FreqCombo.SelectedIndex;
            RenderSpectrogram();
        }

        private void AmpCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initializingSettings || AmpCombo.SelectedIndex < 0) return;
            PluginSettings.AmpScale = (AmplitudeScale)AmpCombo.SelectedIndex;
            RenderSpectrogram();
        }

        private void ColorCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initializingSettings || ColorCombo.SelectedIndex < 0) return;
            PluginSettings.Colormap = (SpectrogramColormap)ColorCombo.SelectedIndex;
            RenderSpectrogram();
        }

        private void FftCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initializingSettings || FftCombo.SelectedIndex < 0) return;
            PluginSettings.FftSize = FftSizes[FftCombo.SelectedIndex];
            ShowSpectrogramAsync(true); // FFT size change requires a recompute
        }

        private void MaxFreqCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initializingSettings || MaxFreqCombo.SelectedIndex < 0) return;
            PluginSettings.MaxFrequencyHz = MaxFreqs[MaxFreqCombo.SelectedIndex];
            RenderSpectrogram();
        }

        private void ContrastSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_initializingSettings) return;
            PluginSettings.DbFloor = (int)Math.Round(e.NewValue);
            RenderSpectrogram();
        }

        // Translate vertical wheel/trackpad scrolling into horizontal timeline scrolling
        // (the ScrollViewer only scrolls horizontally, so the wheel does nothing by default).
        private void ViewScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Wheel over a vertically-scrollable lane stack (many-channel files) scrolls
            // the lanes instead — otherwise this preview handler (which tunnels before
            // WaveScroll's own bubbling handler ever sees the event) would swallow it for
            // horizontal timeline scrolling and lanes below the viewport would be unreachable.
            if (WaveScroll.IsVisible && WaveScroll.ScrollableHeight > 0 && WaveScroll.IsMouseOver)
                return;

            if (ViewScroll.ScrollableWidth <= 0) return; // nothing to scroll; let it bubble
            ViewScroll.ScrollToHorizontalOffset(ViewScroll.HorizontalOffset - e.Delta);
            e.Handled = true;
        }

        private void SpectrogramButton_Click(object sender, RoutedEventArgs e)
        {
            // Single-view layout only: toggle which view fills the pane.
            _showingSpectrogram = !_showingSpectrogram;
            if (_showingSpectrogram) ShowSpectrogramAsync(false);

            WaveScroll.Visibility = _showingSpectrogram ? Visibility.Collapsed : Visibility.Visible;
            SpectrogramImage.Visibility = _showingSpectrogram ? Visibility.Visible : Visibility.Collapsed;
            SpectrogramButton.Content = _showingSpectrogram ? "◂ Waveform" : "Spectrogram ▸";
            UpdateSpectrogramPlayheadVisibility();
        }

        /// <summary>Arrange the waveform/spectrogram panes for the given layout mode.</summary>
        private void ApplyLayout(LayoutMode mode)
        {
            if (mode == LayoutMode.Stacked)
            {
                WaveRow.Height = new GridLength(1, GridUnitType.Star);
                SpecRow.Height = new GridLength(4, GridUnitType.Star);
                Grid.SetRow(WaveScroll, 0);
                WaveScroll.Visibility = Visibility.Visible;
                SpectrogramImage.Visibility = Visibility.Visible;
                SpectrogramButton.Visibility = Visibility.Collapsed;
                _showingSpectrogram = true;
            }
            else // Single view
            {
                WaveRow.Height = new GridLength(0);
                SpecRow.Height = new GridLength(1, GridUnitType.Star);
                Grid.SetRow(WaveScroll, 1);
                SpectrogramButton.Visibility = Visibility.Visible;
                _showingSpectrogram = false;
                WaveScroll.Visibility = Visibility.Visible;
                SpectrogramImage.Visibility = Visibility.Collapsed;
                SpectrogramButton.Content = "Spectrogram ▸";
            }
            UpdateSpectrogramPlayheadVisibility();
        }

        /// <summary>Ensure linear STFT frames are cached (recomputing on FFT-size change), then render.</summary>
        private async void ShowSpectrogramAsync(bool forceRecompute)
        {
            int fftSize = PluginSettings.FftSize;
            var selection = _channelSelection;
            bool selectionChanged = _spectrogramChannelSelection.Mode != selection.Mode
                || _spectrogramChannelSelection.ChannelA != selection.ChannelA
                || _spectrogramChannelSelection.ChannelB != selection.ChannelB;
            bool needCompute = forceRecompute || _spectrogramFrames == null
                || _spectrogramFftSize != fftSize || selectionChanged;

            if (needCompute)
            {
                StatusText.Text = "Generating spectrogram…";
                StatusText.Visibility = Visibility.Visible;
                SpectrogramButton.IsEnabled = false;

                float[][] frames = null;
                int sr = 44100;
                bool capped = false;
                try
                {
                    await Task.Run(() =>
                    {
                        using (var stream = AudioReaderFactory.Create(_path))
                        {
                            sr = stream.WaveFormat.SampleRate;
                            // Choose a hop that spreads ~TimelineColumns frames across the
                            // whole file (bounded), so long files render fully instead of
                            // truncating — the wider result scrolls horizontally.
                            int targetCols = TimelineColumns(stream.TotalTime.TotalSeconds);
                            long audioFrames = stream.BlockAlign > 0 ? stream.Length / stream.BlockAlign : 0;
                            int hop = audioFrames > 0
                                ? Math.Max(fftSize / 4, (int)(audioFrames / Math.Max(1, targetCols)))
                                : fftSize / 2;
                            ISampleProvider source = stream.ToSampleProvider();
                            if (stream.WaveFormat.Channels > 2)
                                source = new ChannelMixSampleProvider(source, selection);
                            frames = SpectrogramGenerator.ComputeMagnitudes(source,
                                fftSize, hop, MaxTimelineColumns + 16, out capped);
                        }
                    });
                }
                catch (Exception ex)
                {
                    StatusText.Text = "Could not build spectrogram. " + ex.Message;
                    StatusText.Visibility = Visibility.Visible;
                    SpectrogramButton.IsEnabled = true;
                    return;
                }

                _spectrogramFrames = frames;
                _spectrogramSampleRate = sr;
                _spectrogramFftSize = fftSize;
                _spectrogramChannelSelection = selection;
                SpectrogramButton.IsEnabled = true;
            }

            RenderSpectrogram();
        }

        /// <summary>Colorize the cached frames with the current display settings (no FFT).</summary>
        private async void RenderSpectrogram()
        {
            var frames = _spectrogramFrames;
            if (frames == null) return;

            int gen = ++_renderGeneration;
            var opts = new SpectrogramRenderOptions
            {
                SampleRate = _spectrogramSampleRate,
                FftSize = _spectrogramFftSize,
                PixelWidth = Math.Max(1, Math.Min(MaxTimelineColumns, frames.Length)),
                PixelHeight = 512,
                FrequencyScale = PluginSettings.FreqScale,
                AmplitudeScale = PluginSettings.AmpScale,
                Colormap = PluginSettings.Colormap,
                DbFloor = PluginSettings.DbFloor,
                MaxFrequencyHz = PluginSettings.MaxFrequencyHz
            };

            System.Windows.Media.Imaging.WriteableBitmap bmp = null;
            await Task.Run(() =>
            {
                bmp = SpectrogramGenerator.Render(frames, opts);
                bmp.Freeze();
            });

            if (gen != _renderGeneration) return; // a newer render superseded this one

            SpectrogramImage.Source = bmp;
            _spectrogramBuilt = true;
            StatusText.Visibility = Visibility.Collapsed; // whole file is rendered; no truncation note
            UpdateSpectrogramPlayheadVisibility();
        }

        /// <summary>Set the scrollable timeline width from the file duration.</summary>
        private void SetTimelineWidth(double seconds)
        {
            double w = seconds * TimelinePxPerSec;
            if (w > MaxTimelineColumns) w = MaxTimelineColumns;
            if (w < 1) w = 1;
            Timeline.Width = w; // MinWidth binding keeps short files filling the pane
        }

        private void UpdateSpectrogramPlayhead(double frac)
        {
            if (SpectrogramImage.Visibility != Visibility.Visible || !_spectrogramBuilt) return;
            double x = frac * Timeline.ActualWidth;
            SpectrogramPlayhead.Margin = new Thickness(x, 0, 0, 0);

            // During playback, keep the playhead in view as it nears a scrolled edge.
            if (_player != null && _player.IsPlaying && !_scrubbing)
            {
                double off = ViewScroll.HorizontalOffset, vw = ViewScroll.ViewportWidth;
                if (vw > 0 && Timeline.ActualWidth > vw && (x < off + 40 || x > off + vw - 40))
                    ViewScroll.ScrollToHorizontalOffset(Math.Max(0, x - vw / 2));
            }
        }

        private void ScrollPlayheadIntoView(double frac)
        {
            double vw = ViewScroll.ViewportWidth;
            if (vw <= 0 || Timeline.ActualWidth <= vw) return;
            ViewScroll.ScrollToHorizontalOffset(Math.Max(0, frac * Timeline.ActualWidth - vw / 2));
        }

        private void UpdateSpectrogramPlayheadVisibility()
        {
            SpectrogramPlayhead.Visibility =
                (SpectrogramImage.Visibility == Visibility.Visible && _spectrogramBuilt)
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Gather a human-readable summary of the audio file for the info overlay.</summary>
        private static string BuildFileInfo(string path, WaveStream stream)
        {
            var fi = new FileInfo(path);
            var wf = stream.WaveFormat;
            double secs = stream.TotalTime.TotalSeconds;

            string ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
            string format = ext;
            if (stream is AiffReader ar)
                format = "AIFF-C (" + ar.Compression + ")";

            string channels = wf.Channels == 1 ? "1 (Mono)"
                : wf.Channels == 2 ? "2 (Stereo)" : wf.Channels.ToString();
            long fileKbps = secs > 0 ? (long)(fi.Length * 8 / secs / 1000) : 0;
            long pcmKbps = (long)wf.AverageBytesPerSecond * 8 / 1000;
            long sampleFrames = stream.BlockAlign > 0 ? stream.Length / stream.BlockAlign : 0;

            var sb = new StringBuilder();
            void L(string k, string v) => sb.AppendLine(k.PadRight(13) + v);
            L("File", fi.Name);
            L("Size", FormatSize(fi.Length));
            L("Format", format);
            L("Decoder", stream.GetType().Name);
            L("Duration", stream.TotalTime.ToString(@"hh\:mm\:ss\.fff"));
            L("Sample rate", wf.SampleRate.ToString("N0") + " Hz");
            L("Channels", channels);
            L("Bit depth", wf.BitsPerSample + "-bit");
            L("Encoding", wf.Encoding.ToString());
            L("Bitrate", "~" + fileKbps.ToString("N0") + " kbps (file)");
            if (pcmKbps > 0) L("PCM rate", pcmKbps.ToString("N0") + " kbps");
            L("Samples", sampleFrames.ToString("N0") + " frames");
            return sb.ToString().TrimEnd();
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1L << 30) return (bytes / (double)(1L << 30)).ToString("0.00") + " GB";
            if (bytes >= 1L << 20) return (bytes / (double)(1L << 20)).ToString("0.0") + " MB";
            if (bytes >= 1L << 10) return (bytes / (double)(1L << 10)).ToString("0.0") + " KB";
            return bytes + " B";
        }

        public void Unload()
        {
            _timer?.Stop();
            _player?.Dispose();
            _player = null;
        }
    }
}
