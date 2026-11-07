# Soviet Jump Game — Patch

A client-side patch that revives **Soviet Jump Game** (Steam AppID `1072710`, by
Fantastic Passion / Game Grumps) after its live services were shut down.

The game logs in through a chain that is now dead:

```
Steam  ──►  PlayFab (LoginWithSteam)  ──►  api.fantasticpassion.games/authenticate
```

PlayFab now answers `This request is not allowed due to API usage limits for the
title.`, the custom backend domain no longer exists, and the client sits forever on the
"authentication" screen.

This patch (a **BepInEx 5 / Harmony** plugin) short-circuits that dead gate so the game
reaches the main menu, can play offline (**Practice**), can host a **local match with
bots**, and can **join servers** from the companion backend
([`soviet-jump-game-server`](../soviet-jump-game-server)).

Works on **Windows** and **Linux (Steam Proton)**.

## Status

- ✅ Past the dead authentication screen → main menu
- ✅ **Playable offline**: `PRACTICE` starts the game's own local Bolt server
- ✅ **Solo bot match**: `MATCHMAKING` (or `F8`) hosts a local server on a real map with bots
- ✅ **Server browser** + **config-file direct connect** (see below)
- ⚠️ Verified in a headless Wine test environment; your real GPU/Proton session may vary

![Main menu](docs/screenshot-menu.png)

---

## Requirements

- The retail game (bring your own, legally obtained copy).
- Nothing else — the patch ships the built `dist/SjgPatch.dll` and fetches BepInEx for you.

---

## Install — Windows

1. Put the repo somewhere convenient.
2. Open **PowerShell** in the repo folder and run (adjust the path):

   ```powershell
   .\install\install.bat "C:\Program Files (x86)\Steam\steamapps\common\Soviet Jump Game"
   ```

   (or `powershell -ExecutionPolicy Bypass -File .\install\install.ps1 -GameDir "…"`)

   This downloads **BepInEx 5 x64**, installs it, and copies `SjgPatch.dll` into
   `BepInEx\plugins\`.
3. Launch the game normally through **Steam**.

## Install — Linux (Steam / Proton)

1. Open a terminal in the repo folder and run (adjust the path):

   ```bash
   ./install/install.sh "$HOME/.steam/steam/steamapps/common/Soviet Jump Game"
   ```

   This fetches BepInEx 5 x64 and copies `SjgPatch.dll` into `BepInEx/plugins/`.
2. In Steam, set the game's **Launch Options** to the following (Proton needs to load
   BepInEx's `winhttp.dll` instead of its own):

   ```
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```
3. Launch the game normally through **Steam**.

## Uninstall

```bash
./install/uninstall.sh  "/path/to/Soviet Jump Game"          # Linux
.\install\install.bat   "C:\path\to\Soviet Jump Game" -Remove # Windows
```

Add `--with-bepinex` (Linux) / `-WithBepInEx` (Windows) to remove BepInEx too.

---

## Using it

| Input | Action |
| --- | --- |
| `PRACTICE` | Play the tutorial offline (always works) |
| `MATCHMAKING` / **F8** | Host a local match on a real map with bots |
| **F6** | Join the server in `Server.DirectConnectAddress` |
| **F9** | Log the server list to `BepInEx/LogOutput.log` |
| **`~`** | Open the in-game **Quantum Console** (server browser commands) |

### Server browser (in-game console)

Custom IMGUI does not render in this build, so the browser uses the game's own dev
console. Press **`~`**, then:

| Command | Does |
| --- | --- |
| `sjg-refresh` | Fetch the server list from the backend (`Server.ApiBaseUrl`) |
| `sjg-servers` | List servers (name, address, players) |
| `sjg-join <index>` | Join a server by its index |
| `sjg-join-address <host:port>` | Join a specific server |
| `sjg-connect` | Join `Server.DirectConnectAddress` |
| `sjg-host` | Host a local bot match |

### Config-file fallback (no console)

`BepInEx/config/games.gfi.sjg.patch.cfg` (created on first run):

```ini
[General]
OfflineMode = true          ; bypass the dead auth (keep true)
ForceDeveloper = true       ; unlock the dev console

[Server]
ApiBaseUrl = http://127.0.0.1:8080/   ; your backend; enables the server browser
DirectConnectAddress = 192.168.1.50:42010  ; fixed server (host:port)
ConnectHotkey = F6
ServerBrowserHotkey = F9
MatchmakingAction = Host    ; Host | Connect | Vanilla

