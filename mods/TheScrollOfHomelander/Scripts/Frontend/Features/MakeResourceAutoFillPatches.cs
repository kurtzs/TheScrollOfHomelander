#nullable disable

using System;
using System.Reflection;
using HarmonyLib;

namespace BetterTaiwuScroll.Frontend;

internal static class MakeResourceAutoFillController
{
    private static readonly sbyte[] AutoFillLifeSkillTypes = { 14, 8, 9 };
    private static readonly FieldInfo MaterialSlotField = AccessTools.Field(typeof(MakeSubPageMake), "materialSlot");
    private static readonly FieldInfo MakeItemSubTypeIdField = AccessTools.Field(typeof(MakeSubPageMake), "_makeItemSubTypeId");
    private static readonly FieldInfo MaxMakeResourceTotalCountField = AccessTools.Field(typeof(MakeSubPageMake), "_maxMakeResourceTotalCount");
    private static readonly FieldInfo MaxMakeResourceCountIntsField = AccessTools.Field(typeof(MakeSubPageMake), "_maxMakeResourceCountInts");
    private static readonly FieldInfo CurMakeResourceCountIntsField = AccessTools.Field(typeof(MakeSubPageMake), "_curMakeResourceCountInts");
    private static readonly FieldInfo LastMakeResourceCountIntsField = AccessTools.Field(typeof(MakeSubPageMake), "_lastMakeResourceCountInts");
    private static readonly FieldInfo MainRequiredResourceTypeField = AccessTools.Field(typeof(MakeSubPageMake), "_mainRequiredResourceType");

    internal static void Apply(MakeSubPageMake page, bool refreshCondition)
    {
        if (page == null)
            return;

        var view = MakeSelectMaterialPatch.GetParentView(page);
        if (view == null || !Plugin.IsEnabledForLifeSkill(view.CurLifeSkillType)
            || Array.IndexOf(AutoFillLifeSkillTypes, view.CurLifeSkillType) < 0)
            return;

        try
        {
            if (!HasSelectedMaterial(page) || GetIntField(page, MakeItemSubTypeIdField, "_makeItemSubTypeId", -1) < 0)
                return;

            var totalMax = Math.Max(0, GetIntField(page, MaxMakeResourceTotalCountField, "_maxMakeResourceTotalCount"));
            if (totalMax <= 0)
                return;

            var maxResourceInts = GetField<ResourceInts>(page, MaxMakeResourceCountIntsField, "_maxMakeResourceCountInts");
            var current = GetField<ResourceInts>(page, CurMakeResourceCountIntsField, "_curMakeResourceCountInts");
            var mainResourceType = (sbyte)GetIntField(page, MainRequiredResourceTypeField, "_mainRequiredResourceType", -1);

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

            SetField(page, CurMakeResourceCountIntsField, "_curMakeResourceCountInts", next);
            SetField(page, LastMakeResourceCountIntsField, "_lastMakeResourceCountInts", next);

            if (refreshCondition)
                MakePageDeferredActionQueue.RequestCheckCondition(page);

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

    private static bool HasSelectedMaterial(MakeSubPageMake page)
    {
        var materialSlot = GetField<MakeTargetSlot>(page, MaterialSlotField, "materialSlot");
        return materialSlot != null && materialSlot.IsValid && materialSlot.ItemData != null;
    }

    private static T GetField<T>(MakeSubPageMake page, FieldInfo field, string fieldName)
    {
        if (field != null)
            return (T)field.GetValue(page);

        return Traverse.Create(page).Field(fieldName).GetValue<T>();
    }

    private static int GetIntField(MakeSubPageMake page, FieldInfo field, string fieldName, int fallback = 0)
    {
        var raw = field != null ? field.GetValue(page) : Traverse.Create(page).Field(fieldName).GetValue();
        if (raw == null)
            return fallback;

        try
        {
            return Convert.ToInt32(raw);
        }
        catch
        {
            return fallback;
        }
    }

    private static void SetField<T>(MakeSubPageMake page, FieldInfo field, string fieldName, T value)
    {
        if (field != null)
        {
            field.SetValue(page, value);
            return;
        }

        Traverse.Create(page).Field(fieldName).SetValue(value);
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
