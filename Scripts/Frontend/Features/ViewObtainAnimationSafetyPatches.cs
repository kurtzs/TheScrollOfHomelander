#nullable disable

using Game.Views.Obtain;
using HarmonyLib;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(ViewObtain), "OnItemRender")]
internal static class ViewObtainAnimationSafetyPatch
{
    private static void Postfix(GameObject obj)
    {
        if (obj == null || obj.GetComponent<CEmptyGraphic>() != null)
            return;

        var graphic = obj.AddComponent<CEmptyGraphic>();
        graphic.enabled = false;
    }
}
