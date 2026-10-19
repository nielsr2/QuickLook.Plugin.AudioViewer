<img src="assets/icon.svg" alt="AudioViewer icon" width="88" align="right"/>

# QuickLook.Plugin.AudioViewer

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A [QuickLook](https://github.com/QL-Win/QuickLook) plugin that previews audio
files (`.wav .mp3 .flac .ogg .oga .m4a .aac .wma .aif .aiff`) with a waveform,
a moving playhead, playback transport (play/pause, seek, volume, persisted
auto-play), and a configurable spectrogram.

![QuickLook.Plugin.AudioViewer previewing an AIFF file — auto-fitted waveform with a dB ruler above a magma spectrogram, with frequency ruler, colorbar, time axis and playhead](assets/screenshot.png)

## Waveform and channels

Stereo files draw as **Two lanes (split)** by default; the settings flyout's *Stereo display*
option can combine them into one lane as a **mono average** or a **peak union** (the envelope
of both channels, so nothing transient is lost).

Mono and multichannel files always show one lane per channel. Files with more than two
channels — up to 16-channel WAVs — stack one lane per channel in a vertically scrollable
column with adaptive lane heights, and open to the waveform-only view. A channel selector
appears in the toolbar for these files and drives both playback and the spectrogram source;
playback downmixes to stereo, or solos the selected channel.

## Spectrogram

A gear (⚙) button in the toolbar opens a settings flyout. All choices persist:

- **Layout** — *Stacked* (waveform above spectrogram, the default) or *Single view*
  (a button toggles between the full-area waveform and spectrogram).
- **Frequency scale** — Linear, Log, or Mel. The active frequency and amplitude scales
  are shown under the frequency ruler (`Hz LOG` / `Amp dB²`); click either to cycle it.
- **Amplitude** — Decibel (dB) or Linear.
- **Colormap** — Magma, Viridis, Grayscale, Inferno, Plasma, Turbo, or Heat (RX-style).
- **Reverse colormap** — flip any map light-based (for print / light backgrounds).
- **FFT size** — 512 / 1024 / 2048 / 4096 (time-vs-frequency resolution).
- **Max frequency** — Full / 20 kHz / 10 kHz / 5 kHz.
- **Contrast** — dB-floor slider (black clip point).
- **Gamma (detail)** — lifts quiet detail without moving the noise floor.
- **Hover readout** — MIDI note (default) or Frequency: what the cursor shows over the
  spectrogram (time · nearest note or Hz · level).
- **Show file info** — an overlay with embedded tags (title, artist, album, year,
  genre) and cover art when present, plus the file's format, decoder, sample rate,
  channels, bit depth, encoding, bitrate, and duration.
- **File info style** — *Icon (hover)* (default): a small "info" pill in the corner
  that reveals the box on hover; or *Draggable box*: a box you can drag and close.

Display changes re-render instantly from cached FFT data; only an FFT-size change
recomputes the transform.

Long files render in full: the waveform and spectrogram share one horizontally
scrollable timeline (they stay aligned in time), and the view auto-follows the
playhead during playback. Short files fill the pane with no scrollbar. The mouse
wheel scrolls the timeline, and trackpad horizontal swipes scroll it directly.

### Experimental

An **EXPERIMENTAL** section at the bottom of the settings flyout enables the **note
overlay**: draw MIDI notes over the spectrogram with a pencil tool, move/resize/select/
delete them, and export to a Standard MIDI File or copy them as text. Notes align on any
frequency scale, with a one-click nudge to the Log scale for evenly-spaced semitone rows.
It is off by default and has rough edges.

The UI follows QuickLook's light/dark theme. Click the waveform (or the seek bar)
to jump to a position.

## Keyboard shortcuts

When the preview has focus (click it first: on open, Explorer keeps the keyboard, so the
arrow keys switch files):

| Key | Action |
| --- | --- |
| `K` | Play / pause |
| `J` / `L` | Seek back / forward 5 s |
| `M` | Mute / unmute |
| `+` / `-` | Volume up / down |

(Space and the arrow keys are intentionally left to QuickLook itself, which uses
them to open/close previews and move between files.)

## Build

```
git clone --recurse-submodules <this repo>
dotnet build QuickLook.Plugin.AudioViewer/QuickLook.Plugin.AudioViewer.csproj -c Release
powershell -File Scripts/pack-zip.ps1
```

Produces `QuickLook.Plugin.AudioViewer.qlplugin`.

## Install

1. In QuickLook, press **Space** on `QuickLook.Plugin.AudioViewer.qlplugin`.
2. Confirm the install prompt.
3. Restart QuickLook.

Then press **Space** on any supported audio file.

## Tests

```
dotnet test tests/QuickLook.Plugin.AudioViewer.Tests
```

## Requirements

- Windows with **.NET Framework 4.8** (built in on Windows 10 1903+ / Windows 11;
  installable on older Windows). Media Foundation codecs are used for FLAC/AAC/M4A/WMA.
- QuickLook.

## Releases

Pushing a version tag (e.g. `git tag v1.5.0 && git push origin v1.5.0`) runs the
release workflow, which builds, packages the `.qlplugin`, and attaches it to a
GitHub Release. CI builds and runs the tests on every push/PR. See
[CHANGELOG.md](CHANGELOG.md) for release notes.

## Roadmap

- visual presets for waveform

## License

[MIT](LICENSE) © nielsr2.

## Acknowledgements

- [QuickLook](https://github.com/QL-Win/QuickLook) — the host application (GPL-3.0).
- [QuickLook.Common](https://github.com/QL-Win/QuickLook.Common) — the plugin API this
  project builds against (MIT). Its assembly is supplied by the host at runtime, not
  redistributed here.
- [NAudio](https://github.com/naudio/NAudio),
  [NAudio.Vorbis](https://github.com/naudio/Vorbis), and
  [NVorbis](https://github.com/NVorbis/NVorbis) — MIT.

Full third-party license texts are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
