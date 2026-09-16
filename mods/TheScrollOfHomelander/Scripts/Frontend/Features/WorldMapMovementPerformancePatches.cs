#nullable disable

using System;
using System.Collections.Generic;
using Game.Views.Map;
using Game.Views.MapBlockCharList;
using GameData.Domains.Character;
using GameData.Domains.Map;
using GameData.Domains.Taiwu;
using HarmonyLib;
using Map.RenderSystem;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(MapElementContainer), "ScaleAll", new[] { typeof(float) })]
internal static class MapElementContainerScaleAllPerformancePatch
{
    [HarmonyPrepare]
    private static bool Prepare() => GamePerformancePatchLifecycle.AllowInstallation;

    private static readonly AccessTools.FieldRef<MapElementContainer, float> ScaleCacheRef =
        AccessTools.FieldRefAccess<MapElementContainer, float>("_scaleCache");

    private static bool Prefix(MapElementContainer __instance, float scale)
    {
        if (!Plugin.IsGameWorldMapMovementOptimizationEnabled || __instance == null)
            return true;

        // CheckUpdate() applies the cached scale to newly created/refreshed elements.
        // Repeating Scale() for an unchanged map scale only traverses every active element.
        return ScaleCacheRef(__instance) != scale;
    }
}

[HarmonyPatch(typeof(WorldMapModel), "GetPathBlockMoveCost", new[] { typeof(Location) })]
internal static class WorldMapPathCostPerformancePatch
{
    [HarmonyPrepare]
    private static bool Prepare() => GamePerformancePatchLifecycle.AllowInstallation;

    private static bool Prefix(WorldMapModel __instance, Location location, ref int __result)
    {
        if (!Plugin.IsGameWorldMapMovementOptimizationEnabled || __instance == null)
            return true;

        return !WorldMapPathCostSupport.TryGetCost(__instance, location, out __result);
    }
}

[HarmonyPatch(typeof(ViewWorldMap), "RerenderMovePath")]
internal static class ViewWorldMapRerenderMovePathPerformancePatch
{
    [HarmonyPrepare]
    private static bool Prepare() => GamePerformancePatchLifecycle.AllowInstallation;

    private static void Prefix(ViewWorldMap __instance)
    {
        if (Plugin.IsGameWorldMapMovementOptimizationEnabled)
            WorldMapPathCostSupport.BeginRender(__instance);
    }

    private static Exception Finalizer(Exception __exception)
    {
        WorldMapPathCostSupport.EndRender();
        return __exception;
    }
}

[HarmonyPatch(typeof(ViewWorldMap), "RefreshCricketPlace", new[] { typeof(bool) })]
internal static class ViewWorldMapRefreshCricketPlacePerformancePatch
{
    [HarmonyPrepare]
    private static bool Prepare() => GamePerformancePatchLifecycle.AllowInstallation;

    private static void Prefix()
    {
        if (Plugin.IsGameWorldMapMovementOptimizationEnabled)
            WorldMapCricketRpcSupport.BeginCricketRefresh();
    }

    private static Exception Finalizer(Exception __exception)
    {
        WorldMapCricketRpcSupport.EndCricketRefresh();
        return __exception;
    }
}

[HarmonyPatch(typeof(CharacterDomainMethod.Call), "GetInventoryItemAmount",
    new[] { typeof(int), typeof(int), typeof(sbyte), typeof(short) })]
internal static class CharacterGetInventoryItemAmountDuringMovementPatch
{
    [HarmonyPrepare]
    private static bool Prepare() => GamePerformancePatchLifecycle.AllowInstallation;

    private static bool Prefix()
    {
        return !Plugin.IsGameWorldMapMovementOptimizationEnabled
            || !WorldMapCricketRpcSupport.ShouldSuppressCurrentRequest;
    }
}

[HarmonyPatch(typeof(ViewWorldMap), "UpdateSettlementAndStationBtnUsable")]
internal static class ViewWorldMapSettlementButtonsDuringMovementPatch
{
    [HarmonyPrepare]
    private static bool Prepare() => GamePerformancePatchLifecycle.AllowInstallation;

    private static bool Prefix()
    {
        return !Plugin.IsGameWorldMapMovementOptimizationEnabled
            || !WorldMapMovementRefreshSupport.IsIntermediateMovementStep;
    }
}

internal static class WorldMapCricketRpcSupport
{
    private static int _cricketRefreshDepth;

    internal static bool ShouldSuppressCurrentRequest =>
        _cricketRefreshDepth > 0 && WorldMapMovementRefreshSupport.IsMovementInProgress;

    internal static void BeginCricketRefresh()
    {
        _cricketRefreshDepth++;
    }

    internal static void EndCricketRefresh()
    {
        if (_cricketRefreshDepth > 0)
            _cricketRefreshDepth--;
    }

    internal static void Reset()
    {
        _cricketRefreshDepth = 0;
    }
}

