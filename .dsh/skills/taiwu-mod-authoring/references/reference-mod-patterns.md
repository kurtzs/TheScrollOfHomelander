# Reference Mod Patterns

These patterns come from Mods analyzed under `C:\Users\Administrator\Documents\GitHub\taiwu_studio\docs\reference`. Use them as implementation patterns, not copy sources.

## Base Helper Mod

Example: `2878665107`

Useful patterns:

- Shared attributes describe settings.
- Frontend and backend each reflect fields and load settings through the correct side API.
- Legacy RPC bridge intercepts `ModDomain.SetSerializableModData` and uses keys such as `_{modId}_method_{methodName}_`.

Use for compatibility helpers. Prefer `DomainManager.Mod.AddModMethod` for new backend calls unless maintaining legacy compatibility.

## Frontend Asset Replacement

Example: `3049112734`

Useful patterns:

- Frontend-only plugin.
- Patch avatar sprite/texture fetch methods.
- Load replacement assets from enabled Mods and known folders.
- Delay scanning until frontend assets are initialized.
- Destroy helper GameObjects and unpatch in `Dispose`.

Use for visual replacement Mods.

## Full Product Mod With Backend Actions

Example: `3729307604`

Useful patterns:

- Frontend owns UI, prompts, external API orchestration, presets, and documentation.
- Backend registers explicit methods through `DomainManager.Mod.AddModMethod`.
- Backend validates each request and returns structured `SerializableModData`.
- Game actions are applied through existing domain/action APIs where possible.

Use for Mods where frontend UX asks backend to perform real game actions.

## Config Platform / SDK Mod

Example: `3737804824`

Useful patterns:

- Frontend registers schema sources and renders config UI.
- Backend registers an action bridge such as `TianDaoInvokeAction`.
- SDK assemblies define shared config/action abstractions.
- Backend subscribes to config changes.

Use for reusable configuration frameworks or Mods that expose settings to other Mods.

## Small Gameplay Patch With External Config

Example: `3746024435`

Useful patterns:

- Frontend registers config source.
- Backend patches a focused set of gameplay methods.
- Runtime config flags drive patch behavior.
- `Dependencies` points to the config platform Mod.

Use for focused gameplay tweaks.

## Runtime Developer Tool

Example: `3746078506`

Useful patterns:

- Frontend and backend communicate with external tools through JSON files.
- Frontend executes scene/UI queries and optional frontend C# scripts.
- Backend executes backend C# scripts or game-data queries.
- Watchers feed async queues/channels and write result files.
- Extra Roslyn dependencies load from `FrontLib`, `BackLib`, and `CommonLib`.

Use for tooling, diagnostics, automation, and runtime experimentation. Avoid for normal gameplay actions.

## Split UI Search + Backend Search

Example: `3746087946`

Useful patterns:

- Shared request contract on both sides.
- Frontend adds UI controls, encodes scope/keyword, and calls backend.
- Backend intercepts an existing domain call or custom method id, performs authoritative search, and serializes ids.
- Failure falls back to original search behavior.

Use when UI needs a broader or smarter query than vanilla frontend can safely perform.

## Pattern Selection

- Visual replacement: frontend asset replacement.
- UI plus game mutation: frontend UI + backend action.
- Game rule tweak: backend Harmony + optional frontend config source.
- Reusable settings UI: config platform / SDK.
- AI/external integration: full product Mod with backend actions.
- Developer automation: runtime tool with file bridge.
- Search/filter feature: frontend control + backend authoritative query.

## What To Extract From A Reference Mod

- `Config.lua`: declared frontend/backend plugin paths (including nested paths), `TagList`, `Dependencies`, and whether settings are declared inline or through a framework.
- The plugin entry classes: which side each assembly targets, and how it registers Harmony, settings, and bridge methods.
- Patch targets: side, type, method, patch kind, and parameter list — then verify each one against the installed DLL before reusing the idea.
- The bridge contract between the sides, and any JSON/settings files the Mod writes at runtime.
