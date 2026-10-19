# QuickLook.Plugin.AudioViewer

A QuickLook plugin: audio preview with waveform, playback, and a configurable
spectrogram. C# / WPF / .NET Framework 4.8.

## Build and test

```
dotnet build QuickLook.Plugin.AudioViewer/QuickLook.Plugin.AudioViewer.csproj -c Release
dotnet test tests/QuickLook.Plugin.AudioViewer.Tests/QuickLook.Plugin.AudioViewer.Tests.csproj -c Release
powershell -File Scripts/pack-zip.ps1
```

The editor's LSP reports false `CS0103 "does not exist in the current context"`
for XAML-generated fields (`x:Name` controls) and for freshly added files.
Confirm against a real `dotnet build` before believing it.

## Versioning

The plugin version lives in **two** files that a unit test keeps in sync:

- `Directory.Build.props` — `<PluginVersion>`, the actual source feeding `$(PluginVersion)`
- `QuickLook.Plugin.AudioViewer/QuickLook.Plugin.Metadata.config` — read by QuickLook's installer

The csproj comment claims the config is the single source. It is not. Bumping
only one turns `VersionConsistencyTests` red. Verify with:

```
dotnet msbuild QuickLook.Plugin.AudioViewer/QuickLook.Plugin.AudioViewer.csproj -p:Configuration=Release -getProperty:Version
```

## Settings pattern

Every user setting follows the same four steps:

1. Enum in `Settings/SpectrogramEnums.cs` (only for >2 states). **Append, never
   reorder** — the settings combos cast `SelectedIndex` straight to the enum, so
   the XAML item order is load-bearing. Each such enum has a test pinning its
   member values.
2. Property in `Settings/PluginSettings.cs`. Primitives go through
   `SettingHelper.Get/Set`; enums are stored as their `.ToString()` name via the
   private `ParseEnum` helper, because `SettingHelper` round-trips through
   `Convert.ChangeType`. A `Color` cannot round-trip at all — store hex text
   (see `WaveColorHex`). Never store `null`; `Set` throws on it.
3. Control in `AudioViewerControl.xaml`, in the matching settings section.
4. Init under the `_initializingSettings` guard, plus a `_Changed` handler that
   early-returns on that guard and calls whatever re-render is needed.

## Builds folder

This repo does **not** use the global `builds.ps1` / `builds.json` convention.
`builds/` is gitignored, artifacts are hand-named (`KnowGood*` = user-confirmed),
and `builds/BUILDS.md` is a hand-written log. Do not run `builds.ps1 init` here —
it would scaffold a manifest and regenerate over that log.

## Local-only design docs

`docs/` is gitignored (`.gitignore:19`), so anything there is local and must not
be linked from `README.md` — the link would be dead for anyone cloning the repo.

- `docs/superpowers/specs/2026-08-01-navigation-zoom-pan-scroll-report.md` —
  how zooming, panning and scrolling work across every view type and input
  device, the gesture conflicts, and a proposed resolution table. Note that no
  real *zoom* exists on the time axis: the timeline is a fixed-scale render that
  pans, and its px/s silently degrades on long files (80 px/s up to 150 s,
  dropping toward 3.3 px/s for an hour-long file, because the width is capped at
  12000 px).

## Things that are easy to get wrong

- **STFT frame alignment.** The sample that completes the FFT window is itself
  counted, so frame *f* covers samples `[f*hop + hop-1 .. f*hop + fftSize+hop-2]`
  — it does not start at 0. `SpectrogramFrameAlignmentTests` pins both window
  edges to the sample; the other spectrogram tests only assert a peak bin within
  ±1.5 bins and would not notice a whole-frame shift.
- **The wave bitmap bakes in colour and gain.** `WaveformView` caches an ARGB
  bitmap keyed on (waveform ref, lane pixel height, gain). Anything changing the
  colour or the magnification must go through the same invalidation `SetTheme`
  does: drop the bitmap, reset every requested key, bump `_bitmapGeneration`.
- **The plain mouse wheel is fully allocated** — horizontal timeline for
  mono/stereo, vertical lanes for multichannel. Alt+wheel is amplitude zoom,
  Shift+wheel forces the time axis, and Ctrl is deliberately left free for a
  future time zoom.
- **Arrow keys belong to Explorer.** QuickLook opens its window with
  `ShowActivated="False"`. The arrows move Explorer's selection, and the host's
  keyboard hook sends a Switch message that re-reads that selection. Never call
  `Focus()` unprompted (on load, or from an event that can fire while Explorer
  is active), because it activates the QuickLook window and arrow switching stops.
  Media keys therefore only work after the user clicks the preview. The root also
  turns off directional navigation, and every main-pane control is
  `Focusable="False"`. A new control outside the settings popup needs the same
  attribute.
- **Hover "Level" is not dBFS.** It is the raw modulus of a windowed FFT bin,
  unnormalised and with no reference, so it is a relative figure — and it is not
  clamped to the contrast floor, so it can read far below it.
- **`ContextObject` outlives the preview.** The theme `PropertyChanged`
  subscription must be removed in `Unload()`, or the control and its FFT frames
  are rooted for the host's lifetime.
