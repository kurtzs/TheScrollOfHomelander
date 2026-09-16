# Core Assemblies

Taiwu Studio's first decompiler pass targets these assemblies:

- `Assembly-CSharp.dll`: game-side compiled Unity code, generated RPC wrappers, UI/controller classes, and many runtime behaviors.
- `GameData.*.dll`: shared data contracts, domain helpers, IDs, config/data model structures, and cross-domain DTOs.
- `TaiwuModdingLib.dll`: official Mod entry interfaces, plugin contracts, settings/config types, and loading hooks.
- `0Harmony.dll`: Harmony runtime API used by Taiwu Mods for Prefix/Postfix/Transpiler/Finalizer patches.

Search strategy:

- Start with `GameData.*` for stable data model and domain helper names.
- Use `Assembly-CSharp` for actual runtime behavior and UI implementation.
- Use `TaiwuModdingLib` when implementing Mod entry points or checking plugin lifecycle APIs.
- Use `0Harmony` only when confirming patch API signatures.
