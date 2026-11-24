# CollectiblesTracker

A RedLoader mod for Sons of the Forest that tracks the collection achievements live, while you play.

- **F7** opens a panel with your count for each achievement. It also lists exactly which items you're still missing, where they are, and how far away they are. **Page Up / Page Down** switch between achievements.
- **F8** shows or hides markers on your GPS for notes, blueprints, weapons to plate, clothing and action cameras you're still missing. A marker disappears once you have that item.
- An on-screen message shows your new count when something counts, for example "Note pages: 37/50 · Old note A".

It tracks note pages, blueprints, Solafite-plated weapons, clothing, action camera videos, every edible, crafted weapons and 3D-printed items. It reads the same progress flags the game writes to your save, so the numbers match the [web checker](https://exexexexe.github.io/sotf-collectibles-checker/).

## Settings

You can change these in the in-game mod settings, or in `UserData/CollectiblesTracker.cfg`:

- The keys for the panel and the markers
- Markers on or off
- Keep markers for items you've already found
- Only show markers within a set range
- Marker size and icon

## Building

This mod builds against RedLoader 0.8.6. Put a copy of RedLoader's `net6` libraries in `sdk-ref/`, then run `dotnet build -c Release`. The build copies the mod into the game's `Mods` folder and makes a zip in `ReleaseBuild/`.

`Data.cs` is generated from the web checker's lists. Run `node gen-data.js <path to index.html>` to regenerate it.
