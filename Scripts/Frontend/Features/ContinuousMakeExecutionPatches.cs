#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using GameData.Domains.Building;
using GameData.Domains.Item.Display;
using GameData.Domains.TaiwuEvent;
using GameData.Serializer;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(MakeSubPageMake), "OnClickButtonConfirm")]
internal static class ContinuousMakeConfirmPatch
{
    private static bool Prefix(MakeSubPageMake __instance)
    {
        return !ContinuousMakeExecutionController.TryStopByConfirmClick(__instance);
    }

    private static void Postfix(MakeSubPageMake __instance)
    {
        ContinuousMakeExecutionController.MarkAfterConfirm(__instance);
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "RefreshButtonConfirm")]
internal static class ContinuousMakeConfirmNativeStatePatch
{
    private static void Postfix(MakeSubPageMake __instance)
    {
        ContinuousMakeExecutionController.RememberNativeConfirmButtonState(__instance);
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "CheckCondition")]
internal static class ContinuousMakeConfirmButtonStatePatch
{
    private static void Postfix(MakeSubPageMake __instance)
    {
        ContinuousMakeExecutionController.RefreshConfirmButtonState(__instance);
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

[HarmonyPatch(typeof(MakeSubPageMake), "Refresh")]
internal static class ContinuousMakeSubPageRefreshPatch
{
    private static void Postfix(MakeSubPageMake __instance)
    {
        ContinuousMakeExecutionController.CleanupAfterSubPageRefresh(__instance);
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "ReloadSlot")]
internal static class ContinuousMakeReloadSlotPatch
{
    private static void Postfix(MakeSubPageMake __instance)
    {
        ContinuousMakeExecutionController.CleanupAfterReloadSlot(__instance);
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "RefreshAllMaterialList")]
internal static class ContinuousMakeRefreshAllMaterialListPatch
{
    private static void Postfix(MakeSubPageMake __instance)
    {
        ContinuousMakeExecutionController.RemoveZeroAmountMaterialsFromAllList(__instance);
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "RefreshMaterialList")]
internal static class ContinuousMakeRefreshMaterialListPatch
{
    private static void Postfix(MakeSubPageMake __instance)
    {
        ContinuousMakeExecutionController.RemoveZeroAmountMaterialsFromVisibleList(__instance);
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "OnItemClickMaterial")]
internal static class ContinuousMakeMaterialClickPatch
{
    private static bool Prefix(MakeSubPageMake __instance, object content)
    {
        return !ContinuousMakeExecutionController.ShouldBlockZeroAmountMaterialClick(__instance, content);
    }
}

[HarmonyPatch(typeof(Game.Components.ListStyleGeneralScroll.Item.ItemListScroll), "Init")]
internal static class ContinuousMakeMaterialListScrollInitPatch
{
    private static void Prefix(Game.Components.ListStyleGeneralScroll.Item.ItemListScroll __instance, string sortSaveKey)
    {
        ContinuousMakeExecutionController.MarkMaterialListScroll(__instance, sortSaveKey);
    }
}

[HarmonyPatch(typeof(Game.Components.ListStyleGeneralScroll.Item.ItemListScroll), "ApplySortAndFilter")]
internal static class ContinuousMakeMaterialListScrollApplySortAndFilterPatch
{
    private static void Postfix(Game.Components.ListStyleGeneralScroll.Item.ItemListScroll __instance)
    {
        ContinuousMakeExecutionController.RemoveZeroAmountMaterialsFromFilteredList(__instance);
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
    private const int MinBatchMakeSpeed = 1;
    private const int MaxBatchMakeSpeed = 20;

    private static readonly Dictionary<ViewMake, MakeSubPageMake> PendingPages = new();
    private static readonly HashSet<ViewMake> AwaitingDataRefreshViews = new();
    private static readonly HashSet<MakeSubPageMake> RunningPages = new();
    private static readonly HashSet<ViewMake> ActiveContinuousViews = new();
    private static readonly HashSet<ViewMake> StopRequestedViews = new();
    private static readonly HashSet<ViewMake> OneShotBatchMakeViews = new();
    private static readonly HashSet<MakeSubPageMake> PendingMaterialSlotCleanupPages = new();
    private static readonly HashSet<Game.Components.ListStyleGeneralScroll.Item.ItemListScroll> MaterialListScrolls = new();
    private static readonly Dictionary<ViewMake, ResultCollector> ResultCollectors = new();
    private static readonly Dictionary<MakeSubPageMake, bool> NativeConfirmInteractable = new();

    private static ViewMake _activeResultView;
    private static bool _suppressNextGetItemMask;
    private static bool _showingMergedGetItem;
    internal static bool IsSelectingMaterialForContinuation { get; private set; }

    internal static bool TryStopByConfirmClick(MakeSubPageMake page)
    {
        var view = MakeSelectMaterialPatch.GetParentView(page);
        if (view == null || !ActiveContinuousViews.Contains(view))
            return false;

        StopRequestedViews.Add(view);
        PendingPages[view] = page;
        RefreshConfirmButtonState(page);
        return true;
    }

    internal static void RememberNativeConfirmButtonState(MakeSubPageMake page)
    {
        var button = GetConfirmButton(page);
        if (page != null && button != null)
            NativeConfirmInteractable[page] = button.interactable;
    }

    internal static void RefreshConfirmButtonState(MakeSubPageMake page)
    {
        var view = MakeSelectMaterialPatch.GetParentView(page);
        if (view == null || !ActiveContinuousViews.Contains(view))
            return;

        var button = GetConfirmButton(page);
        var text = GetConfirmText(page);
        var tip = GetConfirmTip(page);
        var stopRequested = StopRequestedViews.Contains(view);

        if (text != null)
            text.text = ModLocalization.T("Stop Crafting");
        if (button != null)
            button.interactable = !stopRequested;
        if (tip != null)
            tip.enabled = false;
    }

    internal static void MarkAfterConfirm(MakeSubPageMake page)
    {
        if (!ShouldRun(page))
            return;

        var view = MakeSelectMaterialPatch.GetParentView(page);
        if (view != null)
        {
            if (ActiveContinuousViews.Contains(view))
            {
                RefreshConfirmButtonState(page);
                return;
            }

            ActiveContinuousViews.Add(view);
            StopRequestedViews.Remove(view);
            PendingPages[view] = page;
            PendingMaterialSlotCleanupPages.Add(page);
            GetOrCreateCollector(view).ExpectedBatchCount++;
            _activeResultView = view;
            RefreshConfirmButtonState(page);
        }
    }

    internal static void RequestOneShotBatchMake(ViewMake view)
    {
        if (view != null)
            OneShotBatchMakeViews.Add(view);
    }

    internal static void CancelOneShotBatchMake(ViewMake view)
    {
        if (view != null)
            OneShotBatchMakeViews.Remove(view);
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

        if (!ShouldContinue(page) || GetNextMaterial(page) == null)
        {
            CancelExpiredMaterialSelection(page);
            FinishAndShowResults(view, page);
            return;
        }

        page.StartCoroutine(ContinueAfterRefresh(page));
    }

    internal static void CleanupAfterSubPageRefresh(MakeSubPageMake page)
    {
        if (page == null || !PendingMaterialSlotCleanupPages.Remove(page))
            return;

        var canceled = CancelExpiredMaterialSelection(page);
        ForceMaterialListRerender(page);
        if (canceled)
            RefreshMakeCondition(page);
    }

    internal static void CleanupAfterReloadSlot(MakeSubPageMake page)
    {
        CancelExpiredMaterialSelection(page);
    }

    internal static void RemoveZeroAmountMaterialsFromAllList(MakeSubPageMake page)
    {
        RemoveZeroAmountMaterials(page, "_allMaterialList", false);
    }

    internal static void RemoveZeroAmountMaterialsFromVisibleList(MakeSubPageMake page)
    {
        if (!RemoveZeroAmountMaterials(page, "_materialList", true))
            return;

        CancelExpiredMaterialSelection(page);
        RefreshMakeCondition(page);
    }

    internal static bool ShouldBlockZeroAmountMaterialClick(MakeSubPageMake page, object content)
    {
        if (page == null || content is not ItemDisplayData material || material.Amount > 0)
            return false;

        CancelExpiredMaterialSelection(page);
        ForceMaterialListRerender(page);
        RefreshMakeCondition(page);
        return true;
    }

    internal static void MarkMaterialListScroll(Game.Components.ListStyleGeneralScroll.Item.ItemListScroll scroll, string sortSaveKey)
    {
        if (scroll == null)
            return;

        if (string.Equals(sortSaveKey, "MakeSubPageMakeMaterial", StringComparison.Ordinal))
            MaterialListScrolls.Add(scroll);
    }

    internal static void RemoveZeroAmountMaterialsFromFilteredList(Game.Components.ListStyleGeneralScroll.Item.ItemListScroll scroll)
    {
        if (scroll == null || !MaterialListScrolls.Contains(scroll))
            return;

        try
        {
            var traverse = Traverse.Create(scroll);
            var filteredData = traverse.Field("_filteredData").GetValue() as IList;
            if (filteredData == null)
                return;

            var removedAny = false;
            var removedSelected = false;
            var selectedIndex = traverse.Field("_selectedIndex").GetValue<int>();
            for (var i = filteredData.Count - 1; i >= 0; i--)
            {
                if (!IsZeroAmountContent(filteredData[i]))
                    continue;

                if (i == selectedIndex)
                    removedSelected = true;

                filteredData.RemoveAt(i);
                removedAny = true;
            }

            if (removedSelected || selectedIndex >= filteredData.Count)
                traverse.Field("_selectedIndex").SetValue(-1);

            if (removedAny)
                AccessTools.Method(scroll.GetType(), "RefreshEmpty")?.Invoke(scroll, Array.Empty<object>());
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make filtered material list cleanup failed: " + ex.Message);
        }
    }

    private static IEnumerator ContinueAfterRefresh(MakeSubPageMake page)
    {
        RunningPages.Add(page);
        var view = MakeSelectMaterialPatch.GetParentView(page);
        var requestRefresh = false;
        try
        {
            var settings = ContinuousMakeSettingsStore.GetFor(view);
            var batchMakeSpeed = GetBatchMakeSpeed(settings);
            yield return WaitForUiStage(page, batchMakeSpeed, 1, null);

            if (!ShouldRun(page))
                yield break;
            if (IsStopRequested(page))
                yield break;

            var completedInBurst = 0;
            for (var i = 0; i < batchMakeSpeed; i++)
            {
                if (!ShouldRun(page))
                    yield break;
                if (IsStopRequested(page))
                    break;

                var material = GetNextMaterial(page);
                if (material == null)
                    yield break;

                SelectMaterial(page, material);

                yield return WaitForUiStage(
                    page,
                    batchMakeSpeed,
                    GetMinimumStageFrames(batchMakeSpeed, ContinuationStage.AfterMaterialSelect),
                    () => IsSelectedMaterial(page, material));

                if (!ShouldRun(page))
                    yield break;
                if (IsStopRequested(page))
                    break;

                var parentView = MakeSelectMaterialPatch.GetParentView(page);
                if (parentView != null && Plugin.EnableBestTool)
                    AccessTools.Method(typeof(ViewMake), "AutoSelectTool")?.Invoke(parentView, Array.Empty<object>());

                yield return WaitForUiStage(
                    page,
                    batchMakeSpeed,
                    GetMinimumStageFrames(batchMakeSpeed, ContinuationStage.AfterToolSelect),
                    () => IsToolSelectionReady(page, settings));

                if (!ShouldRun(page))
                    yield break;
                if (IsStopRequested(page))
                    break;

                if (Plugin.EnableMaxProductCount)
                    MakeSelectMaterialPatch.SetMakeCountToMax(page);

                yield return WaitForUiStage(page, batchMakeSpeed, 1, () => IsConfirmInteractable(page));

                if (!ShouldRun(page))
                    yield break;
                if (IsStopRequested(page))
                    break;
                if (!IsConfirmInteractable(page))
                    yield break;

                if (!settings.AllowBareHand && IsSelectedToolEmpty(page))
                    yield break;

                if (settings.EnableDurabilityProtection && !ApplyDurabilityProtectionMakeCount(page))
                    yield break;

                if (settings.EnableDurabilityProtection && WouldSelectedToolBreak(page))
                    yield break;

                var makeResultReady = false;
                yield return RefreshCurrentRandomMakeResult(page, material, value => makeResultReady = value);
                if (IsStopRequested(page))
                    break;
                if (!makeResultReady)
                    yield break;

                var makeResult = DirectMakeResult.Fail;
                yield return MakeOnceWithoutViewRefresh(page, material, value => makeResult = value);
                if (!makeResult.Success)
                    yield break;

                completedInBurst++;
                ApplyLocalConsumption(page, material, makeResult.MakeCount);
            }

            requestRefresh = completedInBurst > 0 && CanRequestData(view);
        }
        finally
        {
            RunningPages.Remove(page);
            if (requestRefresh && view != null)
            {
                ClearDisplayData(page);
                PendingMaterialSlotCleanupPages.Add(page);
                PendingPages[view] = page;
                view.RequestData();
            }
            else
            {
                FinishAndShowResults(view, page);
            }
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

        var view = MakeSelectMaterialPatch.GetParentView(page);
        return view != null
            && Plugin.IsEnabledForLifeSkill(view.CurLifeSkillType)
            && (ContinuousMakeUiController.IsContinuousMakeEnabledFor(view) || OneShotBatchMakeViews.Contains(view));
    }

    private static bool ShouldContinue(MakeSubPageMake page)
    {
        return ShouldRun(page) && !IsStopRequested(page);
    }

    private static bool IsStopRequested(MakeSubPageMake page)
    {
        var view = MakeSelectMaterialPatch.GetParentView(page);
        return view != null && StopRequestedViews.Contains(view);
    }

    private static int GetBatchMakeSpeed(ContinuousMakeSettings settings)
    {
        return Mathf.Clamp(settings?.BatchMakeSpeed ?? MinBatchMakeSpeed, MinBatchMakeSpeed, MaxBatchMakeSpeed);
    }

    private static IEnumerator WaitForUiStage(MakeSubPageMake page, int speed, int minimumFrames, Func<bool> ready)
    {
        speed = Mathf.Clamp(speed, MinBatchMakeSpeed, MaxBatchMakeSpeed);
        minimumFrames = Mathf.Max(0, minimumFrames);
        for (var frame = 0; frame < minimumFrames; frame++)
        {
            if (!ShouldContinue(page))
                yield break;

            yield return null;
        }

        if (ready == null)
            yield break;

        var budget = GetUiWaitFrameBudget(speed);
        for (var frame = minimumFrames; frame < budget; frame++)
        {
            if (!ShouldContinue(page) || ready())
                yield break;

            yield return null;
        }
    }

    private static int GetUiWaitFrameBudget(int speed)
    {
        speed = Mathf.Clamp(speed, MinBatchMakeSpeed, MaxBatchMakeSpeed);
        return Mathf.RoundToInt(Mathf.Lerp(18f, 8f, (speed - MinBatchMakeSpeed) / (float)(MaxBatchMakeSpeed - MinBatchMakeSpeed)));
    }

    private static int GetMinimumStageFrames(int speed, ContinuationStage stage)
    {
        speed = Mathf.Clamp(speed, MinBatchMakeSpeed, MaxBatchMakeSpeed);
        if (speed <= 1)
            return 1;

        return stage switch
        {
            ContinuationStage.AfterMaterialSelect => speed >= 7 ? 0 : 1,
            ContinuationStage.AfterToolSelect => speed >= 4 ? 0 : 1,
            _ => 1
        };
    }

    private static bool CanStartCoroutine(MakeSubPageMake page)
    {
        return page != null && page.gameObject != null && page.gameObject.activeInHierarchy;
    }

    private static bool CanRequestData(ViewMake view)
    {
        return view != null && view.gameObject != null && view.gameObject.activeInHierarchy;
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
        var activePage = GetActiveMakePage(view) ?? coroutineOwner as MakeSubPageMake;
        EndContinuousMake(view, activePage);

        if (view == null || !ResultCollectors.TryGetValue(view, out var collector))
            return;

        if (collector.Finishing)
            return;

        collector.Finishing = true;
        PendingPages.Remove(view);
        AwaitingDataRefreshViews.Remove(view);
        if (activePage != null)
            PendingMaterialSlotCleanupPages.Remove(activePage);

        if (coroutineOwner != null && coroutineOwner.gameObject != null && coroutineOwner.gameObject.activeInHierarchy)
            coroutineOwner.StartCoroutine(ShowCollectedResultsWhenReady(view, collector));
        else
            ShowCollectedResults(view, collector);
    }

    private static void EndContinuousMake(ViewMake view, MakeSubPageMake page)
    {
        if (view != null)
        {
            ActiveContinuousViews.Remove(view);
            StopRequestedViews.Remove(view);
            OneShotBatchMakeViews.Remove(view);
            PendingPages.Remove(view);
            AwaitingDataRefreshViews.Remove(view);
        }

        if (page != null)
        {
            NativeConfirmInteractable.Remove(page);
            RefreshMakeCondition(page);
        }
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
            if (item is not ItemDisplayData material || !IsMaterialAllowed(page, material, randomMake, randomMakeSubType))
                continue;

            if (bestMaterial == null || IsBetterMaterial(material, bestMaterial))
                bestMaterial = material;
        }

        return bestMaterial;
    }

    private static bool IsMaterialAllowed(MakeSubPageMake page, ItemDisplayData material, bool randomMake, short randomMakeSubType)
    {
        if (material == null || material.Amount <= 0)
            return false;

        var view = MakeSelectMaterialPatch.GetParentView(page);
        if (view == null)
            return false;

        if (!ContinuousMakeSettingsStore.IsSourceAllowed(view.CurLifeSkillType, material.ItemSourceTypeEnum))
            return false;

        if (!IsMaterialCompatibleWithTarget(page, material, randomMake, randomMakeSubType))
            return false;

        var settings = ContinuousMakeSettingsStore.GetFor(view);
        var grade = GetMaterialConfigGrade(material);
        var highestRawGrade = DisplayGradeToRaw(settings.HighestMaterialGrade);
        var lowestRawGrade = DisplayGradeToRaw(settings.LowestMaterialGrade);
        return grade <= highestRawGrade && grade >= lowestRawGrade;
    }

    private static bool IsMaterialCompatibleWithTarget(MakeSubPageMake page, ItemDisplayData material, bool randomMake, short randomMakeSubType)
    {
        if (page == null || material == null)
            return false;

        if (randomMake)
            return MakeSubPageMakeHelper.CheckCanMakeTargetRandomType(randomMakeSubType, material);

        var targetSlot = Traverse.Create(page).Field("targetSlot").GetValue<MakeTargetSlot>();
        if (targetSlot == null || !targetSlot.IsValid || targetSlot.ItemData == null)
            return false;

        var target = targetSlot.ItemData;
        if (MakeSubPageMakeHelper.CheckIsRandomMake(target))
            return MakeSubPageMakeHelper.CheckCanMakeTargetRandomType(randomMakeSubType, material);

        if (material.RealKey.ItemType == 12)
            return true;

        try
        {
            var makeItemTypeId = Traverse.Create(page).Field("_makeItemTypeId").GetValue<short>();
            if (makeItemTypeId < 0)
                return false;

            var makeItemType = Config.MakeItemType.Instance[makeItemTypeId];
            var materialConfig = Config.Material.Instance[material.RealKey.TemplateId];
            if (makeItemType == null || materialConfig == null || !materialConfig.CraftableItemTypes.Contains(makeItemType.TemplateId))
                return false;

            var targetGrade = target.Grade;
            var resultGrade = (sbyte)0;
            var requiredAttainment = (short)0;
            var displayData = Traverse.Create(page).Field("DisplayData").GetValue<GameData.Domains.Building.BuildingMakeDisplayData>();
            var buildingUpgradeMakeItem = displayData != null && displayData.BuildingUpgradeMakeItem;

            if (target.RealKey.ItemType == 7)
            {
                resultGrade = targetGrade;
                requiredAttainment = materialConfig.RequiredAttainment;
            }
            else
            {
                var parentView = MakeSelectMaterialPatch.GetParentView(page);
                if (parentView == null)
                    return false;

                var isManual = Traverse.Create(page).Field("_isManual").GetValue<bool>();
                var makeItemSubTypeId = isManual
                    ? Traverse.Create(page).Field("_makeItemSubTypeId").GetValue<short>()
                    : (short)-1;
                var maxFinalAttainment = Traverse.Create(page).Field("_maxFinalAttainment").GetValue<int>();
                var cookingSkillBookCount = displayData?.AllPagesReadCookingSkillBookCount ?? 0;
                var buildingAttainmentEffect = displayData?.BuildingAttainmentEffect ?? 0;

                requiredAttainment = GameData.Domains.Building.SharedMethods.GetMaterialGradeAndAttainment(
                    material.RealKey.TemplateId,
                    target.RealKey.ItemType,
                    parentView.CurLifeSkillType,
                    maxFinalAttainment,
                    makeItemType.MakeItemSubTypes,
                    out resultGrade,
                    out _,
                    cookingSkillBookCount,
                    makeItemSubTypeId,
                    buildingAttainmentEffect);
            }

            var range = GameData.Domains.Building.SharedMethods.GetMakeResultGradeRange(resultGrade, target.RealKey.ItemType);
            var gradeMatchesTarget = targetGrade >= range.Item1 && targetGrade <= range.Item2;
            if (targetGrade == range.Item2 && !buildingUpgradeMakeItem && materialConfig.Transferable)
                gradeMatchesTarget = false;

            var maxFinal = Traverse.Create(page).Field("_maxFinalAttainment").GetValue<int>();
            return gradeMatchesTarget && maxFinal >= requiredAttainment;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make target/material compatibility check failed: " + ex.Message);
            return false;
        }
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
        var nativeOptions = MakePageNativeOptionSnapshot.Capture(page);
        IsSelectingMaterialForContinuation = true;
        try
        {
            AccessTools.Method(typeof(MakeSubPageMake), "SelectMaterial", new[] { typeof(ItemDisplayData), typeof(bool) })
                ?.Invoke(page, new object[] { material, true });
        }
        finally
        {
            IsSelectingMaterialForContinuation = false;
            nativeOptions.Restore(page);
        }
    }

    private static IEnumerator MakeOnceWithoutViewRefresh(MakeSubPageMake page, ItemDisplayData selectedMaterial, Action<DirectMakeResult> setResult)
    {
        setResult(DirectMakeResult.Fail);
        var view = MakeSelectMaterialPatch.GetParentView(page);
        if (page == null || view == null || selectedMaterial == null)
            yield break;

        DirectMakeArguments arguments;
        try
        {
            if (!TryBuildDirectMakeArguments(page, view, out arguments))
                yield break;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to build continuous make arguments: " + ex.Message);
            yield break;
        }

        var checkDone = false;
        var canMake = false;
        try
        {
            BuildingDomainMethod.AsyncCall.CheckMakeCondition(view, arguments.Condition, (offset, dataPool) =>
            {
                try
                {
                    Serializer.Deserialize(dataPool, offset, ref canMake);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[BetterTaiwuScroll] Continuous make condition deserialize failed: " + ex.Message);
                }
                finally
                {
                    checkDone = true;
                }
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make condition request failed: " + ex.Message);
            yield break;
        }

        for (var i = 0; i < 120 && !checkDone; i++)
            yield return null;

        if (!checkDone || !canMake)
        {
            UIElement.FullScreenMask.Hide();
            yield break;
        }

        if (!ShouldContinue(page))
            yield break;

        try
        {
            BuildingDomainMethod.Call.StartMakeItem(view.Element.GameDataListenerId, arguments.Start);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make start failed: " + ex.Message);
            yield break;
        }

        var itemsDone = false;
        List<ItemDisplayData> itemDataList = null;
        try
        {
            BuildingDomainMethod.AsyncCall.GetMakeItems(view, view.BuildingBlockKey, (offset, pool) =>
            {
                try
                {
                    Serializer.Deserialize(pool, offset, ref itemDataList);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[BetterTaiwuScroll] Continuous make item deserialize failed: " + ex.Message);
                }
                finally
                {
                    itemsDone = true;
                }
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make item request failed: " + ex.Message);
            yield break;
        }

        for (var i = 0; i < 120 && !itemsDone; i++)
            yield return null;

        if (!itemsDone)
            yield break;

        try
        {
            TaiwuEventDomainMethod.Call.OnCollectedMakingSystemItem(view.BuildingBlockKey, view.BlockData.TemplateId, showingGetItem: true);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make collect event failed: " + ex.Message);
        }

        var collector = GetOrCreateCollector(view);
        collector.ExpectedBatchCount++;
        collector.CapturedBatchCount++;
        collector.LastCaptureFrame = Time.frameCount;
        collector.InWarehouse = arguments.InWarehouse;
        collector.AddItems(itemDataList);
        AddTutorialCloseAction(page, collector, itemDataList);
        SaveLastMakeResourceCounts(page, arguments.ResourceCount);
        setResult(new DirectMakeResult(true, arguments.MakeCount));
    }

    private static bool TryBuildDirectMakeArguments(MakeSubPageMake page, ViewMake view, out DirectMakeArguments arguments)
    {
        arguments = default;
        var traverse = Traverse.Create(page);
        var targetSlot = traverse.Field("targetSlot").GetValue<MakeTargetSlot>();
        var materialSlot = traverse.Field("materialSlot").GetValue<MakeTargetSlot>();
        var toolSlot = traverse.Field("toolSlot").GetValue<MakeTargetSlot>();
        if (targetSlot == null || materialSlot == null || toolSlot == null
            || !targetSlot.IsValid || !materialSlot.IsValid || !toolSlot.IsValid)
            return false;

        var makeCount = Math.Max(1, (int)traverse.Field("_makeCount").GetValue<short>());
        var makeItemTypeId = traverse.Field("_makeItemTypeId").GetValue<short>();
        var makeItemSubTypeId = traverse.Field("_makeItemSubTypeId").GetValue<short>();
        if (makeItemTypeId < 0 || makeItemSubTypeId < 0)
            return false;

        var resourceCount = traverse.Field("_curMakeResourceCountInts").GetValue<GameData.Domains.Character.ResourceInts>();
        var needResource = traverse.Field("_makeRequiredResourceInts").GetValue<GameData.Domains.Character.ResourceInts>();
        var isManual = traverse.Field("_isManual").GetValue<bool>();
        var isPerfect = targetSlot.IsToggleOn;
        var itemList = BuildMakeResultTemplateList(page, targetSlot, makeCount);
        if (itemList == null || itemList.Count == 0)
            return false;

        var condition = new MakeConditionArguments
        {
            BuildingBlockKey = view.BuildingBlockKey,
            CharId = view.TaiwuCharId,
            IsManual = isManual,
            MakeCount = (short)makeCount,
            MakeItemSubTypeId = makeItemSubTypeId,
            MakeItemTypeId = makeItemTypeId,
            MaterialKey = materialSlot.ItemData.RealKey,
            ResourceCount = resourceCount,
            ToolKey = toolSlot.ItemData.RealKey,
            IsPerfect = isPerfect
        };

        var makeItemSubTypeItem = Config.MakeItemSubType.Instance[makeItemSubTypeId];
        var start = new StartMakeArguments
        {
            CharId = view.TaiwuCharId,
            BuildingBlockKey = view.BuildingBlockKey,
            Tool = toolSlot.ItemData?.Clone(),
            Material = materialSlot.ItemData?.Clone(),
            ItemList = itemList,
            ItemType = makeItemSubTypeItem.Result.ItemType,
            MakeItemSubTypeId = makeItemSubTypeId,
            ResourceCount = resourceCount,
            NeedResource = needResource,
            EquipmentEffectId = GetPerfectEffectId(page, traverse, isPerfect)
        };

        var displayData = traverse.Field("DisplayData").GetValue<BuildingMakeDisplayData>();
        arguments = new DirectMakeArguments(condition, start, resourceCount, makeCount, displayData != null && !displayData.CanTransferItemToWarehouse);
        return true;
    }

    private static List<short> BuildMakeResultTemplateList(MakeSubPageMake page, MakeTargetSlot targetSlot, int makeCount)
    {
        var result = new List<short>(makeCount);
        var randomMake = MakeSubPageMakeHelper.CheckIsRandomMake(targetSlot.ItemData);
        if (!randomMake)
        {
            for (var i = 0; i < makeCount; i++)
                result.Add(targetSlot.ItemData.RealKey.TemplateId);
            return result;
        }

        var makeResult = Traverse.Create(page).Property("CurMakeResult").GetValue<MakeResult>();
        var stage = makeResult.TargetResultStage;
        for (var i = 0; i < makeCount; i++)
        {
            if (stage.TemplateId >= 0)
            {
                result.Add(stage.TemplateId);
                continue;
            }

            if (stage.TemplateIdList == null || stage.TemplateIdList.Count == 0)
                return null;

            result.Add(stage.TemplateIdList[UnityEngine.Random.Range(0, stage.TemplateIdList.Count)]);
        }

        return result;
    }

    private static short GetPerfectEffectId(MakeSubPageMake page, Traverse traverse, bool isPerfect)
    {
        if (!isPerfect)
            return -1;

        var perfectEffectIdList = traverse.Field("_perfectEffectIdList").GetValue<List<short>>();
        var perfectDropdown = traverse.Field("perfectDropdown").GetValue<CDropdown>();
        if (perfectEffectIdList == null || perfectDropdown == null || perfectEffectIdList.Count == 0)
            return -1;

        var index = Mathf.Clamp(perfectDropdown.value, 0, perfectEffectIdList.Count - 1);
        return perfectEffectIdList[index];
    }

    private static void ApplyLocalConsumption(MakeSubPageMake page, ItemDisplayData material, int makeCount)
    {
        var materialDepleted = false;
        if (material != null)
        {
            material.Amount = Math.Max(0, material.Amount - makeCount);
            materialDepleted = material.Amount <= 0;
        }

        var traverse = Traverse.Create(page);
        var toolSlot = traverse.Field("toolSlot").GetValue<MakeTargetSlot>();
        var materialSlot = traverse.Field("materialSlot").GetValue<MakeTargetSlot>();
        var tool = toolSlot?.ItemData;
        if (tool == null || materialSlot == null || !materialSlot.IsValid || ViewMake.IsEmptyTool(tool))
        {
            if (materialDepleted)
                CleanupDepletedMaterialAfterLocalConsumption(page);
            return;
        }

        var cost = traverse.Field("_makeToolDurabilityCost").GetValue<int>();
        if (cost <= 0)
            cost = ViewMake.GetToolDurabilityCost(tool, materialSlot.ItemData.Grade);
        if (cost <= 0)
        {
            if (materialDepleted)
                CleanupDepletedMaterialAfterLocalConsumption(page);
            return;
        }

        tool.Durability = (short)Mathf.Max(0, tool.Durability - cost * makeCount);

        if (materialDepleted)
            CleanupDepletedMaterialAfterLocalConsumption(page);
    }

    private static void AddTutorialCloseAction(MakeSubPageMake page, ResultCollector collector, List<ItemDisplayData> itemDataList)
    {
        if (page == null || collector == null || itemDataList == null || itemDataList.Count == 0)
            return;

        var tutorialMethod = AccessTools.Method(typeof(MakeSubPageMake), "Tutorial");
        if (tutorialMethod == null)
            return;

        var items = new List<ItemDisplayData>(itemDataList);
        collector.CloseActions.Add(() =>
        {
            if (page == null)
                return;

            try
            {
                tutorialMethod.Invoke(page, new object[] { items });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BetterTaiwuScroll] Continuous make tutorial action failed: " + ex.Message);
            }
        });
    }

    private static void SaveLastMakeResourceCounts(MakeSubPageMake page, GameData.Domains.Character.ResourceInts resourceCount)
    {
        try
        {
            var traverse = Traverse.Create(page);
            var last = traverse.Field("_lastMakeResourceCountInts").GetValue<GameData.Domains.Character.ResourceInts>();
            last.Initialize();
            last.Add(ref resourceCount);
            traverse.Field("_lastMakeResourceCountInts").SetValue(last);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make resource count memory failed: " + ex.Message);
        }
    }

    private static void ClearDisplayData(MakeSubPageMake page)
    {
        try
        {
            Traverse.Create(page).Field("DisplayData").GetValue<BuildingMakeDisplayData>()?.Clear();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make display data clear failed: " + ex.Message);
        }
    }

    private static bool CancelExpiredMaterialSelection(MakeSubPageMake page)
    {
        try
        {
            var materialSlot = Traverse.Create(page).Field("materialSlot").GetValue<MakeTargetSlot>();
            var selectedMaterial = materialSlot?.ItemData;
            if (materialSlot != null
                && materialSlot.IsValid
                && selectedMaterial != null
                && (selectedMaterial.Amount <= 0 || !HasVisibleAvailableSelectedMaterial(page, selectedMaterial)))
            {
                materialSlot.Cancel();
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make material selection cleanup failed: " + ex.Message);
        }

        return false;
    }

    private static bool HasVisibleAvailableSelectedMaterial(MakeSubPageMake page, ItemDisplayData selectedMaterial)
    {
        var materialList = Traverse.Create(page).Field("_materialList").GetValue() as IEnumerable;
        if (materialList == null)
            return false;

        foreach (var item in materialList)
        {
            if (item is ItemDisplayData material
                && material.RealKey == selectedMaterial.RealKey
                && material.ItemSourceTypeEnum == selectedMaterial.ItemSourceTypeEnum
                && material.Amount > 0)
                return true;
        }

        return false;
    }

    private static bool RemoveZeroAmountMaterials(MakeSubPageMake page, string fieldName, bool updateScroll)
    {
        try
        {
            var list = Traverse.Create(page).Field(fieldName).GetValue<List<ItemDisplayData>>();
            if (list == null)
                return false;

            var removed = list.RemoveAll(item => item == null || item.Amount <= 0);
            if (removed <= 0)
                return false;

            if (updateScroll)
            {
                ForceMaterialListRerender(page);
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make zero amount material filter failed: " + ex.Message);
            return false;
        }
    }

    private static bool IsZeroAmountContent(object content)
    {
        if (content == null)
            return true;

        if (content is ItemDisplayData item)
            return item.Amount <= 0;

        try
        {
            var property = AccessTools.Property(content.GetType(), "Amount");
            if (property == null)
                return false;

            var value = property.GetValue(content, null);
            return value != null && Convert.ToInt32(value) <= 0;
        }
        catch
        {
            return false;
        }
    }

    private static void ForceMaterialListRerender(MakeSubPageMake page)
    {
        try
        {
            var materialListScroll = Traverse.Create(page).Field("materialListScroll").GetValue();
            if (materialListScroll == null)
                return;

            var refreshList = AccessTools.Method(materialListScroll.GetType(), "RefreshList");
            if (refreshList != null)
            {
                refreshList.Invoke(materialListScroll, Array.Empty<object>());
                return;
            }

            AccessTools.Method(materialListScroll.GetType(), "ReRender")?.Invoke(materialListScroll, Array.Empty<object>());
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make material list rerender failed: " + ex.Message);
        }
    }

    private static void CleanupDepletedMaterialAfterLocalConsumption(MakeSubPageMake page)
    {
        if (page == null)
            return;

        RemoveZeroAmountMaterials(page, "_allMaterialList", false);
        if (RemoveZeroAmountMaterials(page, "_materialList", true))
        {
            CancelExpiredMaterialSelection(page);
            RefreshMakeCondition(page);
        }
    }

    private static void RefreshMakeCondition(MakeSubPageMake page)
    {
        try
        {
            AccessTools.Method(typeof(MakeSubPageMake), "CheckCondition")?.Invoke(page, Array.Empty<object>());
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make condition refresh failed: " + ex.Message);
        }
    }

    private static bool IsToolSelectionReady(MakeSubPageMake page, ContinuousMakeSettings settings)
    {
        if (page == null)
            return false;

        var toolSlot = Traverse.Create(page).Field("toolSlot").GetValue<MakeTargetSlot>();
        if (toolSlot == null || !toolSlot.IsValid)
            return false;

        return settings == null || settings.AllowBareHand || !ViewMake.IsEmptyTool(toolSlot.ItemData);
    }

    private static IEnumerator RefreshCurrentRandomMakeResult(MakeSubPageMake page, ItemDisplayData selectedMaterial, Action<bool> setResult)
    {
        setResult(false);

        var targetSlot = Traverse.Create(page).Field("targetSlot").GetValue<MakeTargetSlot>();
        if (targetSlot == null || !targetSlot.IsValid || !MakeSubPageMakeHelper.CheckIsRandomMake(targetSlot.ItemData))
        {
            setResult(IsSelectedMaterial(page, selectedMaterial));
            yield break;
        }

        if (!IsSelectedMaterial(page, selectedMaterial))
            yield break;

        var parentView = MakeSelectMaterialPatch.GetParentView(page);
        var materialSlot = Traverse.Create(page).Field("materialSlot").GetValue<MakeTargetSlot>();
        var toolSlot = Traverse.Create(page).Field("toolSlot").GetValue<MakeTargetSlot>();
        var makeItemSubtypeIdList = Traverse.Create(page).Field("_makeItemSubtypeIdList").GetValue<List<short>>();
        if (parentView == null || materialSlot == null || !materialSlot.IsValid || makeItemSubtypeIdList == null || makeItemSubtypeIdList.Count == 0)
            yield break;

        var isManual = Traverse.Create(page).Field("_isManual").GetValue<bool>();
        var makeItemSubTypeId = isManual ? Traverse.Create(page).Field("_makeItemSubTypeId").GetValue<short>() : (short)-1;
        var materialTemplateId = materialSlot.ItemData.RealKey.TemplateId;
        var toolKey = toolSlot?.ItemData?.Key ?? GameData.Domains.Item.ItemKey.Invalid;
        var subtypeIdListSnapshot = new List<short>(makeItemSubtypeIdList);
        var done = false;
        var callbackSucceeded = false;
        var makeResult = default(GameData.Domains.Building.MakeResult);

        try
        {
            GameData.Domains.Building.BuildingDomainMethod.AsyncCall.GetMakeResult(
                parentView,
                materialTemplateId,
                toolKey,
                parentView.BuildingBlockKey,
                parentView.CurLifeSkillType,
                subtypeIdListSnapshot,
                makeItemSubTypeId,
                targetSlot.IsToggleOn,
                isManual,
                (offset, pool) =>
                {
                    try
                    {
                        GameData.Serializer.Serializer.Deserialize(pool, offset, ref makeResult);
                        callbackSucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[BetterTaiwuScroll] Continuous make result deserialize failed: " + ex.Message);
                    }
                    finally
                    {
                        done = true;
                    }
                });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make result request failed: " + ex.Message);
            yield break;
        }

        for (var i = 0; i < 90 && !done; i++)
            yield return null;

        if (!done || !callbackSucceeded)
            yield break;

        if (!ShouldContinue(page) || !IsSelectedMaterial(page, selectedMaterial))
            yield break;

        if (!IsValidRandomMakeResult(makeResult))
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make stopped because random make result is not ready.");
            yield break;
        }

        var resultDict = Traverse.Create(page).Field("_makeResultDict").GetValue<Dictionary<int, GameData.Domains.Building.MakeResult>>();
        if (resultDict == null)
            yield break;

        resultDict.Clear();
        resultDict[makeItemSubTypeId] = makeResult;
        setResult(true);
    }

    private static bool IsSelectedMaterial(MakeSubPageMake page, ItemDisplayData material)
    {
        if (page == null || material == null || material.Amount <= 0)
            return false;

        var materialSlot = Traverse.Create(page).Field("materialSlot").GetValue<MakeTargetSlot>();
        var selected = materialSlot?.ItemData;
        return selected != null
            && selected.RealKey.Equals(material.RealKey)
            && selected.ItemSourceTypeEnum == material.ItemSourceTypeEnum;
    }

    private static bool IsValidRandomMakeResult(GameData.Domains.Building.MakeResult makeResult)
    {
        var stage = makeResult.TargetResultStage;
        if (!stage.IsInit)
            return false;

        return stage.TemplateId >= 0 || (stage.TemplateIdList != null && stage.TemplateIdList.Count > 0);
    }

    private static bool IsConfirmInteractable(MakeSubPageMake page)
    {
        var view = MakeSelectMaterialPatch.GetParentView(page);
        if (view != null
            && ActiveContinuousViews.Contains(view)
            && NativeConfirmInteractable.TryGetValue(page, out var nativeInteractable))
            return nativeInteractable;

        var button = GetConfirmButton(page);
        return button != null && button.interactable;
    }

    private static CButton GetConfirmButton(MakeSubPageMake page)
    {
        return page != null ? Traverse.Create(page).Field("buttonConfirm").GetValue<CButton>() : null;
    }

    private static TextMeshProUGUI GetConfirmText(MakeSubPageMake page)
    {
        return page != null ? Traverse.Create(page).Field("textConfirm").GetValue<TextMeshProUGUI>() : null;
    }

    private static TooltipInvoker GetConfirmTip(MakeSubPageMake page)
    {
        return page != null ? Traverse.Create(page).Field("tipConfirm").GetValue<TooltipInvoker>() : null;
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

    private readonly struct DirectMakeArguments
    {
        internal readonly MakeConditionArguments Condition;
        internal readonly StartMakeArguments Start;
        internal readonly GameData.Domains.Character.ResourceInts ResourceCount;
        internal readonly int MakeCount;
        internal readonly bool InWarehouse;

        internal DirectMakeArguments(
            MakeConditionArguments condition,
            StartMakeArguments start,
            GameData.Domains.Character.ResourceInts resourceCount,
            int makeCount,
            bool inWarehouse)
        {
            Condition = condition;
            Start = start;
            ResourceCount = resourceCount;
            MakeCount = makeCount;
            InWarehouse = inWarehouse;
        }
    }

    private readonly struct DirectMakeResult
    {
        internal static readonly DirectMakeResult Fail = new DirectMakeResult(false, 0);

        internal readonly bool Success;
        internal readonly int MakeCount;

        internal DirectMakeResult(bool success, int makeCount)
        {
            Success = success;
            MakeCount = makeCount;
        }
    }

    private enum ContinuationStage
    {
        AfterMaterialSelect,
        AfterToolSelect
    }

    internal sealed class MakePageNativeOptionSnapshot
    {
        private bool _valid;
        private bool _isPerfect;
        private int _perfectDropdownValue;
        private GameData.Domains.Character.ResourceInts _currentResources;

        internal static MakePageNativeOptionSnapshot Capture(MakeSubPageMake page)
        {
            var snapshot = new MakePageNativeOptionSnapshot();
            if (page == null)
                return snapshot;

            try
            {
                var traverse = Traverse.Create(page);
                var targetSlot = traverse.Field("targetSlot").GetValue<MakeTargetSlot>();
                var perfectDropdown = traverse.Field("perfectDropdown").GetValue<CDropdown>();

                snapshot._isPerfect = targetSlot != null && targetSlot.IsToggleOn;
                snapshot._perfectDropdownValue = perfectDropdown != null ? perfectDropdown.value : 0;
                snapshot._currentResources = traverse.Field("_curMakeResourceCountInts").GetValue<GameData.Domains.Character.ResourceInts>();
                snapshot._valid = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BetterTaiwuScroll] Continuous make native option capture failed: " + ex.Message);
            }

            return snapshot;
        }

        internal void Restore(MakeSubPageMake page)
        {
            if (!_valid || page == null)
                return;

            try
            {
                var traverse = Traverse.Create(page);
                if (TryNormalizeResources(traverse, _currentResources, out var resources))
                {
                    traverse.Field("_curMakeResourceCountInts").SetValue(resources);
                    traverse.Field("_lastMakeResourceCountInts").SetValue(resources);
                }

                var targetSlot = traverse.Field("targetSlot").GetValue<MakeTargetSlot>();
                if (targetSlot != null)
                {
                    targetSlot.IsToggleOn = _isPerfect;
                    if (targetSlot.IsValid)
                        targetSlot.Refresh();
                }

                RestorePerfectDropdown(page, traverse);
                AccessTools.Method(typeof(MakeSubPageMake), "CheckCondition")?.Invoke(page, Array.Empty<object>());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BetterTaiwuScroll] Continuous make native option restore failed: " + ex.Message);
            }
        }

        private static bool TryNormalizeResources(Traverse traverse, GameData.Domains.Character.ResourceInts source, out GameData.Domains.Character.ResourceInts result)
        {
            result = default;
            var maxResources = traverse.Field("_maxMakeResourceCountInts").GetValue<GameData.Domains.Character.ResourceInts>();
            var maxTotal = traverse.Field("_maxMakeResourceTotalCount").GetValue<int>();
            if (maxTotal <= 0)
                return false;

            var sum = 0;
            for (var i = 0; i < 6; i++)
            {
                var value = Mathf.Clamp(source.Get(i), 0, maxResources.Get(i));
                result.Set(i, value);
                sum += value;
            }

            if (sum != maxTotal)
            {
                var mainResourceType = traverse.Field("_mainRequiredResourceType").GetValue<sbyte>();
                if (mainResourceType < 0 || mainResourceType >= 6)
                    return false;

                var currentMainValue = result.Get(mainResourceType);
                var adjustedMainValue = Mathf.Clamp(currentMainValue + maxTotal - sum, 0, maxResources.Get(mainResourceType));
                result.Set(mainResourceType, adjustedMainValue);
                sum += adjustedMainValue - currentMainValue;
            }

            return sum == maxTotal;
        }

        private void RestorePerfectDropdown(MakeSubPageMake page, Traverse traverse)
        {
            var perfectDropdown = traverse.Field("perfectDropdown").GetValue<CDropdown>();
            if (perfectDropdown == null || !perfectDropdown.gameObject.activeSelf || perfectDropdown.options == null || perfectDropdown.options.Count == 0)
                return;

            var value = Mathf.Clamp(_perfectDropdownValue, 0, perfectDropdown.options.Count - 1);
            perfectDropdown.SetValueWithoutNotify(value);
            AccessTools.Method(typeof(MakeSubPageMake), "OnPerfectDropdownValueChanged")?.Invoke(page, new object[] { value });
        }
    }
}

