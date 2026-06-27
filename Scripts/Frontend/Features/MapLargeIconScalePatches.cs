#nullable disable

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(MapElementCharacter), "Scale")]
internal static class MapElementCharacterScalePatch
{
    private static void Postfix(MapElementCharacter __instance)
    {
        MapLargeIconScaleSupport.ApplyAfterOriginalScale(__instance?.transform);
    }
}

[HarmonyPatch(typeof(MapElementCricket), "Scale")]
internal static class MapElementCricketScalePatch
{
    private static void Postfix(MapElementCricket __instance)
    {
        MapLargeIconScaleSupport.ApplyAfterOriginalScale(__instance?.transform);
    }
}

[HarmonyPatch(typeof(MapElementAdventureRemake), "Scale")]
internal static class MapElementAdventureRemakeScalePatch
{
    private static void Postfix(MapElementAdventureRemake __instance)
    {
        MapLargeIconScaleSupport.ApplyAfterOriginalScale(MapLargeIconScaleSupport.GetAdventureRemakeRoot(__instance));
    }
}

[HarmonyPatch(typeof(MapElementAdventureMajorEvent), "Scale")]
internal static class MapElementAdventureMajorEventScalePatch
{
    private static void Postfix(MapElementAdventureMajorEvent __instance)
    {
        MapLargeIconScaleSupport.ApplyAfterOriginalScale(MapLargeIconScaleSupport.GetAdventureMajorEventRoot(__instance));
    }
}

internal static class MapLargeIconScaleSupport
{
    private static readonly FieldInfo AdventureRemakeRootField = AccessTools.Field(typeof(MapElementAdventureRemake), "root");
    private static readonly FieldInfo AdventureMajorEventRootField = AccessTools.Field(typeof(MapElementAdventureMajorEvent), "root");

    internal static RectTransform GetAdventureRemakeRoot(MapElementAdventureRemake instance)
    {
        return instance == null ? null : AdventureRemakeRootField?.GetValue(instance) as RectTransform;
    }

    internal static RectTransform GetAdventureMajorEventRoot(MapElementAdventureMajorEvent instance)
    {
        return instance == null ? null : AdventureMajorEventRootField?.GetValue(instance) as RectTransform;
    }

    internal static void RefreshAllActive()
    {
        foreach (var element in Resources.FindObjectsOfTypeAll<MapElementCharacter>())
            Apply(element?.transform);

        foreach (var element in Resources.FindObjectsOfTypeAll<MapElementCricket>())
            Apply(element?.transform);

        foreach (var element in Resources.FindObjectsOfTypeAll<MapElementAdventureRemake>())
            Apply(GetAdventureRemakeRoot(element));

        foreach (var element in Resources.FindObjectsOfTypeAll<MapElementAdventureMajorEvent>())
            Apply(GetAdventureMajorEventRoot(element));
    }

    internal static void RestoreAll()
    {
        ScaleState.RestoreAll();
    }

    internal static void ApplyAfterOriginalScale(Transform transform)
    {
        ScaleState.Apply(transform, GetScaleFactor(), originalScaleJustRefreshed: true);
    }

    private static void Apply(Transform transform)
    {
        ScaleState.Apply(transform, GetScaleFactor(), originalScaleJustRefreshed: false);
    }

    private static float GetScaleFactor()
    {
        return Mathf.Clamp(Plugin.MapLargeIconScalePercent, 40, 120) / 100f;
    }

    private static class ScaleState
    {
        private const float ScaleEpsilon = 0.001f;
        private static readonly Dictionary<int, ScaleRecord> Records = new();

        internal static void Apply(Transform transform, float factor, bool originalScaleJustRefreshed)
        {
            if (transform == null)
                return;

            var id = transform.GetInstanceID();
            if (!Records.TryGetValue(id, out var record) || record.Transform == null)
            {
                record = new ScaleRecord(transform, transform.localScale);
                Records[id] = record;
            }
            else if (originalScaleJustRefreshed)
            {
                record.BaseScale = transform.localScale;
            }
            else
            {
                var expectedScale = Multiply(record.BaseScale, record.LastFactor);
                if (!ApproximatelySame(transform.localScale, expectedScale))
                    record.BaseScale = Divide(transform.localScale, record.LastFactor);
            }

            transform.localScale = Multiply(record.BaseScale, factor);
            record.LastFactor = factor;
        }

        internal static void RestoreAll()
        {
            foreach (var record in Records.Values)
            {
                if (record.Transform != null)
                    record.Transform.localScale = record.BaseScale;
            }

            Records.Clear();
        }

        private static Vector3 Multiply(Vector3 scale, float factor)
        {
            return new Vector3(scale.x * factor, scale.y * factor, scale.z);
        }

        private static Vector3 Divide(Vector3 scale, float factor)
        {
            if (Math.Abs(factor) < ScaleEpsilon)
                return scale;

            return new Vector3(scale.x / factor, scale.y / factor, scale.z);
        }

        private static bool ApproximatelySame(Vector3 left, Vector3 right)
        {
            return Math.Abs(left.x - right.x) < ScaleEpsilon
                && Math.Abs(left.y - right.y) < ScaleEpsilon
                && Math.Abs(left.z - right.z) < ScaleEpsilon;
        }

        private sealed class ScaleRecord
        {
            internal ScaleRecord(Transform transform, Vector3 baseScale)
            {
                Transform = transform;
                BaseScale = baseScale;
            }

            internal Transform Transform { get; }
            internal Vector3 BaseScale { get; set; }
            internal float LastFactor { get; set; } = 1f;
        }
    }
}
