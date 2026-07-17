# QuickLook.Plugin.AudioViewer

A [QuickLook](https://github.com/QL-Win/QuickLook) plugin that previews audio
files (`.wav .mp3 .flac .ogg .oga .m4a .aac .wma .aif .aiff`) with a waveform,
a moving playhead, playback transport (play/pause, seek, volume, persisted
auto-play), and an on-demand spectrogram.

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
