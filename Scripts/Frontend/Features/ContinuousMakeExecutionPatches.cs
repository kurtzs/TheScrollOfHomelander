#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(MakeSubPageMake), "OnClickButtonConfirm")]
internal static class ContinuousMakeConfirmPatch
{
    private static void Postfix(MakeSubPageMake __instance)
    {
        ContinuousMakeExecutionController.MarkAfterConfirm(__instance);
    }
}

[HarmonyPatch(typeof(ViewMake), "RequestData")]
internal static class ContinuousMakeRequestDataPatch
{
    private static void Postfix(ViewMake __instance)
    {
        ContinuousMakeExecutionController.MarkRequestDataStarted(__instance);
    }
}

[HarmonyPatch(typeof(ViewMake), "Refresh")]
internal static class ContinuousMakeViewRefreshPatch
{
    private static void Postfix(ViewMake __instance)
    {
        ContinuousMakeExecutionController.TryContinueAfterViewRefresh(__instance);
    }
}

[HarmonyPatch(typeof(UIElement), "SetOnInitArgs")]
internal static class ContinuousMakeGetItemArgsPatch
{
    private static bool Prefix(UIElement __instance, FrameWork.ArgumentBox box)
    {
        return !ContinuousMakeExecutionController.TryCaptureGetItemArgs(__instance, box);
    }
}

[HarmonyPatch(typeof(UIManager), "MaskUI")]
internal static class ContinuousMakeGetItemMaskPatch
{
    private static bool Prefix(UIElement elem)
    {
        return !ContinuousMakeExecutionController.ShouldSuppressGetItemMask(elem);
    }
}

internal static class ContinuousMakeExecutionController
{
    private static readonly Dictionary<ViewMake, MakeSubPageMake> PendingPages = new();
    private static readonly HashSet<ViewMake> AwaitingDataRefreshViews = new();
    private static readonly HashSet<MakeSubPageMake> RunningPages = new();
    private static readonly Dictionary<ViewMake, ResultCollector> ResultCollectors = new();

    private static ViewMake _activeResultView;
    private static bool _suppressNextGetItemMask;
    private static bool _showingMergedGetItem;

    internal static void MarkAfterConfirm(MakeSubPageMake page)
    {
        if (!ShouldRun(page))
            return;

        var view = MakeSelectMaterialPatch.GetParentView(page);
        if (view != null)
        {
            PendingPages[view] = page;
            GetOrCreateCollector(view).ExpectedBatchCount++;
            _activeResultView = view;
        }
    }

    internal static void MarkRequestDataStarted(ViewMake view)
    {
        if (view != null && PendingPages.ContainsKey(view))
            AwaitingDataRefreshViews.Add(view);
    }

    internal static void TryContinueAfterViewRefresh(ViewMake view)
    {
        if (view == null || !AwaitingDataRefreshViews.Remove(view))
            return;

        if (!PendingPages.TryGetValue(view, out var pendingPage))
            return;

        PendingPages.Remove(view);

        var page = GetActiveMakePage(view) ?? pendingPage;
        if (page == null || RunningPages.Contains(page) || !CanStartCoroutine(page))
        {
            FinishAndShowResults(view, null);
            return;
        }

        page.StartCoroutine(ContinueAfterRefresh(page));
    }

