#nullable disable

using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(Game.Components.SortAndFilter.SortButtonGroup), "RefreshAll")]
internal static class CompactItemSortButtonGroupPatch
{
    private const float CompactButtonWidth = 112f;
    private const float CompactButtonHeight = 48f;
    private const float CompactSpacing = 4f;
    private const float CompactBottomGap = 12f;
    private const float MinButtonWidth = 44f;
    private const float MaxButtonWidth = 112f;
    private const float AncestorWidthMargin = 24f;

    private static readonly HashSet<string> ItemSortLabels = new()
    {
        "名称",
        "品阶",
        "数量",
        "重量",
        "耐久",
        "价值",
        "价格",
        "造诣",
        "工具效果",
        "效果",
        "年龄",
        "心情",
        "健康",
        "状态",
        "属性",
        "命中",
        "技艺",
        "武学",
        "赋性",
        "持有",
        "指令",
        "培养次数",
        "培养",
    };

    private static void Postfix(Game.Components.SortAndFilter.SortButtonGroup __instance)
    {
        if (__instance == null || !Plugin.EnableCompactSortButtons)
            return;

        var itemRoot = Traverse.Create(__instance).Field("itemRoot").GetValue<RectTransform>();
        if (itemRoot == null || !LooksLikeItemSortGroup(itemRoot))
            return;

        ApplyCompactLayout(itemRoot);
    }

    private static bool LooksLikeItemSortGroup(RectTransform itemRoot)
    {
        var matched = 0;
        for (var i = 0; i < itemRoot.childCount; i++)
        {
            var child = itemRoot.GetChild(i);
            if (child == null || !child.gameObject.activeSelf)
                continue;

            var labels = child.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var label in labels)
            {
                var text = label == null ? string.Empty : label.text?.Trim();
                if (!string.IsNullOrEmpty(text) && ItemSortLabels.Contains(text))
                {
                    matched++;
                    break;
                }
            }
        }

