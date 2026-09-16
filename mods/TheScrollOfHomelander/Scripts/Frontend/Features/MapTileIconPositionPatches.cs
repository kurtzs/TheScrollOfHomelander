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

[HarmonyPatch(typeof(MapElementInfo), "RefreshCharacterCount")]
internal static class MapElementInfoRefreshCharacterCountIconPositionPatch
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
            var currentPosition = GetCurrentPosition(transform);
            if (!Positions.TryGetValue(id, out var record) || record.Transform == null)
            {
                record = new PositionRecord(transform, currentPosition);
                Positions[id] = record;
                MapVisualLifetime.Register(transform, Release);
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
            SetCurrentPosition(transform, newPosition);
            record.LastYOffset = yOffset;
        }

        internal static void Restore(Transform transform)
        {
            if (transform == null)
                return;

            var id = transform.GetInstanceID();
            if (!Positions.TryGetValue(id, out var record) || record.Transform == null)
                return;

            SetCurrentPosition(transform, record.BasePosition);
            record.LastYOffset = 0f;
        }

        private static void Release(int id)
        {
            Positions.Remove(id);
        }

        internal static void RestoreAll()
        {
            foreach (var record in Positions.Values)
            {
                if (record.Transform != null)
                    SetCurrentPosition(record.Transform, record.BasePosition);
            }

            Positions.Clear();
        }

        private static Vector3 GetCurrentPosition(Transform transform)
        {
            if (transform is RectTransform rect)
            {
                var position = rect.anchoredPosition3D;
                position.z = rect.localPosition.z;
                return position;
            }

            return transform.localPosition;
        }

        private static void SetCurrentPosition(Transform transform, Vector3 position)
        {
            if (transform is RectTransform rect)
            {
                rect.anchoredPosition3D = position;
                return;
            }

            transform.localPosition = position;
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
