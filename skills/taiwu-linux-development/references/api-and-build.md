# API Queries And Development Builds

Run from the workspace root. Set `TAIWU_GAME_ROOT` before a snippet only if the active Steam library moved. These examples read actual game assemblies but never execute the game.

## Check Installation First

```bash
set -euo pipefail
GAME="${TAIWU_GAME_ROOT:-/home/shishanyue/.local/share/Steam/steamapps/common/The Scroll Of Taiwu}"
FRONT="$GAME/The Scroll of Taiwu_Data/Managed"
BACK="$GAME/Backend"
for dll in Assembly-CSharp GameData.Shared TaiwuModdingLib 0Harmony UnityEngine.CoreModule; do
  test -s "$FRONT/$dll.dll" || { printf 'Missing frontend reference: %s\n' "$dll" >&2; exit 1; }
done
for dll in GameData GameData.Shared TaiwuModdingLib 0Harmony; do
  test -s "$BACK/$dll.dll" || { printf 'Missing backend reference: %s\n' "$dll" >&2; exit 1; }
done
```

Check Steam's manifest/download state too. Existence is only a prerequisite: stop if Steam is replacing files and repeat queries/builds once the installation is stable. Do not substitute old Windows-install DLLs to conceal missing or changed APIs.

## Query The Installed DLLs

After the preflight snippet:

```bash
bash tools/run-worker.sh decompiler --command find-type \
  --managed-dir "$FRONT" --assembly Assembly-CSharp.dll --query UI_ --limit 5

bash tools/run-worker.sh decompiler --command find-type \
  --managed-dir "$BACK" --assembly GameData.dll --query TaiwuDomain --limit 5

bash tools/run-worker.sh decompiler --command find-member \
  --managed-dir "$BACK" --assembly GameData.dll --query GetTaiwuCharId --limit 20
```

For the next call, use the actual token returned for the intended overload:

```bash
# TOKEN must be set to a token from the current find-member result.
bash tools/run-worker.sh decompiler --command decompile-member \
  --managed-dir "$BACK" --assembly GameData.dll --token "${TOKEN:?Use the current member token}"
```

Never hardcode an old token into a patch based solely on documentation. `decompile-member` returns the whole declaring type and an approximate `sourceSpan`; capture its JSON with the execution tool, inspect a narrow span, and confirm overload parameters in source. Do not dump giant types into the conversation. `GameData.Shared.dll` is side-specific: explicitly choose `FRONT` or `BACK`, rather than using the Windows wrapper's backend-first selection.

Record SHA256 alongside API evidence:

```bash
sha256sum "$FRONT/Assembly-CSharp.dll" "$FRONT/GameData.Shared.dll" \
  "$BACK/GameData.dll" "$BACK/GameData.Shared.dll"
```

On build `25596993`, `UI_Make` and `UI_BuildingManage` were absent from the current frontend assembly. Search the current UI hierarchy and lifecycle to identify replacements before any compatibility patch; do not create guessed aliases or swap in old DLLs.

## Compile Without Publishing

Use the same Roslyn worker and source partition as the Windows pipeline. After the preflight, run this block with `set -euo pipefail`; if either side fails, stop and inspect its JSON. For diagnostics on the other side, invoke that side separately, still without deploying anything.

```bash
MOD="$PWD/mods/TheScrollOfHomelander"
OUT="$PWD/tools/.linux/mod-check"
mkdir -p "$OUT"

bash tools/run-worker.sh roslyn build \
  --project-root "$MOD" --game-libs "$FRONT" \
  --assembly-name BetterTaiwuScrollFrontend \
  --output-plugin-path "$OUT/Front/BetterTaiwuScrollFrontend.dll" \
  --source-dir Scripts/Frontend --source-dir Scripts/Shared \
  --configuration Release > "$OUT/Frontend.json"

bash tools/run-worker.sh roslyn build \
  --project-root "$MOD" --game-libs "$BACK" \
  --assembly-name BetterTaiwuScrollBackend \
  --output-plugin-path "$OUT/Back/BetterTaiwuScrollBackend.dll" \
  --source-dir Scripts/Backend --source-dir Scripts/Shared \
  --configuration Release > "$OUT/Backend.json"
```

Output paths are absolute deliberately: relative `--output-plugin-path` values are placed under the Mod's `Plugins/`. Check exit status, `success`, and all Error diagnostics, not merely the existence of a DLL left by an earlier run. Each successful output has a portable PDB. Never compile all of `Scripts` into one assembly or omit `Scripts/Shared`.

This is an isolated development check. It does not update production `Plugins`, `build-records`, or `release-manifest.json`, and is not a replacement release pipeline. A failing frontend with a passing backend blocks the whole release. Keep logs/artifacts out of Git and do not claim runtime validation from these results.
