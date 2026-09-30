#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Config;
using HarmonyLib;
using UnityEngine;
using GameData.Domains.Building;
using GameData.Domains.Item.Display;
using Game.Views.Building.BuildingManage;
using Game.Views.Building.BuildingManage.Production;
using Game.Views.Make;
using GameData.Domains.Taiwu.ExchangeSystem;
using ItemListScroll = Game.Components.ListStyleGeneralScroll.Item.ItemListScroll;
using ExchangeContainerView = Game.Views.Exchange.ExchangeContainer;
using ExchangeViewBase = Game.Views.Exchange.ViewExchangeBase;
using FilterConfig = Game.Components.SortAndFilter.SortAndFilterConfig;
using FilterDetailedLineState = Game.Components.SortAndFilter.DetailedFilterLineState;
using FilterDetailedState = Game.Components.SortAndFilter.DetailedFilterState;
using FilterLineState = Game.Components.SortAndFilter.LineState;
using FilterMenuState = Game.Components.SortAndFilter.DetailedFilterMenuState;
using FilterView = Game.Components.SortAndFilter.SortAndFilter;
using SortButtonGroup = Game.Components.SortAndFilter.SortButtonGroup;
using SortItemState = Game.Components.SortAndFilter.SortItemState;
using SortStateData = Game.Components.SortAndFilter.SortStateData;
using FilterToggleKey = Game.Components.SortAndFilter.ToggleKey;
using DebateView = Game.Views.Debate.ViewDebate;
using LifeSkillCombatBeginView = Game.Views.LifeSkillCombat.ViewLifeSkillCombatBegin;
using ToggleGroup = FrameWork.UISystem.UIElements.CToggleGroup;

namespace BetterTaiwuScroll.Frontend;

internal static class BuildingOutputStorageMemoryController
{
    private static readonly FieldInfo HandlerField =
        AccessTools.Field(typeof(ProductionCollectDestination), "_handler");

    private static bool _applyingMemory;
    private static int _userChangeDepth;
    private static bool _installed;

    internal static void Install(Harmony harmony)
    {
        if (_installed || harmony == null)
            return;

        try
        {
            var viewInit = AccessTools.Method(
                typeof(ViewBuildingManage),
                "Init",
                new[] { typeof(FrameWork.ArgumentBox) });
            var destinationRefresh = AccessTools.Method(
                typeof(ProductionCollectDestination),
                "Refresh",
                Type.EmptyTypes);
            var destinationChange = AccessTools.Method(
                typeof(ProductionCollectDestination),
                "DoChangeType",
                new[] { typeof(int) });

            if (viewInit == null || destinationRefresh == null || destinationChange == null)
            {
                Debug.LogWarning(
                    "[BetterTaiwuScroll] Building output storage memory targets were not found "
                    + "(ViewBuildingManage.Init=" + (viewInit != null)
                    + ", ProductionCollectDestination.Refresh=" + (destinationRefresh != null)
                    + ", ProductionCollectDestination.DoChangeType=" + (destinationChange != null) + ").");
                return;
            }

            harmony.Patch(
                viewInit,
                postfix: new HarmonyMethod(
                    typeof(ViewBuildingManageInitStorageMemoryPatch),
                    nameof(ViewBuildingManageInitStorageMemoryPatch.Postfix)));
            harmony.Patch(
                destinationRefresh,
                prefix: new HarmonyMethod(
                    typeof(ProductionCollectDestinationRefreshMemoryPatch),
                    nameof(ProductionCollectDestinationRefreshMemoryPatch.Prefix)));
            harmony.Patch(
                destinationChange,
                prefix: new HarmonyMethod(
                    typeof(ProductionCollectDestinationChangeMemoryPatch),
                    nameof(ProductionCollectDestinationChangeMemoryPatch.Prefix)),
                postfix: new HarmonyMethod(
                    typeof(ProductionCollectDestinationChangeMemoryPatch),
                    nameof(ProductionCollectDestinationChangeMemoryPatch.Postfix)),
                finalizer: new HarmonyMethod(
                    typeof(ProductionCollectDestinationChangeMemoryPatch),
                    nameof(ProductionCollectDestinationChangeMemoryPatch.Finalizer)));

            _installed = true;
            Debug.Log("[BetterTaiwuScroll] Per-building output storage memory patches installed.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to install building output storage memory patches: " + ex);
        }
    }

    internal static void ResetInstallState()
    {
        _installed = false;
        _applyingMemory = false;
        _userChangeDepth = 0;
    }

    internal static void BeginUserChange()
    {
        _userChangeDepth++;
    }

    internal static void EndUserChange()
    {
        if (_userChangeDepth > 0)
            _userChangeDepth--;
    }

    internal static void Restore(ProductionCollectDestination view, IProductionHandler handler)
    {
        if (_applyingMemory || _userChangeDepth > 0 || view == null)
            return;

        try
        {
            handler ??= GetHandler(view);
            if (handler == null || handler.TemplateId < 0 || handler.Key.IsInvalid)
                return;

            var model = SingletonObject.getInstance<BuildingModel>();
            RestoreCore(model, handler.Key, handler.TemplateId, "ProductionCollectDestination");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to restore building output storage memory: " + ex);
        }
    }

