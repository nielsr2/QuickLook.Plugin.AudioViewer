using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
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
        // File duration, read from the stream before the AudioPlayer ctor takes ownership.
        // The spectrogram/note timeline uses this rather than _player.Duration so it still
        // works when playback init fails (e.g. WASAPI rejects the channel layout).
        private double _durationSeconds;
        private DispatcherTimer _timer;
        private bool _scrubbing;

        // Set when the user scrolls the timeline themselves, suspending playhead
        // auto-follow so it stops yanking the view back on the next tick. Cleared on a
        // seek or when playback (re)starts, both of which are explicit "go here" gestures.
        private bool _userScrolled;

        private bool _showingSpectrogram;
        private bool _spectrogramBuilt;

        // True when ComputeMagnitudes stopped at the frame cap, so the frames cover less
        // than the whole file and every x->time mapping downstream is off.
        private bool _spectrogramCapped;

        // Lifecycle: the whole load runs async after the control is created, but the
        // preview can be closed (Unload) mid-load. _cts cancels in-flight decode work
        // and _unloaded guards every post-await UI continuation so we never touch a
        // dead control or leak the audio player / file handle / timer.
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _unloaded;

        // Trackpad horizontal scroll: WPF never surfaces WM_MOUSEHWHEEL as a routed
        // event, so a two-finger horizontal swipe is caught via a top-level window hook
        // and turned into a horizontal timeline scroll. Mirrors the XdfViewer plugin.
        private HwndSource _hwndSource;
        private const int WM_MOUSEHWHEEL = 0x020E;

        // Raw per-channel scan result (source of truth). The stereo display-mode transform is
        // applied on top of this when feeding WaveformView, so switching modes is instant with
        // no re-scan. Progressive snapshots overwrite it as the scan fills in.
        private WaveformData _waveformData;

        // Vertical magnification of the waveform. Session-only and deliberately not
        // persisted: it belongs to the file being viewed, not to the user.
        private double _waveformGain = 1.0;
        // Segoe MDL2 Assets, like the mute and settings buttons: E768 = play, E769 = pause.
        private const string PlayGlyph = "";
        private const string PauseGlyph = "";

        private bool _suppressGainSlider;   // guards the slider against our own writes

        // Cached linear STFT frames so setting changes re-render without re-running
        // the FFT; only an FFT-size change forces a recompute.
        private float[][] _spectrogramFrames;
        private int _spectrogramSampleRate;
        private int _spectrogramFftSize = -1;

        private ChannelSelection _channelSelection = ChannelSelection.Mix;
        private ChannelSelection _spectrogramChannelSelection = ChannelSelection.Mix;

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
            // The host may resolve or change its theme after we are built; without this the
            // viewer would keep whatever palette it snapshotted here. Detached in Unload().
            if (_context != null) _context.PropertyChanged += OnContextPropertyChanged;
            AutoPlayCheck.IsChecked = PluginSettings.AutoPlay;
            InitSettingsControls();
            Waveform.SeekRequested += OnWaveformSeek; // click/drag on the waveform seeks
            Notes.NotesChanged += OnNotesChanged;      // overlay draw/edit/delete -> refresh export buttons
            Loaded += OnLoaded;
        }

        /// <summary>
        /// Resolve and apply the light/dark palette, letting the user's ThemeMode override
        /// QuickLook's own theme, and honouring a custom waveform colour.
        /// </summary>
        /// <param name="rerender">
        /// Re-run the spectrogram and axes too. Skipped during construction, where no
        /// audio is loaded yet; needed whenever the palette changes afterwards, because
        /// the AutoLight reverse mode makes the image itself theme-dependent.
        /// </param>
        private void ApplyTheme(bool rerender = false)
        {
            _isDark = ThemePalette.ResolveIsDark(PluginSettings.ThemeMode, _context?.Theme ?? Themes.None);
            ThemePalette.Apply(Resources, _isDark);
            Waveform.SetTheme(ThemePalette.ResolveWaveColor(PluginSettings.WaveColorHex, _isDark),
                ThemePalette.Playhead(_isDark), ThemePalette.WaveformBackground(_isDark),
                ThemePalette.WaveformClip(_isDark));

            var axisText = ThemePalette.AxisText(_isDark);
            var axisLine = ThemePalette.AxisLine(_isDark);
            FreqRuler.SetTheme(axisText, axisLine);
            TimeAxis.SetTheme(axisText, axisLine);
            Colorbar.SetTheme(axisText);
            AmpRuler.SetTheme(axisText, axisLine);
            Notes.SetTheme(ThemePalette.NoteFill(_isDark), ThemePalette.NoteBorder(_isDark),
                ThemePalette.NoteSelected(_isDark));

            if (rerender && _spectrogramBuilt)
            {
                RenderSpectrogram();
                RefreshAxes();
            }
        }

        /// <summary>
        /// QuickLook can resolve or change its theme after this control is constructed.
        /// ApplyTheme used to run once from the ctor and nothing listened for a later
        /// change, so the viewer could keep a palette the host had already moved on from —
        /// and since the OS-theme lookup falls back to dark on failure, that stuck palette
        /// was usually the dark one.
        /// </summary>
        private void OnContextPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ContextObject.Theme)) return;
            if (_unloaded) return;
            // Only the host theme moved; an explicit user choice must still win.
            if (PluginSettings.ThemeMode != ThemeMode.FollowHost) return;
            ApplyTheme(rerender: true);
        }

        /// <summary>
        /// Apply a vertical magnification and keep the badge in sync. The badge is the
        /// user-visible signal that the waveform is not at its natural scale — without it
        /// an auto-fitted quiet file looks like a loud one.
        /// </summary>
        private void SetWaveformGain(double gain, bool fromAutoFit)
        {
            if (gain < 1.0) gain = 1.0;
            if (gain > WaveformGain.MaxGain) gain = WaveformGain.MaxGain;

            _waveformGain = gain;
            Waveform.ShowClipping = PluginSettings.WaveformShowClipping;
            Waveform.Gain = gain;

            bool magnified = gain > 1.0 + 1e-9;
            bool slider = PluginSettings.ShowGainSlider;

            // Keep the row present while the slider is on, so there is always something to
            // drag back up from 1x.
            GainBadge.Visibility = magnified || slider ? Visibility.Visible : Visibility.Collapsed;
            GainSlider.Visibility = slider ? Visibility.Visible : Visibility.Collapsed;
            GainBadgeText.Text = magnified
                ? (fromAutoFit ? "Auto-fit " : "Zoom ") + WaveformGain.FormatGain(gain)
                : "Zoom ×1";

            // The slider is log2(gain), so each unit is a doubling and the travel feels even
            // across 1x..64x instead of cramming everything below 8x into the first eighth.
            _suppressGainSlider = true;
            GainSlider.Value = Math.Log(gain, 2.0);
            _suppressGainSlider = false;

            RefreshAmplitudeRuler();   // the dB ticks move with the magnification
        }

        private void GainSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressGainSlider || _initializingSettings) return;
            SetWaveformGain(Math.Pow(2.0, e.NewValue), fromAutoFit: false);
        }

        /// <summary>
        /// Fit the waveform to the file once the full scan is in.
        /// </summary>
        /// <remarks>
        /// Deliberately not run on the progressive snapshots: those arrive ~10 times with
        /// partially-filled data, so re-fitting each time would make the waveform visibly
        /// breathe as the scan discovers louder material. One settle at the end is fine.
        /// </remarks>
        private void ApplyAutoFit()
        {
            if (!PluginSettings.WaveformAutoFit || _waveformData == null) return;
            var shown = StereoWaveform.Apply(_waveformData, PluginSettings.StereoMode);
            double fit = WaveformGain.FitToPercentile(shown);
            if (fit > 1.0 + 1e-9) SetWaveformGain(fit, fromAutoFit: true);
        }

        private void GainResetButton_Click(object sender, RoutedEventArgs e)
            => SetWaveformGain(1.0, fromAutoFit: false);

        private void WaveScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Lane stacks scroll vertically inside WaveScroll while the ruler sits in the
            // outer gutter, so it has to be told the offset or its ticks drift off the lanes.
            if (e.VerticalChange != 0 || e.ExtentHeightChange != 0) RefreshAmplitudeRuler();
        }

        /// <summary>
        /// Point the amplitude ruler at the waveform band as it currently stands. Hidden
        /// whenever the waveform is not on screen, but the control keeps its 46px width so
        /// the shared left gutter — which the seek bar and scrollbar also sit in — does not
        /// change size as views toggle.
        /// </summary>
        private void RefreshAmplitudeRuler()
        {
            var shown = Waveform.Waveform;
            bool visible = shown != null && WaveScroll.IsVisible && _specTopFraction > 0.01;
            AmpRuler.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
            if (!visible) return;

            int lanes = Math.Max(1, shown.ChannelCount);
            double laneHeightDip = Math.Max(WaveformView.LaneHeight,
                WaveScroll.ViewportHeight > 0 ? WaveScroll.ViewportHeight / lanes : WaveformView.LaneHeight);

            AmpRuler.Configure(_waveformGain, lanes, _specTopFraction, laneHeightDip,
                WaveScroll.VerticalOffset);
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
            HoverStyleCombo.SelectedIndex = (int)PluginSettings.HoverStyle;
            HoverTimeCheck.IsChecked = PluginSettings.HoverShowTime;
            HoverPitchCheck.IsChecked = PluginSettings.HoverShowPitch;
            HoverLevelCheck.IsChecked = PluginSettings.HoverShowLevel;
            MillisecondsCheck.IsChecked = PluginSettings.ShowMilliseconds;
            NoteLayerCheck.IsChecked = PluginSettings.NoteLayerEnabled;
            LayoutCombo.SelectedIndex = (int)PluginSettings.Layout;
            StereoModeCombo.SelectedIndex = (int)PluginSettings.StereoMode;
            AutoFitCheck.IsChecked = PluginSettings.WaveformAutoFit;
            ClipMarkCheck.IsChecked = PluginSettings.WaveformShowClipping;
            GainSliderCheck.IsChecked = PluginSettings.ShowGainSlider;
            TimeDensityCombo.SelectedIndex = (int)PluginSettings.TimeAxisDensity;
            HoverKeepInViewCheck.IsChecked = PluginSettings.HoverKeepInView;
            FreqCombo.SelectedIndex = (int)PluginSettings.FreqScale;
            AmpCombo.SelectedIndex = (int)PluginSettings.AmpScale;
            ColorCombo.SelectedIndex = (int)PluginSettings.Colormap;
            ReverseCombo.SelectedIndex = (int)PluginSettings.ReverseMode;
            ThemeCombo.SelectedIndex = (int)PluginSettings.ThemeMode;
            InitWaveColorCombo();
            GammaSlider.Value = PluginSettings.Gamma;
            FftCombo.SelectedIndex = Math.Max(0, Array.IndexOf(FftSizes, PluginSettings.FftSize));
            MaxFreqCombo.SelectedIndex = Math.Max(0, Array.IndexOf(MaxFreqs, PluginSettings.MaxFrequencyHz));
            ContrastSlider.Value = PluginSettings.DbFloor;
            ApplySettingsPaneSize();
            _initializingSettings = false;
        }

        /// <summary>
        /// Populate the waveform-colour list from <see cref="ThemePalette.WavePalette"/>,
        /// each row carrying a swatch of the colour it selects. Generated rather than
        /// authored in XAML so the visible list and the persisted hex values cannot drift.
        /// </summary>
        private void InitWaveColorCombo()
        {
            WaveColorCombo.Items.Clear();
            string current = PluginSettings.WaveColorHex ?? string.Empty;
            int selected = 0;

            var palette = ThemePalette.WavePalette;
            for (int i = 0; i < palette.Length; i++)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new System.Windows.Shapes.Rectangle
                {
                    Width = 12,
                    Height = 12,
                    RadiusX = 2,
                    RadiusY = 2,
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    // "Theme default" has no hex, so its swatch shows the current theme's blue.
                    Fill = new System.Windows.Media.SolidColorBrush(
                        ThemePalette.ResolveWaveColor(palette[i].Hex, _isDark))
                });
                row.Children.Add(new TextBlock
                {
                    Text = palette[i].Name,
                    VerticalAlignment = VerticalAlignment.Center
                });
                WaveColorCombo.Items.Add(new ComboBoxItem { Content = row });

                if (string.Equals(palette[i].Hex ?? string.Empty, current, StringComparison.OrdinalIgnoreCase))
                    selected = i;
            }

            WaveColorCombo.SelectedIndex = selected;
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

        /// <summary>
        /// Restore the remembered settings-popup size, re-clamped to this screen. A height
        /// of 0 means "never resized" and starts from the screen-aware default.
        /// </summary>
        private void ApplySettingsPaneSize()
        {
            double workArea = SystemParameters.WorkArea.Height;
            int savedHeight = PluginSettings.SettingsPaneHeight;

            SettingsPanel.Width = SettingsPaneMetrics.ClampWidth(PluginSettings.SettingsPaneWidth);
            SettingsPanel.Height = savedHeight > 0
                ? SettingsPaneMetrics.ClampHeight(savedHeight, workArea)
                : SettingsPaneMetrics.DefaultHeightFor(workArea);
        }

        private void SettingsResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
        {
            SettingsPanel.Width =
                SettingsPaneMetrics.ClampWidth(SettingsPanel.Width + e.HorizontalChange);
            SettingsPanel.Height =
                SettingsPaneMetrics.ClampHeight(SettingsPanel.Height + e.VerticalChange,
                                                SystemParameters.WorkArea.Height);
        }

        private void SettingsResizeGrip_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            // Persist once at the end of the drag, not per delta: SettingHelper rewrites
            // the whole XML document on every Set.
            PluginSettings.SettingsPaneWidth = (int)Math.Round(SettingsPanel.Width);
            PluginSettings.SettingsPaneHeight = (int)Math.Round(SettingsPanel.Height);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;

            // The control is now attached to a presentation source, so hook the top-level
            // window for horizontal-wheel (trackpad) messages. Removed in Unload().
            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            _hwndSource?.AddHook(WndHook);

            var token = _cts.Token;
            WaveStream playbackStream = null;
            bool playerCtorInvoked = false; // once true, AudioPlayer owns/disposes the stream
            try
            {
                string infoText = null;
                TagInfo tags = null;

                // Phase 1 (fast): open the playback stream and build the file-info text
                // only. The slow full-file waveform scan is deferred to Phase 2 so info,
                // transport, and audio appear almost immediately instead of waiting on it.
                await Task.Run(() =>
                {
                    playbackStream = AudioReaderFactory.Create(_path);
                    infoText = BuildFileInfo(_path, playbackStream);
                    tags = TagReader.Read(_path); // embedded metadata; null on none/failure
                }, token);

                // Closed during the fast Phase-1 open (preview dismissed): release the stream
                // (else the file stays locked) before touching the dead control.
                if (_unloaded || token.IsCancellationRequested) { playbackStream?.Dispose(); return; }

                // Phase 1 UI: file info + transport, shown independently of the waveform (Phase 2)
                // and even of playback — a device that rejects the channel layout still shows the
                // info and the waveform. Timeline width comes from the stream here (the player may
                // fail to init, and it must be read before the ctor takes ownership of the stream).
                SetTimelineWidth(playbackStream.TotalTime.TotalSeconds);
                FileInfoText.Text = ComposeInfoText(tags, infoText);
                SetAlbumArt(tags?.CoverArt);
                FileInfoTranslate.X = PluginSettings.InfoPosX;
                FileInfoTranslate.Y = PluginSettings.InfoPosY;
                ApplyFileInfoMode(); // hover-pill vs. draggable-box presentation (sets visibility)
                // Keep the status line up while the background waveform scan runs (Phase 2 clears
                // it). The exact text is also the guard Phase 2 checks before collapsing, so it
                // doesn't stomp the Stacked spectrogram path.
                StatusText.Text = "Analyzing waveform…";
                StatusText.Visibility = Visibility.Visible;
                InitChannelCombo(playbackStream.WaveFormat.Channels);

                // Many-channel files (>2ch) open showing just the waveform lane stack, regardless
                // of the persisted Layout (an immediate 16-channel spectrogram is expensive and
                // rarely wanted). Presentation default only — PluginSettings.Layout is untouched.
                // Mono/stereo keep the persisted layout (and reveal the spectrogram if Stacked).
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

                // From here the constructor owns playbackStream: it disposes the stream itself on
                // a failed WASAPI init, so OnLoaded must not also dispose it.
                playerCtorInvoked = true;
                try
                {
                    _player = new AudioPlayer(playbackStream);
                }
                catch (Exception ex)
                {
                    // Playback unavailable (e.g. WASAPI rejected the channel layout); the stream is
                    // already disposed by the ctor. Keep info + waveform, just disable transport.
                    TimeText.Text = "Playback unavailable: " + ex.Message;
                    PlayPauseButton.IsEnabled = false;
                    SeekSlider.IsEnabled = false;
                    VolumeSlider.IsEnabled = false;
                }

                if (_player != null)
                {
                    _player.PlaybackStopped += (s, a) => Dispatcher.BeginInvoke((Action)OnPlaybackStopped);
                    _player.Volume = (float)VolumeSlider.Value;
                }
                UpdateTimeText();

                _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
                _timer.Tick += (s, a) => Tick();
                _timer.Start();

                if (PluginSettings.AutoPlay) TogglePlay(true);

                // No Focus() here. QuickLook opens its window with ShowActivated="False" so
                // Explorer keeps the keyboard: the arrow keys move Explorer's selection and
                // QuickLook follows it. Taking focus would activate our window and the arrows
                // would stop switching files. Media keys work once the user clicks the preview.

                // Phase 2 (background): the slow full-file waveform scan, delivered progressively.
                // Its own stream (disposed by the using); playbackStream is owned by _player now.
                // Own try/catch so a waveform failure never tears down the running player.
                try
                {
                    WaveformData result = null;
                    // One-pending throttle: the scan thread fires ~10 progressive snapshots, but a
                    // slow UI thread must not accumulate a backlog. While a dispatched snapshot is
                    // still pending we skip delivering another; the final full result below always
                    // lands regardless of whether the last snapshot was skipped.
                    bool snapshotPending = false;
                    // Progressive snapshots post at Background priority, but the completion below
                    // resumes at Normal priority — WPF drains by priority, not FIFO — so a late
                    // snapshot could otherwise clobber the final complete waveform with a partial
                    // one. Once the final result is shown this flag blocks any late snapshot. Both
                    // run on the single UI thread, so it's race-free.
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
                                        if (waveformFinalized) return; // final already shown; never regress
                                        if (_unloaded) return;         // unloaded mid-scan; don't touch UI
                                        _waveformData = snapshot;
                                        RefreshWaveformForMode();
                                    }));
                                },
                                token: token);
                    }, token);

                    if (_unloaded || token.IsCancellationRequested) return;
                    waveformFinalized = true;
                    _waveformData = result;
                    RefreshWaveformForMode();
                    ApplyAutoFit();   // once, on the complete scan — see ApplyAutoFit
                    // Only collapse the status line if it still shows our Phase-1 message — the
                    // Stacked spectrogram path may have replaced it with its own status.
                    if (StatusText.Text == "Analyzing waveform…")
                        StatusText.Visibility = Visibility.Collapsed;
                }
                catch (OperationCanceledException) { /* preview closed mid-scan; nothing to show */ }
                catch (Exception ex)
                {
                    if (!_unloaded)
                    {
                        StatusText.Text = "Waveform unavailable: " + ex.Message;
                        StatusText.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled mid Phase-1 (preview closed): the reader may already be open and the
                // constructor hadn't taken ownership yet, so release it here.
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
                if (!_unloaded) ShowError("Could not open this audio file. " + ex.Message);
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
            FollowPlayhead(frac);   // runs for every view, not just when the spectrogram is up
            UpdateTimeText();
        }

        private void UpdateTimeText()
        {
            if (_player == null) return;
            // Called every ~33ms timer tick. At seconds resolution the string only changes
            // about once a second, so skipping an identical write avoids needless TextBlock
            // invalidation. With milliseconds on it changes nearly every tick and the guard
            // rarely fires, but it costs one string compare and still pays on pause.
            bool ms = PluginSettings.ShowMilliseconds;
            string text = $"{Fmt(_player.Position, ms)} / {Fmt(_player.Duration, ms)}";
            if (TimeText.Text != text) TimeText.Text = text;
        }

        // Show hours only when the duration needs them; otherwise stay mm:ss.
        internal static string Fmt(TimeSpan t, bool withMilliseconds = false)
        {
            string frac = withMilliseconds ? $".{t.Milliseconds:000}" : string.Empty;
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}{frac}"
                : $"{(int)t.TotalMinutes:00}:{t.Seconds:00}{frac}";
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e) => TogglePlay(!_player?.IsPlaying ?? false);

        private void TogglePlay(bool play)
        {
            if (_player == null) return;
            // Pressing play is an explicit "show me what is happening", so resume following
            // even if the user had scrolled elsewhere while paused.
            if (play) { _userScrolled = false; _player.Play(); PlayPauseButton.Content = PauseGlyph; }
            else { _player.Pause(); PlayPauseButton.Content = PlayGlyph; }
        }

        private void OnPlaybackStopped()
        {
            if (_unloaded || _player == null) return; // event can arrive during teardown
            // reached end (or stopped): reset button glyph
            if (_player.Position >= _player.Duration - TimeSpan.FromMilliseconds(100))
                PlayPauseButton.Content = PlayGlyph;
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

            // A seek is an explicit "go here", so resume following — but do not yank the
            // view while a waveform drag has the pointer captured: re-centring on every
            // drag step slides the waveform out from under the cursor being dragged.
            _userScrolled = false;
            if (!Waveform.IsDragging) ScrollPlayheadIntoView(fraction);

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

        // A settings combo/slider kept focus while the popup was open; hand it back so the
        // media keys work again and arrows can't be eaten by a now-hidden control.
        // Only while our window is active: the popup also closes when the user clicks back into
        // Explorer, and focusing then would re-activate this window and steal the keyboard.
        private void SettingsPopup_Closed(object sender, EventArgs e)
        {
            if (Window.GetWindow(this)?.IsActive == true) Focus();
        }

        // Media-player keys chosen to NOT collide with QuickLook host gestures (Space
        // toggles the preview; arrow keys navigate files in Explorer). Unhandled keys
        // bubble up to the host untouched. Arrows must never be handled here — and see
        // the root of AudioViewerControl.xaml for why nothing else may grab them either.
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

                // Reset vertical zoom. "0" is the usual reset-zoom key and is unclaimed
                // here; +/- deliberately stay on volume rather than changing meaning
                // depending on where the pointer happens to be.
                case Key.D0:
                case Key.NumPad0: SetWaveformGain(1.0, fromAutoFit: false); e.Handled = true; break;
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

        private void HoverStyleCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initializingSettings || HoverStyleCombo.SelectedIndex < 0) return;
            // Read live on the next MouseMove, so nothing to re-render.
            PluginSettings.HoverStyle = (HoverReadoutStyle)HoverStyleCombo.SelectedIndex;
        }

        private void HoverFieldCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializingSettings) return;
            PluginSettings.HoverShowTime = HoverTimeCheck.IsChecked == true;
            PluginSettings.HoverShowPitch = HoverPitchCheck.IsChecked == true;
            PluginSettings.HoverShowLevel = HoverLevelCheck.IsChecked == true;
        }

        private void MillisecondsCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializingSettings) return;
            PluginSettings.ShowMilliseconds = MillisecondsCheck.IsChecked == true;
            // Repaint now rather than waiting for the next timer tick, which never comes
            // when playback is paused or the file has already finished.
            UpdateTimeText();
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

        private void AutoFitCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializingSettings) return;
            PluginSettings.WaveformAutoFit = AutoFitCheck.IsChecked == true;
            // Apply or undo immediately so the effect of the toggle is visible without
            // reopening the preview.
            if (PluginSettings.WaveformAutoFit) ApplyAutoFit();
            else SetWaveformGain(1.0, fromAutoFit: false);
        }

        private void ClipMarkCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializingSettings) return;
            PluginSettings.WaveformShowClipping = ClipMarkCheck.IsChecked == true;
            Waveform.ShowClipping = PluginSettings.WaveformShowClipping;
        }

        private void GainSliderCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializingSettings) return;
            PluginSettings.ShowGainSlider = GainSliderCheck.IsChecked == true;
            SetWaveformGain(_waveformGain, fromAutoFit: false); // re-evaluates row visibility
        }

        private void TimeDensityCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initializingSettings || TimeDensityCombo.SelectedIndex < 0) return;
            PluginSettings.TimeAxisDensity = (TimeAxisDensity)TimeDensityCombo.SelectedIndex;
            TimeAxis.Configure(_durationSeconds, PluginSettings.TimeAxisMinPxPerLabel);
        }

        private void HoverKeepInViewCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializingSettings) return;
            // Read live on the next MouseMove, so nothing to re-render.
            PluginSettings.HoverKeepInView = HoverKeepInViewCheck.IsChecked == true;
        }

        private void StereoModeCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initializingSettings || StereoModeCombo.SelectedIndex < 0) return;
            PluginSettings.StereoMode = (StereoWaveformMode)StereoModeCombo.SelectedIndex;
            RefreshWaveformForMode(); // instant re-collapse of the stored scan; no re-read
        }

        /// <summary>Feeds WaveformView the current scan run through the stereo display-mode
        /// transform. A no-op until the first scan (snapshot or final) has populated the data.</summary>
        private void RefreshWaveformForMode()
        {
            if (_waveformData == null) return;
            Waveform.Waveform = StereoWaveform.Apply(_waveformData, PluginSettings.StereoMode);
            // The lane count changes with the stereo mode, so the ruler has to follow.
            RefreshAmplitudeRuler();
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

        // The corner badges cycle through the settings combos rather than writing the setting
        // themselves, so the combo, the saved setting and the badge cannot drift apart.
        private void FreqScaleBadge_Click(object sender, MouseButtonEventArgs e)
        {
            FreqCombo.SelectedIndex = ScaleBadge.Next(FreqCombo.SelectedIndex, FreqCombo.Items.Count);
            e.Handled = true;
        }

        private void AmpScaleBadge_Click(object sender, MouseButtonEventArgs e)
        {
            AmpCombo.SelectedIndex = ScaleBadge.Next(AmpCombo.SelectedIndex, AmpCombo.Items.Count);
            e.Handled = true;
        }

        private void UpdateScaleBadges()
        {
            // Prefixed because both can read "LIN"; the bare names stay in ScaleBadge.
            FreqScaleBadge.Text = "Hz " + ScaleBadge.Short(PluginSettings.FreqScale);
            FreqScaleBadge.ToolTip = $"Frequency scale: {ComboText(FreqCombo)} (click to change)";
            AmpScaleBadge.Text = "Amp " + ScaleBadge.Short(PluginSettings.AmpScale);
            AmpScaleBadge.ToolTip = $"Amplitude scale: {ComboText(AmpCombo)} (click to change)";
            FreqScaleBadge.Visibility = Visibility.Visible;
            AmpScaleBadge.Visibility = Visibility.Visible;
        }

        private static string ComboText(ComboBox combo) =>
            (combo.SelectedItem as ComboBoxItem)?.Content as string ?? "";

        private void ColorCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initializingSettings || ColorCombo.SelectedIndex < 0) return;
            PluginSettings.Colormap = (SpectrogramColormap)ColorCombo.SelectedIndex;
            RenderSpectrogram();
        }

        private void ReverseCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initializingSettings || ReverseCombo.SelectedIndex < 0) return;
            PluginSettings.ReverseMode = (ReverseColormapMode)ReverseCombo.SelectedIndex;
            RenderSpectrogram();
            RefreshAxes();   // the colorbar gradient mirrors the same inversion
        }

        private void ThemeCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initializingSettings || ThemeCombo.SelectedIndex < 0) return;
            PluginSettings.ThemeMode = (ThemeMode)ThemeCombo.SelectedIndex;
            ApplyTheme(rerender: true);

            // The "Theme default" swatch tracks the palette, so rebuild the list. Guarded
            // because repopulating the combo re-fires SelectionChanged.
            _initializingSettings = true;
            InitWaveColorCombo();
            _initializingSettings = false;
        }

        private void WaveColorCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initializingSettings || WaveColorCombo.SelectedIndex < 0) return;
            int i = WaveColorCombo.SelectedIndex;
            if (i >= ThemePalette.WavePalette.Length) return;
            PluginSettings.WaveColorHex = ThemePalette.WavePalette[i].Hex ?? string.Empty;
            // Goes through SetTheme, which rebuilds the pens and drops the cached wave
            // bitmap — the colour is baked into those pixels, so nothing short of a
            // re-raster will change it.
            ApplyTheme();
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
            // Alt + wheel over the waveform magnifies it vertically.
            //
            // This has to come first, before the lane-stack bail-out below, because this is
            // a tunnelling PreviewMouseWheel on ViewScroll and WaveScroll would otherwise
            // consume the event. Alt rather than Ctrl: Ctrl is the conventional zoom
            // modifier and is reserved for a future time zoom, which is the more common
            // need. The plain wheel is already fully spoken for — horizontal timeline for
            // mono/stereo, vertical lanes for multichannel — so there is no unmodified
            // gesture left to take.
            if (Keyboard.Modifiers == ModifierKeys.Alt && WaveScroll.IsVisible && WaveScroll.IsMouseOver)
            {
                SetWaveformGain(WaveformGain.Step(_waveformGain, e.Delta > 0 ? +1 : -1), fromAutoFit: false);
                e.Handled = true;
                return;
            }

            // Wheel over a vertically-scrollable lane stack (many-channel files) scrolls
            // the lanes instead — otherwise this preview handler (which tunnels before
            // WaveScroll's own bubbling handler ever sees the event) would swallow it for
            // horizontal timeline scrolling and lanes below the viewport would be unreachable.
            //
            // Shift forces the time axis regardless. Without it the timeline is simply
            // unreachable by wheel over a lane stack — only the scrollbar or a trackpad
            // swipe could move it — and Shift+wheel is the conventional "other axis".
            bool forceTimeAxis = Keyboard.Modifiers == ModifierKeys.Shift;
            if (!forceTimeAxis
                && WaveScroll.IsVisible && WaveScroll.ScrollableHeight > 0 && WaveScroll.IsMouseOver)
                return;

            if (ViewScroll.ScrollableWidth <= 0) return; // nothing to scroll; let it bubble
            NoteManualScroll();
            ViewScroll.ScrollToHorizontalOffset(ViewScroll.HorizontalOffset - e.Delta);
            e.Handled = true;
        }

        // Two-finger horizontal trackpad swipe. WPF drops WM_MOUSEHWHEEL, so we read it
        // straight off the window and scroll the timeline. Swipe right (delta > 0) moves
        // the window forward in time — matching the XdfViewer plugin, and consistent with
        // the vertical-wheel handler above (scroll up = toward the start).
        private IntPtr WndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_MOUSEHWHEEL || _unloaded) return IntPtr.Zero;

            long w = IntPtr.Size == 8 ? wParam.ToInt64() : (uint)wParam.ToInt32();
            int delta = (short)((w >> 16) & 0xFFFF);
            if (delta == 0) return IntPtr.Zero;

            // Only act when the timeline is visible, actually scrollable, and the pointer is
            // over it (DPI-safe via WPF hit coordinates rather than raw Win32 points).
            if (!ViewScroll.IsVisible || ViewScroll.ScrollableWidth <= 0) return IntPtr.Zero;
            var p = Mouse.GetPosition(ViewScroll);
            if (p.X < 0 || p.Y < 0 || p.X > ViewScroll.ActualWidth || p.Y > ViewScroll.ActualHeight)
                return IntPtr.Zero;

            NoteManualScroll();
            ViewScroll.ScrollToHorizontalOffset(ViewScroll.HorizontalOffset + delta);
            handled = true;
            return IntPtr.Zero;
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
            RefreshAxes();
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
                _specTopFraction = 1.0 / 5.0; // spectrogram is the lower 4/5
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
                _specTopFraction = 0.0; // spectrogram fills the pane
            }
            UpdateSpectrogramPlayheadVisibility();
            RefreshAxes();
            RefreshAmplitudeRuler();   // the waveform band moved
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
                            // Feed the selected channel(s) into the FFT: >2ch sources are mixed
                            // down (or soloed) per the current selection, mono/stereo pass through.
                            ISampleProvider source = stream.ToSampleProvider();
                            if (stream.WaveFormat.Channels > 2)
                                source = new ChannelMixSampleProvider(source, selection);
                            frames = SpectrogramGenerator.ComputeMagnitudes(source,
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
                _spectrogramChannelSelection = selection;
                _spectrogramCapped = capped;
                SpectrogramButton.IsEnabled = true;
            }

            RenderSpectrogram();

            // `capped` was computed and passed but never read. When the frame cap is hit
            // the spectrogram stops short of the end of the file, while the playhead, the
            // time ruler and the hover readout all keep mapping x across the full duration
            // — so every time they report is wrong, with nothing on screen saying so. The
            // hop is chosen to land under the cap, so this should not normally fire; say so
            // when it does rather than quietly mis-mapping.
            if (_spectrogramCapped && !_unloaded)
            {
                StatusText.Text = "Spectrogram truncated at the frame limit — times shown past " +
                                  "this point are approximate.";
                StatusText.Visibility = Visibility.Visible;
            }
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
                ReverseColormap = ThemePalette.ResolveReverse(PluginSettings.ReverseMode, _isDark),
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
            _durationSeconds = seconds;
            double w = seconds * TimelinePxPerSec;
            if (w > MaxTimelineColumns) w = MaxTimelineColumns;
            if (w < 1) w = 1;
            Timeline.Width = w; // MinWidth binding keeps short files filling the pane

            // TimeAxis.Width is bound to Timeline.ActualWidth in XAML, so it matches the
            // stretched width for short files too; here we only supply the duration.
            TimeAxis.Configure(seconds, PluginSettings.TimeAxisMinPxPerLabel);
            // Seed the geometry from the width we just set, so the axis is correct on the
            // very first paint; ViewScroll_ScrollChanged refines it once layout settles.
            TimeAxis.SetGeometry(w, 0);
            TimeAxis.Visibility = Visibility.Visible;
        }

        // Keep the time ruler aligned under the spectrogram as the shared timeline scrolls,
        // and mirror the scroll state onto the dedicated horizontal scrollbar below the plot.
        private void ViewScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // ExtentWidth is the timeline's real laid-out width, so short files that stretch
            // to the viewport via MinWidth are handled without a second code path. This also
            // fires on extent changes, so the ruler picks up the width as soon as layout
            // settles rather than depending on a binding having resolved first.
            TimeAxis.SetGeometry(ViewScroll.ExtentWidth, ViewScroll.HorizontalOffset);

            double scrollable = ViewScroll.ScrollableWidth;
            HScroll.Maximum = scrollable;
            HScroll.ViewportSize = ViewScroll.ViewportWidth;
            HScroll.Value = ViewScroll.HorizontalOffset;
            HScroll.Visibility = scrollable > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HScroll_Scroll(object sender, ScrollEventArgs e)
        {
            NoteManualScroll();   // dragging the scrollbar is as deliberate as it gets
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
                FreqScaleBadge.Visibility = Visibility.Collapsed;
                AmpScaleBadge.Visibility = Visibility.Collapsed;
                RefreshNotes(); // keep the overlay/toolbar in step (hidden while the spectrogram is off)
                return;
            }

            var scale = PluginSettings.FreqScale;
            double fMin = FrequencyScaleMapper.EffectiveFMin(scale, _spectrogramFftSize, _spectrogramSampleRate);
            double fMax = FrequencyScaleMapper.EffectiveFMax(_spectrogramSampleRate, PluginSettings.MaxFrequencyHz);
            FreqRuler.Configure(scale, fMin, fMax, _specTopFraction);
            Colorbar.Configure(PluginSettings.Colormap, _lastLevels, _specTopFraction,
                ThemePalette.ResolveReverse(PluginSettings.ReverseMode, _isDark), PluginSettings.Gamma);
            FreqRuler.Visibility = Visibility.Visible;
            Colorbar.Visibility = Visibility.Visible;
            UpdateScaleBadges();
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
                Notes.Configure(scale, fMin, fMax, _durationSeconds, _notes);
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
            if (!_spectrogramBuilt || frames == null || frames.Length == 0 || _durationSeconds <= 0)
            {
                HoverReadout.Visibility = Visibility.Collapsed;
                return;
            }
            double w = SpectrogramImage.ActualWidth, h = SpectrogramImage.ActualHeight;
            if (w <= 0 || h <= 0) return;

            var p = e.GetPosition(SpectrogramImage);
            double tx = Clamp01(p.X / w);
            double u = Clamp01(1.0 - p.Y / h); // y=0 (top) is the highest frequency

            double time = tx * _durationSeconds;
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
            string readout = HoverReadoutFormatter.FormatReadout(
                time, PluginSettings.ShowMilliseconds ? 3 : 0,
                freq, PluginSettings.HoverReadoutMode,
                mag, PluginSettings.AmpScale,
                PluginSettings.HoverStyle,
                PluginSettings.HoverShowTime, PluginSettings.HoverShowPitch, PluginSettings.HoverShowLevel);

            if (readout.Length == 0)
            {
                // Every field switched off — there is nothing to show.
                HoverReadout.Visibility = Visibility.Collapsed;
                return;
            }
            HoverText.Text = readout;

            // Measure against the text we just set: ActualWidth still reflects the previous
            // readout, and in box mode the width changes with the values, so placing from
            // it puts the box in the wrong place for one frame and mis-detects overflow.
            HoverReadout.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var box = HoverReadout.DesiredSize;

            var pc = e.GetPosition(ContentGrid);
            HoverReadoutFormatter.PlaceBox(pc.X, pc.Y, box.Width, box.Height,
                ContentGrid.ActualWidth, ContentGrid.ActualHeight, 12,
                PluginSettings.HoverKeepInView,
                out double hx, out double hy);
            HoverTranslate.X = hx;
            HoverTranslate.Y = hy;
            HoverReadout.Visibility = Visibility.Visible;
        }

        private void SpectrogramImage_MouseLeave(object sender, MouseEventArgs e)
            => HoverReadout.Visibility = Visibility.Collapsed;

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

        private void UpdateSpectrogramPlayhead(double frac)
        {
            if (SpectrogramImage.Visibility != Visibility.Visible || !_spectrogramBuilt) return;
            SpectrogramPlayhead.Margin = new Thickness(frac * Timeline.ActualWidth, 0, 0, 0);
        }

        /// <summary>
        /// During playback, keep the playhead in view as it nears a scrolled edge.
        /// </summary>
        /// <remarks>
        /// This used to live inside <see cref="UpdateSpectrogramPlayhead"/>, behind its
        /// early return — so it only ran when the spectrogram was visible. Multichannel
        /// files open to the waveform-only view by design and therefore never followed the
        /// playhead at all. It is now driven from Tick() independently of which view is up.
        ///
        /// It also no longer fights a deliberate scroll. The old condition was "the
        /// playhead is not comfortably inside the viewport", which is equally true of every
        /// position outside it, so any manual scroll away during playback was undone on the
        /// next 33 ms tick. Manual scrolling now suspends following until the next seek or
        /// until playback restarts.
        /// </remarks>
        private void FollowPlayhead(double frac)
        {
            if (_player == null || !_player.IsPlaying || _scrubbing || _userScrolled) return;

            double vw = ViewScroll.ViewportWidth;
            double tw = Timeline.ActualWidth;
            if (vw <= 0 || tw <= vw) return;

            double x = frac * tw;
            double off = ViewScroll.HorizontalOffset;

            // Only chase it while it is actually in (or just past) the viewport. If it is
            // far outside, the user scrolled somewhere on purpose and _userScrolled has
            // already suppressed us; this is the belt-and-braces half of the same idea.
            if (x < off - vw || x > off + 2 * vw) return;
            if (x >= off + 40 && x <= off + vw - 40) return;

            ViewScroll.ScrollToHorizontalOffset(Math.Max(0, x - vw / 2));
        }

        /// <summary>
        /// Note that the user took control of the horizontal scroll, suspending auto-follow.
        /// </summary>
        private void NoteManualScroll() => _userScrolled = true;

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
            if (_hwndSource != null) { _hwndSource.RemoveHook(WndHook); _hwndSource = null; }
            // The ContextObject belongs to the host and outlives this preview, so a live
            // handler here would root the control and its FFT frames for the host's lifetime.
            if (_context != null) _context.PropertyChanged -= OnContextPropertyChanged;
            _cts.Cancel();      // abort in-flight decode/FFT so it doesn't run on a dead control
            _timer?.Stop();
            _player?.Dispose(); // disposes the WASAPI output and releases the file handle
            _player = null;
            _cts.Dispose();
        }
    }
}
