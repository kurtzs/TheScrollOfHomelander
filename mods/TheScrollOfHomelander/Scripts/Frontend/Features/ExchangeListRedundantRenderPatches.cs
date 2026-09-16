#nullable disable

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using FrameWork.UISystem.Components;
using Game.Views.Exchange;
using HarmonyLib;

namespace BetterTaiwuScroll.Frontend;

// ItemListScroll.SetItemList() already reaches InfinityScroll.SetDataCount(),
// which queues a render.  ViewExchangeBase then immediately calls ReRender()
// again for the same list.  Remove only those known redundant calls from the
// two indexed refresh methods; no data or filtering logic is replaced.
[HarmonyPatch]
internal static class ViewExchangeBaseRedundantItemListRenderPatch
{
    [HarmonyPrepare]
    private static bool Prepare() => GamePerformancePatchLifecycle.AllowInstallation;

    private static readonly MethodInfo ReRenderMethod =
        AccessTools.Method(typeof(InfinityScroll), nameof(InfinityScroll.ReRender));
    private static readonly MethodInfo ConditionalReRenderMethod =
        AccessTools.Method(typeof(ViewExchangeBaseRedundantItemListRenderPatch), nameof(ConditionalReRender));

    private static readonly MethodBase[] Targets =
    {
        AccessTools.Method(typeof(ViewExchangeBase), "RefreshSelfItems", new[] { typeof(int) }),
        AccessTools.Method(typeof(ViewExchangeBase), "RefreshTargetItems", new[] { typeof(int) }),
    };

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var target in Targets)
        {
            if (target != null)
                yield return target;
        }
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (ReRenderMethod != null
                && ConditionalReRenderMethod != null
                && (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                && instruction.operand is MethodInfo method
                && method == ReRenderMethod)
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = ConditionalReRenderMethod;
            }

            yield return instruction;
        }
    }

    private static void ConditionalReRender(InfinityScroll scroll)
    {
        if (!Plugin.IsGameExchangeRedundantRenderOptimizationEnabled)
            scroll?.ReRender();
    }
}