    internal static void Restore(ViewBuildingManage view)
    {
        if (_applyingMemory || _userChangeDepth > 0 || view == null)
            return;

        try
        {
            var key = view.BlockKey;
            var data = view.BlockData;
            var template = view.ConfigData;
            if (key.IsInvalid
                || data == null
                || data.TemplateId < 0
                || template == null
                || (!template.ShowItemStoreLocation && !template.ShowResourceStoreLocation))
                return;

            RestoreCore(view.BuildingModel, key, data.TemplateId, "ViewBuildingManage");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to restore building output storage from building view: " + ex);
        }
    }

    internal static void SaveSelection(ProductionCollectDestination view, int newIndex)
    {
        if (_applyingMemory || view == null)
            return;

        try
        {
            var handler = GetHandler(view);
            if (handler == null || handler.TemplateId < 0 || handler.Key.IsInvalid)
                return;

            var current = SingletonObject.getInstance<BuildingModel>()
                .GetBuildingShopEventSetting(handler.BlockIndex);
            var setting = new BuildingResourceOutputSetting(current);

            // GetBuildingShopEventSetting returns an untracked default object when
            // this block index has no dictionary entry yet. Derive the selected
            // value from DoChangeType's argument instead of relying on a second
            // model lookup to return the object mutated by the original method.
            if (view.Type == EProductionCollectDestinationType.Resource)
            {
                var allowedTypes = BuildingResourceOutputSetting.AllowedResourceStorageTypes;
                if (newIndex < 0 || newIndex >= allowedTypes.Count)
                    return;
                setting.ResourceStorage = allowedTypes[newIndex];
            }
            else if (view.Type == EProductionCollectDestinationType.Item)
            {
                var allowedTypes = BuildingResourceOutputSetting.AllowedItemStorageTypes;
                if (newIndex < 0 || newIndex >= allowedTypes.Count)
                    return;
                setting.ItemStorage = allowedTypes[newIndex];
            }
            else
            {
                return;
            }

            BuildingOutputStorageSettingsStore.Save(handler.Key, handler.TemplateId, setting);
            Debug.Log("[BetterTaiwuScroll] Saved building output storage memory for "
                + FormatContext(handler.Key, handler.TemplateId)
                + " (resource=" + setting.ResourceStorage + ", item=" + setting.ItemStorage + ").");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save building output storage memory: " + ex);
        }
    }

    private static void RestoreCore(BuildingModel model, BuildingBlockKey key, short templateId, string source)
    {
        if (model == null || key.IsInvalid || templateId < 0)
            return;

        var current = model.GetBuildingShopEventSetting(key.BuildingBlockIndex);
        var remembered = BuildingOutputStorageSettingsStore.GetFor(key, templateId).ToGameSetting();
        if (!ApplyIfChanged(model, key.BuildingBlockIndex, current, remembered))
            return;

        Debug.Log("[BetterTaiwuScroll] Restored building output storage memory via " + source
            + " for " + FormatContext(key, templateId)
            + " (resource=" + remembered.ResourceStorage + ", item=" + remembered.ItemStorage + ").");
    }

    private static bool ApplyIfChanged(
        BuildingModel model,
        int blockIndex,
        BuildingResourceOutputSetting current,
        BuildingResourceOutputSetting remembered)
    {
        if (current.ResourceStorage == remembered.ResourceStorage
            && current.ItemStorage == remembered.ItemStorage)
            return false;

        _applyingMemory = true;
        try
        {
            var applied = new BuildingResourceOutputSetting(remembered);
            model.BuildingResourceOutputSettings[blockIndex] = applied;
            model.SetBuildingResourceOutputSetting(blockIndex, new BuildingResourceOutputSetting(applied));
        }
        finally
        {
            _applyingMemory = false;
        }

        return true;
    }

    private static IProductionHandler GetHandler(ProductionCollectDestination view)
    {
        return HandlerField?.GetValue(view) as IProductionHandler;
    }

    private static string FormatContext(BuildingBlockKey key, short templateId)
    {
        return key.AreaId + "/" + key.BlockId + "/" + key.BuildingBlockIndex + "/" + templateId;
    }
}

internal static class MakeStorageLocationMemoryController
{
    private static readonly FieldInfo ParentViewField =
        AccessTools.Field(typeof(MakeSubPage), "ParentView");

    private static bool _installed;

