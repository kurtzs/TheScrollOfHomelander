# Harmony And Communication

Use this reference before writing patches, frontend/backend calls, custom method ids, or file bridges.

## Harmony Rules

Patch exact targets only: side, assembly, type, method, and overload.

Patch choices:

- Postfix: observe or adjust results after original logic. Prefer for rendering, generated values, and additive behavior.
- Prefix returning `true`: pre-validate or adjust arguments while keeping original logic.
- Prefix returning `false`: only when fully replacing original behavior and setting all outputs.
- Transpiler: use only when Prefix/Postfix cannot express the change.
- Finalizer: use for exception handling only when the Mod must absorb or transform exceptions.

Constructors:

```csharp
[HarmonyPatch(typeof(TargetType), MethodType.Constructor, new[] { typeof(int) })]
```

Static constructors:

```csharp
[HarmonyPatch(typeof(TargetType), MethodType.StaticConstructor)]
```

Always unpatch in `Dispose`:

```csharp
_harmony?.UnpatchSelf();
_harmony = null;
```

## Backend Method Bridge

Prefer the modern backend bridge for frontend-triggered game actions:

```csharp
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Mod;
using TaiwuModdingLib.Core.Plugin;

public override void Initialize()
{
    DomainManager.Mod.AddModMethod(ModIdStr, "DoThing", DoThing);
}

private static SerializableModData DoThing(DataContext context, SerializableModData parameter)
{
    var result = new SerializableModData();
    result.Set("success", true);
    return result;
}
```

Backend validation checklist:

- Ensure `context` and `parameter` are usable.
- Require all needed keys.
- Re-check current Taiwu id, target ids, item ids, quantities, relations, ownership, and action preconditions.
- Prefer existing domain/action APIs for applying state changes.
- Return structured failure payloads for recoverable errors.

```csharp
private static SerializableModData Fail(string code, string message)
{
    var result = new SerializableModData();
    result.Set("success", false);
    result.Set("code", code);
    result.Set("message", message);
    return result;
}
```

## Request Contracts

Keep contracts explicit:

- `version`: integer or string for future changes.
- `action`: action name when one method handles multiple actions.
- `requestId`: useful for async UI and file bridges.
- Target ids and scope fields.
- Clear response shape: `success`, `code`, `message`, and payload fields.

If using a custom method id, define constants on both sides and document the mapping. Reference Mods show method ids being intercepted by domain `CallMethod` patches; this is useful but more fragile than `AddModMethod`.

## Frontend Calls

A frontend may invoke a registered backend method with `ModDomainMethod.AsyncCall.CallModMethodWithParamAndRet(modId, methodName, request, callback)`; the callback receives the backend's `SerializableModData` response. This workspace's Mod uses exactly that call with an `AsyncMethodCallbackDelegate`, so treat it as the known-good shape rather than inventing a dispatcher. Other helpers exist in the installed `Assembly-CSharp`; confirm any of them with `TaiwuStudio.DecompilerWorker` before use.

Before writing a call:

- Find the exact frontend helper signature in the installed API.
- Encode the request payload with side-safe types.
- Handle async failure, timeout, and display-state reset.
- Avoid letting frontend trust stale UI selections; the backend must validate again.
- Include a `requestId` when several calls can overlap, and ignore responses that no longer match the open panel.

## File Bridge

Use file request/response bridges for developer tooling, runtime scripting, or external automation. Avoid for ordinary gameplay actions.

If using a file bridge:

- Read paths from config.
- Write JSON atomically when possible.
- Debounce watcher events.
- Include request ids.
- Keep large result indexes rather than rewriting huge files repeatedly.
- Clean up `FileSystemWatcher`, cancellation tokens, channels, tasks, and temporary files where appropriate.

## Frontend UI Patch Checklist

- Patch stable lifecycle methods such as `Awake`, `OnInit`, or refresh/reapply methods.
- Search for existing controls before creating duplicates.
- Reattach after UI rebuilds.
- Guard null Unity objects.
- Store view state per instance, query, or panel key.
- Use backend calls for authoritative search or mutation.

## Backend Patch Checklist

- Patch backend `GameData.*` domain methods only from backend assemblies.
- Avoid frontend-only types.
- Preserve original behavior on decode/validation failure.
- Keep behavior deterministic where game simulation expects determinism.
- Log enough context for debugging without spamming every game tick.
