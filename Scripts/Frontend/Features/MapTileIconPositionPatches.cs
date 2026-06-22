#nullable disable

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(MapElementInfo), "OnRefresh")]
internal static class MapElementInfoRefreshIconPositionPatch
{
    private static void Postfix(MapElementInfo __instance)
    {
        MapTileIconPositionSupport.RefreshFindIcon(__instance);
    }
}

[HarmonyPatch(typeof(MapElementInfo), "RefreshActorCount")]
internal static class MapElementInfoRefreshActorIconPositionPatch
{
    private static void Postfix(MapElementInfo __instance)
    {
        MapTileIconPositionSupport.RefreshActorCountIcons(__instance);
    }
}

internal static class MapTileIconPositionSupport
{
    private static readonly FieldInfo ItemLayoutField = AccessTools.Field(typeof(MapElementInfo), "itemLayout");
    private static readonly FieldInfo ImageFindField = AccessTools.Field(typeof(MapElementInfo), "imageFind");

    internal static void RefreshFindIcon(MapElementInfo instance)
    {
        ApplyOrRestore((ImageFindField?.GetValue(instance) as Component)?.transform);
    }

    internal static void RefreshActorCountIcons(MapElementInfo instance)
    {
        if (ItemLayoutField?.GetValue(instance) is not RectTransform layout)
            return;

        IconPositionState.Restore(layout.transform);
        for (var i = 0; i < layout.childCount; i++)
        {
            var child = layout.GetChild(i);
            if (child != null && child.gameObject.activeSelf)
                ApplyOrRestore(child);
            else
                IconPositionState.Restore(child);
        }
    }

    internal static void RestoreAll()
    {
        IconPositionState.RestoreAll();
    }

    private static void ApplyOrRestore(Transform transform)
    {
        if (transform == null || !Plugin.EnableMapTileIconYOffset)
        {
            IconPositionState.Restore(transform);
            return;
        }

        var yOffset = Map.RenderSystem.MapRenderSystem.BlockBaseHeight
            * Mathf.Clamp(Plugin.MapTileIconYOffsetPercent, 0, 50)
            / 100f;
        if (Math.Abs(yOffset) < 0.001f)
        {
            IconPositionState.Restore(transform);
            return;
        }

        IconPositionState.Apply(transform, yOffset);
    }

    private static class IconPositionState
    {
        private const float PositionEpsilon = 0.001f;
        private static readonly Dictionary<int, PositionRecord> Positions = new();

        internal static void Apply(Transform transform, float yOffset)
        {
            if (transform == null)
                return;

            var id = transform.GetInstanceID();
            var currentPosition = transform.localPosition;
            if (!Positions.TryGetValue(id, out var record) || record.Transform == null)
            {
                record = new PositionRecord(transform, currentPosition);
                Positions[id] = record;
            }
            else
            {
                var expectedPosition = record.BasePosition;
                expectedPosition.y += record.LastYOffset;
                if (!ApproximatelySame(currentPosition, expectedPosition))
                    record.BasePosition = currentPosition;
            }

            var newPosition = record.BasePosition;
            newPosition.y += yOffset;
            transform.localPosition = newPosition;
            record.LastYOffset = yOffset;
        }

        internal static void Restore(Transform transform)
        {
            if (transform == null)
                return;

            var id = transform.GetInstanceID();
            if (!Positions.TryGetValue(id, out var record) || record.Transform == null)
                return;

            transform.localPosition = record.BasePosition;
            record.LastYOffset = 0f;
        }

        internal static void RestoreAll()
        {
            foreach (var record in Positions.Values)
            {
                if (record.Transform != null)
                    record.Transform.localPosition = record.BasePosition;
            }

            Positions.Clear();
        }

        private static bool ApproximatelySame(Vector3 left, Vector3 right)
        {
            return Math.Abs(left.x - right.x) < PositionEpsilon
                && Math.Abs(left.y - right.y) < PositionEpsilon
                && Math.Abs(left.z - right.z) < PositionEpsilon;
        }

        private sealed class PositionRecord
        {
            internal PositionRecord(Transform transform, Vector3 basePosition)
            {
                Transform = transform;
                BasePosition = basePosition;
            }

            internal Transform Transform { get; }
            internal Vector3 BasePosition { get; set; }
            internal float LastYOffset { get; set; }
        }
    }
}
