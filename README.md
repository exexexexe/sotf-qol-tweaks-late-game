# Sons of the Forest – QoL & late-game tweaks

Five small [RedLoader](https://github.com/ToniMacaroni/RedLoader) mods for Sons of the Forest. They're aimed at a late-game save, where you've built a base, have lots of traps, have Kelvin working for you and are cleaning up the last collectibles. Each mod is separate, so you can install only the ones you want.

**Forks and pull requests are very welcome.** If something's broken, could be smarter, or you have a better idea, go for it. See [CONTRIBUTING.md](CONTRIBUTING.md).

| Mod | What it does | Status |
|---|---|---|
| [TrapResetSwitch](#trapresetswitch) | One light switch resets all your triggered traps | Works in single-player; multiplayer not tested |
| [BlueprintShiftFill](#blueprintshiftfill) | Shift+E fills a blueprint; shift-click moves whole stacks | Not tested in game yet |
| [ArtifactPads](#artifactpads) | Up to 9 saved teleport spots for the artifact | Not tested in game yet |
| [KelvinHelper](#kelvinhelper) | Kelvin gets unstuck, avoids fire, keeps working and stays at base | Partly tested (multiplayer host) |
| [CollectiblesTracker](#collectiblestracker) | Live 100% tracker with GPS markers and a big map | GPS markers confirmed working |

All settings appear in the in-game mod settings menu and in `UserData/<ModName>.cfg`.

---

## TrapResetSwitch

Flip an electrical light switch and every triggered trap nearby resets, the same as screwing each one back by hand. It's useful when your base is surrounded by traps and a raid sets them all off.

- **Radius mode** (default): resets every trap within `radius` metres of the switch (50 m).
- **Wired mode** (`wired_mode`): only resets traps that a wire connected to the switch runs past (within `wire_reach`, 4 m).
- `require_power`: the switch's circuit needs a battery or solar panel.
- Grind and spin traps are skipped while they're still running.

## BlueprintShiftFill

- **Shift+E on a blueprint** adds every material it still needs from your inventory in one go, instead of one item per click (`reach`, 6 m).
- **Shift-click in the grab bag** (shelves, giving items to Kelvin and Virginia, the drying rack) moves the whole stack (`max_per_shift_click`, `repeat_delay`).
- **Shift with stick, stone and log holders** puts in or takes out as many as fit.

## ArtifactPads

Holding the artifact in **teleport mode**:

| Key | Action |
|---|---|
| `1`–`9` (number row or numpad) | Teleport to pad *N* |
| `Ctrl` + `1`–`9` | Save your current position as pad *N* |
| `Alt` + `1`–`9` | Delete pad *N* |
| `0` | List your pads |

The game's own artifact portal still works as your "home". By default, a pad teleport costs the same solafite as the normal teleport (`use_fuel`). Teleporting into or out of caves is blocked. Pads are saved per save game in `Mods/ArtifactPads/`.

## KelvinHelper

Makes Kelvin less dumb. His AI runs on the host, so in multiplayer only the host needs this mod.

- **Unstuck** (`unstuck`, `stuck_seconds`): if he walks into something for 4 s without getting anywhere, he's moved past it along his own path. If he gets stuck in the same place again, it moves him further. It logs what he was stuck on (lines starting with `[stuck]` in the RedLoader log).
- **Walk through traps** (`through_traps`): traps stop blocking his paths, so he doesn't walk around (or get stuck at) your trap fields. Other AI can also path through them.
- **Fire escape** (`fire_escape`): when he catches fire, he's put back where he was a moment earlier.
- **Resume tasks** (`resume_tasks`, `resume_delay`): when he drops a gather, clear-area or maintain-base task by himself, he's given it again after 15 s. It gives up if he keeps dropping it straight away.
- **Stay at base** (`stay_at_base`): with nothing to do, he stays where he is instead of following you across the map. Telling him to follow on the notepad still works.

## CollectiblesTracker

A live version of the [save checker web page](https://github.com/exexexexe/sotf-collectibles-checker). It tracks note pages, blueprints, plated weapons, clothing, action-camera videos, edibles, crafted weapons and 3D-printed items.

- **F7** shows a panel with your count per achievement and exactly what's missing, with distance and direction. Page Up/Down or Left/Right switch lists, the arrow keys move the cursor and Enter hides or shows an item.
- **F8** turns the GPS markers for missing items on or off.
- **F9** opens a big map with your markers. `+` and `-` zoom in around you.
- A message shows your new count whenever something counts.

See [CollectiblesTracker/README.md](CollectiblesTracker/README.md) for details. `Data.cs` is generated from the web checker with `gen-data.js`.

---

## Installing

1. Install RedLoader (for example with Vortex or by hand).
2. From each mod you want, copy `<Mod>.dll` into `Sons Of The Forest/Mods/`, and its `manifest.json` into `Mods/<Mod>/manifest.json`.

## Building

You need the .NET 8 SDK, and RedLoader deployed in the game folder.

```sh
dotnet build KelvinHelper/KelvinHelper.sln -c Release
```

- Build each mod through its `.sln` (the zip step needs the solution dir).
- A build copies the dll and manifest into the game's `Mods` folder. Add `-p:DisableCopyToGame=True` while the game is running, because the dll is locked then.
- A Release build also puts a zip in `<Mod>/ReleaseBuild/`.
- The game path defaults to the normal Steam install (see `Directory.Build.props`). Override it with `-p:GameDir=...`.
- If RedLoader isn't deployed (for example when Vortex has undeployed it), point `-p:RedLoaderLibDir=...` at a copy of RedLoader's `net6` folder.

## Contributing & license

Bug reports, in-game test results, fixes and new ideas are all welcome. Open an issue or a pull request, or fork the repo and make it your own. See [CONTRIBUTING.md](CONTRIBUTING.md) for how to get started and a list of ideas that are up for grabs.

Released under the [MIT License](LICENSE): use, change and share these mods however you like.
