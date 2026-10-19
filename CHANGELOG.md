# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- The amplitude-scale label moved from under the colorbar to under the frequency-scale
  label, and both are now prefixed (`Hz LOG` over `Amp dB²`). They could both read `LIN`,
  so on its own the right-hand one wasn't recognisable. Click either to cycle it, as before.
- New README screenshot.

## [1.6.0] — 2026-10-19

### Added
- Vertical waveform zoom. Quiet files are auto-fitted on load (99th-percentile, so a
  single transient does not defeat it), and Alt + wheel over the waveform magnifies
  manually; `0` resets. A badge shows the factor whenever the waveform is magnified, so a
  stretched waveform is never mistaken for the file's real level. Columns the lane edge
  cut off are marked in amber.
- A dBFS amplitude ruler beside the waveform, so the Y axis reflects the magnification.
- Hover readout as a labelled box (Time / Note / Level) by default, with the previous
  single line kept as a Compact option and each field individually switchable.
- Milliseconds in the transport readout (`mm:ss.mmm`), on by default.
- `dB²` amplitude scale: half the log gain against the same contrast floor, lifting
  background detail and upper partials that the plain dB scale crushes toward black.
- Appearance setting — Follow QuickLook / Light / Dark — overriding the host's theme.
- Customizable waveform colour, with nine presets plus the theme default.
- The active frequency scale (`LIN` / `LOG` / `MEL`) and amplitude scale (`dB` / `LIN` /
  `dB²`) are shown in the plot's bottom corners, under the frequency ruler and the
  colorbar. Click either one to cycle to the next scale.

### Changed
- Reverse colormap is now Never / Always / Auto (light mode only), replacing the on-off
  checkbox. The previously ticked state migrates to Always.
- The spectrogram scan no longer shifts the whole FFT window once per input sample. On a
  3-minute file this is 2.8x faster at the default FFT 1024 and 5.4x at 4096, with
  bit-identical output.
- Playhead auto-follow now works in the waveform-only view (which multichannel files open
  to) and no longer undoes a deliberate scroll on the next frame.
- Shift + wheel forces the time axis, so the timeline stays reachable over a lane stack.

### Fixed
- The arrow keys switch to the previous/next file in Explorer again. The preview no
  longer takes keyboard focus away from Explorer when it opens (the first ↓ used to
  land on the volume slider). K/J/L/M/+/−/0 now work once you click the preview.
- Time-axis labels no longer pile up at the right edge. The label at the edge (e.g. `0:10`
  while scrolled near the start) was pulled left onto its neighbour. Now only the file's
  final label is kept on screen, and it is dropped if it would collide.
- The play/pause button uses the same icon font as the mute and settings buttons, centred
  instead of sitting low and clipped.
- The colorbar legend no longer renders every tick as `0.00` in linear mode. The values
  are raw FFT magnitudes spanning several orders of magnitude, and a fixed two-decimal
  format collapsed them.
- Duplicate time-axis labels on short files (`0:00, 0:00, 0:01, 0:01, …`): the tick ladder
  bottomed out at 0.5 s while labels only ever showed whole seconds. The time ruler also
  now de-clutters overlapping labels, as the frequency ruler already did.
- The viewer follows a theme change made after the preview opens. `ApplyTheme` ran once
  from the constructor and nothing listened for a later change, so the palette could stick
  — usually on dark, since the OS-theme lookup falls back to dark on failure.
- Dragging on the waveform to scrub no longer re-centres the timeline under the pointer on
  every step.
- A truncated spectrogram now says so instead of silently mis-reporting every time past
  the truncation point.

## [1.5.0] — 2026-08-01

### Added
- Multichannel support. Files with more than two channels render one stacked waveform lane
  per channel, vertically scrollable, with adaptive lane height and separators; they open to
  the waveform-only view. A channel selector in the toolbar drives both playback and the
  spectrogram source, and `ChannelMixSampleProvider` downmixes N channels to stereo (or solos
  one) for playback. Verified against 16-channel WAV files.
- Stereo display modes for the waveform, under a new WAVEFORM settings section: *Two lanes
  (split)*, *Combined — mono average*, or *Combined — peak union*. Mono and multichannel
  files always show one lane per channel.