    private static IEnumerator ContinueAfterRefresh(MakeSubPageMake page)
    {
        RunningPages.Add(page);
        var view = MakeSelectMaterialPatch.GetParentView(page);
        var continued = false;
        try
        {
            yield return null;

            if (!ShouldRun(page))
                yield break;

            var material = GetNextMaterial(page);
            if (material == null)
                yield break;

            SelectMaterial(page, material);

            yield return null;

            if (!ShouldRun(page))
                yield break;

            var parentView = MakeSelectMaterialPatch.GetParentView(page);
            if (parentView != null && Plugin.EnableBestTool)
                AccessTools.Method(typeof(ViewMake), "AutoSelectTool")?.Invoke(parentView, Array.Empty<object>());

            yield return null;

            if (!ShouldRun(page))
                yield break;

            var settings = ContinuousMakeSettingsStore.Current;
            if (Plugin.EnableMaxProductCount)
                MakeSelectMaterialPatch.SetMakeCountToMax(page);

            yield return null;

            if (!ShouldRun(page) || !IsConfirmInteractable(page))
                yield break;

            if (!settings.AllowBareHand && IsSelectedToolEmpty(page))
                yield break;

            if (settings.EnableDurabilityProtection && !ApplyDurabilityProtectionMakeCount(page))
                yield break;

            if (settings.EnableDurabilityProtection && WouldSelectedToolBreak(page))
                yield break;

            AccessTools.Method(typeof(MakeSubPageMake), "OnClickButtonConfirm")?.Invoke(page, Array.Empty<object>());
            continued = true;
        }
        finally
        {
            RunningPages.Remove(page);
            if (!continued)
                FinishAndShowResults(view, page);
        }
    }

    internal static bool TryCaptureGetItemArgs(UIElement element, FrameWork.ArgumentBox box)
    {
        if (_showingMergedGetItem || element != UIElement.GetItem || _activeResultView == null || box == null)
            return false;

        if (!ResultCollectors.TryGetValue(_activeResultView, out var collector))
            return false;

        if (!box.Get("ItemList", out List<ItemDisplayData> itemList) || itemList == null)
            return false;

        if (!box.Get("ObtainType", out sbyte obtainType) || obtainType != 6)
            return false;

        collector.AddItems(itemList);
        if (box.Get("InWareHouse", out bool inWarehouse))
            collector.InWarehouse = inWarehouse;
        if (box.Get("CloseAction", out Action closeAction) && closeAction != null)
            collector.CloseActions.Add(closeAction);

        collector.CapturedBatchCount++;
        collector.LastCaptureFrame = Time.frameCount;
        _suppressNextGetItemMask = true;
        return true;
    }

    internal static bool ShouldSuppressGetItemMask(UIElement element)
    {
        if (element != UIElement.GetItem || !_suppressNextGetItemMask)
            return false;

        _suppressNextGetItemMask = false;
        return true;
    }

    private static bool ShouldRun(MakeSubPageMake page)
    {
        if (page == null || !CanStartCoroutine(page))
            return false;

        if (!ContinuousMakeUiController.IsContinuousMakeEnabled)
            return false;

        var view = MakeSelectMaterialPatch.GetParentView(page);
        return view != null && Plugin.IsEnabledForLifeSkill(view.CurLifeSkillType);
    }

    private static bool CanStartCoroutine(MakeSubPageMake page)
    {
        return page != null && page.gameObject != null && page.gameObject.activeInHierarchy;
    }

    private static ResultCollector GetOrCreateCollector(ViewMake view)
    {
        if (!ResultCollectors.TryGetValue(view, out var collector))
        {
            collector = new ResultCollector();
            ResultCollectors[view] = collector;
        }

        return collector;
    }

    private static void FinishAndShowResults(ViewMake view, MonoBehaviour coroutineOwner)
    {
        if (view == null || !ResultCollectors.TryGetValue(view, out var collector))
            return;

        if (collector.Finishing)
            return;

        collector.Finishing = true;
        PendingPages.Remove(view);
        AwaitingDataRefreshViews.Remove(view);

        if (coroutineOwner != null && coroutineOwner.gameObject != null && coroutineOwner.gameObject.activeInHierarchy)
            coroutineOwner.StartCoroutine(ShowCollectedResultsWhenReady(view, collector));
        else
            ShowCollectedResults(view, collector);
    }

    private static IEnumerator ShowCollectedResultsWhenReady(ViewMake view, ResultCollector collector)
    {
        for (var i = 0; i < 90; i++)
        {
            if (collector.CapturedBatchCount >= collector.ExpectedBatchCount)
                break;

            yield return null;
        }

        ShowCollectedResults(view, collector);
    }

