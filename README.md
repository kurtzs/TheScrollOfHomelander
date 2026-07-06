<p align="center">
  <img src="icon.png" alt="Better Taiwu Scroll icon" width="500">
</p>

# Better Taiwu Scroll (太祖绘卷)

A quality-of-life mod for **The Scroll of Taiwu** (太吾绘卷, Remake) that makes the
day-to-day interface far less painful: compact UI, batch crafting, bulk shop purchasing,
advanced item filtering with text search, smarter shortcuts, plus a handful of combat, map,
and month-advance improvements.

Every feature can be toggled independently in the mod's settings, so if you don't want a
particular change — or you hit a bug — just switch it off and the rest keeps working.

- **Author:** TenMountainMoon & ElysianSunaker
- **Game version:** 1.0.44.0
- **Steam Workshop ID:** 3749026190
- **Languages:** English and Chinese (see [Languages](#languages))

---

## Features

Settings are organized into groups (the same groups you'll see in the in-game config screen).

### Item & Filter UI
- **Compact sort buttons** — packs name / tier / quantity / weight / durability / price sort
  buttons into a single tight row.
- **Inline first-level filters** — surfaces the first filter level above the item list.
  **Click "All" again to open the detailed filter window.**
- **Search box** — filter the current list by name keyword in inventory and dual-panel screens.
- **Grade color tuning** — clearer Tier 1–9 colors.
- Optional filter-icon simplification and a full-width filter row.

### Containers & Exchange
- **Compact containers** — shrink inventory / exchange / skill-select card lists so your screen
  shows more than ten items. Column counts are configurable.
- **Default card view** for item screens that support list/card toggling.
- **Left/right sync** — keep item-category filters in sync across dual-panel screens.
- Consistent left-side item text coloring in shop / gift / warehouse / storehouse / book screens.

### Crafting
- **Continuous & batch crafting** — set limits once (reagent tier range, tool priority, bare-hand
  and durability rules), then batch-produce food, medicine, poison, smithing, carpentry, weaving,
  and jewelcrafting.
- **Auto best tool** and **auto max quantity**.
- **Auto-pick reagent** after each craft for hands-free continuous production.
- Adds a combined **Food** target for cooking.

### Purchasing
- **Bulk purchase** — buy reagents in bulk from merchants, with configurable tier range and
  rules for price-increased / locked-shop items.
- **Cargo store** shown on the merchant screen for buying into / selling from cargo.

### Map
- Raise tile mini-icons out of the map seams; brighten and rescale merchant / special icons on
  both the map and the character list.

### Controls
- **Fast transfer** — `Shift`+left-click to transfer / sell / buy the max amount (invertible).
- `Ctrl`+left-click to move half.
- `Space` to complete a trade or start a building.

### Memory
- Remember your last filter, sort, strategy preset, sparring mode, and crafting choices.

### Combat
- Show skill / weapon tooltips on hover without holding `Shift`.

### Month Advance (performance)
- A set of opt-in optimizations and diagnostics for the monthly world update (save copy buffers,
  secret-information caching/indexing, and action-planning caches). These only cache repeated work;
  they don't change game outcomes.

---

## Languages

The mod runs in the player's own language automatically — no separate build per language.

- **In-game UI** (crafting/purchase panels, buttons, tooltips, and the text the mod matches
  against) auto-detects the client language via the game's language setting and shows English
  on an English client, Chinese otherwise.
- **Settings screen** (name / description / group of each option) is shown **bilingually**
  (`中文 / English`), because the game's mod manager renders `Config.lua` text directly and has no
  per-language field for mod settings.

All translatable strings for the compiled UI live in one place:
`Scripts/Frontend/Shared/ModLocalizationCatalog.cs`.

---

## Installation

**From Steam Workshop:** subscribe to the mod (ID `3749026190`) and enable it in the in-game
mod manager.

**Manual / from source:** build (see below) and place the assembled folder at:

```
<Game>\Mod\BetterTaiwuScroll\
```

---

## Building from source

The mod is split into a Unity **frontend** plugin and a separate .NET **backend** plugin, each
compiled against a different set of game assemblies.

### Option A — one command (no Taiwu Studio required)

`deploy.ps1` compiles both halves with the Roslyn compiler directly against the installed game's
assemblies, then assembles the mod folder and copies it into the game's local `Mod` directory.

```powershell
.\deploy.ps1                 # build both plugins + deploy to the game's Mod folder
.\deploy.ps1 -SkipDeploy     # build only, into .\build
.\deploy.ps1 -GameDir "D:\Path\To\The Scroll Of Taiwu"   # custom install path
```

Requirements: the .NET SDK (provides the Roslyn compiler) and a local install of the game.

### Option B — Taiwu Studio

Open the project in Taiwu Studio and build; it uses the `roslyn-worker` toolchain configured in
`.taiwu-studio/project.toml` and outputs to `Plugins/Front` and `Plugins/Back`.

---

## Project structure

```
Config.lua                     Mod metadata + settings shown in the mod manager (bilingual)
Settings.Lua                   Current setting values
icon.png                       Cover image
deploy.ps1                     Build + deploy script (Roslyn, no Taiwu Studio needed)
.taiwu-studio/project.toml     Taiwu Studio project config

Scripts/
  Frontend/                    Unity-side plugin (Harmony patches, UI)
    Plugin.cs                  Entry point; loads settings, detects language
    Shared/
      ModLocalization.cs         Language detection + T() lookup
      ModLocalizationCatalog.cs  Single translation catalog (English-keyed)
    Features/                  One file per feature area
  Backend/                     Server-process plugin (month-advance optimizations)

UserData/                      Runtime settings files + grade-background assets
Plugins/                       Build output (Front/ and Back/, git-ignored)
```

---

## Notes

- Adding or fixing English wording for the in-game UI: edit
  `Scripts/Frontend/Shared/ModLocalizationCatalog.cs`.
- Adding or fixing settings-screen wording: edit `Config.lua` directly (it's the source the mod
  manager reads).
- If you report a bug, please include the red error text, which mods were enabled, and the steps
  that triggered it so it can be reproduced.
