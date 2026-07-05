#nullable disable

using System;
using HarmonyLib;

namespace BetterTaiwuScroll.Frontend;

internal static class MakeResourceAutoFillController
{
    private static readonly sbyte[] AutoFillLifeSkillTypes = { 14, 8, 9 };

    internal static void Apply(MakeSubPageMake page, bool refreshCondition)
    {
        if (page == null)
            return;

        var view = MakeSelectMaterialPatch.GetParentView(page);
        if (view == null || Array.IndexOf(AutoFillLifeSkillTypes, view.CurLifeSkillType) < 0)
            return;

        try
        {
            var traverse = Traverse.Create(page);
            if (!HasSelectedMaterial(traverse) || traverse.Field("_makeItemSubTypeId").GetValue<short>() < 0)
                return;

            var totalMax = Math.Max(0, (int)traverse.Field("_maxMakeResourceTotalCount").GetValue<short>());
            if (totalMax <= 0)
                return;

            var maxResourceInts = traverse.Field("_maxMakeResourceCountInts").GetValue<ResourceInts>();
            var currentField = traverse.Field("_curMakeResourceCountInts");
            var lastField = traverse.Field("_lastMakeResourceCountInts");
            var current = currentField.GetValue<ResourceInts>();
            var mainResourceType = traverse.Field("_mainRequiredResourceType").GetValue<sbyte>();

            var next = new ResourceInts();
            next.Initialize();

            var remaining = totalMax;
            Fill(mainResourceType);
            for (sbyte resourceType = 0; resourceType < 6 && remaining > 0; resourceType++)
            {
                if (resourceType == mainResourceType)
                    continue;

                Fill(resourceType);
            }

            if (ResourceEquals(current, next))
                return;

            currentField.SetValue(next);
            lastField.SetValue(next);

            if (refreshCondition)
                Traverse.Create(page).Method("CheckCondition").GetValue();

            void Fill(sbyte resourceType)
            {
                if (resourceType < 0 || resourceType >= 6 || remaining <= 0)
                    return;

                var max = Math.Max(0, maxResourceInts.Get(resourceType));
                if (max <= 0)
                    return;

                var value = Math.Min(max, remaining);
                next.Set(resourceType, value);
                remaining -= value;
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning("[BetterTaiwuScroll] Failed to auto fill make resources: " + ex);
        }
    }

    private static bool ResourceEquals(ResourceInts left, ResourceInts right)
    {
        for (sbyte resourceType = 0; resourceType < 6; resourceType++)
        {
            if (left.Get(resourceType) != right.Get(resourceType))
                return false;
        }

        return true;
    }

    private static bool HasSelectedMaterial(Traverse pageTraverse)
    {
        var materialSlot = pageTraverse.Field("materialSlot").GetValue<MakeTargetSlot>();
        return materialSlot != null && materialSlot.IsValid && materialSlot.ItemData != null;
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "ResetResourceCount")]
internal static class MakeResourceAutoFillAfterResetPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(MakeSubPageMake __instance)
    {
        MakeResourceAutoFillController.Apply(__instance, refreshCondition: false);
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "SelectTarget")]
internal static class MakeResourceAutoFillAfterSelectTargetPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(MakeSubPageMake __instance)
    {
        MakeResourceAutoFillController.Apply(__instance, refreshCondition: true);
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "SelectMaterial")]
internal static class MakeResourceAutoFillAfterSelectMaterialPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(MakeSubPageMake __instance)
    {
        MakeResourceAutoFillController.Apply(__instance, refreshCondition: true);
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "SelectTool")]
internal static class MakeResourceAutoFillAfterSelectToolPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(MakeSubPageMake __instance)
    {
        MakeResourceAutoFillController.Apply(__instance, refreshCondition: true);
    }
}