[HarmonyPatch(typeof(MapRenderSystem), "RefreshAllMapBlocks", new[] { typeof(bool) })]
internal static class MapRenderSystemRefreshAllMapBlocksPerformancePatch
{
    [HarmonyPrepare]
    private static bool Prepare() => GamePerformancePatchLifecycle.AllowInstallation;

    private static void Prefix()
    {
        if (Plugin.IsGameWorldMapMovementOptimizationEnabled)
            WorldMapWorkerSightBatchSupport.Begin();
    }

    private static Exception Finalizer(Exception __exception)
    {
        WorldMapWorkerSightBatchSupport.End();
        return __exception;
    }
}

[HarmonyPatch(typeof(WorldMapModel), "IsAnyWorkerOnMap", new[] { typeof(MapBlockData) })]
internal static class WorldMapIsAnyWorkerOnMapPerformancePatch
{
    [HarmonyPrepare]
    private static bool Prepare() => GamePerformancePatchLifecycle.AllowInstallation;

    private static bool Prefix(MapBlockData blockData, ref bool __result)
    {
        return !Plugin.IsGameWorldMapMovementOptimizationEnabled
            || !WorldMapWorkerSightBatchSupport.TryGetResult(blockData, out __result);
    }
}

internal static class WorldMapWorkerSightBatchSupport
{
    private static readonly Dictionary<Location, HashSet<int>> WorkerIdsByLocation = new();
    private static int _batchDepth;

    internal static void Begin()
    {
        _batchDepth++;
        if (_batchDepth != 1)
            return;

        WorkerIdsByLocation.Clear();
        var buildingModel = SingletonObject.getInstance<BuildingModel>();
        var villagerWork = buildingModel?.VillagerWork;
        if (villagerWork == null || villagerWork.Count == 0)
            return;

        foreach (var workData in villagerWork.Values)
        {
            if (!VillagerWorkType.IsWorkOnMap(workData.WorkType))
                continue;

            var location = new Location(workData.AreaId, workData.BlockId);
            if (!WorkerIdsByLocation.TryGetValue(location, out var characterIds))
            {
                characterIds = new HashSet<int>();
                WorkerIdsByLocation.Add(location, characterIds);
            }

            characterIds.Add(workData.CharacterId);
        }
    }

    internal static bool TryGetResult(MapBlockData blockData, out bool result)
    {
        if (_batchDepth <= 0)
        {
            result = false;
            return false;
        }

        var characterSet = blockData?.CharacterSet;
        if (characterSet == null || characterSet.Count == 0 ||
            !WorkerIdsByLocation.TryGetValue(blockData.GetLocation(), out var workerIds))
        {
            result = false;
            return true;
        }

        result = workerIds.Overlaps(characterSet);
        return true;
    }

    internal static void End()
    {
        if (_batchDepth <= 0)
            return;

        _batchDepth--;
        if (_batchDepth == 0)
            WorkerIdsByLocation.Clear();
    }

    internal static void Reset()
    {
        _batchDepth = 0;
        WorkerIdsByLocation.Clear();
    }
}

internal static class WorldMapPathCostSupport
{
    private static WorldMapModel _model;
    private static IReadOnlyList<Location> _path;
    private static WorldMapModel _renderingModel;
    private static int _nextIndex = -1;
    private static int _runningCost;

    internal static bool TryGetCost(WorldMapModel model, Location location, out int result)
    {
        var path = model.MovePath;
        if (path == null || path.Count == 0)
        {
            result = 0;
            return false;
        }

        // Match the original current-location result before using the fast path.
        if (location == model.CurrentLocation)
        {
            result = -1;
            return true;
        }

        if (model.CrossArchiveLockMoveTime)
        {
            if (FindLocationIndex(path, location) < 0)
            {
                result = -1;
                return true;
            }

            result = 0;
            return true;
        }

        // RerenderMovePath() asks for path[0], path[1], ... in order. Reuse the
        // accumulated prefix in that case; any other caller falls back exactly.
        if (location == path[0])
        {
            _model = model;
            _path = path;
            _nextIndex = 0;
            _runningCost = GetMoveCost(model, path[0]);
            result = _runningCost;
            return true;
        }

        if (ReferenceEquals(_renderingModel, model) && ReferenceEquals(_model, model) && ReferenceEquals(_path, path) &&
            _nextIndex + 1 < path.Count && path[_nextIndex + 1] == location)
        {
            _nextIndex++;
            _runningCost += GetMoveCost(model, path[_nextIndex]);
            result = _runningCost;
            return true;
        }

        var locationIndex = FindLocationIndex(path, location);
        if (locationIndex < 0)
        {
            result = -1;
            return true;
        }

        result = 0;
        for (var i = 0; i <= locationIndex; i++)
            result += GetMoveCost(model, path[i]);

        _model = model;
        _path = path;
        _nextIndex = locationIndex;
        _runningCost = result;
        return true;
    }

    internal static void Reset()
    {
        _model = null;
        _path = null;
        _renderingModel = null;
        _nextIndex = -1;
        _runningCost = 0;
    }

