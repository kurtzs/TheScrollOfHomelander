#nullable disable

using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(ViewBuildingOverview), "Update")]
internal static class ViewBuildingOverviewSpaceStartPatch
{
    private static bool Prefix(ViewBuildingOverview __instance)
    {
        if (!Plugin.EnableSpaceStartBuilding)
            return true;

        BuildingSpaceStartSupport.TryStartBySpace(__instance);
        return false;
    }
}

internal static class BuildingSpaceStartSupport
{
    private static int _lastStartFrame = -1;

    internal static void TryStartBySpace(ViewBuildingOverview view)
    {
        if (view == null || !view.gameObject.activeInHierarchy)
            return;

        if (!CommonCommandKit.Space.Check(view.Element) || _lastStartFrame == Time.frameCount)
            return;

        if (IsTextInputFocused() || IsBlockingOverlayActive())
            return;

        var confirm = Traverse.Create(view).Field("confirmOperation").GetValue<CButton>();
        if (confirm == null || !confirm.gameObject.activeInHierarchy || !confirm.interactable)
            return;

        try
        {
            _lastStartFrame = Time.frameCount;
            AccessTools.Method(typeof(ViewBuildingOverview), "OnClick")?.Invoke(view, new object[] { confirm.transform });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to start building by Space: " + ex);
        }
    }

    private static bool IsTextInputFocused()
    {
        var selected = EventSystem.current?.currentSelectedGameObject;
        return selected != null &&
               (selected.GetComponentInParent<TMP_InputField>() != null ||
                selected.GetComponentInParent<UnityEngine.UI.InputField>() != null);
    }

    private static bool IsBlockingOverlayActive()
    {
        return IsUiElementActive(UIElement.Dialog) ||
               IsUiElementActive(UIElement.SetSelectCount);
    }

    private static bool IsUiElementActive(UIElement element)
    {
        var uiBase = element?.UiBase;
        return uiBase != null && uiBase.gameObject.activeInHierarchy;
    }
}
