# QuickLook.Plugin.AudioViewer

A [QuickLook](https://github.com/QL-Win/QuickLook) plugin that previews audio
files (`.wav .mp3 .flac .ogg .oga .m4a .aac .wma .aif .aiff`) with a waveform,
a moving playhead, playback transport (play/pause, seek, volume, persisted
auto-play), and a configurable spectrogram.

## Spectrogram

A gear (⚙) button in the toolbar opens a settings flyout. All choices persist:

- **Layout** — *Stacked* (waveform above spectrogram, the default) or *Overlay*
  (a button toggles between the two full-area views).
- **Frequency scale** — Linear, Log, or Mel.
- **Amplitude** — Decibel (dB) or Linear.
- **Colormap** — Magma, Viridis, or Grayscale.
- **FFT size** — 512 / 1024 / 2048 / 4096 (time-vs-frequency resolution).
- **Max frequency** — Full / 20 kHz / 10 kHz / 5 kHz.
- **Contrast** — dB-floor slider.
- **Show file info** — a top-left overlay with the file's format, decoder, sample
  rate, channels, bit depth, encoding, bitrate, and duration.

Display changes re-render instantly from cached FFT data; only an FFT-size change
recomputes the transform.

Long files render in full: the waveform and spectrogram share one horizontally
scrollable timeline (they stay aligned in time), and the view auto-follows the
playhead during playback. Short files fill the pane with no scrollbar.

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

- Windows 10 1709+ / Windows 11 (Media Foundation codecs for FLAC/AAC/M4A/WMA).
- QuickLook (.NET Framework 4.x runtime).