    internal static void BeginRender(ViewWorldMap view)
    {
        var model = SingletonObject.getInstance<WorldMapModel>();
        _renderingModel = model;
        _model = null;
        _path = null;
        _nextIndex = -1;
        _runningCost = 0;
    }

    internal static void EndRender()
    {
        _renderingModel = null;
    }

    private static int FindLocationIndex(IReadOnlyList<Location> path, Location location)
    {
        for (var i = 0; i < path.Count; i++)
        {
            if (path[i] == location)
                return i;
        }

        return -1;
    }

    private static int GetMoveCost(WorldMapModel model, Location location)
    {
        return model.GetBlockData(location).MoveCostActionPoint;
    }
}

[HarmonyPatch(typeof(ViewMapBlockCharList), "Refresh", new[] { typeof(bool), typeof(MapBlockData) })]
internal static class ViewMapBlockCharListRefreshDuringMovementPatch
{
    [HarmonyPrepare]
    private static bool Prepare() => GamePerformancePatchLifecycle.AllowInstallation;

    private static bool Prefix(ViewMapBlockCharList __instance)
    {
        return !Plugin.IsGameWorldMapMovementOptimizationEnabled
            || WorldMapMovementRefreshSupport.ShouldRefresh(__instance);
    }
}

[HarmonyPatch(typeof(ViewMapBlockCharList), "OnDisable")]
internal static class ViewMapBlockCharListDisableMovementRefreshPatch
{
    [HarmonyPrepare]
    private static bool Prepare() => GamePerformancePatchLifecycle.AllowInstallation;

    private static void Postfix(ViewMapBlockCharList __instance)
    {
        WorldMapMovementRefreshSupport.Forget(__instance);
    }
}

[HarmonyPatch(typeof(ViewWorldMap), "OnMoveFinished")]
internal static class ViewWorldMapMoveFinishedCharacterListRefreshPatch
{
    [HarmonyPrepare]
    private static bool Prepare() => GamePerformancePatchLifecycle.AllowInstallation;

    private static void Prefix()
    {
        if (Plugin.IsGameWorldMapMovementOptimizationEnabled)
            WorldMapMovementRefreshSupport.PrepareFinishMovement();
    }

    private static void Postfix()
    {
        if (Plugin.IsGameWorldMapMovementOptimizationEnabled)
            WorldMapMovementRefreshSupport.FinishMovement();
    }
}

[HarmonyPatch(typeof(ViewWorldMap), "MoveToNextTruly")]
internal static class ViewWorldMapMoveStartedCharacterListRefreshPatch
{
    [HarmonyPrepare]
    private static bool Prepare() => GamePerformancePatchLifecycle.AllowInstallation;

    private static void Postfix()
    {
        if (Plugin.IsGameWorldMapMovementOptimizationEnabled)
            WorldMapMovementRefreshSupport.TrackMovementStart();
    }
}

internal static class WorldMapMovementRefreshSupport
{
    private static readonly HashSet<ViewMapBlockCharList> PendingViews = new();
    private static bool _forceRefresh;
    private static bool _movementInProgress;

    internal static bool IsMovementInProgress => _movementInProgress;

    internal static bool IsIntermediateMovementStep
    {
        get
        {
            if (!_movementInProgress)
                return false;

            var model = SingletonObject.getInstance<WorldMapModel>();
            return model != null && model.MovePath != null && model.MovePath.Count > 1;
        }
    }

    internal static bool ShouldRefresh(ViewMapBlockCharList view)
    {
        if (_forceRefresh || view == null)
            return true;

        if (!_movementInProgress)
        {
            PendingViews.Remove(view);
            return true;
        }

        PendingViews.Add(view);
        return false;
    }

    internal static void Forget(ViewMapBlockCharList view)
    {
        if (view != null)
            PendingViews.Remove(view);
    }

    internal static void TrackMovementStart()
    {
        var model = SingletonObject.getInstance<WorldMapModel>();
        if (model != null && model.TaiwuMoveState == WorldMapModel.MoveState.SendMoveMessage)
            _movementInProgress = true;
    }

    internal static void FinishMovement()
    {
        if (PendingViews.Count == 0)
            return;

        var model = SingletonObject.getInstance<WorldMapModel>();
        if (model == null || model.TaiwuMoveState != WorldMapModel.MoveState.Idle)
            return;

        _forceRefresh = true;
        try
        {
            foreach (var view in PendingViews)
            {
                if (view != null && view.gameObject.activeInHierarchy)
                    view.Refresh(scrollToTop: true);
            }
        }
        finally
        {
            _forceRefresh = false;
            PendingViews.Clear();
        }
    }

    internal static void PrepareFinishMovement()
    {
        _movementInProgress = false;
    }

    internal static void Reset()
    {
        _forceRefresh = false;
        _movementInProgress = false;
        PendingViews.Clear();
        WorldMapPathCostSupport.Reset();
        WorldMapCricketRpcSupport.Reset();
        WorldMapWorkerSightBatchSupport.Reset();
    }

    internal static void OnSettingChanged()
    {
        if (!Plugin.IsGameWorldMapMovementOptimizationEnabled)
            Reset();
    }
}