[LocalMatch]
HostLocalMatches = true
MatchMap = Soviet Jump Game Map A
BotCount = 20
HostPort = 42010
HostHotkey = F8
RegisterWithServer = true   ; advertise hosted matches to the backend
```

Full reference: see the table in [#configuration](#configuration) below.

---

## How it works

The plugin (`plugin/src/`) makes the client reach the menu without the live services:

1. **Skips the dead auth flow** — Harmony-prefixes `EPB.Networking.Authentication.CheckForAuth`
   so the failing Steam→PlayFab login never runs (Steam is still brought up, which the menu needs),
   and `ForceUpdatePlayFab.AuthenticationRoutine` so the menu gate is satisfied instead of blocking.
2. **Injects a local session** — builds a synthetic `PlayFabAccount` + `TitleData`.
3. **Stubs the local player's PlayFab customization fetch** so spawning doesn't abort.
4. Forces `ClientPermission.IsDeveloper = true` and can redirect the backend URL.

**Local bot match** fixes several bugs that only surface when you "play as the server":
`PlayerObjectRegistry.ServerPlayer` (bots polluted it, so the real player never spawned),
`RoundManager.GetTargetBotPlayerCount` (returned 0 → no bots), the empty-catalog palette
crash in `PlayerMachine.Attached`, and `PlayerObject.RandomizeCustomization`'s infinite loop.

---

## Configuration

| Key | Default | Meaning |
| --- | --- | --- |
| `OfflineMode` | `true` | Short-circuit the dead auth and run a local session. |
| `ForceDeveloper` | `true` | Force `ClientPermission.IsDeveloper = true`. |
| `ApiBaseUrl` | *(empty)* | Base URL of a self-hosted server. Enables the server browser + matchmaking. |
| `DirectConnectAddress` | *(empty)* | Fixed server to join, as `host:port`. |
| `ConnectHotkey` | `F6` | Join `DirectConnectAddress` (empty to disable). |
| `ServerBrowserHotkey` | `F9` | Log the server list to the log file (empty to disable). |
| `MatchmakingAction` | `Host` | What MATCHMAKING does: `Host` (local bot match), `Connect` (join `DirectConnectAddress`), `Vanilla`. |
| `HostLocalMatches` | `true` | MATCHMAKING hosts a local bot match instead of the dead services. |
| `MatchMap` | `Soviet Jump Game Map A` | Map to host locally (Map A / Map B / Map Moon / Tutorial). |
| `BotCount` | `20` | Bots spawned by a locally-hosted match. |
| `HostPort` | `42010` | UDP port a locally-hosted match listens on. |
| `HostHotkey` | `F8` | Host a local bot match from anywhere (empty to disable). |
| `RegisterWithServer` | `true` | Advertise a hosted match to the backend so it appears in server browsers. |

---

## Building from source (optional)

The prebuilt `dist/SjgPatch.dll` works on Windows and Linux, so you only need this if you
change the code. The plugin is plain C# compiled with Mono's `mcs`:

```bash
# Linux / macOS
./plugin/build.sh "/path/to/Soviet Jump Game"     # -> dist/SjgPatch.dll
```

```powershell
# Windows (requires Mono on PATH)
powershell -ExecutionPolicy Bypass -File .\plugin\build.ps1 -GameDir "C:\path\to\Soviet Jump Game"
```

---

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| Nothing changes; no `BepInEx/LogOutput.log` | BepInEx isn't loading. On Linux/Proton add `WINEDLLOVERRIDES="winhttp=n,b" %command%` to Launch Options. |
| Still stuck on the auth screen | Check `OfflineMode = true` and that BepInEx loaded (see `BepInEx/LogOutput.log`). |
| `sjg-refresh` says "no Server.ApiBaseUrl configured" | Set `Server.ApiBaseUrl` to your backend. |
| Console doesn't open | The toggle key is configurable in the game; try `~`, `F1`, or `F12`. |

---

## Layout

```
plugin/src/      C# plugin sources
plugin/build.sh  builds dist/SjgPatch.dll with mcs (Linux/macOS)
plugin/build.ps1 builds it on Windows (Mono required)
install/         install / uninstall / fetch-bepinex  (.sh and .ps1/.bat)
dist/            prebuilt SjgPatch.dll
docs/            screenshots
```

## Legal / disclaimer

Reverse-engineered for interoperability and preservation, with the developer's
permission, using a legally purchased copy. No copyrighted game assets are included —
you must own the game.

## License

MIT — see [LICENSE](LICENSE).
