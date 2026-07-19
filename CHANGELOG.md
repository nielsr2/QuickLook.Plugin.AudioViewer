# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.4.0] — Unreleased

### Added
- Embedded tags (title, artist, album, year, genre) and cover art in the file-info
  overlay, via TagLibSharp.
- Spectrogram axes: a frequency ruler (Linear/Log/Mel aware) and a time ruler that
  scrolls with the shared timeline.
- Colorbar legend showing the active colormap and its dB / magnitude range.
- Hover readout: time, frequency, and level under the cursor on the spectrogram.
- Light/dark theme awareness — the UI follows QuickLook's theme.
- Click-to-seek on the waveform, and click-to-seek on the seek-bar track.
- Keyboard shortcuts: `K` play/pause, `J`/`L` seek ±5 s, `M` mute, `+`/`-` volume.
- A real mute toggle (the speaker icon), and hours in the time readout for long files.
- `THIRD-PARTY-NOTICES.md` bundling the licenses of the redistributed libraries.

### Changed
- High-quality bitmap scaling for a crisper spectrogram on high-DPI displays.
- The DLL now carries the plugin version (previously `1.0.0.0`), single-sourced with
  the metadata config.

### Fixed
- Closing a preview mid-load no longer leaks the audio player, file handle, or timer
  (which could keep the file locked so it couldn't be deleted or moved).
- The playback stream is always released when loading fails.
- The spectrogram render path can no longer crash the QuickLook host on an error.
- Files shorter than one FFT window now show a message instead of a blank spectrogram.
- Corrected the license attribution for QuickLook.Common (MIT, not GPL-3.0) and the
  file-info overlay position in the README.

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