    internal static void Install(Harmony harmony)
    {
        if (_installed || harmony == null)
            return;

        try
        {
            var refreshStorageDropdown = AccessTools.Method(
                typeof(MakeSubPageMake),
                "RefreshStorageDropdown",
                Type.EmptyTypes);
            var storageDropdownValueChanged = AccessTools.Method(
                typeof(MakeSubPageMake),
                "OnStorageDropdownValueChanged",
                new[] { typeof(int) });

            if (ParentViewField == null
                || refreshStorageDropdown == null
                || storageDropdownValueChanged == null)
            {
                Debug.LogWarning(
                    "[BetterTaiwuScroll] Make storage memory targets were not found "
                    + "(MakeSubPage.ParentView=" + (ParentViewField != null)
                    + ", MakeSubPageMake.RefreshStorageDropdown=" + (refreshStorageDropdown != null)
                    + ", MakeSubPageMake.OnStorageDropdownValueChanged=" + (storageDropdownValueChanged != null)
                    + ").");
                return;
            }

            harmony.Patch(
                refreshStorageDropdown,
                prefix: new HarmonyMethod(
                    typeof(MakeStorageDropdownRefreshMemoryPatch),
                    nameof(MakeStorageDropdownRefreshMemoryPatch.Prefix)));
            harmony.Patch(
                storageDropdownValueChanged,
                postfix: new HarmonyMethod(
                    typeof(MakeStorageDropdownValueChangedMemoryPatch),
                    nameof(MakeStorageDropdownValueChangedMemoryPatch.Postfix)));

            _installed = true;
            Debug.Log("[BetterTaiwuScroll] Make storage memory patches installed.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to install make storage memory patches: " + ex);
        }
    }

    internal static void ResetInstallState()
    {
        _installed = false;
    }

    internal static void Restore(MakeSubPageMake page)
    {
        if (!TryGetContext(page, out var parentView, out var key, out var templateId))
            return;

        try
        {
            var remembered = MakeStorageLocationSettingsStore.GetFor(key, templateId);
            var displayData = parentView.DisplayData;
            if (displayData == null)
                return;

            var changed = displayData.StoreLocation != remembered;
            displayData.StoreLocation = remembered;

            // The updated game still consumes the global -1 cache entry when the
            // item is actually made. Keep that entry synchronized with the
            // currently open building while leaving the vanilla method intact.
            BuildingDomainMethod.Call.SetStoreLocation(-1, remembered);

            if (changed)
            {
                Debug.Log("[BetterTaiwuScroll] Restored make storage for "
                    + FormatContext(key, templateId) + " = " + remembered + ".");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to restore make storage: " + ex);
        }
    }

    internal static void Save(MakeSubPageMake page, int storageIndex)
    {
        if (!TryGetContext(page, out _, out var key, out var templateId))
            return;

        try
        {
            MakeStorageLocationSettingsStore.Save(key, templateId, storageIndex);
            Debug.Log("[BetterTaiwuScroll] Saved make storage for "
                + FormatContext(key, templateId) + " = " + storageIndex + ".");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save make storage: " + ex);
        }
    }

    private static bool TryGetContext(
        MakeSubPageMake page,
        out ViewMake parentView,
        out BuildingBlockKey key,
        out short templateId)
    {
        parentView = null;
        key = BuildingBlockKey.Invalid;
        templateId = -1;

        if (page == null || ParentViewField == null)
            return false;

        parentView = ParentViewField.GetValue(page) as ViewMake;
        if (parentView == null || parentView.BlockData == null)
            return false;

        key = parentView.BuildingBlockKey;
        templateId = parentView.BlockData.TemplateId;
        return !key.IsInvalid && templateId >= 0;
    }

    private static string FormatContext(BuildingBlockKey key, short templateId)
    {
        return key.AreaId + "/" + key.BlockId + "/" + key.BuildingBlockIndex + "/" + templateId;
    }
}

internal static class MakeStorageDropdownRefreshMemoryPatch
{
    internal static void Prefix(MakeSubPageMake __instance)
    {
        MakeStorageLocationMemoryController.Restore(__instance);
    }
}

internal static class MakeStorageDropdownValueChangedMemoryPatch
{
    internal static void Postfix(MakeSubPageMake __instance, int value)
    {
        MakeStorageLocationMemoryController.Save(__instance, value);
    }
}

internal static class ViewBuildingManageInitStorageMemoryPatch
{
    internal static void Postfix(ViewBuildingManage __instance)
    {
        BuildingOutputStorageMemoryController.Restore(__instance);
    }
}

internal static class ProductionCollectDestinationRefreshMemoryPatch
{
    internal static void Prefix(ProductionCollectDestination __instance)
    {
        // Setup runs only once for this reusable component. Refresh runs after the
        // parent handler has switched to the building that is currently open.
        BuildingOutputStorageMemoryController.Restore(__instance, null);
    }
}

internal static class ProductionCollectDestinationChangeMemoryPatch
{
    internal static void Prefix()
    {
        // The original method calls Refresh before Harmony reaches our Postfix.
        // Suppress restore during that nested Refresh so the new user selection
        // is not replaced with the previously remembered value.
        BuildingOutputStorageMemoryController.BeginUserChange();
    }

    internal static void Postfix(ProductionCollectDestination __instance, int newIndex)
    {
        BuildingOutputStorageMemoryController.SaveSelection(__instance, newIndex);
    }

    internal static void Finalizer()
    {
        BuildingOutputStorageMemoryController.EndUserChange();
    }
}
