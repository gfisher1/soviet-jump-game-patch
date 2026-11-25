# Third-party notices

This project (`soviet-jump-game-patch`) is licensed under the MIT License — see
[`LICENSE`](LICENSE). To make the installer one-click it also **redistributes,
unmodified**, the third-party components listed below. Those components stay under
their own licenses; the notices here apply to them, not to our own code.

Our plugin (`SjgPatch.dll`) is a separate assembly that is loaded at runtime by
BepInEx through its public interface. It contains no BepInEx code and is not
statically linked against it, so it is a "work that uses the library" rather than
a derivative work, and it remains under our MIT license.

## Bundled components

| Component | Files in the bundle | License | Upstream |
|---|---|---|---|
| BepInEx 5.4.23.6 | `BepInEx/core/BepInEx*.dll` | LGPL-2.1 | <https://github.com/BepInEx/BepInEx> |
| UnityDoorstop | `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` | LGPL-2.1 | <https://github.com/NeighTools/UnityDoorstop> |
| HarmonyX | `BepInEx/core/0Harmony*.dll`, `BepInEx/core/HarmonyXInterop.dll` | MIT | <https://github.com/BepInEx/HarmonyX> |
| MonoMod | `BepInEx/core/MonoMod.RuntimeDetour.dll`, `BepInEx/core/MonoMod.Utils.dll` | MIT | <https://github.com/MonoMod/MonoMod> |
| Mono.Cecil | `BepInEx/core/Mono.Cecil*.dll` | MIT | <https://github.com/jbevain/cecil> |

All of them are copied **verbatim** from the official BepInEx 5.4.23.6 x64 release
(<https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.6>). Nothing has been
modified, renamed or recompiled.

## LGPL-2.1 components (BepInEx, UnityDoorstop)

BepInEx and UnityDoorstop are licensed under the GNU Lesser General Public
License, version 2.1. A copy of that license is included in
[`licenses/LGPL-2.1.txt`](licenses/LGPL-2.1.txt) and in the release bundle.

They are loaded at runtime (dynamic linking), so you can replace them with your own
builds at any time: the installer only copies files into the game folder, and the
game loads whatever is there. No modifications were made, so the corresponding
source is the exact upstream release:

- BepInEx 5.4.23.6 — <https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.6>
- UnityDoorstop, as shipped inside that BepInEx release —
  <https://github.com/NeighTools/UnityDoorstop>

## MIT components (HarmonyX, MonoMod, Mono.Cecil)

These are distributed under the MIT License:

- HarmonyX — Copyright (c) 2017 Andreas Pardeike; HarmonyX modifications
  Copyright (c) BepInEx Team
- MonoMod — Copyright (c) 2019-2024 MonoMod Team
- Mono.Cecil — Copyright (c) 2008-2015 Jb Evain

```
MIT License

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

## Game code

No game code or game assets are included in this project or in the release bundle.
You must own a copy of Soviet Jump Game.