        return matched >= Math.Min(5, GetActiveChildCount(itemRoot));
    }

    private static void ApplyCompactLayout(RectTransform itemRoot)
    {
        var activeCount = GetActiveChildCount(itemRoot);
        var availableWidth = GetAvailableWidth(itemRoot);
        var columns = CalculateColumnCount(availableWidth, activeCount);
        var rows = Mathf.Max(1, Mathf.CeilToInt(activeCount / (float)columns));
        var buttonWidth = CalculateButtonWidth(availableWidth, columns);
        var targetWidth = columns * buttonWidth + CompactSpacing * Math.Max(columns - 1, 0);
        var targetHeight = rows * CompactButtonHeight + CompactSpacing * Math.Max(rows - 1, 0) + CompactBottomGap;

        itemRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);

        var grid = itemRoot.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.enabled = true;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.cellSize = new Vector2(buttonWidth, CompactButtonHeight);
            grid.spacing = new Vector2(CompactSpacing, CompactSpacing);
            grid.childAlignment = TextAnchor.UpperLeft;
        }

        var horizontal = itemRoot.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null)
        {
            horizontal.enabled = grid == null && rows <= 1;
            horizontal.spacing = Math.Min(horizontal.spacing, CompactSpacing);
            horizontal.childControlWidth = true;
            horizontal.childForceExpandWidth = false;
        }

        var rootLayoutElement = itemRoot.GetComponent<LayoutElement>() ?? itemRoot.gameObject.AddComponent<LayoutElement>();
        rootLayoutElement.minWidth = -1f;
        rootLayoutElement.preferredWidth = -1f;
        rootLayoutElement.minHeight = targetHeight;
        rootLayoutElement.preferredHeight = targetHeight;
        rootLayoutElement.flexibleWidth = -1f;
        rootLayoutElement.flexibleHeight = 0f;

        for (var i = 0; i < itemRoot.childCount; i++)
        {
            var child = itemRoot.GetChild(i) as RectTransform;
            if (child == null)
                continue;

            child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, buttonWidth);
            child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, CompactButtonHeight);

            var layoutElement = child.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();
            layoutElement.minWidth = buttonWidth;
            layoutElement.preferredWidth = buttonWidth;
            layoutElement.flexibleWidth = 0f;
            layoutElement.minHeight = CompactButtonHeight;
            layoutElement.preferredHeight = CompactButtonHeight;
            layoutElement.flexibleHeight = 0f;

            foreach (var label in child.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (label == null)
                    continue;

                var text = label.text?.Trim();
                if (text == "工具效果")
                    label.SetText("效果");
                else if (text == "培养次数")
                    label.SetText("培养");

                label.enableAutoSizing = true;
                label.fontSizeMax = Math.Min(label.fontSizeMax <= 0f ? 28f : label.fontSizeMax, 20f);
                label.fontSizeMin = Math.Max(label.fontSizeMin, 12f);
                label.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(itemRoot);
    }

    private static int GetActiveChildCount(RectTransform itemRoot)
    {
        var count = 0;
        for (var i = 0; i < itemRoot.childCount; i++)
        {
            var child = itemRoot.GetChild(i);
            if (child != null && child.gameObject.activeSelf)
                count++;
        }

        return Math.Max(count, 1);
    }

    private static int CalculateColumnCount(float availableWidth, int activeCount)
    {
        if (availableWidth <= 0f)
            return activeCount;

        var maxColumns = Mathf.FloorToInt((availableWidth + CompactSpacing) / (CompactButtonWidth + CompactSpacing));
        return Mathf.Clamp(maxColumns, 1, activeCount);
    }

    private static float CalculateButtonWidth(float availableWidth, int columns)
    {
        if (availableWidth <= 0f)
            return CompactButtonWidth;

        var spacingTotal = CompactSpacing * Math.Max(columns - 1, 0);
        var width = (availableWidth - spacingTotal) / columns;
        return Mathf.Clamp(width, MinButtonWidth, MaxButtonWidth);
    }

    private static float GetAvailableWidth(RectTransform itemRoot)
    {
        var currentWidth = itemRoot.rect.width;
        var bestWidth = currentWidth;
        var current = itemRoot.parent as RectTransform;
        while (current != null)
        {
            var local = current.InverseTransformPoint(itemRoot.position);
            var leftSpace = current.rect.width * (1f - current.pivot.x) - local.x;
            var rightAnchoredWidth = Mathf.Max(current.rect.width - Mathf.Max(0f, local.x), 0f);
            var candidate = Mathf.Max(leftSpace, rightAnchoredWidth) - AncestorWidthMargin;
            if (candidate > bestWidth)
                bestWidth = candidate;

            current = current.parent as RectTransform;
        }

        return Mathf.Max(bestWidth, currentWidth);
    }
}

[HarmonyPatch(typeof(SortAndFilter), "Setup")]
internal static class SortAndFilterSetupInlineFilterPatch
{
    private static void Postfix(SortAndFilter __instance)
    {
        InlineFilterButtonsController.GetOrAdd(__instance)?.Refresh();
    }
}

[HarmonyPatch(typeof(SortAndFilter), "UpdateLineActive")]
internal static class SortAndFilterUpdateLineActiveInlineFilterPatch
{
    private static void Postfix(SortAndFilter __instance)
    {
        InlineFilterButtonsController.Get(__instance)?.Refresh();
    }
}

[HarmonyPatch(typeof(SortAndFilter), "ApplyFilterLineStates")]
internal static class SortAndFilterApplyLineStatesInlineFilterPatch
{
    private static void Postfix(SortAndFilter __instance)
    {
        InlineFilterButtonsController.Get(__instance)?.Refresh();
    }
}

[HarmonyPatch(typeof(SortAndFilter), "SetDropdownOption")]
internal static class SortAndFilterSetDropdownOptionInlineFilterPatch
{
    private static void Postfix(SortAndFilter __instance)
    {
        InlineFilterButtonsController.Get(__instance)?.Refresh();
    }
}

[HarmonyPatch(typeof(SortAndFilter), "SetToggleVisible")]
internal static class SortAndFilterSetToggleVisibleInlineFilterPatch
{
    private static void Postfix(SortAndFilter __instance)
    {
        InlineFilterButtonsController.Get(__instance)?.Refresh();
    }
}

[HarmonyPatch(typeof(SortAndFilter), "SetToggleIsOnWithoutNotify")]
internal static class SortAndFilterSetToggleIsOnWithoutNotifyInlineFilterPatch
{
    private static void Postfix(SortAndFilter __instance)
    {
        InlineFilterButtonsController.Get(__instance)?.Refresh();
    }
}

