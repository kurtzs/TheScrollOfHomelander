#nullable disable

using FrameWork;
using Game.Views.Combat;
using HarmonyLib;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(TipsFolder), "Update")]
internal static class CombatTooltipDetailPatch
{
    private static readonly AccessTools.FieldRef<TipsFolder, TooltipInvoker> MouseTipRef =
        AccessTools.FieldRefAccess<TipsFolder, TooltipInvoker>("mouseTip");

    private static bool Prefix(TipsFolder __instance)
    {
        if (__instance == null)
            return true;

        var mouseTip = MouseTipRef(__instance);
        if (!ShouldAlwaysShowDetail(mouseTip))
            return true;

        ApplyTipType(mouseTip, __instance.baseType);
        return false;
    }

    private static bool ShouldAlwaysShowDetail(TooltipInvoker mouseTip)
    {
        if (mouseTip == null || mouseTip.GetComponentInParent<ViewCombat>(true) == null)
            return false;

        var runtimeParam = mouseTip.RuntimeParam;
        if (runtimeParam == null)
            return false;

        return runtimeParam.Get("CombatSkillId", out short _)
            || runtimeParam.Get("WeaponIndex", out int _)
            || runtimeParam.Get("WeaponTemplateId", out int _);
    }

    private static void ApplyTipType(TooltipInvoker mouseTip, TipType targetType)
    {
        if (mouseTip.Type == targetType)
            return;

        var showing = mouseTip.Showing;
        if (showing)
            mouseTip.HideTips();

        mouseTip.Type = targetType;

        if (showing)
            mouseTip.ShowTips();
    }
}
