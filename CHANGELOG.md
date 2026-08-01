# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
