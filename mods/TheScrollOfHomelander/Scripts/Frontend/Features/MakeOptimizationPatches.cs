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
    private static readonly FieldInfo ParentViewField = AccessTools.Field(typeof(MakeSubPage), "ParentView")
        ?? AccessTools.Field(typeof(MakeSubPageMake), "ParentView");
    private static readonly MethodInfo RefreshToolListMethod = AccessTools.Method(
        typeof(MakeSubPageMake),
        "RefreshToolList",
        new[] { typeof(bool) });

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

        if (Plugin.EnableBestTool)
        {
            // SelectMaterial now refreshes tools with isAuto=false. Re-run the
            // current page's selector with auto selection enabled so its
            // callback also updates the page's tool slot.
            RefreshToolListMethod?.Invoke(page, new object[] { true });
        }

        yield return null;

        if (Plugin.EnableMaxProductCount)
            SetMakeCountToMax(page);
    }

    internal static void SetMakeCountToMax(MakeSubPageMake page)
    {
        if (!HasValidMakeSubType(page))
            return;

        var maxMakeCount = GetIntField(page, "_maxMakeCount");
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

        SetNumericField(page, "_makeCount", targetValue);
        Traverse.Create(page).Method("OnSliderMakeCountValueChanged", (float)targetValue).GetValue();
        Traverse.Create(page).Method("RefreshMakeCount").GetValue();
    }

    private static bool HasValidMakeSubType(MakeSubPageMake page)
    {
        if (page == null)
            return false;

        var makeItemSubTypeId = GetIntField(page, "_makeItemSubTypeId", -1);
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
        if (page == null)
            return null;

        var parent = ParentViewField?.GetValue(page) as ViewMake;
        if (parent != null)
            return parent;

        // ParentView is declared on MakeSubPage in some game builds, so the
        // derived-type FieldInfo lookup can be unavailable.
        return Traverse.Create(page).Field("ParentView").GetValue() as ViewMake;
    }

    private static int GetIntField(MakeSubPageMake page, string fieldName, int fallback = 0)
    {
        var raw = Traverse.Create(page).Field(fieldName).GetValue();
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

    private static bool GetBoolField(MakeSubPageMake page, string fieldName)
    {
        var raw = Traverse.Create(page).Field(fieldName).GetValue();
        if (raw == null)
            return false;

        try
        {
            return Convert.ToBoolean(raw);
        }
        catch
        {
            return false;
        }
    }

    private static void SetNumericField(MakeSubPageMake page, string fieldName, int value)
    {
        var field = AccessTools.Field(typeof(MakeSubPageMake), fieldName);
        if (field == null)
            return;

        try
        {
            var targetType = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
            field.SetValue(page, Convert.ChangeType(value, targetType));
        }
        catch
        {
            // The game may remove or change this private field; the slider/UI path
            // remains usable even when the backing cache cannot be updated.
        }
    }
}

[HarmonyPatch(typeof(ViewMake), "AutoSelectTool")]
internal static class ViewMakeAutoSelectToolPatch
{
    [ThreadStatic] internal static ViewMake CurrentView;
    private static void Prefix(ViewMake __instance)
    {
        CurrentView = __instance;
        if (__instance == null || !Plugin.EnableBestTool)
            return;

        try
        {
            var selector = MakeGameApi.ToolSelector.GetValue(__instance) as MakeToolSelector;
            if (selector?.SelectorConfig != null) selector.SelectorConfig.IsAutoSelectBest = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to enable best-tool selection: " + ex.Message);
        }
    }

    private static Exception Finalizer(Exception __exception)
    {
        CurrentView = null;
        return __exception;
    }
}

[HarmonyPatch(typeof(MakeToolSelector), "GetAutoSelectTool")]
internal static class ViewMakeGetAutoSelectToolPatch
{
    private static void Postfix(MakeToolSelector __instance, ref ItemDisplayData __result)
    {
        var view = ViewMakeAutoSelectToolPatch.CurrentView;
        if (view == null || !Plugin.EnableBestTool || !Plugin.IsEnabledForLifeSkill(view.CurLifeSkillType))
            return;

        var continuousMode = ContinuousMakeExecutionController.IsBatchActive(view);
        var toolList = continuousMode ? (IEnumerable)__instance.SelectorConfig.AllToolList : __instance.ToolList;
        if (toolList == null)
            return;

        if (continuousMode)
        {
            __result = GetContinuousMakeTool(view, toolList);
            return;
        }

        __result = GetDefaultBestTool(view, toolList);
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
        emptyTool = GetSelector(view)?.EmptyTool;
        return emptyTool != null && IsToolAvailable(view, emptyTool);
    }

    private static bool IsToolAvailable(ViewMake view, ItemDisplayData tool)
    {
        if (view == null || tool == null)
            return false;

        try
        {
            // Since the 2026 update tool validation lives in MakeToolSelector,
            // while older builds exposed ViewMake.CheckTool directly.
            var selector = AccessTools.Field(typeof(ViewMake), "_makeToolSelector")?.GetValue(view);
            var selectorMethod = selector == null
                ? null
                : AccessTools.Method(selector.GetType(), "CheckTool", new[]
                {
                    typeof(ITradeableContent), typeof(bool).MakeByRefType(),
                    typeof(bool).MakeByRefType(), typeof(bool).MakeByRefType()
                });
            if (selectorMethod != null)
            {
                var args = new object[] { tool, false, false, false };
                return selectorMethod.Invoke(selector, args) is bool result && result;
            }

            var legacyMethod = AccessTools.Method(typeof(ViewMake), "CheckTool");
            if (legacyMethod != null)
                return legacyMethod.Invoke(view, new object[] { tool, false, false, false }) is bool legacyResult && legacyResult;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Tool availability check failed: " + ex.Message);
        }

        return false;
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
        var targetGradeLists = GetSelector(view)?.SelectorConfig?.ToolTargetGradeList;
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

    private static readonly System.Reflection.FieldInfo SelectorField = MakeGameApi.ToolSelector;
    private static MakeToolSelector GetSelector(ViewMake view) => SelectorField.GetValue(view) as MakeToolSelector;

    private static long GetLongProperty(object value, string propertyName)
    {
        var property = ReflectionHelpers.FindProperty(value.GetType(), propertyName);
        if (property == null)
            return 0;

        var rawValue = property.GetValue(value, null);
        return rawValue == null ? 0 : Convert.ToInt64(rawValue);
    }
}
