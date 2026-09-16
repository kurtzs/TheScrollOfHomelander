#nullable disable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Game.Views.Exchange;
using GameData.Domains.Taiwu.ExchangeSystem;
using HarmonyLib;

namespace BetterTaiwuScroll.Frontend;

internal static class WarehouseRefreshListCache
{
    private sealed class RefreshState
    {
        internal int Depth;
        internal readonly Dictionary<int, IReadOnlyList<ITradeableContent>> SelfLists = new();
        internal readonly Dictionary<int, IReadOnlyList<ITradeableContent>> TargetLists = new();
    }

    private static ConditionalWeakTable<ViewWarehouse, RefreshState> States = new();

    internal static void Begin(ViewExchangeBase view)
    {
        if (!Plugin.IsGameWarehouseRefreshListCacheEnabled
            || view is not ViewWarehouse warehouse)
            return;

        var state = States.GetOrCreateValue(warehouse);
        if (state.Depth++ == 0)
        {
            state.SelfLists.Clear();
            state.TargetLists.Clear();
        }
    }

    internal static void End(ViewExchangeBase view)
    {
        if (view is not ViewWarehouse warehouse || !States.TryGetValue(warehouse, out var state))
            return;

        state.Depth = state.Depth > 0 ? state.Depth - 1 : 0;
        if (state.Depth == 0)
        {
            state.SelfLists.Clear();
            state.TargetLists.Clear();
        }
    }

    internal static bool TryGetSelf(ViewWarehouse view, int index, out IReadOnlyList<ITradeableContent> result)
    {
        result = null;
        return Plugin.IsGameWarehouseRefreshListCacheEnabled
               && view != null
               && States.TryGetValue(view, out var state)
               && state.Depth > 0
               && state.SelfLists.TryGetValue(index, out result);
    }

    internal static bool TryGetTarget(ViewWarehouse view, int index, out IReadOnlyList<ITradeableContent> result)
    {
        result = null;
        return Plugin.IsGameWarehouseRefreshListCacheEnabled
               && view != null
               && States.TryGetValue(view, out var state)
               && state.Depth > 0
               && state.TargetLists.TryGetValue(index, out result);
    }

    internal static void StoreSelf(ViewWarehouse view, int index, IReadOnlyList<ITradeableContent> result)
    {
        if (Plugin.IsGameWarehouseRefreshListCacheEnabled
            && view != null
            && result != null
            && States.TryGetValue(view, out var state)
            && state.Depth > 0)
        {
            state.SelfLists[index] = result;
        }
    }

    internal static void StoreTarget(ViewWarehouse view, int index, IReadOnlyList<ITradeableContent> result)
    {
        if (Plugin.IsGameWarehouseRefreshListCacheEnabled
            && view != null
            && result != null
            && States.TryGetValue(view, out var state)
            && state.Depth > 0)
        {
            state.TargetLists[index] = result;
        }
    }

    internal static void Reset()
    {
        States = new ConditionalWeakTable<ViewWarehouse, RefreshState>();
    }
}

[HarmonyPatch(typeof(ViewExchangeBase), "Refresh")]
internal static class ViewExchangeBaseWarehouseRefreshScopePatch
{
    [HarmonyPrepare]
    private static bool Prepare() => GamePerformancePatchLifecycle.AllowInstallation;

    private static void Prefix(ViewExchangeBase __instance)
    {
        WarehouseRefreshListCache.Begin(__instance);
    }

    private static Exception Finalizer(ViewExchangeBase __instance, Exception __exception)
    {
        WarehouseRefreshListCache.End(__instance);
        return __exception;
    }
}

[HarmonyPatch(typeof(ViewWarehouse), nameof(ViewWarehouse.GetSelfTradeableList), new[] { typeof(int) })]
internal static class ViewWarehouseSelfTradeableListCachePatch
{
    [HarmonyPrepare]
    private static bool Prepare() => GamePerformancePatchLifecycle.AllowInstallation;

    private static bool Prefix(
        ViewWarehouse __instance,
        int index,
        ref IReadOnlyList<ITradeableContent> __result)
    {
        return !WarehouseRefreshListCache.TryGetSelf(__instance, index, out __result);
    }

    private static void Postfix(
        ViewWarehouse __instance,
        int index,
        IReadOnlyList<ITradeableContent> __result)
    {
        WarehouseRefreshListCache.StoreSelf(__instance, index, __result);
    }
}

[HarmonyPatch(typeof(ViewWarehouse), nameof(ViewWarehouse.GetTargetTradeableList), new[] { typeof(int) })]
internal static class ViewWarehouseTargetTradeableListCachePatch
{
    [HarmonyPrepare]
    private static bool Prepare() => GamePerformancePatchLifecycle.AllowInstallation;

    private static bool Prefix(
        ViewWarehouse __instance,
        int index,
        ref IReadOnlyList<ITradeableContent> __result)
    {
        return !WarehouseRefreshListCache.TryGetTarget(__instance, index, out __result);
    }

    private static void Postfix(
        ViewWarehouse __instance,
        int index,
        IReadOnlyList<ITradeableContent> __result)
    {
        WarehouseRefreshListCache.StoreTarget(__instance, index, __result);
    }
}
