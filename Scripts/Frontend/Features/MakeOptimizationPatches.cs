#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(MakeSubPageMake), "OnClickButtonConfirm")]
internal static class MakeConfirmPatch
{
    internal static readonly HashSet<MakeSubPageMake> WaitingPages = new();

    private static void Postfix(MakeSubPageMake __instance)
    {
        var view = MakeSelectMaterialPatch.GetParentView(__instance);
        if (__instance == null || view == null)
            return;

        if (!Plugin.IsEnabledForLifeSkill(view.CurLifeSkillType)
            || !Plugin.IsAutoSelectMaterialEnabledForLifeSkill(view.CurLifeSkillType))
            return;

        if (ContinuousMakeUiController.IsContinuousMakeEnabledFor(view))
            return;

        WaitingPages.Add(__instance);
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "RefreshPanel")]
internal static class MakeRefreshPanelPatch
{
    private static void Postfix(MakeSubPageMake __instance)
    {
        if (__instance == null || !MakeConfirmPatch.WaitingPages.Remove(__instance))
            return;

        var view = MakeSelectMaterialPatch.GetParentView(__instance);
        if (view == null || !Plugin.IsEnabledForLifeSkill(view.CurLifeSkillType))
            return;

        __instance.StartCoroutine(SelectFirstMaterialAfterRefresh(__instance));
    }

    private static IEnumerator SelectFirstMaterialAfterRefresh(MakeSubPageMake page)
    {
        yield return null;

        var view = MakeSelectMaterialPatch.GetParentView(page);
        if (page == null || view == null || !Plugin.IsEnabledForLifeSkill(view.CurLifeSkillType))
            yield break;

        var firstMaterial = GetFirstAvailableMaterial(page);
        if (firstMaterial == null)
            yield break;

        var nativeOptions = ContinuousMakeExecutionController.MakePageNativeOptionSnapshot.Capture(page);
        if (!ClickMaterialListItem(page, firstMaterial))
            AccessTools.Method(typeof(MakeSubPageMake), "SelectMaterial")?.Invoke(page, new object[] { firstMaterial, false });
        nativeOptions.Restore(page);

        yield return null;
        if (Plugin.EnableMaxProductCount)
            MakeSelectMaterialPatch.SetMakeCountToMax(page);
    }

    private static ItemDisplayData GetFirstAvailableMaterial(MakeSubPageMake page)
    {
        var materialList = GetCurrentMaterialList(page);
        if (materialList == null)
            return null;

        foreach (var item in materialList)
        {
            if (item is not ItemDisplayData material)
                continue;

            if (!GetBoolProperty(material, "Interactable"))
                continue;

            if (GetIntProperty(material, "Amount") <= 0)
                continue;

            return material;
        }

        return null;
    }

    private static bool ClickMaterialListItem(MakeSubPageMake page, ItemDisplayData material)
    {
        var materialListScroll = Traverse.Create(page).Field("materialListScroll").GetValue();
        if (materialListScroll == null)
            return false;

        var clickMethod = AccessTools.Method(materialListScroll.GetType(), "Click", new[] { typeof(ITradeableContent) });
        if (clickMethod == null)
            return false;

        clickMethod.Invoke(materialListScroll, new object[] { material });
        return true;
    }

    private static IEnumerable GetCurrentMaterialList(MakeSubPageMake page)
    {
        var materialListScroll = Traverse.Create(page).Field("materialListScroll").GetValue();
        if (materialListScroll != null)
        {
            var filteredData = ReflectionHelpers.FindProperty(materialListScroll.GetType(), "FilteredData")?.GetValue(materialListScroll, null) as IEnumerable;
            if (filteredData != null)
                return filteredData;

            var dataList = ReflectionHelpers.FindProperty(materialListScroll.GetType(), "DataList")?.GetValue(materialListScroll, null) as IEnumerable;
            if (dataList != null)
                return dataList;
        }

        return Traverse.Create(page).Field("_materialList").GetValue() as IEnumerable;
    }

    private static bool GetBoolProperty(object value, string propertyName)
    {
        var property = ReflectionHelpers.FindProperty(value.GetType(), propertyName);
        if (property == null)
            return false;

        var rawValue = property.GetValue(value, null);
        return rawValue != null && Convert.ToBoolean(rawValue);
    }

    private static int GetIntProperty(object value, string propertyName)
    {
        var property = ReflectionHelpers.FindProperty(value.GetType(), propertyName);
        if (property == null)
            return 0;

        var rawValue = property.GetValue(value, null);
        return rawValue == null ? 0 : Convert.ToInt32(rawValue);
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "SelectMaterial")]
internal static class MakeSelectMaterialPatch
{
    private static void Postfix(MakeSubPageMake __instance)
    {
        var view = GetParentView(__instance);
        if (__instance == null || view == null)
            return;

        if (!Plugin.IsEnabledForLifeSkill(view.CurLifeSkillType))
            return;

        if (ContinuousMakeExecutionController.IsSelectingMaterialForContinuation)
            return;

        __instance.StartCoroutine(ApplyAfterUiRefresh(__instance));
    }