[HarmonyPatch(typeof(SortAndFilter), "SetVisibleDropdownMenus")]
internal static class SortAndFilterSetVisibleDropdownMenusInlineFilterPatch
{
    private static void Postfix(SortAndFilter __instance)
    {
        InlineFilterButtonsController.Get(__instance)?.Refresh();
    }
}

[HarmonyPatch(typeof(SortAndFilter), "ClearAllFilter")]
internal static class SortAndFilterClearAllInlineFilterPatch
{
    private static void Postfix(SortAndFilter __instance)
    {
        InlineFilterButtonsController.Get(__instance)?.Refresh();
    }
}

[HarmonyPatch(typeof(SortAndFilter), "IsPointOverEntryButton")]
internal static class SortAndFilterInlineFilterPointerPatch
{
    private static void Postfix(SortAndFilter __instance, Vector2 screenPoint, ref bool __result)
    {
        if (!__result && Plugin.EnableInlineFilterButtons)
            __result = InlineFilterButtonsController.Get(__instance)?.ContainsScreenPoint(screenPoint) == true;
    }
}

[HarmonyPatch(typeof(SortAndFilter), "GetSummaryItems")]
internal static class SortAndFilterGetSummaryItemsInlineFilterPatch
{
    private static void Postfix(SortAndFilter __instance, ref List<SortAndFilter.SummaryItemData> __result)
    {
        InlineFilterButtonsController.Get(__instance)?.RemoveInlineRootSummary(__result);
    }
}

[HarmonyPatch(typeof(FilterPanel), "Refresh")]
internal static class FilterPanelRefreshInlineFilterPatch
{
    private static void Postfix(FilterPanel __instance)
    {
        InlineFilterButtonsController.GetFromPanel(__instance)?.AfterPanelRefresh();
    }
}

[HarmonyPatch(typeof(FilterPanel), "RefreshFilterOptionCounts")]
internal static class FilterPanelRefreshCountsInlineFilterPatch
{
    private static void Postfix(FilterPanel __instance, IReadOnlyList<OptionCountData> optionCounts)
    {
        InlineFilterButtonsController.GetFromPanel(__instance)?.AfterPanelRefresh();
    }
}

internal sealed class InlineFilterButtonsController : MonoBehaviour
{
    private const float InlinePanelHeight = 58f;
    private const float InlineButtonHeight = 38f;
    private const float InlineButtonMinWidth = 46f;
    private const float InlineButtonPreferredWidth = 60f;
    private const float InlineButtonMaxWidth = 96f;
    private const float InlineButtonSpacing = 4f;
    private const float CompactParentSpacing = 12f;

    private SortAndFilter _owner;
    private FilterPanel _filterPanel;
    private RectTransform _entryRect;
    private FilterSection _inlineSection;
    private VerticalLayoutGroup _parentVerticalLayout;
    private float _originalParentSpacing;
    private bool _hasOriginalParentSpacing;
    private int _lineId;
    private int _lineIndex;
    private int _menuId;
    private bool _hasInlineRoot;
    private readonly List<int> _inlineOptionIndexMap = new();
    private bool _hideInlineAllOption;

    internal static InlineFilterButtonsController GetOrAdd(SortAndFilter owner)
    {
        if (owner == null)
            return null;

        var controller = owner.GetComponent<InlineFilterButtonsController>();
        if (controller == null)
            controller = owner.gameObject.AddComponent<InlineFilterButtonsController>();

        controller.Initialize(owner);
        return controller;
    }

    internal static InlineFilterButtonsController Get(SortAndFilter owner)
    {
        return owner == null ? null : owner.GetComponent<InlineFilterButtonsController>();
    }

    internal static InlineFilterButtonsController GetFromPanel(FilterPanel panel)
    {
        var owner = panel == null ? null : Traverse.Create(panel).Field("_owner").GetValue<SortAndFilter>();
        return Get(owner);
    }

    internal void Initialize(SortAndFilter owner)
    {
        _owner = owner;
        _filterPanel = Traverse.Create(owner).Field("filterPanel").GetValue<FilterPanel>();
        var entryToggle = Traverse.Create(owner).Field("entryToggle").GetValue();
        _entryRect = entryToggle is Component component ? component.transform as RectTransform : null;
    }

