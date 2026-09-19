# Settings And User Data

Two different persistence layers live side by side. Do not confuse them.

| Layer | File | Holds | Written by |
| --- | --- | --- | --- |
| Game settings | `mods\TheScrollOfHomelander\Config.lua` | every user-facing toggle and slider, its default, group, and text | the developer, then the game's settings UI |
| Mod memory | `%USERPROFILE%\Documents\TheScrollOfHomelander\*.json` | structural memory: last filter, last tool, thresholds, per-building choices | the Mod at runtime |

## Adding A Setting

1. `Config.lua` → append to `DefaultSettings` with the next free index:

   ```lua
   [95] = {
       SettingType = "Toggle",            -- or "Slider"
       Key = "my_new_feature",
       DisplayName = "我的新功能",
       Description = "一句话说明它在做什么，以及关闭后会回到原版行为。",
       GroupName = "操作优化",             -- reuse an existing SettingGroups value
       DefaultValue = true,
       -- Slider only: MinValue, MaxValue, StepSize
   },
   ```

2. `Scripts\Frontend\Plugin.cs` → add `internal static bool EnableMyNewFeature = true;` (or `int` for a slider) and one load line in `LoadSettings`:

   ```csharp
   LoadSettingDefault(ModIdStr, "my_new_feature", ref EnableMyNewFeature, true);
   LoadIntSetting(modIdStr, "my_container_line_count", ref MyContainerLineCount);
   MyContainerLineCount = Mathf.Clamp(MyContainerLineCount, 3, 7);
   ```

   `LoadSetting` reads the saved value, then falls back to the `Config.lua` entry's current value; `LoadSettingDefault` / `LoadIntSetting` also honour the declared default. Always clamp slider values straight after reading.

3. Read the field in the feature (`Plugin.EnableMyNewFeature`), never call `ModManager.GetSetting` from feature code.

4. `Settings.Lua` is the companion defaults file; keep it consistent when a default changes.

5. React to live changes in `OnModSettingUpdate`: reload settings, then refresh or restore whatever is already on screen (`…RefreshAllActive(allowRestore: true)`, `…RestoreAll()`, `…ApplyOrRestore()`), and reset controllers for features that were just disabled.

## Group Switches

Several settings are gates for a whole family of features. Breaking one of these silently disables unrelated behaviour:

- `enable_advance_month_optimization` gates every month-turn diagnostic, save, and secret-information optimization.
- `enable_game_performance_optimization` gates the in-game caches (map movement, filter counts, warehouse refresh, exchange re-render) and must stay independent of the month-turn switch.
- `enable_auto_harvest_after_advance_month` gates the harvest children and the auto-recruit chain; disabling it resets those controllers.

## Runtime JSON Stores

Directory: `%USERPROFILE%\Documents\TheScrollOfHomelander\` (game-written data; never put source or build output there). `ModUserDataPaths.GetFilePathCandidates` also looks in the legacy `<modRoot>\UserData\` directory for backward compatibility.

| File | Owner |
| --- | --- |
| `MemoryOptimizationSettings.json` | `MemoryOptimizationSettingsStore` — filter/sort/strategy/subtype/perfect-selection memory |
| `PurchaseOptimizationSettings.json` | `PurchaseOptimizationSettingsStore` (file name comes from `ModProtocol.PurchaseSettings`) |
| `AutoRepairSettings.json` | `AutoRepairSettingsStore` (tool priority, bare-hand rules) |
| `WorldStateIconVisibilitySettings.json` | `WorldStateIconVisibilitySettingsStore` |
| `BatchItemFilterSettings.json` | `BatchItemFilterPatches` settings store |
| `AutoChickenCareSettings.json`, `AutoCricketRoomSettings.json` | frontend store, read by the backend handlers in `AutoCultivationPatches` |
| `ContinuousMakeSettings_<lifeSkillType>.json` | per-crafting-type continuous-make settings |
| `MakeStorageLocationSettings_<key>.json`, `BuildingOutputStorageSettings_<key>.json` | per-target storage-location memory |
| `ContinuousMakeSettings.json` | legacy single-file form, still read as a fallback |

## Store Rules

- Every store has `Load()` (called from `Plugin.Initialize`) and `Save()`; saves go through `AsyncSettingsSaveQueue.Enqueue(path, snapshot)` — never write the file directly.
- Clone the object before enqueueing (`Current.Clone()`), normalize before both load and save, and clamp/validate every numeric field.
- New JSON fields must be optional. The established pattern checks the raw text for the key and applies a default when it is absent:

  ```csharp
  var json = File.ReadAllText(path);
  Current = JsonUtility.FromJson<PurchaseOptimizationSettings>(json) ?? new PurchaseOptimizationSettings();
  if (!json.Contains("\"AutoPurchaseSkipInventoryOutsideIndustry\""))
      Current.AutoPurchaseSkipInventoryOutsideIndustry = true;
  ```

  This keeps old files loading without a migration step and without losing the user's other choices.
- Load failures are non-fatal: log `[BetterTaiwuScroll] Failed to load …` and fall back to a fresh default instance. A save failure keeps the previous file and the in-memory snapshot.
- Per-key stores encode identity in the file name (`BuildingOutputStorageSettings_<key>.json`); keep the key format stable or old files orphan silently.
- Never write a JSON store from a hot path, and never save just because a view initialized — distinguish UI replay from a real user action, or "All" will overwrite a remembered filter.
