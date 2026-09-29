#!/usr/bin/env bash
set -euo pipefail

tools_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

if ! command -v dotnet >/dev/null 2>&1; then
    printf 'Install a .NET SDK (8 or newer) before building the workers.\n' >&2
    exit 1
fi

# Isolate Linux restore/build artifacts from the shipped Windows binaries and obj.
for worker in TaiwuStudio.RoslynWorker TaiwuStudio.DecompilerWorker; do
    dotnet build "$tools_root/$worker/$worker.csproj" \
        --configuration Release \
        --artifacts-path "$tools_root/.linux" \
        -p:UseAppHost=false
done
