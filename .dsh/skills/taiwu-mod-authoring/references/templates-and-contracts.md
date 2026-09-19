# Templates And Contracts

Use this reference when generating C# files, backend contracts, Harmony patches, or project files.

## Frontend Entry Template

```csharp
using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

namespace MyMod.Frontend;

[PluginConfig("MyModFrontend", "Author", "1.0.0")]
public sealed class MyModFrontend : TaiwuRemakePlugin
{
    private Harmony? _harmony;

    public override void Initialize()
    {
        LoadSettings();
        _harmony = Harmony.CreateAndPatchAll(typeof(MyModFrontend));
    }

    public override void Dispose()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
    }

    public override void OnModSettingUpdate()
    {
        LoadSettings();
    }

    private static void LoadSettings()
    {
        // Read frontend settings through ModManager after confirming API signature.
    }
}
```

## Backend Entry Template

```csharp
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Mod;
using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

namespace MyMod.Backend;

[PluginConfig("MyModBackend", "Author", "1.0.0")]
public sealed class MyModBackend : TaiwuRemakePlugin
{
    private Harmony? _harmony;

    public override void Initialize()
    {
        LoadSettings();
        DomainManager.Mod.AddModMethod(ModIdStr, "DoThing", DoThing);
        _harmony = Harmony.CreateAndPatchAll(typeof(MyModBackend));
    }

    public override void Dispose()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
    }

    public override void OnModSettingUpdate()
    {
        LoadSettings();
    }

    private static SerializableModData DoThing(DataContext context, SerializableModData parameter)
    {
        if (parameter == null)
        {
            return Fail("bad_request", "Missing parameter.");
        }

        var result = new SerializableModData();
        result.Set("success", true);
        return result;
    }

    private static SerializableModData Fail(string code, string message)
    {
        var result = new SerializableModData();
        result.Set("success", false);
        result.Set("code", code);
        result.Set("message", message);
        return result;
    }

    private static void LoadSettings()
    {
        // Read backend settings through DomainManager.Mod after confirming API signature.
    }
}
```

## Harmony Patch Template

Prefer exact target type and parameter list. Replace placeholders only after API inspection.

```csharp
using HarmonyLib;

namespace MyMod.Backend.Patches;

[HarmonyPatch(typeof(TargetType), nameof(TargetType.TargetMethod), new[] { typeof(int), typeof(string) })]
internal static class TargetTypeTargetMethodPatch
{
    private static void Postfix(int arg0, string arg1, ref int __result)
    {
        // Adjust result after original logic.
    }
}
```

Constructor:

```csharp
[HarmonyPatch(typeof(TargetType), MethodType.Constructor, new[] { typeof(int) })]
internal static class TargetTypeCtorPatch
{
    private static void Postfix(TargetType __instance)
    {
    }
}
```

Static constructor:

```csharp
[HarmonyPatch(typeof(TargetType), MethodType.StaticConstructor)]
internal static class TargetTypeCctorPatch
{
    private static void Postfix()
    {
    }
}
```

## Contract Shape

For `SerializableModData`, prefer stable constants:

```csharp
internal static class Contract
{
    public const string MethodDoThing = "DoThing";
    public const string KeyVersion = "version";
    public const string KeySuccess = "success";
    public const string KeyCode = "code";
    public const string KeyMessage = "message";
    public const string KeyTargetId = "targetId";
}
```

Request:

```text
version: 1
targetId: int
mode: string
```

Response:

```text
success: bool
code: string
message: string
payload fields...
```

## Build Inputs Instead Of A Project File

There is no `.csproj` in the Mod build path. `TaiwuStudio.RoslynWorker` is the build system: it collects `.cs` files from the directories you name and references the managed DLLs found in `--game-libs`, choosing the frontend or backend required set by probing for `Assembly-CSharp.dll`.

```powershell
& $worker build --project-root <modRoot> --game-libs <frontendLibs> `
  --assembly-name <Name> --output-plugin-path "Front\<Name>.dll" `
  --source-dir "Scripts\Frontend" --source-dir "Scripts\Shared" --configuration Release
```

- Frontend libraries: `The Scroll of Taiwu_Data\Managed`. Backend libraries: `<gameRoot>\Backend`.
- End the output path with `.dll`; it is resolved under `<project-root>\Plugins`.
- Repeat `--source-dir` for each source root, and always include the shared one.
- The worker sets `LangVersion latest`, nullable enabled, `allowUnsafe` on, `OutputKind.DynamicallyLinkedLibrary`, and emits a portable PDB next to the DLL.
- If a `.csproj` exists in the project root, the worker only harvests its `<HintPath>` entries as extra references; it never runs MSBuild. Do not create one to control the build.
