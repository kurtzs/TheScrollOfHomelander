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
        _harmony.PatchAll(typeof(Plugin).Assembly);
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
