#nullable disable

using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

namespace BetterTaiwuScroll.Backend;

[PluginConfig("更好的太吾绘卷 Backend", "Taiwu Studio", "0.1.0")]
public sealed class Plugin : TaiwuRemakePlugin
{
    private Harmony _harmony;

    public override void Initialize()
    {
        AdvanceMonthDiagnosticsSettings.Load(ModIdStr);
        _harmony = new Harmony("taiwu-studio.the-scroll-of-homelander.backend");
        ApplyPatchesIsolated(_harmony);
    }

    // Apply each Harmony patch class independently so that one patch whose target can't be
    // resolved (e.g. a game update changes a method signature) disables only that optimization
    // instead of aborting PatchAll and leaving the whole backend unpatched. Failures are
    // swallowed per class (the backend assembly has no logging/UnityEngine dependency).
    private static void ApplyPatchesIsolated(Harmony harmony)
    {
        foreach (var type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
        {
            if (type == null)
                continue;

            try
            {
                harmony.CreateClassProcessor(type).Patch();
            }
            catch
            {
            }
        }
    }

    public override void OnModSettingUpdate()
    {
        AdvanceMonthDiagnosticsSettings.Load(ModIdStr);
    }

    public override void Dispose()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
        AdvanceMonthDiagnosticsRecorder.Reset();
        AdvanceMonthActionTargetRangeCache.Reset();
        AdvanceMonthActionRelationCache.Reset();
        AdvanceMonthMetabolismHolderIndex.Reset();
        AdvanceMonthSecretInformationRemoveBatch.Reset();
    }
}
