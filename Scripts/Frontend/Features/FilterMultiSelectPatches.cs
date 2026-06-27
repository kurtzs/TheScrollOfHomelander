#nullable disable

using System.Collections.Generic;
using FrameWork.UISystem.UIElements;
using HarmonyLib;
using Game.Components.SortAndFilter;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(CToggle), nameof(CToggle.OnPointerClick))]
internal static class ShiftMultiSelectFilterTogglePatch
{
    private static bool Prefix(CToggle __instance, PointerEventData eventData)
    {
        if (__instance == null || eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return true;

        if (!FilterMultiSelectSupport.IsShiftPressed())
            return true;

        var binding = __instance.GetComponent<FilterMultiSelectOptionBinding>();
        if (binding == null || !binding.IsValid)
            return true;

        binding.HandleShiftClick();
        eventData.Use();
        return false;
    }
}

[HarmonyPatch(typeof(FilterPanel), "Refresh")]
internal static class FilterPanelRefreshShiftMultiSelectPatch
{
    private static void Postfix(FilterPanel __instance)
    {
        FilterMultiSelectSupport.BindPanelSections(__instance);
    }
}

[HarmonyPatch(typeof(FilterPanel), "RefreshFilterOptionCounts")]
internal static class FilterPanelRefreshCountsShiftMultiSelectPatch
{
    private static void Postfix(FilterPanel __instance)
    {
        FilterMultiSelectSupport.BindPanelSections(__instance);
    }
}

internal sealed class FilterMultiSelectOptionBinding : MonoBehaviour
{
    private SortAndFilter _owner;
    private FilterSection _section;
    private int _lineId;
    private int _menuId;
    private int _originalOptionIndex;

    internal bool IsValid => _owner != null && _section != null;
    internal int OriginalOptionIndex => _originalOptionIndex;

    internal void Setup(SortAndFilter owner, FilterSection section, int lineId, int menuId, int originalOptionIndex)
    {
        _owner = owner;
        _section = section;
        _lineId = lineId;
        _menuId = menuId;
        _originalOptionIndex = originalOptionIndex;
    }

    internal void HandleShiftClick()
    {
        if (!IsValid)
            return;

        FilterMultiSelectSupport.ToggleOption(_owner, _lineId, _menuId, _originalOptionIndex);
        FilterMultiSelectSupport.BindPanelSectionsFromOwner(_owner);
        FilterMultiSelectSupport.ApplySelectionVisuals(_section, _owner, _lineId, _menuId);
    }
}

internal static class FilterMultiSelectSupport
{
    internal static bool IsShiftPressed()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    internal static void BindOption(GameObject optionObject, SortAndFilter owner, FilterSection section, int lineId, int menuId, int originalOptionIndex)
    {
        if (optionObject == null || owner == null || section == null)
            return;

        var binding = optionObject.GetComponent<FilterMultiSelectOptionBinding>() ?? optionObject.AddComponent<FilterMultiSelectOptionBinding>();
        binding.Setup(owner, section, lineId, menuId, originalOptionIndex);
    }

    internal static void BindPanelSections(FilterPanel panel)
    {
        if (panel == null)
            return;

        var owner = Traverse.Create(panel).Field("_owner").GetValue<SortAndFilter>();
        if (owner == null)
            return;

        var sectionMap = Traverse.Create(panel)
            .Field("_sectionMap")
            .GetValue<Dictionary<(int LineId, int MenuId), FilterSection>>();
        if (sectionMap == null)
            return;

        foreach (var entry in sectionMap)
        {
            var section = entry.Value;
            if (section == null)
                continue;

            BindSectionOptions(section, owner, entry.Key.LineId, entry.Key.MenuId);
            ApplySelectionVisuals(section, owner, entry.Key.LineId, entry.Key.MenuId);
        }
    }

    internal static void BindPanelSectionsFromOwner(SortAndFilter owner)
    {
        if (owner == null)
            return;

        var filterPanel = Traverse.Create(owner).Field("filterPanel").GetValue<FilterPanel>();
        BindPanelSections(filterPanel);
    }

    internal static void BindSectionOptions(FilterSection section, SortAndFilter owner, int lineId, int menuId)
    {
        var contentRoot = section == null ? null : section.GetContentRoot();
        if (contentRoot == null)
            return;

        for (var i = 0; i < contentRoot.childCount; i++)
        {
            var child = contentRoot.GetChild(i);
            if (child == null)
                continue;

            BindOption(child.gameObject, owner, section, lineId, menuId, i - 1);
        }
    }

    internal static void ApplySelectionVisuals(FilterSection section, SortAndFilter owner, int lineId, int menuId)
    {
        var contentRoot = section == null ? null : section.GetContentRoot();
        if (contentRoot == null || owner == null)
            return;

        var selected = GetSelectedIndices(owner, lineId, menuId);
        for (var i = 0; i < contentRoot.childCount; i++)
        {
            var child = contentRoot.GetChild(i);
            var option = child == null ? null : child.GetComponent<FilterSectionOption>();
            if (option == null)
                continue;

            var binding = child.GetComponent<FilterMultiSelectOptionBinding>();
            var originalOptionIndex = binding == null ? i - 1 : binding.OriginalOptionIndex;
            option.SetIsOnWithoutNotify(originalOptionIndex < 0 ? selected.Count == 0 : selected.Contains(originalOptionIndex));
        }
    }

    internal static void ToggleOption(SortAndFilter owner, int lineId, int menuId, int optionIndex)
    {
        if (owner == null)
            return;

        var selectedIndices = Traverse.Create(owner)
            .Field("_selectedIndices")
            .GetValue<Dictionary<int, List<int>>>();
        if (selectedIndices == null)
            return;

        var selectionKey = GetSelectionKey(lineId, menuId);
        if (optionIndex < 0)
        {
            selectedIndices.Remove(selectionKey);
        }
        else
        {
            if (!selectedIndices.TryGetValue(selectionKey, out var selected))
            {
                selected = new List<int>();
                selectedIndices[selectionKey] = selected;
            }

            if (selected.Contains(optionIndex))
                selected.Remove(optionIndex);
            else
                selected.Add(optionIndex);

            selected.Sort();
            if (selected.Count == 0)
                selectedIndices.Remove(selectionKey);
        }

        owner.Config?.OnFilterChanged?.Invoke(lineId);
        Traverse.Create(owner).Method("RefreshSectionsSummaryAndPanel").GetValue();
        FilterMemoryController.TrySave(owner);
    }

    internal static List<int> GetSelectedIndices(SortAndFilter owner, int lineId, int menuId)
    {
        var selectedIndices = Traverse.Create(owner)
            .Field("_selectedIndices")
            .GetValue<Dictionary<int, List<int>>>();
        if (selectedIndices == null)
            return new List<int>();

        return selectedIndices.TryGetValue(GetSelectionKey(lineId, menuId), out var selected)
            ? new List<int>(selected)
            : new List<int>();
    }

    private static int GetSelectionKey(int lineId, int menuId)
    {
        return lineId * 1000 + menuId;
    }
}
