# Update-Compatible Debugging

Use this reference when a Mod worked before a game update, when a Harmony target is undefined, or when the UI appears correct but behavior is wrong.

## Evidence First

1. Capture the newest `Player.log` section and the exact game build/version.
2. Record whether the error is frontend, backend, or a frontend/backend bridge failure.
3. Resolve the current target from the installed DLL, not from an old dump. Compare the target signature, private fields, callbacks, and call order with the previous version.
4. Reproduce with the feature disabled when possible. A disabled Mod must leave vanilla behavior intact.

## Current Decompiler Worker

```powershell
$worker = "tools\TaiwuStudio.DecompilerWorker\bin\Release\net8.0\TaiwuStudio.DecompilerWorker.exe"
$managed = "C:\Program Files (x86)\Steam\steamapps\common\The Scroll Of Taiwu\The Scroll of Taiwu_Data\Managed"

& $worker --command find-type --managed-dir $managed --assembly Assembly-CSharp --query "SortAndFilter" --limit 50
& $worker --command find-member --managed-dir $managed --assembly Assembly-CSharp --query "RefreshList" --limit 50
& $worker --command decompile-member --managed-dir $managed --assembly Assembly-CSharp --type "Game.Components.SortAndFilter.SortAndFilter" --member "ApplyFilterLineStates"
```

Inspect only the relevant source span; never copy an entire decompiled type into the Mod. The `taiwu-decompiled-api` skill wraps these commands and slices the span for you.

## Common Failure Patterns

- **Undefined target method:** re-check namespace, overload parameters, generic arity, and whether the method moved sides. Use a `TargetMethod()` resolver only when overloads genuinely vary, and return `null` safely when the feature is disabled or unavailable.
- **Invalid cast/private field:** inspect the current field/property type and nullability. Prefer public state or an exact field helper; isolate reflection in a guarded compatibility adapter rather than casting blindly in a hot path.
- **Visible control but wrong behavior:** trace the data controller after the UI callback. Many updated sort/filter views contain synthetic or private selections that `GetStateFromUI()` omits. Include the effective state in signatures and restore it after vanilla `Refresh`/`Restore...` callbacks finish.
- **Selection resets after moving an item:** capture the ordering of `SetItemList`, controller save/restore, owner callbacks, and final list filtering. A restore scheduled before a later vanilla reset can be de-duplicated incorrectly; schedule once after the destructive callback, including in `finally` when the callback may throw.
- **Coroutine on inactive object:** check `activeInHierarchy`, `isDestroyed`, and panel lifecycle before starting. Cancel or drop pending work on `OnDisable`/`OnDestroy`; never revive a closed view just to finish a search.
- **Duplicate or missing UI:** patch `Awake`/`OnEnable`/rebuild paths idempotently, cache per-instance references, and verify parent/anchor layout after the vanilla hierarchy exists.
- **Backend red error:** stop before mutating state, log a compact code and target identity, and return a structured failure. Do not fall back to an unsafe item/tool/resource or continue consuming data after validation fails.

## Runtime Trace Discipline

Log lifecycle transitions, target resolution failures, request IDs, and one summary per action. Avoid logging every successful list render or frame. When debugging a state mismatch, temporarily log both the UI state and the controller/effective state, then remove or gate verbose diagnostics before release.

## Regression Matrix

Test cold open, repeated refresh, moving/creating/removing an item, panel close/reopen, scene change, empty data, disabled setting, save/load, and a second supported view. For split features, test backend unavailable, stale request, invalid ID, and out-of-order response. Verify both the UI indicator and the actual filtered/action data.
