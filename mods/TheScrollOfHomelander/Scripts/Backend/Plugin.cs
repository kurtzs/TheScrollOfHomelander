#nullable disable

using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

namespace BetterTaiwuScroll.Backend;

[PluginConfig("更好的太吾绘卷 Backend", "Taiwu Studio", "0.1.0")]
public sealed class Plugin : TaiwuRemakePlugin
{
    private Harmony _harmony;
    private readonly BetterTaiwuScroll.Shared.ModPatchGroups _patchGroups = new();

    public override void Initialize()
    {
        GamePerformanceSettings.Load(ModIdStr);
        AdvanceMonthDiagnosticsSettings.Load(ModIdStr);
        AutoCultivationFeatureSettings.Load(ModIdStr);
        AutoRepairService.Register(ModIdStr);
        CharacterSearchService.Register(ModIdStr);
        _harmony = new Harmony("taiwu-studio.the-scroll-of-homelander.backend");
        _patchGroups.Install(typeof(Plugin).Assembly, _harmony.Id,
            BetterTaiwuScroll.Shared.ModPatchGroups.Classify,
            message => GameData.Utilities.AdaptableLog.Info("[BetterTaiwuScroll] " + message));
    }

    public override void OnModSettingUpdate()
    {
        GamePerformanceSettings.Load(ModIdStr);
        AdvanceMonthDiagnosticsSettings.Load(ModIdStr);
        AutoCultivationFeatureSettings.Load(ModIdStr);
        AdvanceMonthActionTargetRangeCache.Reset();
        AdvanceMonthActionRelationCache.Reset();
        CharacterSearchService.LoadSetting();
    }

    public override void Dispose()
    {
        _patchGroups.Dispose();
        _harmony?.UnpatchSelf();
        _harmony = null;
        AdvanceMonthDiagnosticsRecorder.Reset();
        AdvanceMonthActionTargetRangeCache.Reset();
        AdvanceMonthActionRelationCache.Reset();
        AdvanceMonthMetabolismHolderIndex.Reset();
        AdvanceMonthSecretInformationRemoveBatch.Reset();
    }
}
