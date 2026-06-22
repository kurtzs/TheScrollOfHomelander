#nullable disable

using HarmonyLib;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(MapElementContainer), "SyncElementPos")]
internal static class MapElementIconOffsetPatch
{
    private const float IconYOffset = 18f;

    private static void Postfix(MapElementBase element)
    {
        if (element == null)
            return;

        var transform = element.transform;
        transform.localPosition += Vector3.up * IconYOffset;
    }
}
