# Cache Layout

Taiwu Studio stores decompiled artifacts outside Mod projects.

Default locations:

- SQLite index: `%APPDATA%\TaiwuStudio\Taiwu Studio\data\index.sqlite3`
- Decompiled cache root: `%APPDATA%\TaiwuStudio\Taiwu Studio\data\.taiwu-studio\decompiled`
- Per-game-build cache: `<cache-root>\<assembly-csharp-hash>`
- Sources: `<build-cache>\sources\<Assembly>\<Namespace>\<Type>.cs`
- Summary: `<build-cache>\summary.json`

Environment overrides:

- `TAIWU_STUDIO_INDEX` points scripts at a specific SQLite index.
- `TAIWU_DECOMPILED_CACHE` points scripts at a cache root or `summary.json`.

If scripts return no results, run Taiwu Studio's API Browser export action first so the decompiler worker can generate source files and import the source map.