    private static void ShowCollectedResults(ViewMake view, ResultCollector collector)
    {
        if (view == null || collector == null)
            return;

        ResultCollectors.Remove(view);
        if (_activeResultView == view)
            _activeResultView = null;

        if (collector.Items.Count == 0)
            return;

        var items = new List<ItemDisplayData>(collector.Items);
        var closeActions = new List<Action>(collector.CloseActions);
        var argumentBox = FrameWork.EasyPool.Get<FrameWork.ArgumentBox>();
        argumentBox.SetObject("ItemList", items);
        argumentBox.Set("ObtainType", (sbyte)6);
        argumentBox.Set("InWareHouse", collector.InWarehouse);
        if (closeActions.Count > 0)
        {
            argumentBox.SetObject("CloseAction", (Action)(() =>
            {
                foreach (var action in closeActions)
                {
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[BetterTaiwuScroll] Continuous make CloseAction failed: " + ex);
                    }
                }
            }));
        }

        _showingMergedGetItem = true;
        try
        {
            UIElement.GetItem.SetOnInitArgs(argumentBox);
            UIManager.Instance.MaskUI(UIElement.GetItem);
        }
        finally
        {
            _showingMergedGetItem = false;
        }
    }

    private static MakeSubPageMake GetActiveMakePage(ViewMake view)
    {
        if (view == null)
            return null;

        var subPages = Traverse.Create(view).Field("subPages").GetValue<MakeSubPage[]>();
        if (subPages == null)
            return null;

        foreach (var subPage in subPages)
        {
            if (subPage is MakeSubPageMake makePage && makePage.gameObject.activeInHierarchy)
                return makePage;
        }

        return null;
    }

    private static ItemDisplayData GetNextMaterial(MakeSubPageMake page)
    {
        var targetSlot = Traverse.Create(page).Field("targetSlot").GetValue<MakeTargetSlot>();
        if (targetSlot == null || !targetSlot.IsValid)
            return null;

        var randomMake = MakeSubPageMakeHelper.CheckIsRandomMake(targetSlot.ItemData);
        var randomMakeSubType = randomMake
            ? Traverse.Create(page).Field("_currentSelectRandomMakeItemSubType").GetValue<short>()
            : (short)-1;

        var allMaterials = Traverse.Create(page).Field("_allMaterialList").GetValue() as IEnumerable;
        if (allMaterials == null)
            return null;

        ItemDisplayData bestMaterial = null;
        foreach (var item in allMaterials)
        {
            if (item is not ItemDisplayData material || !IsMaterialAllowed(material, randomMake, randomMakeSubType))
                continue;

            if (bestMaterial == null || IsBetterMaterial(material, bestMaterial))
                bestMaterial = material;
        }

        return bestMaterial;
    }

    private static bool IsMaterialAllowed(ItemDisplayData material, bool randomMake, short randomMakeSubType)
    {
        if (material == null || material.Amount <= 0)
            return false;

        if (!ContinuousMakeSettingsStore.IsSourceAllowed(material.ItemSourceTypeEnum))
            return false;

        if (randomMake && !MakeSubPageMakeHelper.CheckCanMakeTargetRandomType(randomMakeSubType, material))
            return false;

        var settings = ContinuousMakeSettingsStore.Current;
        var grade = GetMaterialConfigGrade(material);
        var highestRawGrade = DisplayGradeToRaw(settings.HighestMaterialGrade);
        var lowestRawGrade = DisplayGradeToRaw(settings.LowestMaterialGrade);
        return grade <= highestRawGrade && grade >= lowestRawGrade;
    }

    private static bool IsBetterMaterial(ItemDisplayData candidate, ItemDisplayData current)
    {
        var candidateGrade = GetMaterialConfigGrade(candidate);
        var currentGrade = GetMaterialConfigGrade(current);
        if (candidateGrade != currentGrade)
            return candidateGrade < currentGrade;

        return candidate.Value > current.Value;
    }

    private static sbyte GetMaterialConfigGrade(ItemDisplayData material)
    {
        if (material == null)
            return 0;

        try
        {
            return Config.Material.Instance[material.RealKey.TemplateId].Grade;
        }
        catch
        {
            return material.Grade;
        }
    }

    private static int DisplayGradeToRaw(int displayGrade)
    {
        return 9 - Mathf.Clamp(displayGrade, 1, 9);
    }

    private static void SelectMaterial(MakeSubPageMake page, ItemDisplayData material)
    {
        AccessTools.Method(typeof(MakeSubPageMake), "SelectMaterial", new[] { typeof(ItemDisplayData), typeof(bool) })
            ?.Invoke(page, new object[] { material, true });
    }

    private static bool IsConfirmInteractable(MakeSubPageMake page)
    {
        var button = Traverse.Create(page).Field("buttonConfirm").GetValue<CButton>();
        return button != null && button.interactable;
    }

    private static bool IsSelectedToolEmpty(MakeSubPageMake page)
    {
        var toolSlot = Traverse.Create(page).Field("toolSlot").GetValue<MakeTargetSlot>();
        return toolSlot != null && toolSlot.IsValid && ViewMake.IsEmptyTool(toolSlot.ItemData);
    }

    private static bool ApplyDurabilityProtectionMakeCount(MakeSubPageMake page)
    {
        var safeCount = GetSafeMakeCountByDurability(page);
        if (safeCount < 0)
            return true;
        if (safeCount <= 0)
            return false;

        var makeCount = Math.Max(1, (int)Traverse.Create(page).Field("_makeCount").GetValue<short>());
        if (safeCount < makeCount)
            MakeSelectMaterialPatch.SetMakeCount(page, safeCount);

        return true;
    }

    private static int GetSafeMakeCountByDurability(MakeSubPageMake page)
    {
        var toolSlot = Traverse.Create(page).Field("toolSlot").GetValue<MakeTargetSlot>();
        var materialSlot = Traverse.Create(page).Field("materialSlot").GetValue<MakeTargetSlot>();
        if (toolSlot == null || materialSlot == null || !toolSlot.IsValid || !materialSlot.IsValid)
            return -1;

        var tool = toolSlot.ItemData;
        if (tool == null || ViewMake.IsEmptyTool(tool))
            return -1;

        var cost = Traverse.Create(page).Field("_makeToolDurabilityCost").GetValue<int>();
        if (cost <= 0)
            cost = ViewMake.GetToolDurabilityCost(tool, materialSlot.ItemData.Grade);
        if (cost <= 0)
            return -1;

        return Math.Max(0, (tool.Durability - 1) / cost);
    }

    private static bool WouldSelectedToolBreak(MakeSubPageMake page)
    {
        var toolSlot = Traverse.Create(page).Field("toolSlot").GetValue<MakeTargetSlot>();
        var materialSlot = Traverse.Create(page).Field("materialSlot").GetValue<MakeTargetSlot>();
        if (toolSlot == null || materialSlot == null || !toolSlot.IsValid || !materialSlot.IsValid)
            return false;

        var tool = toolSlot.ItemData;
        if (tool == null || ViewMake.IsEmptyTool(tool))
            return false;

        var cost = Traverse.Create(page).Field("_makeToolDurabilityCost").GetValue<int>();
        if (cost <= 0)
            cost = ViewMake.GetToolDurabilityCost(tool, materialSlot.ItemData.Grade);
        if (cost <= 0)
            return false;

        var makeCount = Math.Max(1, (int)Traverse.Create(page).Field("_makeCount").GetValue<short>());
        return tool.Durability <= cost * makeCount;
    }

    private sealed class ResultCollector
    {
        internal readonly List<ItemDisplayData> Items = new();
        internal readonly List<Action> CloseActions = new();
        internal int ExpectedBatchCount;
        internal int CapturedBatchCount;
        internal int LastCaptureFrame;
        internal bool InWarehouse;
        internal bool Finishing;

        internal void AddItems(IEnumerable<ItemDisplayData> items)
        {
            foreach (var item in items)
            {
                if (item == null)
                    continue;

                var existing = Items.Find(data => data != null && data.RealKey.Equals(item.RealKey));
                if (existing != null)
                {
                    existing.Amount += item.Amount;
                    continue;
                }

                Items.Add(item.Clone());
            }
        }
    }
}