    internal void Refresh()
    {
        if (_owner == null)
            return;

        if (!Plugin.EnableInlineFilterButtons)
        {
            _hasInlineRoot = false;
            RestoreEntryButton();
            SetInlineActive(false);
            return;
        }

        if (ShouldSuppressInlineFilterForMakeList())
        {
            _hasInlineRoot = false;
            HideEntryButton();
            SetInlineActive(false);
            RefreshOwnerSummary();
            return;
        }

        if (!TryGetRootSection(out var section, out var menuConfig))
        {
            _hasInlineRoot = false;
            RestoreEntryButton();
            SetInlineActive(false);
            return;
        }

        HideEntryButton();
        EnsureInlineSection();
        if (_inlineSection == null)
        {
            _hasInlineRoot = false;
            return;
        }

        _lineId = section.LineId;
        _lineIndex = section.LineIndex;
        _menuId = section.MenuId;

        var itemConfigs = menuConfig.DropdownConfig.ItemConfigs;
        if (itemConfigs == null)
        {
            _hasInlineRoot = false;
            SetInlineActive(false);
            return;
        }

        _hasInlineRoot = true;
        var inlineItemConfigs = BuildInlineItemConfigs(section, itemConfigs);
        _inlineSection.gameObject.SetActive(true);
        _inlineSection.Setup(_menuId, string.Empty, inlineItemConfigs, OnInlineSelectionChanged);
        _inlineSection.SetSelectedIndex(ToDisplayOptionIndex(_owner.GetInitialSectionState(_lineId, _menuId)), notify: false);
        ApplyInlineLayout();
        AfterPanelRefresh();
        RefreshOwnerSummary();
    }

    internal void AfterPanelRefresh()
    {
        if (!Plugin.EnableInlineFilterButtons || _owner == null || _filterPanel == null)
            return;

        HidePanelRootSection();
    }

    private List<FilterDropdownItemConfig> BuildInlineItemConfigs(SortAndFilter.SectionViewData section, List<FilterDropdownItemConfig> itemConfigs)
    {
        _inlineOptionIndexMap.Clear();
        _hideInlineAllOption = false;

        if (itemConfigs == null)
            return new List<FilterDropdownItemConfig>();

        if (IsMakeToolSection(section) && TryGetCurrentMakeLifeSkillType(out var lifeSkillType))
        {
            var filtered = new List<FilterDropdownItemConfig>();
            for (var i = 0; i < itemConfigs.Count; i++)
            {
                if (IsLifeSkillOption(itemConfigs[i], lifeSkillType))
                {
                    _inlineOptionIndexMap.Add(i);
                    filtered.Add(itemConfigs[i]);
                }
            }

            if (filtered.Count > 0)
            {
                _hideInlineAllOption = filtered.Count == 1;
                return filtered;
            }
        }

        for (var i = 0; i < itemConfigs.Count; i++)
            _inlineOptionIndexMap.Add(i);
        return itemConfigs;
    }

    private bool IsMakeToolSection(SortAndFilter.SectionViewData section)
    {
        return section.LineId == (int)Game.Components.SortAndFilter.Item.EFilterLine.CraftToolFilter && section.MenuId == 0;
    }

    private bool TryGetCurrentMakeLifeSkillType(out sbyte lifeSkillType)
    {
        var viewMake = _owner == null ? null : _owner.GetComponentInParent<Game.Views.Make.ViewMake>(true);
        if (viewMake != null)
        {
            lifeSkillType = viewMake.CurLifeSkillType;
            return true;
        }

        lifeSkillType = default;
        return false;
    }

    private static bool IsLifeSkillOption(FilterDropdownItemConfig itemConfig, sbyte lifeSkillType)
    {
        Config.LifeSkillTypeItem lifeSkillTypeItem;
        try
        {
            lifeSkillTypeItem = Config.LifeSkillType.Instance[lifeSkillType];
        }
        catch
        {
            return false;
        }

        return itemConfig.Text.GetString() == lifeSkillTypeItem.Name;
    }

    private int ToDisplayOptionIndex(int originalIndex)
    {
        if (originalIndex < 0)
            return -1;

        var displayIndex = _inlineOptionIndexMap.IndexOf(originalIndex);
        return displayIndex >= 0 ? displayIndex : -1;
    }

