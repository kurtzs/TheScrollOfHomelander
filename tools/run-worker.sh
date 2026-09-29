#!/usr/bin/env bash
set -euo pipefail

case "${1:-}" in
    roslyn) worker=TaiwuStudio.RoslynWorker ;;
    decompiler) worker=TaiwuStudio.DecompilerWorker ;;
    *)
        printf 'Usage: bash tools/run-worker.sh {roslyn|decompiler} [worker arguments...]\n' >&2
        exit 2
        ;;
esac
shift

tools_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
dll="$tools_root/.linux/bin/$worker/release/$worker.dll"
if [[ ! -f "$dll" ]]; then
    printf 'Worker not built: %s\nRun bash tools/build-linux.sh first.\n' "$dll" >&2
    exit 1
fi

# Major uses .NET 8 when present and permits the installed .NET 10 otherwise.
# Scope the policy to this process; do not change the game or system runtime.
exec dotnet exec --roll-forward "${DOTNET_ROLL_FORWARD:-Major}" "$dll" "$@"
