# Soviet Jump Game — Patch

A client-side patch that revives **Soviet Jump Game** (Steam AppID `1072710`, by
Fantastic Passion / Game Grumps) after its live services were shut down.

The game's login chain (`Steam → PlayFab → api.fantasticpassion.games`) is dead, so the
client hangs forever on the "authentication" screen. This patch — a **BepInEx 5 /
Harmony** plugin — short-circuits that gate so the game reaches the main menu, plays
offline (**Practice**), hosts a **local match with bots**, and **joins servers** from the
companion backend [`soviet-jump-game-server`](https://github.com/gfisher1/soviet-jump-game-server).

Works on **Windows** and **Linux (Steam Proton)**.

![Main menu](docs/screenshot-menu.png)

## Install

You need your own copy of the game. BepInEx is bundled, so there is nothing else to
download or install.

1. **Download** `sjg-patch-*.zip` from the [releases page](../../releases) and unzip it
   anywhere.
2. **Run the installer** from the folder you unzipped:
   - **Windows** — double-click **`Install.bat`**.
   - **Linux / Steam Proton** — run **`./install.sh`** in a terminal.
3. **Choose a play mode.** The installer asks one question:

   ```
   Play online (public server) or offline (bots only)? [online/offline] [online]:
   ```

   - **`online`** *(the default — just press **Enter**)* — **MATCHMAKING** joins the public
     server at `sjg.dnd.wtf`. If it cannot be reached, you get a local bot match instead.
   - **`offline`** *(type `offline`)* — **MATCHMAKING** hosts a match on your own PC with
     20 bots. No account, no server, no internet needed.

   In online mode it then asks for the **server address** — press **Enter** to keep the
   default, `sjg.dnd.wtf`, or type your own server.
4. **Linux / Steam Proton only:** set the game's Steam Launch Options to
   `WINEDLLOVERRIDES="winhttp=n,b" %command%`
   (Steam → right-click the game → **Properties** → **Launch Options**).
5. **Launch the game through Steam** and click **MATCHMAKING**.

**Re-run the installer at any time** to switch between online and offline, or to change
servers — it updates the config and keeps your other settings.

**Already have BepInEx?** Skip the installer: drop **`SjgPatch.dll`** into
`<game>/BepInEx/plugins/`, then edit
`<game>/BepInEx/config/games.gfi.sjg.patch.cfg` to match the mode you want — see
[Configuration](https://github.com/gfisher1/soviet-jump-game-patch/wiki/Configuration).

## Controls

| Input | Action |
| --- | --- |
| `PRACTICE` | Play the tutorial offline (always works) |
| `MATCHMAKING` | **Just click it** — joins the default server, or hosts a local bot match if none is reachable |
| **F6** | Join the server set in `Server.DirectConnectAddress` |

> **Note:** the game's own **Rooms** (private match / room code) and **server/region
> selection** menu entries do **not** work — they still call the shut-down live services.
> Use `PRACTICE`, `MATCHMAKING` (`F8`) or `F6` instead.

## Documentation

Full documentation lives in the **[wiki](https://github.com/gfisher1/soviet-jump-game-patch/wiki)**:

| Page | Contents |
| --- | --- |
| [Installation](https://github.com/gfisher1/soviet-jump-game-patch/wiki/Installation) | Release bundle, git checkout, BepInEx note, uninstall |
| [Configuration](https://github.com/gfisher1/soviet-jump-game-patch/wiki/Configuration) | Every config key, with an example file |
| [How It Works](https://github.com/gfisher1/soviet-jump-game-patch/wiki/How-It-Works) | The Harmony patches, in detail |
| [Building](https://github.com/gfisher1/soviet-jump-game-patch/wiki/Building) | Rebuild `SjgPatch.dll` from source |
| [Troubleshooting](https://github.com/gfisher1/soviet-jump-game-patch/wiki/Troubleshooting) | Common problems and fixes |

## AI notice

This project — the plugin, the installers, the backend and the release tooling — was
built by **opencode**, an AI coding agent, using **DeepSeek V4.1** as the model. Every
change was authored, built, deployed and tested by that agent.

## Legal / disclaimer

Reverse-engineered for interoperability and preservation, with the developer's
permission, using a legally purchased copy. No copyrighted game assets are included —
you must own the game.

## Third-party components

The install bundle redistributes **BepInEx**, **UnityDoorstop**, **HarmonyX**,
**MonoMod** and **Mono.Cecil** unmodified, so the installer needs no internet
connection. BepInEx and UnityDoorstop are LGPL-2.1; the others are MIT. They are
loaded at runtime and can be replaced with your own builds. Full license texts and
source links are in [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md). This
project's own code is MIT and does not statically link against them.

## License

MIT — see [LICENSE](LICENSE).