- Horizontal trackpad scrolling on the shared timeline.
- Interactive MIDI-note overlay on the spectrogram, opt-in via the new EXPERIMENTAL settings
  section ("Note overlay"): draw notes with a pencil tool, move/resize/select/delete them,
  and export to a Standard MIDI File or copy them as text. Notes align on any frequency scale
  via the shared scale mapping, with a one-click nudge to switch to the Log scale for
  evenly-spaced semitone rows.
- Embedded tags (title, artist, album, year, genre) and cover art in the file-info
  overlay, via TagLibSharp.
- Spectrogram axes: a frequency ruler (Linear/Log/Mel aware) and a time ruler that
  scrolls with the shared timeline.
- Colorbar legend showing the active colormap and its dB / magnitude range.
- Hover readout showing time, the nearest MIDI note (default) or frequency, and the
  level under the cursor on the spectrogram, with a "Hover readout" setting to switch.
- File-info overlay display modes: an unobtrusive hover "info" pill (default, clickable
  to pin it open) or the draggable box, via a "File info style" setting.
- More spectrogram colormaps — Inferno, Plasma, Turbo, and an RX-style Heat ramp — plus a
  Reverse toggle (light-based maps for print) and a Gamma "detail" slider that lifts quiet
  detail without moving the noise floor.
- Light/dark theme awareness — the UI follows QuickLook's theme.
- Click-to-seek on the waveform, and click-to-seek on the seek-bar track.
- Keyboard shortcuts: `K` play/pause, `J`/`L` seek ±5 s, `M` mute, `+`/`-` volume.
- A real mute toggle (the speaker icon), and hours in the time readout for long files.
- `THIRD-PARTY-NOTICES.md` bundling the licenses of the redistributed libraries.

### Changed
- The playback timeline is on its own full-width row aligned with the plots, and the
  horizontal scrollbar sits below the time axis instead of overlaying the spectrogram.
- Dragging the playhead now scrubs the waveform, spectrogram, and time readout live,
  not only on release.
- High-quality bitmap scaling for a crisper spectrogram on high-DPI displays.
- The DLL now carries the plugin version (previously `1.0.0.0`), single-sourced with
  the metadata config.
- Phased loading: file info, the transport, and audio come up immediately while the waveform
  scan runs in the background and fills in progressively, so long files are playable long
  before the waveform finishes.
- The waveform renders as an off-thread bitmap blit instead of ~192k per-sample `DrawLine`
  calls, with a fast PCM16 decode path and cached lane rendering. Large multichannel files
  are dramatically faster to open.

### Fixed
- Closing a preview mid-load no longer leaks the audio player, file handle, or timer
  (which could keep the file locked so it couldn't be deleted or moved).
- The playback stream is always released when loading fails.
- The spectrogram render path can no longer crash the QuickLook host on an error.
- Files shorter than one FFT window now show a message instead of a blank spectrogram.
- Corrected the license attribution for QuickLook.Common (MIT, not GPL-3.0) and the
  file-info overlay position in the README.
- A playback-init failure no longer blanks the whole preview.
- The playback stream the player already owns is no longer disposed twice.
- `WasapiOut` is disposed if `Init` throws in the `AudioPlayer` constructor.
- The channel selection is captured before the async spectrogram compute, so switching
  channels mid-render no longer renders the wrong source.
- A late waveform snapshot can no longer overwrite the final rendered waveform.
- A note drawn near the bottom edge of the spectrogram no longer breaks "Copy notes as text"
  (on the Linear and Mel scales the bottom row is 0 Hz, which has no pitch).
- The hover readout and the note overlay keep working when the audio player fails to
  initialise, instead of going dead along with playback.
- Switching between light and dark themes now repaints the playhead and the lane separators,
  and no longer leaves a stale waveform bitmap in the wrong colours.

## [1.3.2] — 2026-07-18

### Changed
- File-info overlay: dismissing (✕) hides it for the current file only, separate from
  the persistent "Show file info" setting; the dragged position is remembered.

## [1.3.1] — 2026-07-18

### Changed
- File-info overlay moved to the bottom-right; it is now draggable and closable.

## [1.3.0] — 2026-07-18

### Added
- Toggleable file-info overlay (format, decoder, sample rate, channels, bit depth,
  encoding, bitrate, duration).
- MIT license and GitHub Actions workflows (CI + tag-driven release).

## [1.2.0] — 2026-07-17

### Added
- Full-render, horizontally-scrollable shared timeline so long files render in full and
  stay aligned between the waveform and spectrogram.
- Mouse wheel scrolls the timeline.

### Changed
- Renamed the "Overlay" layout to "Single view".
