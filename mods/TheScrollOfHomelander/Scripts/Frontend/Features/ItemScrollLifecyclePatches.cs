#nullable disable

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using FrameWork.UISystem.Components;
using Game.Components.ListStyleGeneralScroll.Item;
using HarmonyLib;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

// Only RowItemLine objects belong in an item scroll's reusable cell maps.
// Keep overlays/decorations in place, but exclude them from native row callbacks.
[HarmonyPatch(typeof(InfinityScroll), "InitContainer")]
internal static class ItemScrollExcludeNonRowPatch
{
    private static readonly FieldInfo PrefabField = AccessTools.Field(typeof(InfinityScroll), "srcPrefab");
    private static readonly MethodInfo EqualityMethod = AccessTools.Method(typeof(Object), "op_Equality", new[] { typeof(Object), typeof(Object) });
    private static readonly MethodInfo SkipMethod = AccessTools.Method(typeof(ItemScrollExcludeNonRowPatch), nameof(IsTemplateOrNonRow));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var matches = 0;
        CodeInstruction previous = null;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(EqualityMethod) && previous != null
                && previous.opcode == OpCodes.Ldfld && Equals(previous.operand, PrefabField))
            {
                instruction.operand = SkipMethod;
                matches++;
            }
            previous = instruction;
            yield return instruction;
        }
        if (matches != 1) throw new System.InvalidOperationException("InfinityScroll.InitContainer row match count: " + matches);
    }

    private static bool IsTemplateOrNonRow(Object candidate, Object prefab)
    {
        if (candidate == prefab || candidate == null)
            return true;
        return prefab is GameObject template && template != null
            && template.GetComponent<RowItemLine>() != null
            && candidate is GameObject cell && cell.GetComponent<RowItemLine>() == null;
    }
}

[HarmonyPatch(typeof(InfinityScroll), nameof(InfinityScroll.ClearCache))]
internal static class ItemScrollRetiredCellPatch
{
    private static readonly MethodInfo DestroyMethod = AccessTools.Method(typeof(Object), nameof(Object.Destroy), new[] { typeof(Object) });
    private static readonly MethodInfo RetireMethod = AccessTools.Method(typeof(ItemScrollRetiredCellPatch), nameof(RetireCell));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var matches = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(DestroyMethod))
            {
                matches++;
                // ClearCache can be followed by InitContainer in the same frame.
                // Keep pending-destruction rows outside the content it enumerates.
                var owner = new CodeInstruction(OpCodes.Ldarg_0);
                owner.labels.AddRange(instruction.labels);
                owner.blocks.AddRange(instruction.blocks);
                instruction.labels.Clear();
                instruction.blocks.Clear();
                yield return owner;
                instruction.opcode = OpCodes.Call;
                instruction.operand = RetireMethod;
            }
            yield return instruction;
        }
        if (matches != 1) throw new System.InvalidOperationException("InfinityScroll.ClearCache destroy match count: " + matches);
    }

    private static void RetireCell(Object value, InfinityScroll scroll)
    {
        if (value is GameObject cell && cell != null && scroll != null
            && scroll.srcPrefab != null && scroll.srcPrefab.GetComponent<RowItemLine>() != null)
        {
            cell.SetActive(false);
            cell.transform.SetParent(scroll.transform, false);
        }
        Object.Destroy(value);
    }
}
