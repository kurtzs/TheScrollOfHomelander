#nullable disable

using System;
using System.Reflection;
using Game.Views.Obtain;
using GameData.Domains.World.Display;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(ViewObtain), "OnItemRender")]
internal static class ViewObtainAnimationSafetyPatch
{
    private static readonly FieldInfo ObtainTypeField = AccessTools.Field(typeof(ViewObtain), "_type");

    private static void Postfix(ViewObtain __instance, GameObject obj)
    {
        try
        {
            if (!ShouldPatch(__instance) || obj == null)
                return;

            if (obj.GetComponent<CanvasGroup>() == null || obj.GetComponent<RectTransform>() == null)
                return;

            if (obj.GetComponent<ViewObtainAnimationSafetyMarker>() == null)
                obj.AddComponent<ViewObtainAnimationSafetyMarker>();

            if (obj.GetComponent<CEmptyGraphic>() != null)
                return;

            if (obj.GetComponent<Graphic>() != null)
                return;

            var graphic = obj.AddComponent<CEmptyGraphic>();
            if (graphic != null)
                graphic.enabled = false;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to add ViewObtain animation safety graphic: " + ex);
        }
    }

    private static bool ShouldPatch(ViewObtain view)
    {
        try
        {
            return view != null && (EObtainType)ObtainTypeField.GetValue(view) == EObtainType.Craft;
        }
        catch
        {
            return false;
        }
    }
}

[HarmonyPatch]
internal static class ViewObtainAnimationGraphicCallbackPatch
{
    private const string DisplayClassTypeName = "Game.Views.Obtain.ViewObtain+<>c__DisplayClass95_0";
    private const string CallbackMethodName = "<ShowGetItemCoroutine>b__0";

    private static readonly Type DisplayClassType = AccessTools.TypeByName(DisplayClassTypeName);
    private static readonly FieldInfo CanvasField = AccessTools.Field(DisplayClassType, "canvas");
    private static readonly FieldInfo GraphicField = AccessTools.Field(DisplayClassType, "graphic");

    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(DisplayClassType, CallbackMethodName);
    }

    private static bool Prefix(object __instance)
    {
        if (__instance == null)
            return false;

        var canvas = CanvasField?.GetValue(__instance) as CanvasGroup;
        var graphic = GraphicField?.GetValue(__instance) as CEmptyGraphic;
        if (graphic != null)
            return true;

        return canvas == null || canvas.GetComponent<ViewObtainAnimationSafetyMarker>() == null;
    }
}

internal sealed class ViewObtainAnimationSafetyMarker : MonoBehaviour
{
}
