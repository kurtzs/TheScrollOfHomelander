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

## csproj Guidance

Use side-specific references. Example shape only; confirm actual assembly names from the API root.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="TaiwuModdingLib">
      <HintPath>$(TaiwuApiRoot)\TaiwuModdingLib.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="0Harmony">
      <HintPath>$(TaiwuApiRoot)\0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

Frontend adds Unity/UI assemblies from the frontend root. Backend adds `GameData.*` assemblies from the backend root. Avoid `Private=true` for game-provided assemblies.
