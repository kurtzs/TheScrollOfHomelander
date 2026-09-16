# Taiwu Studio SQLite Index

Use SQLite as the local query index. Keep authoritative source files on disk; the database is a rebuildable cache.

## Initial Tables

- `mods`: one row per discovered Mod project.
- `mod_files`: indexed files under each Mod root.
- `diagnostics`: project health messages.
- `harmony_patches`: future table for detected/generated Harmony patches.
- `index_meta`: schema version and rebuild timestamps.

## Rules

- Store paths as strings exactly as seen by Rust `PathBuf::display()`.
- Use `INSERT ... ON CONFLICT DO UPDATE` for rebuilds.
- Make `mod.root` unique.
- Delete old child rows for a Mod before re-inserting files and diagnostics.
- Keep schema migrations explicit and versioned.

## Near-Term Queries

- Recent Mods by source/title.
- Broken Mods by diagnostic severity.
- Files by extension/group.
- Harmony usage summary.