    private static IEnumerator ApplyAfterUiRefresh(MakeSubPageMake page)
    {
        yield return null;

        var view = GetParentView(page);
        if (page == null || view == null || !Plugin.IsEnabledForLifeSkill(view.CurLifeSkillType))
            yield break;

        if (Plugin.EnableBestTool && Traverse.Create(page).Field("_isAutoSelectTool").GetValue<bool>())
            AccessTools.Method(typeof(ViewMake), "AutoSelectTool")?.Invoke(view, Array.Empty<object>());

        yield return null;

        if (Plugin.EnableMaxProductCount)
            SetMakeCountToMax(page);
    }

    internal static void SetMakeCountToMax(MakeSubPageMake page)
    {
        if (!HasValidMakeSubType(page))
            return;

        var maxMakeCount = Traverse.Create(page).Field("_maxMakeCount").GetValue<int>();
        if (maxMakeCount <= 1)
            return;

        var slider = Traverse.Create(page).Field("sliderMakeCount").GetValue();
        if (slider == null)
            return;

        var sliderType = slider.GetType();
        var maxValueProperty = sliderType.GetProperty("maxValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var valueProperty = sliderType.GetProperty("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (valueProperty == null)
            return;

        var sliderMax = maxValueProperty != null ? Convert.ToSingle(maxValueProperty.GetValue(slider, null)) : maxMakeCount;
        var targetValue = Math.Min(maxMakeCount, (int)sliderMax);
        if (targetValue <= 1)
            return;

        SetMakeCount(page, targetValue);
    }

    internal static void SetMakeCount(MakeSubPageMake page, int count)
    {
        if (page == null || count < 1)
            return;

        var slider = Traverse.Create(page).Field("sliderMakeCount").GetValue();
        if (slider == null)
            return;

        var sliderType = slider.GetType();
        var minValueProperty = sliderType.GetProperty("minValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var maxValueProperty = sliderType.GetProperty("maxValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var valueProperty = sliderType.GetProperty("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var setValueWithoutNotifyMethod = sliderType.GetMethod("SetValueWithoutNotify", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(float) }, null);
        if (valueProperty == null && setValueWithoutNotifyMethod == null)
            return;

        var sliderMin = minValueProperty != null ? Convert.ToSingle(minValueProperty.GetValue(slider, null)) : 1f;
        var sliderMax = maxValueProperty != null ? Convert.ToSingle(maxValueProperty.GetValue(slider, null)) : count;
        var targetValue = Mathf.Clamp(count, Mathf.Max(1, (int)sliderMin), Math.Max(1, (int)sliderMax));

        if (setValueWithoutNotifyMethod != null)
            setValueWithoutNotifyMethod.Invoke(slider, new object[] { (float)targetValue });
        else
            valueProperty.SetValue(slider, (float)targetValue, null);

        Traverse.Create(page).Field("_makeCount").SetValue((short)targetValue);
        Traverse.Create(page).Method("OnSliderMakeCountValueChanged", (float)targetValue).GetValue();
        Traverse.Create(page).Method("RefreshMakeCount").GetValue();
    }

    private static bool HasValidMakeSubType(MakeSubPageMake page)
    {
        if (page == null)
            return false;

        var makeItemSubTypeId = Traverse.Create(page).Field("_makeItemSubTypeId").GetValue<short>();
        if (makeItemSubTypeId < 0)
            return false;

        try
        {
            return Config.MakeItemSubType.Instance[makeItemSubTypeId] != null;
        }
        catch
        {
            return false;
        }
    }

    internal static ViewMake GetParentView(MakeSubPageMake page)
    {
        return page == null ? null : Traverse.Create(page).Field("ParentView").GetValue<ViewMake>();
    }
}

[HarmonyPatch(typeof(ViewMake), "GetAutoSelectTool")]
internal static class ViewMakeGetAutoSelectToolPatch
{
    private static void Postfix(ViewMake __instance, ref ItemDisplayData __result)
    {
        if (__instance == null || !Plugin.EnableBestTool || !Plugin.IsEnabledForLifeSkill(__instance.CurLifeSkillType))
            return;

        var continuousMode = ContinuousMakeUiController.IsContinuousMakeEnabledFor(__instance);
        var toolList = Traverse.Create(__instance).Field(continuousMode ? "_allToolList" : "_toolList").GetValue() as IEnumerable;
        if (toolList == null)
            return;

        if (continuousMode)
        {
            var continuousTool = GetContinuousMakeTool(__instance, toolList);
            if (continuousTool != null)
                __result = continuousTool;
            return;
        }

        __result = GetDefaultBestTool(__instance, toolList) ?? __result;
    }

    private static ItemDisplayData GetDefaultBestTool(ViewMake view, IEnumerable toolList)
    {
        ItemDisplayData bestTool = null;
        var settings = ContinuousMakeSettingsStore.GetFor(view);
        var highGradeFirst = settings.ToolGradePriority == 0;
        if (!highGradeFirst && TryGetAvailableBareHandTool(view, out var bareHandTool) && settings.AllowBareHand)
            return bareHandTool;

        foreach (var item in toolList)
        {
            if (item is not ItemDisplayData tool)
                continue;

            if (ViewMake.IsEmptyTool(tool))
                continue;

            if (!IsToolAvailable(view, tool))
                continue;

            if (bestTool == null || IsBetterTool(tool, bestTool, highGradeFirst))
                bestTool = tool;
        }

        return bestTool;
    }

    private static ItemDisplayData GetContinuousMakeTool(ViewMake view, IEnumerable toolList)
    {
        var settings = ContinuousMakeSettingsStore.GetFor(view);
        ItemDisplayData bestTool = null;
        var highGradeFirst = settings.ToolGradePriority == 0;

        if (!highGradeFirst && settings.AllowBareHand && TryGetAvailableBareHandTool(view, out var bareHandTool))
            return bareHandTool;

        foreach (var item in toolList)
        {
            if (item is not ItemDisplayData tool)
                continue;

            if (ViewMake.IsEmptyTool(tool))
                continue;

            if (!ContinuousMakeSettingsStore.IsSourceAllowed(view.CurLifeSkillType, tool.ItemSourceTypeEnum))
                continue;

            if (!IsToolAvailable(view, tool))
                continue;

            if (settings.EnableDurabilityProtection && WouldToolBreakOnNextUse(view, tool))
                continue;

            if (bestTool == null || IsBetterTool(tool, bestTool, highGradeFirst))
                bestTool = tool;
        }

        if (bestTool != null)
            return bestTool;

        if (!settings.AllowBareHand)
            return null;

        return TryGetAvailableBareHandTool(view, out var fallbackBareHandTool) ? fallbackBareHandTool : null;
    }

    private static bool TryGetAvailableBareHandTool(ViewMake view, out ItemDisplayData emptyTool)
    {
        emptyTool = Traverse.Create(view).Field("_emptyTool").GetValue<ItemDisplayData>();
        return emptyTool != null && IsToolAvailable(view, emptyTool);
    }

    private static bool IsToolAvailable(ViewMake view, ItemDisplayData tool)
    {
        var method = AccessTools.Method(typeof(ViewMake), "CheckTool");
        if (method == null)
            return true;

        var args = new object[] { tool, false, false, false };
        return (bool)method.Invoke(view, args);
    }

    private static bool IsBetterTool(ItemDisplayData candidate, ItemDisplayData current, bool highGradeFirst)
    {
        var candidateGrade = GetSByteProperty(candidate, "Grade");
        var currentGrade = GetSByteProperty(current, "Grade");
        if (candidateGrade != currentGrade)
            return highGradeFirst ? candidateGrade > currentGrade : candidateGrade < currentGrade;

        return GetLongProperty(candidate, "Value") > GetLongProperty(current, "Value");
    }

    private static bool WouldToolBreakOnNextUse(ViewMake view, ItemDisplayData tool)
    {
        if (tool == null || ViewMake.IsEmptyTool(tool))
            return false;

        var cost = GetCurrentToolDurabilityCost(view, tool);
        if (cost <= 0)
            return false;

        return tool.Durability <= cost;
    }

    private static int GetCurrentToolDurabilityCost(ViewMake view, ItemDisplayData tool)
    {
        var targetGradeLists = Traverse.Create(view).Field("_toolTargetGradeList").GetValue<List<List<sbyte>>>();
        if (targetGradeLists == null || targetGradeLists.Count == 0 || targetGradeLists[0] == null)
            return 0;

        var cost = 0;
        foreach (var grade in targetGradeLists[0])
            cost += ViewMake.GetToolDurabilityCost(tool, grade);
        return cost;
    }

    private static sbyte GetSByteProperty(object value, string propertyName)
    {
        var property = ReflectionHelpers.FindProperty(value.GetType(), propertyName);
        if (property == null)
            return 0;

        var rawValue = property.GetValue(value, null);
        return rawValue == null ? (sbyte)0 : Convert.ToSByte(rawValue);
    }

    private static long GetLongProperty(object value, string propertyName)
    {
        var property = ReflectionHelpers.FindProperty(value.GetType(), propertyName);
        if (property == null)
            return 0;

        var rawValue = property.GetValue(value, null);
        return rawValue == null ? 0 : Convert.ToInt64(rawValue);
    }
}
