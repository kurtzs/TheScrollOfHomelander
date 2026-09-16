#nullable disable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
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

        // Batch-item filtering owns both category and grade toggles. Harmony still
        // runs other prefixes after a prefix skips the original method, so letting
        // the generic Shift handler see these toggles would update the same
        // SortAndFilter twice and can recursively refresh the cloned panel.
        if (__instance.GetComponent<BatchItemCategoryOptionBinding>() != null
            || __instance.GetComponent<BatchItemSubcategoryOptionBinding>() != null
            || __instance.GetComponent<BatchItemGradeOptionBinding>() != null)
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
internal static class FilterPanelRefreshCoordinatorPatch
{
    private static void Postfix(FilterPanel __instance)
    {
        FilterMultiSelectSupport.BindPanelSections(__instance);
        InlineFilterButtonsController.GetFromPanel(__instance)?.AfterPanelRefresh();
    }
}

[HarmonyPatch(typeof(FilterPanel), "RefreshFilterOptionCounts")]
internal static class FilterPanelRefreshCountsCoordinatorPatch
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

    internal void Invalidate()
    {
        _owner = null;
        _section = null;
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
    private static readonly FieldInfo PanelOwnerField = AccessTools.Field(typeof(FilterPanel), "_owner");
    private static readonly FieldInfo PanelSectionMapField = AccessTools.Field(typeof(FilterPanel), "_sectionMap");
    private static readonly FieldInfo OwnerFilterPanelField = AccessTools.Field(typeof(SortAndFilter), "filterPanel");
    private static readonly FieldInfo OwnerSelectedIndicesField = AccessTools.Field(typeof(SortAndFilter), "_selectedIndices");
    private static readonly MethodInfo RefreshSectionsSummaryAndPanelMethod =
        AccessTools.Method(typeof(SortAndFilter), "RefreshSectionsSummaryAndPanel");
    private static readonly ConditionalWeakTable<FilterPanel, PanelBindingState> PanelStates = new();

    private sealed class PanelBindingState
    {
        internal bool Initialized;
        internal int StructureSignature;
        internal int SelectionSignature;
    }

    internal static bool IsShiftPressed()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    internal static void BindOption(GameObject optionObject, SortAndFilter owner, FilterSection section, int lineId, int menuId, int originalOptionIndex)
    {
        if (optionObject == null || owner == null || section == null)
            return;

        if (optionObject.GetComponent<BatchItemCategoryOptionBinding>() != null
            || optionObject.GetComponent<BatchItemSubcategoryOptionBinding>() != null
            || optionObject.GetComponent<BatchItemGradeOptionBinding>() != null)
        {
            RemoveBinding(optionObject);
            return;
        }

        var binding = optionObject.GetComponent<FilterMultiSelectOptionBinding>() ?? optionObject.AddComponent<FilterMultiSelectOptionBinding>();
        binding.Setup(owner, section, lineId, menuId, originalOptionIndex);
    }

    internal static void RemoveBinding(GameObject optionObject)
    {
        if (optionObject == null)
            return;

        var binding = optionObject.GetComponent<FilterMultiSelectOptionBinding>();
        if (binding == null)
            return;

        // Destroy is deferred by Unity, therefore invalidate immediately so a
        // click in the same frame cannot use the stale generic binding.
        binding.Invalidate();
        UnityEngine.Object.Destroy(binding);
    }

    internal static void BindPanelSections(FilterPanel panel)
    {
        if (panel == null)
            return;

        // The cloned batch panel maintains independent category/grade sets and
        // must never receive the generic multi-select bindings.
        if (BatchItemFilterController.IsBatchFilterPanel(panel))
            return;

        var owner = PanelOwnerField?.GetValue(panel) as SortAndFilter;
        if (owner == null)
            return;

        var sectionMap = PanelSectionMapField?.GetValue(panel)
            as Dictionary<(int LineId, int MenuId), FilterSection>;
        if (sectionMap == null)
            return;

        var state = PanelStates.GetOrCreateValue(panel);
        var structureSignature = BuildStructureSignature(owner, sectionMap);
        var selectionSignature = BuildSelectionSignature(owner);
        var structureChanged = !state.Initialized || state.StructureSignature != structureSignature;
        var selectionChanged = !state.Initialized || state.SelectionSignature != selectionSignature;
        if (!structureChanged && !selectionChanged)
            return;

        foreach (var entry in sectionMap)
        {
            var section = entry.Value;
            if (section == null)
                continue;

            if (structureChanged)
                BindSectionOptions(section, owner, entry.Key.LineId, entry.Key.MenuId);
            if (structureChanged || selectionChanged)
                ApplySelectionVisuals(section, owner, entry.Key.LineId, entry.Key.MenuId);
        }

        state.Initialized = true;
        state.StructureSignature = structureSignature;
        state.SelectionSignature = selectionSignature;
    }

    internal static void BindPanelSectionsFromOwner(SortAndFilter owner)
    {
        if (owner == null)
            return;

        var filterPanel = OwnerFilterPanelField?.GetValue(owner) as FilterPanel;
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

        var selected = GetSelectedIndicesDirect(owner, lineId, menuId);
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

        var selectedIndices = OwnerSelectedIndicesField?.GetValue(owner) as Dictionary<int, List<int>>;
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
        RefreshSectionsSummaryAndPanelMethod?.Invoke(owner, null);
        FilterMemoryController.TrySave(owner);
    }

    internal static List<int> GetSelectedIndices(SortAndFilter owner, int lineId, int menuId)
    {
        var selectedIndices = OwnerSelectedIndicesField?.GetValue(owner) as Dictionary<int, List<int>>;
        if (selectedIndices == null)
            return new List<int>();

        return selectedIndices.TryGetValue(GetSelectionKey(lineId, menuId), out var selected)
            ? new List<int>(selected)
            : new List<int>();
    }

    private static List<int> GetSelectedIndicesDirect(SortAndFilter owner, int lineId, int menuId)
    {
        var selectedIndices = OwnerSelectedIndicesField?.GetValue(owner) as Dictionary<int, List<int>>;
        return selectedIndices != null
               && selectedIndices.TryGetValue(GetSelectionKey(lineId, menuId), out var selected)
            ? selected
            : EmptySelection;
    }

    private static readonly List<int> EmptySelection = new();

    private static int BuildStructureSignature(
        SortAndFilter owner,
        Dictionary<(int LineId, int MenuId), FilterSection> sectionMap)
    {
        unchecked
        {
            var signature = owner == null ? 17 : owner.GetInstanceID();
            signature = signature * 31 + sectionMap.Count;
            var entriesHash = 0;
            foreach (var entry in sectionMap)
            {
                var section = entry.Value;
                var contentRoot = section == null ? null : section.GetContentRoot();
                var entryHash = entry.Key.LineId;
                entryHash = entryHash * 397 ^ entry.Key.MenuId;
                entryHash = entryHash * 397 ^ (section == null ? 0 : section.GetInstanceID());
                entryHash = entryHash * 397 ^ (contentRoot == null ? -1 : contentRoot.childCount);
                entriesHash ^= entryHash;
            }

            return signature * 31 + entriesHash;
        }
    }

    private static int BuildSelectionSignature(SortAndFilter owner)
    {
        var selectedIndices = OwnerSelectedIndicesField?.GetValue(owner) as Dictionary<int, List<int>>;
        if (selectedIndices == null || selectedIndices.Count == 0)
            return 0;

        unchecked
        {
            var signature = selectedIndices.Count;
            foreach (var entry in selectedIndices)
            {
                var entryHash = entry.Key;
                var selected = entry.Value;
                if (selected != null)
                {
                    entryHash = entryHash * 397 ^ selected.Count;
                    for (var i = 0; i < selected.Count; i++)
                        entryHash = entryHash * 397 ^ selected[i];
                }

                signature ^= entryHash;
            }

            return signature;
        }
    }

    private static int GetSelectionKey(int lineId, int menuId)
    {
        return lineId * 1000 + menuId;
    }
}
