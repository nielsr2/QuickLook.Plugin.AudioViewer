using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NAudio.Wave;
using QuickLook.Common.Plugin;
using QuickLook.Plugin.AudioViewer.Audio;
using QuickLook.Plugin.AudioViewer.Controls;
using QuickLook.Plugin.AudioViewer.Settings;
using QuickLook.Plugin.AudioViewer.Theming;

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

        // Lifecycle: the whole load runs async after the control is created, but the
        // preview can be closed (Unload) mid-load. _cts cancels in-flight decode work
        // and _unloaded guards every post-await UI continuation so we never touch a
        // dead control or leak the audio player / file handle / timer.
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _unloaded;

        // Cached linear STFT frames so setting changes re-render without re-running
        // the FFT; only an FFT-size change forces a recompute.
        private float[][] _spectrogramFrames;
        private int _spectrogramSampleRate;
        private int _spectrogramFftSize = -1;

        private bool _initializingSettings;
        private int _renderGeneration;
        // Set while Tick() writes SeekSlider.Value programmatically, so the resulting
        // ValueChanged (used for track-click/keyboard seeking) doesn't feed back as a seek.
        private bool _suppressSeekValueChanged;

        // Mute state. The volume slider stays put while muted (showing the pre-mute level);
        // any manual volume change unmutes.
        private bool _muted;
        private float _volumeBeforeMute = 0.8f;

        // Axis state. _specTopFraction is where the spectrogram band starts vertically
        // (0 single-view, 0.2 stacked); _lastLevels is the last render's normalization
        // for the colorbar legend.
        private double _specTopFraction;
        private SpectrogramLevels _lastLevels;
        private bool _isDark;

        private bool _infoDragging;
        private Point _infoDragStart;
        private double _infoStartX, _infoStartY;

        // Hover-pill mode only: when the user clicks the pill, the info box is pinned
        // open (stays visible when not hovering) until unpinned or closed via the ✕.
        private bool _infoPinned;

        // Note-layer state: the shared, in-memory note list the overlay edits in place, plus a
        // session flag so the "switch to Log" suggestion stays dismissed once the user closes it.
        private readonly System.Collections.Generic.List<NoteModel> _notes = new System.Collections.Generic.List<NoteModel>();
        private bool _noteSuggestionDismissed;

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
            ApplyTheme();
            AutoPlayCheck.IsChecked = PluginSettings.AutoPlay;
            InitSettingsControls();
            Waveform.SeekRequested += OnWaveformSeek; // click/drag on the waveform seeks
            Notes.NotesChanged += OnNotesChanged;      // overlay draw/edit/delete -> refresh export buttons
            Loaded += OnLoaded;
        }

        /// <summary>Resolve and apply the light/dark palette from QuickLook's theme.</summary>
        private void ApplyTheme()
        {
            _isDark = ThemePalette.ResolveIsDark(_context?.Theme ?? Themes.None);
            ThemePalette.Apply(Resources, _isDark);
            Waveform.SetTheme(ThemePalette.WaveformWave(_isDark),
                ThemePalette.Playhead(_isDark), ThemePalette.WaveformBackground(_isDark));

            var axisText = ThemePalette.AxisText(_isDark);
            var axisLine = ThemePalette.AxisLine(_isDark);
            FreqRuler.SetTheme(axisText, axisLine);
            TimeAxis.SetTheme(axisText, axisLine);
            Colorbar.SetTheme(axisText);
            Notes.SetTheme(ThemePalette.NoteFill(_isDark), ThemePalette.NoteBorder(_isDark),
                ThemePalette.NoteSelected(_isDark));
        }

        private void OnWaveformSeek(double fraction)
        {
            if (_player == null || _scrubbing) return;
            SeekTo(fraction);
        }

        private void InitSettingsControls()
        {
            _initializingSettings = true;
            FileInfoCheck.IsChecked = PluginSettings.ShowFileInfo;
            FileInfoStyleCombo.SelectedIndex = (int)PluginSettings.FileInfoDisplay;
            HoverModeCombo.SelectedIndex = (int)PluginSettings.HoverReadoutMode;
            NoteLayerCheck.IsChecked = PluginSettings.NoteLayerEnabled;
            LayoutCombo.SelectedIndex = (int)PluginSettings.Layout;
            FreqCombo.SelectedIndex = (int)PluginSettings.FreqScale;
            AmpCombo.SelectedIndex = (int)PluginSettings.AmpScale;
            ColorCombo.SelectedIndex = (int)PluginSettings.Colormap;
            ReverseCheck.IsChecked = PluginSettings.ReverseColormap;
            GammaSlider.Value = PluginSettings.Gamma;
            FftCombo.SelectedIndex = Math.Max(0, Array.IndexOf(FftSizes, PluginSettings.FftSize));
            MaxFreqCombo.SelectedIndex = Math.Max(0, Array.IndexOf(MaxFreqs, PluginSettings.MaxFrequencyHz));
            ContrastSlider.Value = PluginSettings.DbFloor;
            _initializingSettings = false;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            var token = _cts.Token;
            WaveStream playbackStream = null;
            bool playerCtorInvoked = false; // once true, AudioPlayer owns/disposes the stream
            try
            {
                WaveformData waveform = null;
                string infoText = null;
                TagInfo tags = null;

                await Task.Run(() =>
                {
                    playbackStream = AudioReaderFactory.Create(_path);
                    infoText = BuildFileInfo(_path, playbackStream);
                    tags = TagReader.Read(_path); // embedded metadata; null on none/failure
                    using (var scan = AudioReaderFactory.Create(_path))
                        waveform = WaveformData.Compute(scan, TimelineColumns(scan.TotalTime.TotalSeconds), token);
                }, token);

                // Closed during the background decode: release the playback stream (else
                // the file stays locked) and stop before touching the dead control.
                if (_unloaded || token.IsCancellationRequested) { playbackStream?.Dispose(); return; }

                // From here the constructor owns playbackStream: it disposes the stream
                // itself on a failed WASAPI init, so OnLoaded must not also dispose it.
                playerCtorInvoked = true;
                _player = new AudioPlayer(playbackStream);
                _player.PlaybackStopped += (s, a) => Dispatcher.BeginInvoke((Action)OnPlaybackStopped);
                _player.Volume = (float)VolumeSlider.Value;

                Waveform.Waveform = waveform;
                SetTimelineWidth(_player.Duration.TotalSeconds);
                FileInfoText.Text = ComposeInfoText(tags, infoText);
                SetAlbumArt(tags?.CoverArt);
                ApplyFileInfoMode(); // configure hover-pill vs. draggable-box presentation
                StatusText.Visibility = Visibility.Collapsed;
                UpdateTimeText();

                _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
                _timer.Tick += (s, a) => Tick();
                _timer.Start();

                if (PluginSettings.AutoPlay) TogglePlay(true);

                ApplyLayout(PluginSettings.Layout);
                if (PluginSettings.Layout == LayoutMode.Stacked)
                    ShowSpectrogramAsync(false); // spectrogram is always visible when stacked

                Focus(); // take keyboard focus so the media-key shortcuts work without a click
            }
            catch (OperationCanceledException)
            {
                // Cancelled mid-decode (preview closed): the reader may already be open, so
                // release it here — the constructor hadn't taken ownership yet.
                if (!playerCtorInvoked) playbackStream?.Dispose();
            }
            catch (AudioFormatException ex)
            {
                if (!playerCtorInvoked) playbackStream?.Dispose();
                if (!_unloaded) ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                if (!playerCtorInvoked) playbackStream?.Dispose();
                if (!_unloaded) ShowError("Could not play this audio file. " + ex.Message);
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
            _suppressSeekValueChanged = true;
            SeekSlider.Value = frac; // programmatic: don't let ValueChanged re-seek
            _suppressSeekValueChanged = false;
            Waveform.PlayheadFraction = frac;
            UpdateSpectrogramPlayhead(frac);
            UpdateTimeText();
        }

        private void UpdateTimeText()
        {
            if (_player == null) return;
            TimeText.Text = $"{Fmt(_player.Position)} / {Fmt(_player.Duration)}";
        }

        // Show hours only when the duration needs them; otherwise stay mm:ss.
        internal static string Fmt(TimeSpan t) => t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e) => TogglePlay(!_player?.IsPlaying ?? false);

        private void TogglePlay(bool play)
        {
            if (_player == null) return;
            if (play) { _player.Play(); PlayPauseButton.Content = "⏸"; }
            else { _player.Pause(); PlayPauseButton.Content = "▶"; }
        }

        private void OnPlaybackStopped()
        {
            if (_unloaded || _player == null) return; // event can arrive during teardown
            // reached end (or stopped): reset button glyph
            if (_player.Position >= _player.Duration - TimeSpan.FromMilliseconds(100))
                PlayPauseButton.Content = "▶";
        }

        private void SeekSlider_DragStarted(object sender, DragStartedEventArgs e) => _scrubbing = true;

        private void SeekSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _scrubbing = false;
            SeekTo(SeekSlider.Value);
        }

        // Fires for track-clicks, keyboard changes, and thumb-drags. We seek live even
        // while dragging (_scrubbing) so the waveform, spectrogram, and time readout track
        // the drag instead of only updating on release. Tick() is suppressed during
        // _scrubbing, so it won't fight these updates; only Tick()'s own programmatic
        // writes (guarded by _suppressSeekValueChanged) are ignored here.
        private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSeekValueChanged || _player == null) return;
            SeekTo(e.NewValue);
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
            if (_muted) { _muted = false; UpdateMuteGlyph(); } // any manual change unmutes
            if (_player != null) _player.Volume = (float)e.NewValue;
        }

        private void MuteButton_Click(object sender, RoutedEventArgs e) => ToggleMute();

        private void ToggleMute()
        {
            if (_player == null) return;
            _muted = !_muted;
            if (_muted)
            {
                _volumeBeforeMute = (float)VolumeSlider.Value;
                _player.Volume = 0f; // leave the slider where it is
            }
            else
            {
                _player.Volume = _volumeBeforeMute;
            }
            UpdateMuteGlyph();
        }

        private void UpdateMuteGlyph()
        {
            // Segoe MDL2: E767 = speaker, E74F = muted speaker.
            MuteButton.Content = _muted ? "" : "";
        }

        private void AdjustVolume(double delta)
        {
            double v = Math.Min(1.0, Math.Max(0.0, VolumeSlider.Value + delta));
            VolumeSlider.Value = v; // ValueChanged applies it to the player and unmutes
        }

        private void SeekRelative(double seconds)
        {
            if (_player == null || _player.Duration.TotalSeconds <= 0) return;
            double frac = (_player.Position.TotalSeconds + seconds) / _player.Duration.TotalSeconds;
            SeekTo(Math.Min(1.0, Math.Max(0.0, frac)));
        }

        // Media-player keys chosen to NOT collide with QuickLook host gestures (Space
        // toggles the preview; arrow keys navigate files in Explorer). Unhandled keys
        // bubble up to the host untouched.
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Note-layer editing keys, handled before the media keys so they take precedence when
            // the layer is on. Delete/Backspace removes the selected note; Escape cancels an
            // in-progress draw (and only then is swallowed, so a plain Esc still reaches the host).
            if (PluginSettings.NoteLayerEnabled)
            {
                if ((e.Key == Key.Delete || e.Key == Key.Back) && Notes.HasSelection)
                {
                    Notes.DeleteSelected();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Escape && Notes.CancelDraw())
                {
                    e.Handled = true;
                    return;
                }
            }

            if (_player == null) return;
            switch (e.Key)
            {
                case Key.K: TogglePlay(!_player.IsPlaying); e.Handled = true; break;
                case Key.J: SeekRelative(-5); e.Handled = true; break;
                case Key.L: SeekRelative(5); e.Handled = true; break;
                case Key.M: ToggleMute(); e.Handled = true; break;
                case Key.OemPlus:
                case Key.Add: AdjustVolume(0.05); e.Handled = true; break;
                case Key.OemMinus:
                case Key.Subtract: AdjustVolume(-0.05); e.Handled = true; break;
            }
        }

        private void AutoPlayCheck_Changed(object sender, RoutedEventArgs e)
        {
            PluginSettings.AutoPlay = AutoPlayCheck.IsChecked == true;
        }

        private void FileInfoCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializingSettings) return;
            PluginSettings.ShowFileInfo = FileInfoCheck.IsChecked == true;
            ApplyFileInfoMode(); // master on/off; re-applies the current presentation mode
        }

        private void FileInfoStyleCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initializingSettings || FileInfoStyleCombo.SelectedIndex < 0) return;
            PluginSettings.FileInfoDisplay = (FileInfoDisplay)FileInfoStyleCombo.SelectedIndex;
            ApplyFileInfoMode();
        }

        private void HoverModeCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initializingSettings || HoverModeCombo.SelectedIndex < 0) return;
            // No re-render needed: this only affects the next hover readout.
            PluginSettings.HoverReadoutMode = (HoverReadoutMode)HoverModeCombo.SelectedIndex;
        }

        // True while the file-info overlay is in the unobtrusive hover-pill mode.
        private bool IsHoverInfoMode => PluginSettings.FileInfoDisplay == FileInfoDisplay.HoverIcon;

        /// <summary>Configure the file-info overlay for the current FileInfoDisplay + ShowFileInfo.</summary>
        private void ApplyFileInfoMode()
        {
            bool show = PluginSettings.ShowFileInfo;
            _infoPinned = false; // reset pin state whenever the mode/visibility is (re)applied
            if (IsHoverInfoMode)
            {
                // Unobtrusive pill; the box is revealed on hover (ShowHoverFileInfo) and
                // auto-hides on mouse-leave. Not draggable; the ✕ is hidden until pinned.
                InfoIcon.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                FileInfoClose.Visibility = Visibility.Collapsed;
                FileInfoPanel.Cursor = Cursors.Arrow;
                FileInfoPanel.Visibility = Visibility.Collapsed; // shown only while hovering
            }
            else // DraggableBox: today's behavior.
            {
                InfoIcon.Visibility = Visibility.Collapsed;
                FileInfoClose.Visibility = Visibility.Visible;
                FileInfoPanel.Cursor = Cursors.SizeAll;
                FileInfoTranslate.X = PluginSettings.InfoPosX; // restore last dragged position
                FileInfoTranslate.Y = PluginSettings.InfoPosY;
                FileInfoPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // Hover-pill mode: reveal the info box directly above the pill (both anchored
        // bottom-right, so a negative Y translate lifts the box to sit on top of it).
        private void ShowHoverFileInfo()
        {
            FileInfoTranslate.X = 0;
            FileInfoPanel.Visibility = Visibility.Visible;
            FileInfoPanel.UpdateLayout(); // so InfoIcon.ActualHeight reflects the rendered pill
            double lift = InfoIcon.ActualHeight > 0 ? InfoIcon.ActualHeight : 26;
            FileInfoTranslate.Y = -lift; // box bottom meets pill top (touching; no dead zone)
        }

        private void InfoIcon_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!IsHoverInfoMode) return;
            ShowHoverFileInfo();
        }

        private void InfoIcon_MouseLeave(object sender, MouseEventArgs e) => ScheduleHoverInfoHide();

        // Clicking the pill toggles a pinned state so the box persists when not hovering.
        private void InfoIcon_Click(object sender, MouseButtonEventArgs e)
        {
            if (!IsHoverInfoMode) return;
            e.Handled = true;
            _infoPinned = !_infoPinned;
            if (_infoPinned)
            {
                // Keep the box shown+positioned, and expose the ✕ so it can be closed.
                ShowHoverFileInfo();
                FileInfoClose.Visibility = Visibility.Visible;
            }
            else
            {
                // Hide the ✕, then hide the box unless the pointer is still over pill/box.
                FileInfoClose.Visibility = Visibility.Collapsed;
                ScheduleHoverInfoHide();
            }
        }

        private void FileInfoPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!IsHoverInfoMode) return;
            ScheduleHoverInfoHide();
        }

        // Defer the hide so moving between the touching pill and box (which fires
        // MouseLeave on one just before MouseEnter on the other) doesn't flicker it away:
        // by Background priority, IsMouseOver reflects wherever the pointer landed.
        private void ScheduleHoverInfoHide()
        {
            if (_infoPinned) return; // pinned open: never auto-hide
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsHoverInfoMode) return;
                if (InfoIcon.IsMouseOver || FileInfoPanel.IsMouseOver) return;
                FileInfoPanel.Visibility = Visibility.Collapsed;
            }), DispatcherPriority.Background);
        }

        private void FileInfoClose_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // don't also start a drag
            if (IsHoverInfoMode)
            {
                // Pinned hover box: unpin, hide the box, and hide the ✕ again.
                _infoPinned = false;
                FileInfoPanel.Visibility = Visibility.Collapsed;
                FileInfoClose.Visibility = Visibility.Collapsed;
                return;
            }
            // DraggableBox: dismiss for this file only — leave the "Show file info" setting
            // on, so a new file shows it again (unless the user unticks it in settings).
            FileInfoPanel.Visibility = Visibility.Collapsed;
        }

        private void FileInfoPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsHoverInfoMode) return; // dragging disabled in hover-icon mode
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

        private void ReverseCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializingSettings) return;
            PluginSettings.ReverseColormap = ReverseCheck.IsChecked == true;
            RenderSpectrogram();
        }

        private void GammaSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_initializingSettings) return;
            PluginSettings.Gamma = e.NewValue;
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
            if (ViewScroll.ScrollableWidth <= 0) return; // nothing to scroll; let it bubble
            ViewScroll.ScrollToHorizontalOffset(ViewScroll.HorizontalOffset - e.Delta);
            e.Handled = true;
        }

        private void SpectrogramButton_Click(object sender, RoutedEventArgs e)
        {
            // Single-view layout only: toggle which view fills the pane.
            _showingSpectrogram = !_showingSpectrogram;
            if (_showingSpectrogram) ShowSpectrogramAsync(false);

            Waveform.Visibility = _showingSpectrogram ? Visibility.Collapsed : Visibility.Visible;
            SpectrogramImage.Visibility = _showingSpectrogram ? Visibility.Visible : Visibility.Collapsed;
            SpectrogramButton.Content = _showingSpectrogram ? "◂ Waveform" : "Spectrogram ▸";
            UpdateSpectrogramPlayheadVisibility();
            RefreshAxes();
        }

        /// <summary>Arrange the waveform/spectrogram panes for the given layout mode.</summary>
        private void ApplyLayout(LayoutMode mode)
        {
            if (mode == LayoutMode.Stacked)
            {
                WaveRow.Height = new GridLength(1, GridUnitType.Star);
                SpecRow.Height = new GridLength(4, GridUnitType.Star);
                Grid.SetRow(Waveform, 0);
                Waveform.Visibility = Visibility.Visible;
                SpectrogramImage.Visibility = Visibility.Visible;
                SpectrogramButton.Visibility = Visibility.Collapsed;
                _showingSpectrogram = true;
                _specTopFraction = 1.0 / 5.0; // spectrogram is the lower 4/5
            }
            else // Single view
            {
                WaveRow.Height = new GridLength(0);
                SpecRow.Height = new GridLength(1, GridUnitType.Star);
                Grid.SetRow(Waveform, 1);
                SpectrogramButton.Visibility = Visibility.Visible;
                _showingSpectrogram = false;
                Waveform.Visibility = Visibility.Visible;
                SpectrogramImage.Visibility = Visibility.Collapsed;
                SpectrogramButton.Content = "Spectrogram ▸";
                _specTopFraction = 0.0; // spectrogram fills the pane
            }
            UpdateSpectrogramPlayheadVisibility();
            RefreshAxes();
        }

        /// <summary>Ensure linear STFT frames are cached (recomputing on FFT-size change), then render.</summary>
        private async void ShowSpectrogramAsync(bool forceRecompute)
        {
            int fftSize = PluginSettings.FftSize;
            bool needCompute = forceRecompute || _spectrogramFrames == null || _spectrogramFftSize != fftSize;

            if (needCompute)
            {
                var token = _cts.Token;
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
                            frames = SpectrogramGenerator.ComputeMagnitudes(stream.ToSampleProvider(),
                                fftSize, hop, MaxTimelineColumns + 16, out capped, token);
                        }
                    }, token);
                }
                catch (OperationCanceledException)
                {
                    return; // preview closed mid-compute
                }
                catch (Exception ex)
                {
                    if (_unloaded) return;
                    StatusText.Text = "Could not build spectrogram. " + ex.Message;
                    StatusText.Visibility = Visibility.Visible;
                    SpectrogramButton.IsEnabled = true;
                    return;
                }

                if (_unloaded || token.IsCancellationRequested) return;

                _spectrogramFrames = frames;
                _spectrogramSampleRate = sr;
                _spectrogramFftSize = fftSize;
                SpectrogramButton.IsEnabled = true;
            }

            RenderSpectrogram();
        }

        /// <summary>Colorize the cached frames with the current display settings (no FFT).</summary>
        private async void RenderSpectrogram()
        {
            var frames = _spectrogramFrames;
            if (frames == null) return;

            // Files shorter than one FFT window yield zero frames; rendering that would
            // just paint a black rectangle. Tell the user instead and keep the waveform.
            if (frames.Length == 0)
            {
                _spectrogramBuilt = false;
                SpectrogramImage.Source = null;
                UpdateSpectrogramPlayheadVisibility();
                StatusText.Text = $"File too short for a spectrogram at FFT size {PluginSettings.FftSize}. " +
                                  "Try a smaller FFT size.";
                StatusText.Visibility = Visibility.Visible;
                return;
            }

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
                ReverseColormap = PluginSettings.ReverseColormap,
                Gamma = PluginSettings.Gamma,
                DbFloor = PluginSettings.DbFloor,
                MaxFrequencyHz = PluginSettings.MaxFrequencyHz
            };

            try
            {
                System.Windows.Media.Imaging.WriteableBitmap bmp = null;
                SpectrogramLevels levels = default;
                await Task.Run(() =>
                {
                    bmp = SpectrogramGenerator.Render(frames, opts, out levels);
                    bmp.Freeze();
                });

                if (_unloaded || gen != _renderGeneration) return; // superseded or torn down

                SpectrogramImage.Source = bmp;
                _spectrogramBuilt = true;
                _lastLevels = levels;
                StatusText.Visibility = Visibility.Collapsed; // whole file is rendered; no truncation note
                UpdateSpectrogramPlayheadVisibility();
                RefreshAxes();
            }
            catch (Exception ex)
            {
                // async void on the UI thread: an unobserved throw here (e.g. an allocation
                // failure on a very wide bitmap) could tear down the QuickLook host. Contain it.
                if (_unloaded || gen != _renderGeneration) return;
                StatusText.Text = "Could not render spectrogram. " + ex.Message;
                StatusText.Visibility = Visibility.Visible;
            }
        }

        /// <summary>Set the scrollable timeline width from the file duration.</summary>
        private void SetTimelineWidth(double seconds)
        {
            double w = seconds * TimelinePxPerSec;
            if (w > MaxTimelineColumns) w = MaxTimelineColumns;
            if (w < 1) w = 1;
            Timeline.Width = w; // MinWidth binding keeps short files filling the pane

            // TimeAxis.Width is bound to Timeline.ActualWidth in XAML, so it matches the
            // stretched width for short files too; here we only supply the duration.
            TimeAxis.Configure(seconds);
            TimeAxis.Visibility = Visibility.Visible;
        }

        // Keep the time ruler aligned under the spectrogram as the shared timeline scrolls,
        // and mirror the scroll state onto the dedicated horizontal scrollbar below the plot.
        private void ViewScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            TimeRulerTranslate.X = -ViewScroll.HorizontalOffset;

            double scrollable = ViewScroll.ScrollableWidth;
            HScroll.Maximum = scrollable;
            HScroll.ViewportSize = ViewScroll.ViewportWidth;
            HScroll.Value = ViewScroll.HorizontalOffset;
            HScroll.Visibility = scrollable > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HScroll_Scroll(object sender, ScrollEventArgs e)
        {
            ViewScroll.ScrollToHorizontalOffset(e.NewValue);
        }

        /// <summary>Reconfigure and show/hide the frequency ruler and colorbar for the current state.</summary>
        private void RefreshAxes()
        {
            bool show = _spectrogramBuilt && _spectrogramFrames != null && _spectrogramFrames.Length > 0
                        && SpectrogramImage.Visibility == Visibility.Visible;
            if (!show)
            {
                FreqRuler.Visibility = Visibility.Collapsed;
                Colorbar.Visibility = Visibility.Collapsed;
                RefreshNotes(); // keep the overlay/toolbar in step (hidden while the spectrogram is off)
                return;
            }

            var scale = PluginSettings.FreqScale;
            double fMin = FrequencyScaleMapper.EffectiveFMin(scale, _spectrogramFftSize, _spectrogramSampleRate);
            double fMax = FrequencyScaleMapper.EffectiveFMax(_spectrogramSampleRate, PluginSettings.MaxFrequencyHz);
            FreqRuler.Configure(scale, fMin, fMax, _specTopFraction);
            Colorbar.Configure(PluginSettings.Colormap, _lastLevels, _specTopFraction,
                PluginSettings.ReverseColormap, PluginSettings.Gamma);
            FreqRuler.Visibility = Visibility.Visible;
            Colorbar.Visibility = Visibility.Visible;
            RefreshNotes(); // re-align the note overlay on the same scale/fMin/fMax
        }

        /// <summary>
        /// Align the note overlay + toolbar to the current spectrogram state. Uses the SAME
        /// scale/fMin/fMax as <see cref="RefreshAxes"/> so notes line up on any scale/max-freq;
        /// visibility follows <see cref="PluginSettings.NoteLayerEnabled"/> and the spectrogram
        /// actually being shown (you draw on the spectrogram).
        /// </summary>
        private void RefreshNotes()
        {
            bool show = PluginSettings.NoteLayerEnabled
                        && _spectrogramBuilt && _spectrogramFrames != null && _spectrogramFrames.Length > 0
                        && SpectrogramImage.Visibility == Visibility.Visible;

            if (show)
            {
                var scale = PluginSettings.FreqScale;
                double fMin = FrequencyScaleMapper.EffectiveFMin(scale, _spectrogramFftSize, _spectrogramSampleRate);
                double fMax = FrequencyScaleMapper.EffectiveFMax(_spectrogramSampleRate, PluginSettings.MaxFrequencyHz);
                Notes.Configure(scale, fMin, fMax, _player?.Duration.TotalSeconds ?? 0, _notes);
                UpdateNoteExportButtons();
            }

            Notes.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            NoteToolbar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (!show && Notes.DrawMode)
            {
                Notes.DrawMode = false; // leaving the layer exits draw mode so the hover readout returns
                DrawToggle.IsChecked = false;
            }
            UpdateNoteSuggestion();
        }

        /// <summary>
        /// Show the "switch to Log" nudge only while the note layer is active on a scale where
        /// semitones bunch (Linear/Mel) and the user hasn't dismissed it; hidden on Log.
        /// </summary>
        private void UpdateNoteSuggestion()
        {
            bool bunching = PluginSettings.FreqScale == FrequencyScale.Linear
                            || PluginSettings.FreqScale == FrequencyScale.Mel;
            bool show = NoteToolbar.Visibility == Visibility.Visible && bunching && !_noteSuggestionDismissed;
            NoteSuggestion.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateNoteExportButtons()
        {
            bool any = _notes.Count > 0;
            SaveMidiButton.IsEnabled = any;
            CopyNotesButton.IsEnabled = any;
        }

        private void OnNotesChanged() => UpdateNoteExportButtons();

        private void NoteLayerCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializingSettings) return;
            PluginSettings.NoteLayerEnabled = NoteLayerCheck.IsChecked == true;
            if (!PluginSettings.NoteLayerEnabled) _noteSuggestionDismissed = false; // let it reappear next time
            RefreshNotes();
        }

        private void DrawToggle_Click(object sender, RoutedEventArgs e)
        {
            Notes.DrawMode = DrawToggle.IsChecked == true;
            Focus(); // return keyboard focus to the control so Delete/Esc reach OnPreviewKeyDown
        }

        private void SaveMidiButton_Click(object sender, RoutedEventArgs e)
        {
            if (_notes.Count == 0) return;
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "MIDI file (*.mid)|*.mid",
                FileName = Path.GetFileNameWithoutExtension(_path) + ".mid"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                MidiExporter.WriteMidi(dlg.FileName, _notes, PluginSettings.NoteExportBpm);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save the MIDI file. " + ex.Message, "Save as MIDI",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CopyNotesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_notes.Count == 0) return;
            try { Clipboard.SetText(NoteTextSerializer.Format(_notes)); }
            catch { /* clipboard can be transiently locked by another app; ignore */ }
        }

        private void NoteSuggestionSwitch_Click(object sender, RoutedEventArgs e)
        {
            // Sets the scale to Log via the same combo the user would use; FreqCombo_Changed
            // re-renders, and the follow-on RefreshNotes hides this prompt (no longer bunching).
            FreqCombo.SelectedIndex = (int)FrequencyScale.Log;
        }

        private void NoteSuggestionDismiss_Click(object sender, RoutedEventArgs e)
        {
            _noteSuggestionDismissed = true;
            NoteSuggestion.Visibility = Visibility.Collapsed;
        }

        private void SpectrogramImage_MouseMove(object sender, MouseEventArgs e)
        {
            var frames = _spectrogramFrames;
            if (!_spectrogramBuilt || frames == null || frames.Length == 0 || _player == null)
            {
                HoverReadout.Visibility = Visibility.Collapsed;
                return;
            }
            double w = SpectrogramImage.ActualWidth, h = SpectrogramImage.ActualHeight;
            if (w <= 0 || h <= 0) return;

            var p = e.GetPosition(SpectrogramImage);
            double tx = Clamp01(p.X / w);
            double u = Clamp01(1.0 - p.Y / h); // y=0 (top) is the highest frequency

            double time = tx * _player.Duration.TotalSeconds;
            var scale = PluginSettings.FreqScale;
            double fMin = FrequencyScaleMapper.EffectiveFMin(scale, _spectrogramFftSize, _spectrogramSampleRate);
            double fMax = FrequencyScaleMapper.EffectiveFMax(_spectrogramSampleRate, PluginSettings.MaxFrequencyHz);
            double freq = FrequencyScaleMapper.FractionToFrequency(u, scale, fMin, fMax);

            int fi = (int)(tx * (frames.Length - 1));
            if (fi < 0) fi = 0; else if (fi > frames.Length - 1) fi = frames.Length - 1;
            var frame = frames[fi];
            double hzPerBin = (double)_spectrogramSampleRate / _spectrogramFftSize;
            int bin = (int)(freq / hzPerBin);
            if (bin < 0) bin = 0; else if (bin > frame.Length - 1) bin = frame.Length - 1;
            double mag = frame[bin];
            string level = PluginSettings.AmpScale == AmplitudeScale.Decibel
                ? $"{Math.Round(20.0 * Math.Log10(mag + 1e-12))} dB"
                : mag.ToString("0.000");

            string pitch = HoverReadoutFormatter.FormatPitch(freq, PluginSettings.HoverReadoutMode);
            HoverText.Text = $"{AxisTicks.FormatTime(time)}  ·  {pitch}  ·  {level}";

            var pc = e.GetPosition(ContentGrid);
            HoverTranslate.X = Math.Min(pc.X + 12, ContentGrid.ActualWidth - HoverReadout.ActualWidth - 4);
            HoverTranslate.Y = Math.Min(pc.Y + 12, ContentGrid.ActualHeight - HoverReadout.ActualHeight - 4);
            HoverReadout.Visibility = Visibility.Visible;
        }

        private void SpectrogramImage_MouseLeave(object sender, MouseEventArgs e)
            => HoverReadout.Visibility = Visibility.Collapsed;

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

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

        /// <summary>Prepend a block of embedded tags (if any) above the technical info.</summary>
        internal static string ComposeInfoText(TagInfo tags, string technical)
        {
            if (tags == null || !tags.HasText) return technical;

            var sb = new StringBuilder();
            void L(string k, string v)
            {
                if (!string.IsNullOrWhiteSpace(v)) sb.AppendLine(k.PadRight(13) + v);
            }
            L("Title", tags.Title);
            L("Artist", tags.Artist);
            L("Album", tags.Album);
            if (tags.Year > 0) L("Year", tags.Year.ToString());
            L("Genre", tags.Genre);
            sb.AppendLine(); // blank line between tags and the technical block
            sb.Append(technical);
            return sb.ToString();
        }

        /// <summary>Decode cover-art bytes to a frozen bitmap and show it, or hide the image.</summary>
        private void SetAlbumArt(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                AlbumArt.Source = null;
                AlbumArt.Visibility = Visibility.Collapsed;
                return;
            }
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad; // read fully now; don't hold the stream
                bmp.DecodePixelWidth = 128;                 // cap memory for large embedded art
                bmp.StreamSource = new MemoryStream(data);
                bmp.EndInit();
                bmp.Freeze();
                AlbumArt.Source = bmp;
                AlbumArt.Visibility = Visibility.Visible;
            }
            catch
            {
                AlbumArt.Source = null;
                AlbumArt.Visibility = Visibility.Collapsed; // unreadable art: just hide it
            }
        }

        /// <summary>Gather a human-readable summary of the audio file for the info overlay.</summary>
        internal static string BuildFileInfo(string path, WaveStream stream)
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
            _unloaded = true;   // stop every in-flight async continuation from touching the control
            _cts.Cancel();      // abort in-flight decode/FFT so it doesn't run on a dead control
            _timer?.Stop();
            _player?.Dispose(); // disposes the WASAPI output and releases the file handle
            _player = null;
            _cts.Dispose();
        }
    }
}
