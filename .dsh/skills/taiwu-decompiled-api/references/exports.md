# Offline Exports

`export-core` and `export-dir` are the only caching the toolchain offers, and the cache is just files you can grep. They exist for broad offline search; a one-off question is faster with `find-type` / `find-member`.

Windows PowerShell example. On Linux, follow [taiwu-linux-development](../../taiwu-linux-development/SKILL.md) and use `bash tools/run-worker.sh decompiler --command export-core ...` or `--command export-dir` with validated host paths. Output paths below use Windows notation.

```powershell
$worker  = "<repoRoot>\tools\TaiwuStudio.DecompilerWorker\bin\Release\net8.0\TaiwuStudio.DecompilerWorker.exe"
$managed = "<gameRoot>\The Scroll of Taiwu_Data\Managed"
$backend = "<gameRoot>\Backend"
$out     = "<repoRoot>\artifacts\decompiled\frontend"   # anywhere outside the Mod root

& $worker --command export-core --managed-dir $managed --out-dir $out
& $worker --command export-dir  --managed-dir $backend --out-dir "$out-backend"
```

What it writes:

```text
<out>\summary.json                     assemblies, per-type metadata, tokens, source paths, source spans
<out>\sources\<Assembly>\<Namespace>\<Type>.cs
```

`export-core` limits itself to `Assembly-CSharp.dll`, `TaiwuModdingLib.dll`, `0Harmony.dll`, and `GameData*`; `export-dir` walks every `.dll`/`.exe` directly inside the managed directory.

## Using an export

- The `summary.json` fingerprint (SHA256 of each source DLL) is the only trustworthy freshness marker. Re-export after every game update and compare it; a stale export that looks fine is how a compile succeeds while Harmony fails at load time.
- Search the source tree with the normal file tools, then confirm the hit against the live assembly with `find-member` / `decompile-member` before writing a patch.
- Keep exports outside the Mod root and outside the deployed Mod paths. They must never be packaged or deployed.
- Exports are large. Do not read whole files into a conversation; read the span around a hit.
