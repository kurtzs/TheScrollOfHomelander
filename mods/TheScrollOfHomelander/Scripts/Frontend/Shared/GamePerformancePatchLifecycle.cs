#nullable disable

using System;
using HarmonyLib;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

internal static class GamePerformancePatchLifecycle
{
    private sealed class PatchGroup
    {
        private readonly string _harmonyId;
        private readonly Type[] _patchTypes;
        private readonly Action _reset;
        private Harmony _harmony;

        internal PatchGroup(string harmonyId, Type[] patchTypes, Action reset = null)
        {
            _harmonyId = harmonyId;
            _patchTypes = patchTypes;
            _reset = reset;
        }

        internal void Refresh(bool enabled)
        {
            if (enabled == (_harmony != null))
                return;

            if (!enabled)
            {
                Uninstall();
                return;
            }

            var harmony = new Harmony(_harmonyId);
            try
            {
                AllowInstallation = true;
                foreach (var patchType in _patchTypes)
                    harmony.CreateClassProcessor(patchType).Patch();
                _harmony = harmony;
            }
            catch (Exception ex)
            {
                harmony.UnpatchSelf();
                _reset?.Invoke();
                Debug.LogWarning("[BetterTaiwuScroll] Failed to install performance patch group '"
                    + _harmonyId + "': " + ex);
            }
            finally
            {
                AllowInstallation = false;
            }
        }

        internal void Uninstall()
        {
            _harmony?.UnpatchSelf();
            _harmony = null;
            _reset?.Invoke();
        }
    }

    internal static bool AllowInstallation { get; private set; }

    private static readonly PatchGroup WorldMapMovement = new(
        "taiwu-studio.better-taiwu-scroll.performance.world-map-movement",
        new[]
        {
            typeof(MapElementContainerScaleAllPerformancePatch),
            typeof(WorldMapPathCostPerformancePatch),
            typeof(ViewWorldMapRerenderMovePathPerformancePatch),
            typeof(ViewWorldMapRefreshCricketPlacePerformancePatch),
            typeof(CharacterGetInventoryItemAmountDuringMovementPatch),
            typeof(ViewWorldMapSettlementButtonsDuringMovementPatch),
            typeof(MapRenderSystemRefreshAllMapBlocksPerformancePatch),
            typeof(WorldMapIsAnyWorkerOnMapPerformancePatch),
            typeof(ViewMapBlockCharListRefreshDuringMovementPatch),
            typeof(ViewMapBlockCharListDisableMovementRefreshPatch),
            typeof(ViewWorldMapMoveFinishedCharacterListRefreshPatch),
            typeof(ViewWorldMapMoveStartedCharacterListRefreshPatch),
        },
        WorldMapMovementRefreshSupport.Reset);

    private static readonly PatchGroup FilterOptionCounts = new(
        "taiwu-studio.better-taiwu-scroll.performance.filter-option-counts",
        new[] { typeof(FilterPanelOptionCountPerformancePatch) });

    private static readonly PatchGroup WarehouseRefreshList = new(
        "taiwu-studio.better-taiwu-scroll.performance.warehouse-refresh-list",
        new[]
        {
            typeof(ViewExchangeBaseWarehouseRefreshScopePatch),
            typeof(ViewWarehouseSelfTradeableListCachePatch),
            typeof(ViewWarehouseTargetTradeableListCachePatch),
        },
        WarehouseRefreshListCache.Reset);

    private static readonly PatchGroup ExchangeRedundantRender = new(
        "taiwu-studio.better-taiwu-scroll.performance.exchange-redundant-render",
        new[] { typeof(ViewExchangeBaseRedundantItemListRenderPatch) });

    internal static void Refresh()
    {
        WorldMapMovement.Refresh(Plugin.IsGameWorldMapMovementOptimizationEnabled);
        FilterOptionCounts.Refresh(Plugin.IsGameFilterOptionCountOptimizationEnabled);
        WarehouseRefreshList.Refresh(Plugin.IsGameWarehouseRefreshListCacheEnabled);
        ExchangeRedundantRender.Refresh(Plugin.IsGameExchangeRedundantRenderOptimizationEnabled);
    }

    internal static void Dispose()
    {
        ExchangeRedundantRender.Uninstall();
        WarehouseRefreshList.Uninstall();
        FilterOptionCounts.Uninstall();
        WorldMapMovement.Uninstall();
        AllowInstallation = false;
    }
}
