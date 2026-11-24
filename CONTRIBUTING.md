# Contributing

Thanks for taking a look! These mods started as fixes for things that bugged me in my own late-game save, and they can definitely be better. Forks, pull requests, bug reports and ideas are all welcome.

## Ways to help

- **Test in game.** Several mods haven't been tried in game yet (see the table in the README). A quick report like "works", "does nothing" or "breaks X" is really useful. Multiplayer reports are especially welcome.
- **Report bugs.** Open an issue with what you did, what happened, and the relevant lines from `Sons Of The Forest/_RedLoader/Latest.log`. Every mod logs what it's doing there.
- **Improve a mod or add a new one.** Open a pull request. Small, focused changes are easiest to review, but bigger ones are fine too.
- **Fork it.** If you want to take a mod in your own direction, go for it. You don't need to ask. The MIT license lets you change, redistribute and publish your version (for example on Thunderstore or Nexus). A link back is appreciated but not required.

## Working on the code

- Each mod is a separate project with its own `.sln`. See **Building** in the README.
- Match the style of the surrounding code: small classes and short comments that explain *why*.
- Expose new behaviour as a config entry in that mod's `Config.cs`, so players can turn it off.
- Bump the `version` in the mod's `manifest.json` when you change behaviour.
- Say in your PR what you tested in game and what you didn't. Untested is fine, just say so.

## Ideas that are up for grabs

- Kelvin routing around player structures properly (adding navmesh cuts to pieces that don't have them) instead of being teleported past them
- Virginia following you into caves
- Multiplayer testing and fixes for all the mods
- Artifact attract/repel and spotlights that burn enemies

Have fun, and thanks!