    private int ToOriginalOptionIndex(int displayIndex)
    {
        if (displayIndex < 0)
            return -1;

        return displayIndex >= 0 && displayIndex < _inlineOptionIndexMap.Count
            ? _inlineOptionIndexMap[displayIndex]
            : displayIndex;
    }

    internal void RemoveInlineRootSummary(List<SortAndFilter.SummaryItemData> items)
    {
        if (!Plugin.EnableInlineFilterButtons || !_hasInlineRoot || items == null)
            return;

        items.RemoveAll(item => item.LineId == _lineId && item.MenuId == _menuId);
    }

    private void RefreshOwnerSummary()
    {
        if (_owner == null)
            return;

        Traverse.Create(_owner).Method("RefreshSummary").GetValue();
    }

    internal bool ContainsScreenPoint(Vector2 screenPoint)
    {
        if (_inlineSection == null || !_inlineSection.gameObject.activeInHierarchy)
            return false;

        var rect = _inlineSection.transform as RectTransform;
        return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, UIManager.Instance.UiCamera);
    }

    private bool TryGetRootSection(out SortAndFilter.SectionViewData section, out DetailedFilterMenuConfig menuConfig)
    {
        if (TryGetPreferredMakeSection(out section, out menuConfig))
            return true;

        foreach (var candidate in _owner.Sections)
        {
            if (!candidate.IsActive || candidate.Type == Game.Components.SortAndFilter.ESortAndFilterOneLineType.ToggleGroup)
                continue;

            var maybeConfig = _owner.GetMenuConfig(candidate.LineIndex, candidate.MenuId);
            if (!maybeConfig.HasValue)
                continue;

            var config = maybeConfig.Value;
            if (config.DropdownContext.Dependency.HasValue)
                continue;

            section = candidate;
            menuConfig = config;
            return true;
        }

        section = default;
        menuConfig = default;
        return false;
    }

    private bool TryGetPreferredMakeSection(out SortAndFilter.SectionViewData section, out DetailedFilterMenuConfig menuConfig)
    {
        if (!IsInMakeView())
        {
            section = default;
            menuConfig = default;
            return false;
        }

        if (TryFindSection((int)Game.Components.SortAndFilter.Item.EFilterLine.CraftToolFilter, 0, out section, out menuConfig))
            return true;

        return false;
    }

    private bool ShouldSuppressInlineFilterForMakeList()
    {
        if (!IsInMakeView())
            return false;

        return TryFindSection((int)Game.Components.SortAndFilter.Item.EFilterLine.MaterialAdditionalFilter, 1, out _, out _)
            || TryFindSection((int)Game.Components.SortAndFilter.Item.EFilterLine.CraftToolFilter, 0, out _, out _);
    }

    private bool IsInMakeView()
    {
        return _owner != null && _owner.GetComponentInParent<Game.Views.Make.ViewMake>(true) != null;
    }

    private bool TryFindSection(int lineId, int menuId, out SortAndFilter.SectionViewData section, out DetailedFilterMenuConfig menuConfig)
    {
        foreach (var candidate in _owner.Sections)
        {
            if (!candidate.IsActive || candidate.LineId != lineId || candidate.MenuId != menuId)
                continue;

            var maybeConfig = _owner.GetMenuConfig(candidate.LineIndex, candidate.MenuId);
            if (!maybeConfig.HasValue)
                continue;

            section = candidate;
            menuConfig = maybeConfig.Value;
            return true;
        }

        section = default;
        menuConfig = default;
        return false;
    }

    private void EnsureInlineSection()
    {
        if (_inlineSection != null)
            return;

        if (_filterPanel == null)
            _filterPanel = Traverse.Create(_owner).Field("filterPanel").GetValue<FilterPanel>();

        var sectionTemplate = Traverse.Create(_filterPanel).Field("sectionTemplate").GetValue<FilterSection>();
        if (sectionTemplate == null || _entryRect == null)
            return;

        var parent = _entryRect.parent as RectTransform;
        _inlineSection = Instantiate(sectionTemplate, parent);
        _inlineSection.name = "BetterTaiwuScrollInlineFilterButtons";
        _inlineSection.transform.SetSiblingIndex(_entryRect.GetSiblingIndex());
        _inlineSection.gameObject.SetActive(true);
    }

    private void ApplyInlineLayout()
    {
        if (_inlineSection == null || _entryRect == null)
            return;

        var rect = _inlineSection.transform as RectTransform;
        if (rect == null)
            return;

        TuneParentLayout(rect.parent as RectTransform);
        rect.anchorMin = _entryRect.anchorMin;
        rect.anchorMax = _entryRect.anchorMax;
        rect.pivot = _entryRect.pivot;
        rect.anchoredPosition = _entryRect.anchoredPosition;
        rect.localScale = Vector3.one;

        var contentRoot = _inlineSection.GetContentRoot();
        if (contentRoot == null)
            return;

        contentRoot.anchorMin = Vector2.zero;
        contentRoot.anchorMax = Vector2.one;
        contentRoot.offsetMin = Vector2.zero;
        contentRoot.offsetMax = Vector2.zero;
        ApplyInlineOptionVisibility(contentRoot);
        ApplyShortInlineLabels(contentRoot);
        AttachInlineClickRelays(contentRoot);

        var grid = contentRoot.GetComponent<GridLayoutGroup>();
        var panelHeight = InlinePanelHeight;
        var panelWidth = GetInlineAvailableWidth(rect);
        if (grid != null)
        {
            var activeCount = Math.Max(CountActiveChildren(contentRoot), 1);
            var columns = CalculateInlineColumnCount(panelWidth, activeCount);
            var rows = Mathf.Max(1, Mathf.CeilToInt(activeCount / (float)columns));
            var width = Mathf.Clamp((panelWidth - InlineButtonSpacing * Math.Max(columns - 1, 0)) / columns, InlineButtonMinWidth, InlineButtonMaxWidth);
            panelHeight = rows * InlineButtonHeight + InlineButtonSpacing * Math.Max(rows - 1, 0);

            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.cellSize = new Vector2(width, InlineButtonHeight);
            grid.spacing = new Vector2(InlineButtonSpacing, InlineButtonSpacing);
        }

        rect.sizeDelta = new Vector2(panelWidth, panelHeight);
        var layoutElement = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = panelHeight;
        layoutElement.preferredHeight = panelHeight;
        layoutElement.minWidth = panelWidth;
        layoutElement.preferredWidth = panelWidth;
        layoutElement.flexibleHeight = 0f;
        layoutElement.flexibleWidth = 0f;

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private static int CalculateInlineColumnCount(float availableWidth, int activeCount)
    {
        if (availableWidth <= 0f)
            return activeCount;

        var preferredColumns = Mathf.FloorToInt((availableWidth + InlineButtonSpacing) / (InlineButtonPreferredWidth + InlineButtonSpacing));
        return Mathf.Clamp(preferredColumns, 1, activeCount);
    }

    private float GetInlineAvailableWidth(RectTransform rect)
    {
        var width = Mathf.Abs(_entryRect == null ? 0f : _entryRect.rect.width);
        if (width <= 0f)
            width = Mathf.Abs(_entryRect == null ? 0f : _entryRect.sizeDelta.x);
        if (width <= 0f)
            width = Mathf.Abs(rect.rect.width);
        if (rect.parent is RectTransform parent)
            width = Mathf.Max(width, Mathf.Abs(parent.rect.width));

        return Mathf.Max(width, InlineButtonMinWidth);
    }

    private void ApplyShortInlineLabels(RectTransform contentRoot)
    {
        foreach (var label in contentRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (label == null)
                continue;

            var text = label.text?.Trim();
            switch (text)
            {
                case "功法书":
                    label.SetText("功法");
                    break;
                case "技艺书":
                    label.SetText("技艺");
                    break;
                case "西域珍宝":
                    label.SetText("西域");
                    break;
            }

            label.enableAutoSizing = true;
            label.fontSizeMax = Math.Min(label.fontSizeMax <= 0f ? 28f : label.fontSizeMax, 20f);
            label.fontSizeMin = Math.Max(label.fontSizeMin, 12f);
            label.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    private void ApplyInlineOptionVisibility(RectTransform contentRoot)
    {
        if (contentRoot == null || contentRoot.childCount == 0)
            return;

        var allOption = contentRoot.GetChild(0);
        if (allOption != null)
            allOption.gameObject.SetActive(!_hideInlineAllOption);
    }

    private void AttachInlineClickRelays(RectTransform contentRoot)
    {
        if (contentRoot == null)
            return;

        for (var i = 0; i < contentRoot.childCount; i++)
        {
            var child = contentRoot.GetChild(i);
            if (child == null)
                continue;

            var relay = child.GetComponent<InlineFilterOptionClickRelay>() ?? child.gameObject.AddComponent<InlineFilterOptionClickRelay>();
            relay.Setup(this, i - 1);
        }
    }

    internal bool IsInlineDisplayOptionSelected(int displayIndex)
    {
        return _inlineSection != null && _inlineSection.SelectedIndex == displayIndex;
    }

    internal void OnInlineDisplayOptionRepeatedClick(int displayIndex)
    {
        if (_owner == null)
            return;

        var originalIndex = ToOriginalOptionIndex(displayIndex);
        if (originalIndex < 0 || !HasFollowUpSections())
        {
            _owner.CloseFilterPanel();
            return;
        }

        _owner.OpenFilterPanel();
        HidePanelRootSection();
    }

    private static int CountActiveChildren(RectTransform root)
    {
        var count = 0;
        for (var i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child != null && child.gameObject.activeSelf)
                count++;
        }

        return count;
    }
    private void TuneParentLayout(RectTransform parent)
    {
        if (parent == null)
            return;

        var verticalLayout = parent.GetComponent<VerticalLayoutGroup>();
        if (verticalLayout == null)
            return;

        if (!_hasOriginalParentSpacing || _parentVerticalLayout != verticalLayout)
        {
            _parentVerticalLayout = verticalLayout;
            _originalParentSpacing = verticalLayout.spacing;
            _hasOriginalParentSpacing = true;
        }

        verticalLayout.spacing = Math.Min(_originalParentSpacing, CompactParentSpacing);
    }

    private void OnInlineSelectionChanged(int selectedIndex)
    {
        if (_owner == null)
            return;

        selectedIndex = ToOriginalOptionIndex(selectedIndex);
        _owner.SetDropdownOption(_lineId, _menuId, selectedIndex);
        _owner.CloseFilterPanel();
    }

    private bool HasFollowUpSections()
    {
        foreach (var section in _owner.Sections)
        {
            if (!section.IsActive || section.Type == Game.Components.SortAndFilter.ESortAndFilterOneLineType.ToggleGroup)
                continue;

            if (section.LineId != _lineId || section.MenuId != _menuId)
                return true;
        }

        return false;
    }

    private void HidePanelRootSection()
    {
        if (_filterPanel == null)
            return;

        var sectionMap = Traverse.Create(_filterPanel)
            .Field("_sectionMap")
            .GetValue<Dictionary<(int, int), FilterSection>>();
        if (sectionMap == null)
            return;

        if (sectionMap.TryGetValue((_lineId, _menuId), out var rootSection)
            && rootSection != null
            && rootSection != _inlineSection)
            rootSection.gameObject.SetActive(false);
    }

    private void HideEntryButton()
    {
        if (_entryRect != null)
            _entryRect.gameObject.SetActive(false);
    }

    private void RestoreEntryButton()
    {
        if (_entryRect == null || _owner == null)
            return;

        RestoreParentLayout();
        var forceHideEntry = Traverse.Create(_owner).Field("_forceHideEntry").GetValue<bool>();
        _entryRect.gameObject.SetActive(!forceHideEntry && _owner.ShowFilterEntryButton);
    }

    private void RestoreParentLayout()
    {
        if (_hasOriginalParentSpacing && _parentVerticalLayout != null)
            _parentVerticalLayout.spacing = _originalParentSpacing;
    }

    private void SetInlineActive(bool active)
    {
        if (_inlineSection != null)
            _inlineSection.gameObject.SetActive(active);
    }
}

internal sealed class InlineFilterOptionClickRelay : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    private InlineFilterButtonsController _owner;
    private int _displayIndex;
    private bool _wasSelectedOnPointerDown;

    internal void Setup(InlineFilterButtonsController owner, int displayIndex)
    {
        _owner = owner;
        _displayIndex = displayIndex;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _wasSelectedOnPointerDown = _owner != null && _owner.IsInlineDisplayOptionSelected(_displayIndex);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_wasSelectedOnPointerDown)
            _owner?.OnInlineDisplayOptionRepeatedClick(_displayIndex);
    }
}
