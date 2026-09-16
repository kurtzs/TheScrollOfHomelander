#nullable disable

using System;
using HarmonyLib;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(Game.Components.SortAndFilter.SortAndFilter), "OpenFilterPanel")]
internal static class SortAndFilterOpenFilterPanelQuickHidePatch
{
    private static void Postfix(Game.Components.SortAndFilter.SortAndFilter __instance)
    {
        FilterPanelQuickHideSupport.Register(__instance);
    }
}

[HarmonyPatch(typeof(Game.Components.SortAndFilter.SortAndFilter), "CloseFilterPanel")]
internal static class SortAndFilterCloseFilterPanelQuickHidePatch
{
    private static void Postfix(Game.Components.SortAndFilter.SortAndFilter __instance)
    {
        FilterPanelQuickHideSupport.Unregister(__instance);
    }
}

internal static class FilterPanelQuickHideSupport
{
    private static readonly Action CloseActivePanelHandler = CloseActivePanel;
    private static Game.Components.SortAndFilter.SortAndFilter _activeOwner;

    internal static void Register(Game.Components.SortAndFilter.SortAndFilter owner)
    {
        if (owner == null || UIManager.Instance == null)
            return;

        _activeOwner = owner;
        UIManager.Instance.SetCommonSortAndFilterEscHandler(CloseActivePanelHandler);
    }

    internal static void Unregister(Game.Components.SortAndFilter.SortAndFilter owner)
    {
        if (_activeOwner != owner)
            return;

        _activeOwner = null;
        if (UIManager.Instance != null)
            UIManager.Instance.SetCommonSortAndFilterEscHandler(null);
    }

    private static void CloseActivePanel()
    {
        var owner = _activeOwner;
        _activeOwner = null;
        owner?.CloseFilterPanel();
    }
}
