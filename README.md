# Lucid Cats – Bestiary

An in-game bestiary for **Lucid Cats**. It adds a **Bestiary** button to the main menu where you can browse every monster you've encountered, with a rotating 3D model, its tier and a short description.

<img width="1280" height="720" alt="Bestiary-Trim" src="https://github.com/user-attachments/assets/14c357ed-9965-4f3a-a1e1-137561d13d4d" />

<img width="1920" height="1080" alt="20260926023843_1" src="https://github.com/user-attachments/assets/4a4da85e-c679-41f5-bd44-aac4dcd966ac" />

## Features

- New **Bestiary** button in the main menu, right below Stats, matching the game's own style, animations and sounds.
- Every monster starts locked as **???**. See one during a run and its entry unlocks.
- Interactive **3D viewer**: the model spins on its own, and you can drag it with the mouse to rotate it.
- Tier colors (yellow, orange, red), name and a short description for each monster.
- Progress is saved on your PC.
- **Visual and local only**: it doesn't change gameplay, and the other players in your lobby don't need the mod.

## Requirements

- Lucid Cats (Steam, Windows)
- [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) (tested with 5.4.23.5, x64)

## Installation

1. **Install BepInEx 5** (skip this if you already have it):
   - Download `BepInEx_win_x64_5.4.23.x.zip` from the [BepInEx releases page](https://github.com/BepInEx/BepInEx/releases). Use version 5, not 6.
   - Extract it into the game folder, next to `LucidCats.exe`.
   - Launch the game once and close it.
2. **Download the mod** from the [Releases](../../releases) page.
3. **Extract it into the game folder.** The mod should end up at:
   `BepInEx\plugins\LucidCatsBestiary\LucidCatsBestiary.dll`
4. Launch the game. The **Bestiary** button appears in the main menu.

> **Where's the game folder?** In Steam, right-click Lucid Cats → Manage → Browse local files.

## How unlocking works

A monster unlocks the first time you actually see it during a run: it has to be reasonably close and in your view. It works both when you host and when you join someone else's game.

## Configuration

After launching the game once with the mod, you can edit `BepInEx\config\lucidcats.bestiary.cfg` with any text editor:

| Setting | Default | What it does |
|---|---|---|
| `SpinSpeed` | `25` | How fast the 3D models spin on their own (degrees per second). `0` turns it off. |
| `LightIntensity` | `1.5` | Brightness of the 3D viewer's lights. |
| `UnlockAllForTesting` | `false` | Shows every monster as unlocked, without saving anything. |
| `ResetBestiaryOnStart` | `false` | Wipes your bestiary every time the game starts. |

Your progress is stored in `BepInEx\config\LucidCatsBestiary.txt`. Delete that file to start over.

## Uninstall

Delete the `BepInEx\plugins\LucidCatsBestiary` folder.

To remove BepInEx completely, also delete `winhttp.dll`, `doorstop_config.ini` and the `BepInEx` folder from the game folder.

## SOME COMMON QUESTIONS THAT CAME TO MY MIND

**Does it work in multiplayer?**
Yes. Everything happens on your own PC, so it doesn't affect anyone else, and your friends don't need to install it.

**Does the mod include any game files?**
No. It only contains code. The models and sounds are read from your own copy of the game while it runs.

**The game updated and the mod stopped working.**
Game updates can change things the mod relies on. Check the [Releases](../../releases) page for a new version, or open an issue.

**Will monsters added by updates or other mods show up?**
Not yet. The bestiary currently covers the 8 monsters in the base game.

## Building from source

1. Install BepInEx in your game folder (the project uses its files).
2. Install the [.NET SDK](https://dotnet.microsoft.com/download).
3. Open `LucidCatsBestiary.csproj` and set `<GameDir>` to your game folder.
4. Build in **Release**. The mod is copied to `BepInEx\plugins\LucidCatsBestiary` automatically.

## Changelog

### 1.0.0
- First release.

## License

[MIT](LICENSE).

This is an unofficial fan-made mod. It is not affiliated with or endorsed by the developers of Lucid Cats.
