# Third-Party Notices

QuickLook.Plugin.AudioViewer redistributes the following third-party components
inside its `.qlplugin` package. Each is used under its own license, reproduced
below. This file is packaged alongside the binaries to satisfy those licenses.

(The QuickLook host application and its `QuickLook.Common` API — both open source —
are **not** redistributed here: `QuickLook.Common.dll` is supplied by the host at
runtime. See the project README for their licenses.)

---

## NAudio

- Homepage: https://github.com/naudio/NAudio
- Redistributed files: `NAudio.dll`, `NAudio.Core.dll`, `NAudio.Wasapi.dll`,
  `NAudio.WinMM.dll`, `NAudio.Asio.dll`, `NAudio.Midi.dll`, `NAudio.WinForms.dll`
- License: MIT
- Copyright (c) Mark Heath & Contributors

## NAudio.Vorbis

- Homepage: https://github.com/naudio/Vorbis
- Redistributed files: `NAudio.Vorbis.dll`
- License: MIT
- Copyright (c) Mark Heath & Contributors

## NVorbis

- Homepage: https://github.com/NVorbis/NVorbis
- Redistributed files: `NVorbis.dll`
- License: MIT
- Copyright (c) Andrew Ward & Contributors

## .NET runtime libraries

- Homepage: https://github.com/dotnet/runtime
- Redistributed files: `System.Buffers.dll`, `System.Memory.dll`,
  `System.Numerics.Vectors.dll`, `System.Runtime.CompilerServices.Unsafe.dll`,
  `System.ValueTuple.dll`, `System.Security.AccessControl.dll`,
  `System.Security.Principal.Windows.dll`, `Microsoft.Win32.Registry.dll`
- License: MIT
- Copyright (c) .NET Foundation and Contributors

---

## MIT License

NAudio, NAudio.Vorbis, NVorbis, and the .NET runtime libraries above are licensed
under the MIT License. Its terms (reproduced once here) apply to each, under the
copyright notice shown for that component:

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## TagLibSharp — GNU Lesser General Public License, version 2.1 (LGPL-2.1)

- Homepage / source: https://github.com/mono/taglib-sharp
- Redistributed files: `TagLibSharp.dll`
- Copyright (c) Brian Nickel, Gabriel Burt, and the taglib-sharp contributors

TagLibSharp is redistributed **unmodified, as a separate dynamically-linked
assembly**, under the GNU Lesser General Public License, version 2.1. Under the
LGPL you are free to modify or replace this library with a compatible version.
The complete LGPL-2.1 license text is available at
<https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html>, and the corresponding
source code at the homepage above.
