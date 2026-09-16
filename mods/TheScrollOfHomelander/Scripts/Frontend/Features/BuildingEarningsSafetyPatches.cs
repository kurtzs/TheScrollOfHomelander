using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Game.Views.Building;
using GameData.Domains.Building;
using HarmonyLib;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(ViewBuildingArea), "UpdateShopGetItemInfo", new[] { typeof(BuildingBlockKey) })]
internal static class BuildingEarningsSafetyPatch
{
    private static readonly FieldInfo Rows = AccessTools.Field(typeof(ViewBuildingArea), "_blockRefersDict");
    private static readonly FieldInfo Buildings = AccessTools.Field(typeof(ViewBuildingArea), "_buildingDict");
    private static readonly FieldInfo EarnHolder = Rows == null ? null
        : AccessTools.Field(Rows.FieldType.GetGenericArguments()[1], "getEarnHolder");
    private static readonly ConditionalWeakTable<ViewBuildingArea, Epoch> Epochs = new();
    private sealed class Epoch { internal int Value; }

    internal static void Invalidate(ViewBuildingArea view)
    {
        if (view != null) Epochs.GetValue(view, _ => new Epoch()).Value++;
    }

    private static bool TryGetRow(ViewBuildingArea view, BuildingBlockKey key, out object row)
    {
        row = null;
        if (view == null || !view.gameObject.activeInHierarchy ||
            view.CurrentLocation.AreaId != key.AreaId || view.CurrentLocation.BlockId != key.BlockId)
            return false;
        var rows = Rows.GetValue(view) as IDictionary;
        var buildings = Buildings.GetValue(view) as IDictionary;
        if (rows == null || buildings == null || !rows.Contains(key.BuildingBlockIndex)
            || !buildings.Contains(key.BuildingBlockIndex)) return false;
        row = rows[key.BuildingBlockIndex];
        return row is Component component && component != null && component.gameObject.activeInHierarchy
            && EarnHolder?.GetValue(row) is Component holder && holder != null;
    }

    private static bool Prefix(ViewBuildingArea __instance, BuildingBlockKey blockKey)
        => TryGetRow(__instance, blockKey, out _);

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var original = AccessTools.Method(typeof(BuildingDomainMethod.AsyncCall), "GetBuildingEarningData",
            new[] { typeof(IAsyncMethodRequestHandler), typeof(BuildingBlockKey), typeof(AsyncMethodCallbackDelegate) });
        var matches = code.Where(instruction => instruction.Calls(original)).ToList();
        if (original == null || Rows == null || Buildings == null || EarnHolder == null || matches.Count != 1)
            throw new MissingMethodException("Building earnings callback signature or call count changed.");
        matches[0].opcode = OpCodes.Call;
        matches[0].operand = AccessTools.Method(typeof(BuildingEarningsSafetyPatch), nameof(Request));
        return code;
    }

    private static void Request(IAsyncMethodRequestHandler handler, BuildingBlockKey key, AsyncMethodCallbackDelegate callback)
    {
        var view = handler as ViewBuildingArea;
        if (!TryGetRow(view, key, out var row)) return;
        var session = ModSession.Generation;
        var epoch = Epochs.GetValue(view, _ => new Epoch());
        var version = epoch.Value;
        var building = ((IDictionary)Buildings.GetValue(view))[key.BuildingBlockIndex];
        BuildingDomainMethod.AsyncCall.GetBuildingEarningData(handler, key, (offset, pool) =>
        {
            if (ModSession.IsCurrent(session) && epoch.Value == version &&
                TryGetRow(view, key, out var current) && ReferenceEquals(current, row)
                && ReferenceEquals(((IDictionary)Buildings.GetValue(view))[key.BuildingBlockIndex], building))
                callback(offset, pool);
        });
    }
}

[HarmonyPatch(typeof(ViewBuildingArea), "OnDisable")]
internal static class BuildingEarningsClosePatch
{
    private static void Prefix(ViewBuildingArea __instance) => BuildingEarningsSafetyPatch.Invalidate(__instance);
}

[HarmonyPatch(typeof(ViewBuildingArea), "InitBuildingArea")]
internal static class BuildingEarningsRebuildPatch
{
    private static void Prefix(ViewBuildingArea __instance) => BuildingEarningsSafetyPatch.Invalidate(__instance);
}
