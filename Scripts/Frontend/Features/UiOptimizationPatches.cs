#nullable disable

using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(Game.Views.Exchange.ViewExchangeBase), "OnSelfItemRender")]
internal static class ExchangeSelfItemTextColorPatch
{
    private static void Postfix(
        Game.Views.Exchange.ViewExchangeBase __instance,
        GameData.Domains.Item.Display.ITradeableContent itemData,
        Game.Components.ListStyleGeneralScroll.Item.RowItemLine rowItemLine)
    {
        ExchangeItemTextColorHelper.Apply(__instance, rowItemLine);
    }
}

[HarmonyPatch(typeof(Game.Views.Exchange.ViewExchangeBase), "OnTargetItemRender")]
internal static class ExchangeTargetItemTextColorPatch
{
    private static void Postfix(
        Game.Views.Exchange.ViewExchangeBase __instance,
        GameData.Domains.Item.Display.ITradeableContent itemData,
        Game.Components.ListStyleGeneralScroll.Item.RowItemLine rowItemLine)
    {
        ExchangeItemTextColorHelper.Apply(__instance, rowItemLine);
    }
}

[HarmonyPatch(typeof(Game.Views.Exchange.ViewExchangeBase), "OnExchangingTargetItemRender")]
internal static class ExchangeTargetExchangedItemTextColorPatch
{
    private static void Postfix(
        Game.Views.Exchange.ViewExchangeBase __instance,
        GameData.Domains.Item.Display.ITradeableContent itemData,
        Game.Components.ListStyleGeneralScroll.Item.RowItemLine rowItemLine)
    {
        ExchangeItemTextColorHelper.Apply(__instance, rowItemLine);
    }
}

internal static class ExchangeItemTextColorHelper
{
    private static readonly Color ItemInfoTextColor = new(1f, 1f, 1f, 1f);

    internal static void Apply(
        Game.Views.Exchange.ViewExchangeBase view,
        Game.Components.ListStyleGeneralScroll.Item.RowItemLine rowItemLine)
    {
        if (!IsEnabledFor(view) || rowItemLine == null)
        {
            return;
        }

        var cache = rowItemLine.GetComponent<ExchangeItemTextColorCache>()
            ?? rowItemLine.gameObject.AddComponent<ExchangeItemTextColorCache>();

        foreach (var group in cache.CanvasGroups)
        {
            if (group != null && group.alpha < 1f)
                group.alpha = 1f;
        }

        foreach (var text in cache.Texts)
        {
            if (text == null)
                continue;

            var color = text.color;
            var wasOverrideColorTags = text.overrideColorTags;
            text.overrideColorTags = false;
            text.alpha = 1f;
            text.canvasRenderer.SetAlpha(1f);
            if (wasOverrideColorTags || IsDimNeutralText(color))
                text.color = ItemInfoTextColor;
            else if (color.a < 1f)
                text.color = new Color(color.r, color.g, color.b, 1f);
        }
    }

    private static bool IsEnabledFor(Game.Views.Exchange.ViewExchangeBase view)
    {
        return view switch
        {
            Game.Views.Exchange.ViewShop => Plugin.EnableShopSelfItemTextColor,
            Game.Views.Exchange.ViewShopGift => Plugin.EnableShopGiftSelfItemTextColor,
            Game.Views.Exchange.ViewSettlementShop => Plugin.EnableSettlementShopSelfItemTextColor,
            Game.Views.Exchange.ViewWarehouse => Plugin.EnableWarehouseSelfItemTextColor,
            Game.Views.Exchange.ViewExchangeBook => Plugin.EnableExchangeBookSelfItemTextColor,
            Game.Views.Exchange.ViewExchange => Plugin.EnableExchangeSelfItemTextColor,
            _ => false,
        };
    }

    private static bool IsDimNeutralText(Color color)
    {
        var max = Mathf.Max(color.r, color.g, color.b);
        var min = Mathf.Min(color.r, color.g, color.b);
        return max - min < 0.08f && max < 0.9f;
    }
}

internal sealed class ExchangeItemTextColorCache : MonoBehaviour
{
    private CanvasGroup[] _canvasGroups;
    private TMP_Text[] _texts;

    internal CanvasGroup[] CanvasGroups => _canvasGroups ??= GetComponentsInChildren<CanvasGroup>(true);

    internal TMP_Text[] Texts => _texts ??= GetComponentsInChildren<TMP_Text>(true);
}

[HarmonyPatch(typeof(Game.Components.SortAndFilter.SortButtonGroup), "RefreshAll")]
internal static class CompactItemSortButtonGroupPatch
{
    private const float CompactButtonWidth = 112f;
    private const float CompactButtonHeight = 48f;
    private const float CompactSpacing = 4f;
    private const float CompactTopGap = 8f;
    private const float CompactTopGapWithSummary = 2f;
    private const float CompactBottomGap = 12f;
    private const float MinButtonWidth = 44f;
    private const float MaxButtonWidth = 112f;
    private const float AncestorWidthMargin = 24f;
    private const int PreferSingleRowMaxButtonCount = 8;

    private static readonly HashSet<string> ItemSortLabels = new()
    {
        "名称",
        "品阶",
        "数量",
        "类型",
        "重量",
        "耐久",
        "效率",
        "好感",
        "功法造诣",
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

    private static readonly HashSet<string> TeamSortLabels = new()
    {
        "名称",
        "品阶",
        "行为",
        "赋性",
        "身份",
        "年龄",
        "健康",
        "伤势",
        "内息紊乱",
        "魅力",
        "立场",
        "心情",
        "好感",
        "戒心",
        "轮回",
        "名誉",
        "膂力",
        "体质",
        "灵敏",
        "根骨",
        "悟性",
        "定力",
        "音律",
        "弈棋",
        "诗书",
        "绘画",
        "术数",
        "品鉴",
        "锻造",
        "制木",
        "医术",
        "毒术",
        "织锦",
        "巧匠",
        "道法",
        "佛学",
        "厨艺",
        "杂学",
        "合道",
        "成长",
        "食材",
        "木材",
        "金铁",
        "玉石",
        "织物",
        "药材",
        "银钱",
        "威望",
        "负重",
        "行囊",
        "内功",
        "身法",
        "绝技",
        "拳掌",
        "指法",
        "腿法",
        "暗器",
        "剑法",
        "刀法",
        "长兵",
        "奇门",
        "软兵",
        "御射",
        "乐器",
        "持有",
        "指令",
    };

    private static void Postfix(Game.Components.SortAndFilter.SortButtonGroup __instance)
    {
        ApplyIfRecognized(__instance, scheduleDelayed: true);
    }

    internal static bool ApplyIfRecognized(Game.Components.SortAndFilter.SortButtonGroup __instance, bool scheduleDelayed)
    {
        if (__instance == null || !Plugin.EnableCompactSortButtons)
            return false;

        var itemRoot = Traverse.Create(__instance).Field("itemRoot").GetValue<RectTransform>();
        if (itemRoot == null)
            return false;

        var activeCount = GetActiveChildCount(itemRoot);
        var isTeamSortGroup = IsInTeamView(__instance) || LooksLikeTeamSortGroup(itemRoot);
        if (!isTeamSortGroup && !LooksLikeItemSortGroup(itemRoot))
            return false;

        var topGap = HasVisibleFilterSummary(__instance) ? CompactTopGapWithSummary : CompactTopGap;
        var preferSingleRow = isTeamSortGroup || activeCount <= PreferSingleRowMaxButtonCount;
        var availableWidth = GetAvailableWidth(itemRoot, allowFullAncestorWidth: true);
        var signature = BuildLayoutSignature(itemRoot, activeCount, isTeamSortGroup, topGap, preferSingleRow, availableWidth);
        var state = CompactSortButtonLayoutState.GetOrAdd(__instance);
        if (state != null && state.IsApplied(signature))
            return true;

        ApplyCompactLayout(__instance.transform as RectTransform, itemRoot, topGap, preferSingleRow, availableWidth);
        state?.MarkApplied(signature);
        if (scheduleDelayed)
            state?.Schedule();
        return true;
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

        return matched >= Math.Min(3, GetActiveChildCount(itemRoot));
    }

    private static bool LooksLikeTeamSortGroup(RectTransform itemRoot)
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
                if (!string.IsNullOrEmpty(text) && TeamSortLabels.Contains(text))
                {
                    matched++;
                    break;
                }
            }
        }

        return matched >= Math.Min(3, GetActiveChildCount(itemRoot));
    }

    private static bool IsInTeamView(Component component)
    {
        return component != null && component.GetComponentInParent<Game.Views.CharacterMenu.Team.ViewCharacterMenuTeam>(true) != null;
    }

    private static void ApplyCompactLayout(RectTransform sortGroupRect, RectTransform itemRoot, float topGap, bool preferSingleRow, float availableWidth)
    {
        var activeCount = GetActiveChildCount(itemRoot);
        var columns = preferSingleRow
            ? CalculateSingleRowColumnCount(availableWidth, activeCount)
            : CalculateColumnCount(availableWidth, activeCount);
        var rows = Mathf.Max(1, Mathf.CeilToInt(activeCount / (float)columns));
        var buttonWidth = CalculateButtonWidth(availableWidth, columns);
        var targetWidth = columns * buttonWidth + CompactSpacing * Math.Max(columns - 1, 0);
        var targetHeight = topGap + rows * CompactButtonHeight + CompactSpacing * Math.Max(rows - 1, 0) + CompactBottomGap;

        if (preferSingleRow)
        {
            if (sortGroupRect != null)
                sortGroupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
            itemRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
        }
        itemRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);

        if (sortGroupRect != null)
            sortGroupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);

        var grid = itemRoot.GetComponent<GridLayoutGroup>() ?? itemRoot.gameObject.AddComponent<GridLayoutGroup>();
        grid.enabled = true;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.cellSize = new Vector2(buttonWidth, CompactButtonHeight);
        grid.spacing = new Vector2(CompactSpacing, CompactSpacing);
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.padding = new RectOffset(grid.padding.left, grid.padding.right, Mathf.RoundToInt(topGap), grid.padding.bottom);

        var horizontal = itemRoot.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null)
            horizontal.enabled = false;

        var vertical = itemRoot.GetComponent<VerticalLayoutGroup>();
        if (vertical != null)
            vertical.enabled = false;

        var rootLayoutElement = itemRoot.GetComponent<LayoutElement>() ?? itemRoot.gameObject.AddComponent<LayoutElement>();
        rootLayoutElement.minWidth = preferSingleRow ? targetWidth : -1f;
        rootLayoutElement.preferredWidth = preferSingleRow ? targetWidth : -1f;
        rootLayoutElement.minHeight = targetHeight;
        rootLayoutElement.preferredHeight = targetHeight;
        rootLayoutElement.flexibleWidth = -1f;
        rootLayoutElement.flexibleHeight = 0f;

        if (sortGroupRect != null)
        {
            var groupLayoutElement = sortGroupRect.GetComponent<LayoutElement>() ?? sortGroupRect.gameObject.AddComponent<LayoutElement>();
            groupLayoutElement.minWidth = preferSingleRow ? targetWidth : -1f;
            groupLayoutElement.preferredWidth = preferSingleRow ? targetWidth : -1f;
            groupLayoutElement.minHeight = targetHeight;
            groupLayoutElement.preferredHeight = targetHeight;
            groupLayoutElement.flexibleWidth = preferSingleRow ? 0f : -1f;
            groupLayoutElement.flexibleHeight = 0f;
        }

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
                else if (text == "功法造诣")
                    label.SetText("造诣");

                label.enableAutoSizing = true;
                label.fontSizeMax = Math.Min(label.fontSizeMax <= 0f ? 28f : label.fontSizeMax, 20f);
                label.fontSizeMin = Math.Max(label.fontSizeMin, 12f);
                label.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(itemRoot);
        if (sortGroupRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(sortGroupRect);
    }

    private static string BuildLayoutSignature(
        RectTransform itemRoot,
        int activeCount,
        bool isTeamSortGroup,
        float topGap,
        bool preferSingleRow,
        float availableWidth)
    {
        var builder = new StringBuilder(128);
        builder.Append(activeCount).Append('|')
            .Append(isTeamSortGroup ? 1 : 0).Append('|')
            .Append(Mathf.RoundToInt(topGap)).Append('|')
            .Append(preferSingleRow ? 1 : 0).Append('|')
            .Append(Mathf.RoundToInt(availableWidth)).Append('|')
            .Append(itemRoot == null ? 0 : itemRoot.childCount);

        if (itemRoot == null)
            return builder.ToString();

        for (var i = 0; i < itemRoot.childCount; i++)
        {
            var child = itemRoot.GetChild(i);
            builder.Append('|').Append(child != null && child.gameObject.activeSelf ? 1 : 0).Append(':');
            if (child == null)
                continue;

            var labels = child.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (var j = 0; j < labels.Length; j++)
            {
                var text = labels[j] == null ? string.Empty : labels[j].text?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    builder.Append(text);
                    break;
                }
            }
        }

        return builder.ToString();
    }

    private static bool HasVisibleFilterSummary(Game.Components.SortAndFilter.SortButtonGroup sortButtonGroup)
    {
        var owner = sortButtonGroup == null ? null : sortButtonGroup.GetComponentInParent<SortAndFilter>(true);
        if (owner == null)
            return false;

        var summaryArea = Traverse.Create(owner).Field("filterSummaryArea").GetValue<RectTransform>();
        return summaryArea != null && summaryArea.gameObject.activeInHierarchy;
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

    private static int CalculateSingleRowColumnCount(float availableWidth, int activeCount)
    {
        if (availableWidth <= 0f)
            return activeCount;

        var maxColumnsAtMinWidth = Mathf.FloorToInt((availableWidth + CompactSpacing) / (MinButtonWidth + CompactSpacing));
        return Mathf.Clamp(maxColumnsAtMinWidth, 1, activeCount);
    }

    private static float CalculateButtonWidth(float availableWidth, int columns)
    {
        if (availableWidth <= 0f)
            return CompactButtonWidth;

        var spacingTotal = CompactSpacing * Math.Max(columns - 1, 0);
        var width = (availableWidth - spacingTotal) / columns;
        return Mathf.Clamp(width, MinButtonWidth, MaxButtonWidth);
    }

    private static float GetAvailableWidth(RectTransform itemRoot, bool allowFullAncestorWidth)
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

            if (allowFullAncestorWidth)
            {
                var fullAncestorWidth = current.rect.width - AncestorWidthMargin;
                if (fullAncestorWidth > bestWidth)
                    bestWidth = fullAncestorWidth;
            }

            current = current.parent as RectTransform;
        }

        return Mathf.Max(bestWidth, currentWidth);
    }
}

internal sealed class CompactSortButtonLayoutState : MonoBehaviour
{
    private const int RefreshFrames = 6;

    private Game.Components.SortAndFilter.SortButtonGroup _owner;
    private int _remainingFrames;
    private string _appliedSignature;

    internal static CompactSortButtonLayoutState GetOrAdd(Game.Components.SortAndFilter.SortButtonGroup owner)
    {
        if (owner == null)
            return null;

        var state = owner.GetComponent<CompactSortButtonLayoutState>();
        if (state == null)
            state = owner.gameObject.AddComponent<CompactSortButtonLayoutState>();

        state._owner = owner;
        return state;
    }

    internal void Schedule()
    {
        _remainingFrames = RefreshFrames;
    }

    internal bool IsApplied(string signature)
    {
        return !string.IsNullOrEmpty(signature) && signature == _appliedSignature;
    }

    internal void MarkApplied(string signature)
    {
        _appliedSignature = signature;
    }

    private void LateUpdate()
    {
        if (_remainingFrames <= 0)
            return;

        _remainingFrames--;
        if (_owner == null || !_owner.gameObject.activeInHierarchy)
            return;

        CompactItemSortButtonGroupPatch.ApplyIfRecognized(_owner, scheduleDelayed: false);
    }
}

[HarmonyPatch(
    typeof(Game.Components.SortAndFilter.FilterToggle),
    "Refresh",
    new[] { typeof(Game.Components.SortAndFilter.FilterToggleConfig) })]
internal static class SimplifiedFilterTogglePatch
{
    private static void Postfix(
        Game.Components.SortAndFilter.FilterToggle __instance,
        Game.Components.SortAndFilter.FilterToggleConfig config)
    {
        if (__instance == null)
            return;

        SimplifiedFilterToggleVisual.ApplyFromRefresh(__instance, config.TipContent.GetString());
    }
}

[HarmonyPatch(typeof(Game.Components.SortAndFilter.ToggleGroupLine), "RefreshToggles")]
internal static class SimplifiedFilterToggleGroupLayoutPatch
{
    private static void Postfix(Game.Components.SortAndFilter.ToggleGroupLine __instance)
    {
        SimplifiedFilterToggleVisual.ApplyToggleGroupLayout(__instance);
        InventorySearchBoxOptimizationSupport.EnsureToggleLineSearchBox(__instance);
    }
}

internal static class SimplifiedFilterToggleVisual
{
    internal const float SimplifiedWidth = 68f;
    internal const float SimplifiedHeight = 38f;
    internal const float SimplifiedSpacing = 4f;

    internal static void ApplyFromRefresh(Game.Components.SortAndFilter.FilterToggle toggle, string labelText)
    {
        if (toggle == null)
            return;

        var state = toggle.GetComponent<SimplifiedFilterToggleState>() ?? toggle.gameObject.AddComponent<SimplifiedFilterToggleState>();
        state.SetText(labelText);
        state.SetHasFollowUpMenu(HasFollowUpLine(toggle));
        state.CaptureOriginalVisuals();
        state.Apply(Plugin.EnableSimplifyFilterIcons);
    }

    internal static void RefreshAllActive()
    {
        foreach (var toggle in Resources.FindObjectsOfTypeAll<Game.Components.SortAndFilter.FilterToggle>())
        {
            if (toggle == null || !toggle.gameObject.scene.IsValid())
                continue;

            var state = toggle.GetComponent<SimplifiedFilterToggleState>();
            if (state != null)
            {
                state.SetHasFollowUpMenu(HasFollowUpLine(toggle));
                state.Apply(Plugin.EnableSimplifyFilterIcons);
            }
        }

        foreach (var line in Resources.FindObjectsOfTypeAll<Game.Components.SortAndFilter.ToggleGroupLine>())
        {
            if (line == null || !line.gameObject.scene.IsValid())
                continue;

            ApplyToggleGroupLayout(line);
        }
    }

    internal static void ApplyToggleGroupLayout(Game.Components.SortAndFilter.ToggleGroupLine line)
    {
        if (line == null)
            return;

        var toggleRoot = Traverse.Create(line).Field("toggleRoot").GetValue<RectTransform>();
        if (toggleRoot == null)
            return;

        var rootState = toggleRoot.GetComponent<SimplifiedFilterToggleRootState>()
            ?? toggleRoot.gameObject.AddComponent<SimplifiedFilterToggleRootState>();
        rootState.Apply(toggleRoot, Plugin.EnableSimplifyFilterIcons);

        LayoutRebuilder.ForceRebuildLayoutImmediate(toggleRoot);
        if (toggleRoot.parent is RectTransform parent)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
    }

    private static bool HasFollowUpLine(Game.Components.SortAndFilter.FilterToggle toggle)
    {
        if (toggle == null)
            return false;

        var owner = toggle.GetComponentInParent<SortAndFilter>(true);
        var line = toggle.GetComponentInParent<Game.Components.SortAndFilter.ToggleGroupLine>(true);
        if (owner?.Config?.LineConfigs == null || line == null)
            return false;

        if (!TryResolveToggleLineId(owner, line, out var lineId))
            return false;

        var optionIndex = toggle.transform.GetSiblingIndex() - 1;
        if (optionIndex < 0)
            return false;

        return HasLineDependingOnToggle(owner.Config.LineConfigs, lineId, optionIndex);
    }

    private static bool TryResolveToggleLineId(SortAndFilter owner, Game.Components.SortAndFilter.ToggleGroupLine line, out int lineId)
    {
        lineId = default;
        var firstToggleGroupLine = Traverse.Create(owner).Field("firstToggleGroupLine").GetValue<Game.Components.SortAndFilter.ToggleGroupLine>();
        var firstToggleGroupIndex = Traverse.Create(owner).Field("_firstToggleGroupIndex").GetValue<int>();
        if (firstToggleGroupLine == line
            && firstToggleGroupIndex >= 0
            && firstToggleGroupIndex < owner.Config.LineConfigs.Count)
        {
            lineId = owner.Config.LineConfigs[firstToggleGroupIndex].Id;
            return true;
        }

        for (var i = 0; i < owner.Config.LineConfigs.Count; i++)
        {
            var config = owner.Config.LineConfigs[i];
            if (config.Type != Game.Components.SortAndFilter.ESortAndFilterOneLineType.ToggleGroup)
                continue;

            var toggleRoot = Traverse.Create(line).Field("toggleRoot").GetValue<RectTransform>();
            var toggleCount = toggleRoot == null ? 0 : toggleRoot.childCount - 1;
            var configCount = config.ToggleGroupLineConfig?.Config.FilterToggleConfigs?.Count ?? -1;
            if (toggleCount == configCount)
            {
                lineId = config.Id;
                return true;
            }
        }

        return false;
    }

    internal static bool HasLineDependingOnToggle(
        List<Game.Components.SortAndFilter.LineConfig> lineConfigs,
        int targetLineId,
        int targetOptionIndex)
    {
        foreach (var lineConfig in lineConfigs)
        {
            var dependencies = lineConfig.ActiveCondition?.ActiveDependsOn;
            if (dependencies == null)
                continue;

            foreach (var dependency in dependencies)
            {
                if (DoesDependencyTargetToggle(lineConfigs, dependency, targetLineId, targetOptionIndex, null))
                    return true;
            }
        }

        return false;
    }

    private static bool DoesDependencyTargetToggle(
        List<Game.Components.SortAndFilter.LineConfig> lineConfigs,
        Game.Components.SortAndFilter.ToggleIdIndex dependency,
        int targetLineId,
        int targetOptionIndex,
        HashSet<int> visitingLineIds)
    {
        if (dependency.LineId == targetLineId
            && !dependency.ToggleKey.IsAll
            && dependency.ToggleKey.Index == targetOptionIndex)
            return true;

        var parentLine = lineConfigs.Find(config => config.Id == dependency.LineId);
        var parentDependencies = parentLine?.ActiveCondition?.ActiveDependsOn;
        if (parentDependencies == null)
            return false;

        visitingLineIds ??= new HashSet<int>();
        if (!visitingLineIds.Add(dependency.LineId))
            return false;

        foreach (var parentDependency in parentDependencies)
        {
            if (DoesDependencyTargetToggle(lineConfigs, parentDependency, targetLineId, targetOptionIndex, visitingLineIds))
            {
                visitingLineIds.Remove(dependency.LineId);
                return true;
            }
        }

        visitingLineIds.Remove(dependency.LineId);
        return false;
    }
}

internal sealed class SimplifiedFilterToggleState : MonoBehaviour
{
    private static readonly Color NormalBackgroundColor = new(0.10f, 0.13f, 0.12f, 0.92f);
    private static readonly Color SelectedBackgroundColor = new(0.42f, 0.07f, 0.06f, 0.96f);
    private static readonly Color TextColor = new(0.86f, 0.86f, 0.78f, 1f);

    private FrameWork.UISystem.UIElements.CToggle _toggle;
    private TooltipInvoker[] _tooltips;
    private bool[] _originalTooltipEnabledStates;
    private UnityEngine.UI.Graphic _targetGraphic;
    private UnityEngine.UI.Graphic _selectedGraphic;
    private Sprite _originalTargetSprite;
    private Sprite _originalSelectedSprite;
    private Color _originalTargetColor;
    private Color _originalSelectedColor;
    private UnityEngine.UI.Selectable.Transition _originalTransition;
    private UnityEngine.UI.SpriteState _originalSpriteState;
    private RectTransform _rect;
    private Vector2 _originalSizeDelta;
    private LayoutElement _layoutElement;
    private bool _hadLayoutElement;
    private float _originalMinWidth;
    private float _originalPreferredWidth;
    private float _originalFlexibleWidth;
    private float _originalMinHeight;
    private float _originalPreferredHeight;
    private float _originalFlexibleHeight;
    private TextMeshProUGUI _label;
    private RectTransform _followUpUnderline;
    private string _text = string.Empty;
    private bool _hasFollowUpMenu;
    private bool _hasOriginalVisuals;
    private bool _hasOriginalLayout;
    private bool _hasOriginalTooltips;
    private bool _isSimplified;
    private int _applyFrames;

    internal void SetText(string text)
    {
        _text = ShortenFilterLabel(text);
        if (_label != null)
            _label.SetText(_text);
    }

    internal void SetHasFollowUpMenu(bool hasFollowUpMenu)
    {
        _hasFollowUpMenu = hasFollowUpMenu;
        UpdateFollowUpUnderline();
    }

    internal void CaptureOriginalVisuals()
    {
        _toggle = GetComponent<FrameWork.UISystem.UIElements.CToggle>();
        if (_toggle == null)
            return;

        _targetGraphic = _toggle.targetGraphic;
        _selectedGraphic = _toggle.graphic;
        _originalTransition = _toggle.transition;
        _originalSpriteState = _toggle.spriteState;
        CaptureOriginalTooltips();
        CaptureOriginalLayout();

        if (_targetGraphic != null)
        {
            _originalTargetColor = _targetGraphic.color;
            _originalTargetSprite = GetSprite(_targetGraphic);
        }

        if (_selectedGraphic != null)
        {
            _originalSelectedColor = _selectedGraphic.color;
            _originalSelectedSprite = GetSprite(_selectedGraphic);
        }

        _hasOriginalVisuals = true;
    }

    internal void Apply(bool enabled)
    {
        EnsureLabel();
        if (!enabled)
        {
            _applyFrames = 0;
            RestoreOriginalVisuals();
            return;
        }

        _applyFrames = 2;
        ApplySimplifiedVisuals();
    }

    private void LateUpdate()
    {
        if (_isSimplified && Plugin.EnableSimplifyFilterIcons && _applyFrames > 0)
        {
            _applyFrames--;
            ApplySimplifiedVisuals();
        }
    }

    private void EnsureLabel()
    {
        if (_label != null)
            return;

        var labelObject = new GameObject("BetterTaiwuScrollSimplifiedFilterText", typeof(RectTransform));
        labelObject.transform.SetParent(transform, false);
        var rect = labelObject.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(4f, 1f);
            rect.offsetMax = new Vector2(-4f, -1f);
        }

        _label = labelObject.AddComponent<TextMeshProUGUI>();
        ApplyFontFromNearbyLabel(_label);
        _label.raycastTarget = false;
        _label.alignment = TextAlignmentOptions.Center;
        _label.enableAutoSizing = true;
        _label.fontSizeMin = 12f;
        _label.fontSizeMax = 18f;
        _label.overflowMode = TextOverflowModes.Ellipsis;
        _label.color = TextColor;
        _label.SetText(_text);
        _label.gameObject.SetActive(false);
    }

    private void ApplySimplifiedVisuals()
    {
        if (_toggle == null)
            _toggle = GetComponent<FrameWork.UISystem.UIElements.CToggle>();

        if (_toggle != null)
            _toggle.transition = UnityEngine.UI.Selectable.Transition.None;

        SetTooltipsEnabled(false);
        ApplySimplifiedLayout();
        ApplyGraphicAsBackground(_targetGraphic, NormalBackgroundColor);
        ApplyGraphicAsBackground(_selectedGraphic, SelectedBackgroundColor);

        if (_label != null)
        {
            if (_label.font == null)
                ApplyFontFromNearbyLabel(_label);
            _label.color = TextColor;
            _label.canvasRenderer.SetAlpha(1f);
            _label.SetText(_text);
            _label.gameObject.SetActive(true);
            _label.transform.SetAsLastSibling();
        }

        _isSimplified = true;
        UpdateFollowUpUnderline();
    }

    private void RestoreOriginalVisuals()
    {
        if (_label != null)
            _label.gameObject.SetActive(false);
        if (_followUpUnderline != null)
            _followUpUnderline.gameObject.SetActive(false);

        SetTooltipsEnabled(true);
        RestoreOriginalLayout();
        if (_toggle != null && _hasOriginalVisuals)
        {
            _toggle.transition = _originalTransition;
            _toggle.spriteState = _originalSpriteState;
        }

        if (_hasOriginalVisuals)
        {
            RestoreGraphic(_targetGraphic, _originalTargetSprite, _originalTargetColor);
            RestoreGraphic(_selectedGraphic, _originalSelectedSprite, _originalSelectedColor);
        }

        _isSimplified = false;
    }

    internal void ApplySimplifiedLayout()
    {
        CaptureOriginalLayout();
        _rect ??= transform as RectTransform;
        if (_rect != null)
        {
            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, SimplifiedFilterToggleVisual.SimplifiedWidth);
            if (_rect.rect.height <= 0f || _rect.rect.height > SimplifiedFilterToggleVisual.SimplifiedHeight + 8f)
                _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, SimplifiedFilterToggleVisual.SimplifiedHeight);
        }

        _layoutElement ??= GetComponent<LayoutElement>();
        if (_layoutElement == null)
            _layoutElement = gameObject.AddComponent<LayoutElement>();

        _layoutElement.minWidth = SimplifiedFilterToggleVisual.SimplifiedWidth;
        _layoutElement.preferredWidth = SimplifiedFilterToggleVisual.SimplifiedWidth;
        _layoutElement.flexibleWidth = 0f;
        if (_rect == null || _rect.rect.height <= SimplifiedFilterToggleVisual.SimplifiedHeight + 8f)
        {
            _layoutElement.minHeight = SimplifiedFilterToggleVisual.SimplifiedHeight;
            _layoutElement.preferredHeight = SimplifiedFilterToggleVisual.SimplifiedHeight;
            _layoutElement.flexibleHeight = 0f;
        }
    }

    internal void RestoreOriginalLayout()
    {
        if (!_hasOriginalLayout)
            return;

        _rect ??= transform as RectTransform;
        if (_rect != null)
            _rect.sizeDelta = _originalSizeDelta;

        _layoutElement ??= GetComponent<LayoutElement>();
        if (_layoutElement != null)
        {
            if (_hadLayoutElement)
            {
                _layoutElement.minWidth = _originalMinWidth;
                _layoutElement.preferredWidth = _originalPreferredWidth;
                _layoutElement.flexibleWidth = _originalFlexibleWidth;
                _layoutElement.minHeight = _originalMinHeight;
                _layoutElement.preferredHeight = _originalPreferredHeight;
                _layoutElement.flexibleHeight = _originalFlexibleHeight;
            }
            else
            {
                Destroy(_layoutElement);
                _layoutElement = null;
            }
        }
    }

    private void CaptureOriginalLayout()
    {
        if (_hasOriginalLayout)
            return;

        _rect = transform as RectTransform;
        if (_rect != null)
            _originalSizeDelta = _rect.sizeDelta;

        _layoutElement = GetComponent<LayoutElement>();
        _hadLayoutElement = _layoutElement != null;
        if (_layoutElement != null)
        {
            _originalMinWidth = _layoutElement.minWidth;
            _originalPreferredWidth = _layoutElement.preferredWidth;
            _originalFlexibleWidth = _layoutElement.flexibleWidth;
            _originalMinHeight = _layoutElement.minHeight;
            _originalPreferredHeight = _layoutElement.preferredHeight;
            _originalFlexibleHeight = _layoutElement.flexibleHeight;
        }

        _hasOriginalLayout = true;
    }

    private void CaptureOriginalTooltips()
    {
        if (_hasOriginalTooltips)
            return;

        _tooltips = GetComponentsInChildren<TooltipInvoker>(true);
        _originalTooltipEnabledStates = new bool[_tooltips.Length];
        for (var i = 0; i < _tooltips.Length; i++)
            _originalTooltipEnabledStates[i] = _tooltips[i] != null && _tooltips[i].enabled;

        _hasOriginalTooltips = true;
    }

    private void SetTooltipsEnabled(bool enabled)
    {
        CaptureOriginalTooltips();
        if (_tooltips == null)
            return;

        for (var i = 0; i < _tooltips.Length; i++)
        {
            var tooltip = _tooltips[i];
            if (tooltip == null)
                continue;

            if (!enabled)
            {
                tooltip.HideTips();
                tooltip.enabled = false;
            }
            else
            {
                tooltip.enabled = i < _originalTooltipEnabledStates.Length && _originalTooltipEnabledStates[i];
            }
        }
    }

    private void ApplyFontFromNearbyLabel(TextMeshProUGUI label)
    {
        if (label == null)
            return;

        TextMeshProUGUI source = null;
        var owner = GetComponentInParent<SortAndFilter>(true);
        var labels = owner == null
            ? transform.root.GetComponentsInChildren<TextMeshProUGUI>(true)
            : owner.GetComponentsInChildren<TextMeshProUGUI>(true);

        foreach (var candidate in labels)
        {
            if (candidate == null || candidate == label || candidate.name == "BetterTaiwuScrollSimplifiedFilterText")
                continue;

            if (candidate.font != null)
            {
                source = candidate;
                break;
            }
        }

        if (source != null)
        {
            label.font = source.font;
            label.fontSharedMaterial = source.fontSharedMaterial;
            label.spriteAsset = source.spriteAsset;
            label.styleSheet = source.styleSheet;
            label.fontStyle = source.fontStyle;
        }
        else if (TMP_Settings.defaultFontAsset != null)
        {
            label.font = TMP_Settings.defaultFontAsset;
        }
    }

    private void UpdateFollowUpUnderline()
    {
        EnsureFollowUpUnderline();
        if (_followUpUnderline == null)
            return;

        var visible = _hasFollowUpMenu
            && Plugin.EnableSimplifyFilterIcons
            && Plugin.EnableSimplifiedFilterIconUnderlines
            && _isSimplified;
        _followUpUnderline.gameObject.SetActive(visible);
        if (!visible)
            return;

        var width = _label == null ? 32f : Mathf.Clamp(_label.preferredWidth, 22f, SimplifiedFilterToggleVisual.SimplifiedWidth - 14f);
        _followUpUnderline.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        _followUpUnderline.SetAsLastSibling();
    }

    private void EnsureFollowUpUnderline()
    {
        if (_followUpUnderline != null)
            return;

        var underlineObject = new GameObject("BetterTaiwuScrollFollowUpUnderline", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        underlineObject.transform.SetParent(transform, false);
        _followUpUnderline = underlineObject.transform as RectTransform;
        if (_followUpUnderline != null)
        {
            _followUpUnderline.anchorMin = new Vector2(0.5f, 0f);
            _followUpUnderline.anchorMax = new Vector2(0.5f, 0f);
            _followUpUnderline.pivot = new Vector2(0.5f, 0.5f);
            _followUpUnderline.anchoredPosition = new Vector2(0f, 7f);
            _followUpUnderline.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 32f);
            _followUpUnderline.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 2f);
        }

        var image = underlineObject.GetComponent<UnityEngine.UI.Image>();
        image.raycastTarget = false;
        image.color = TextColor;
        underlineObject.SetActive(false);
    }

    private static void ApplyGraphicAsBackground(UnityEngine.UI.Graphic graphic, Color color)
    {
        if (graphic == null)
            return;

        StretchGraphicToParent(graphic);
        SetSprite(graphic, null);
        graphic.color = color;
    }

    private static void RestoreGraphic(UnityEngine.UI.Graphic graphic, Sprite sprite, Color color)
    {
        if (graphic == null)
            return;

        SetSprite(graphic, sprite);
        graphic.color = color;
    }

    private static void StretchGraphicToParent(UnityEngine.UI.Graphic graphic)
    {
        if (graphic.transform is not RectTransform rect)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static Sprite GetSprite(UnityEngine.UI.Graphic graphic)
    {
        return graphic is UnityEngine.UI.Image image ? image.sprite : null;
    }

    private static void SetSprite(UnityEngine.UI.Graphic graphic, Sprite sprite)
    {
        if (graphic is UnityEngine.UI.Image image)
            image.sprite = sprite;
    }

    private static string ShortenFilterLabel(string text)
    {
        return (text ?? string.Empty).Trim() switch
        {
            "功法书" => "功法",
            "技艺书" => "技艺",
            "西域珍宝" => "西域",
            var value => value,
        };
    }
}

internal sealed class SimplifiedFilterToggleRootState : MonoBehaviour
{
    private RectTransform _rect;
    private Vector2 _originalSizeDelta;
    private GridLayoutGroup _grid;
    private Vector2 _originalGridCellSize;
    private Vector2 _originalGridSpacing;
    private HorizontalLayoutGroup _horizontal;
    private float _originalHorizontalSpacing;
    private bool _originalChildControlWidth;
    private bool _originalChildForceExpandWidth;
    private bool _hasOriginal;

    internal void Apply(RectTransform root, bool enabled)
    {
        if (root == null)
            return;

        Capture(root);
        if (!enabled)
        {
            Restore();
            return;
        }

        var activeCount = CountActiveChildren(root);
        if (_grid != null)
        {
            _grid.cellSize = new Vector2(SimplifiedFilterToggleVisual.SimplifiedWidth, Mathf.Max(_originalGridCellSize.y, SimplifiedFilterToggleVisual.SimplifiedHeight));
            _grid.spacing = new Vector2(SimplifiedFilterToggleVisual.SimplifiedSpacing, _originalGridSpacing.y);
        }

        if (_horizontal != null)
        {
            _horizontal.spacing = SimplifiedFilterToggleVisual.SimplifiedSpacing;
            _horizontal.childControlWidth = true;
            _horizontal.childForceExpandWidth = false;
        }

        for (var i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            var state = child == null ? null : child.GetComponent<SimplifiedFilterToggleState>();
            state?.ApplySimplifiedLayout();
        }

        if (activeCount > 0)
        {
            var width = activeCount * SimplifiedFilterToggleVisual.SimplifiedWidth
                + Math.Max(activeCount - 1, 0) * SimplifiedFilterToggleVisual.SimplifiedSpacing;
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }
    }

    private void Capture(RectTransform root)
    {
        if (_hasOriginal)
            return;

        _rect = root;
        _originalSizeDelta = root.sizeDelta;
        _grid = root.GetComponent<GridLayoutGroup>();
        if (_grid != null)
        {
            _originalGridCellSize = _grid.cellSize;
            _originalGridSpacing = _grid.spacing;
        }

        _horizontal = root.GetComponent<HorizontalLayoutGroup>();
        if (_horizontal != null)
        {
            _originalHorizontalSpacing = _horizontal.spacing;
            _originalChildControlWidth = _horizontal.childControlWidth;
            _originalChildForceExpandWidth = _horizontal.childForceExpandWidth;
        }

        _hasOriginal = true;
    }

    private void Restore()
    {
        if (!_hasOriginal)
            return;

        if (_rect != null)
            _rect.sizeDelta = _originalSizeDelta;

        if (_grid != null)
        {
            _grid.cellSize = _originalGridCellSize;
            _grid.spacing = _originalGridSpacing;
        }

        if (_horizontal != null)
        {
            _horizontal.spacing = _originalHorizontalSpacing;
            _horizontal.childControlWidth = _originalChildControlWidth;
            _horizontal.childForceExpandWidth = _originalChildForceExpandWidth;
        }

        if (_rect != null)
        {
            for (var i = 0; i < _rect.childCount; i++)
            {
                var child = _rect.GetChild(i);
                var state = child == null ? null : child.GetComponent<SimplifiedFilterToggleState>();
                state?.RestoreOriginalLayout();
            }
        }
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
    private static void Postfix(SortAndFilter __instance, int lineId, int menuId, int optionIndex)
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
    private const float CompactSummaryHeight = 34f;
    private const int DelayedLayoutRefreshFrames = 2;

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
    private readonly HashSet<int> _inlineOptionsWithFollowUpMenus = new();
    private bool _hideInlineAllOption;
    private bool _entryForceHiddenByInline;
    private bool _originalEntryForceHidden;
    private int _pendingLayoutRefreshFrames;
    private bool _showRootSectionInPanel;
    private string _lastRefreshSignature;

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

    internal bool MatchesInlineRoot(int lineId, int menuId)
    {
        return _hasInlineRoot && _lineId == lineId && _menuId == menuId;
    }

    internal bool TryGetInlineRoot(out int lineId, out int menuId)
    {
        lineId = _lineId;
        menuId = _menuId;
        return _hasInlineRoot;
    }

    internal bool TryGetInlineOptionText(int originalOptionIndex, out string text)
    {
        text = string.Empty;
        if (originalOptionIndex < 0)
            return _hasInlineRoot;

        if (!TryGetInlineMenuConfig(out var menuConfig))
            return false;

        var itemConfigs = menuConfig.DropdownConfig.ItemConfigs;
        if (itemConfigs == null || originalOptionIndex < 0 || originalOptionIndex >= itemConfigs.Count)
            return false;

        text = NormalizeInlineOptionText(itemConfigs[originalOptionIndex].Text.GetString());
        return !string.IsNullOrEmpty(text);
    }

    internal bool TryFindInlineOptionByText(string text, out int originalOptionIndex)
    {
        originalOptionIndex = -1;
        var normalized = NormalizeInlineOptionText(text);
        if (string.IsNullOrEmpty(normalized))
            return _hasInlineRoot;

        if (!TryGetInlineMenuConfig(out var menuConfig))
            return false;

        var itemConfigs = menuConfig.DropdownConfig.ItemConfigs;
        if (itemConfigs == null)
            return false;

        foreach (var candidateIndex in _inlineOptionIndexMap)
        {
            if (candidateIndex < 0 || candidateIndex >= itemConfigs.Count)
                continue;

            var candidateText = NormalizeInlineOptionText(itemConfigs[candidateIndex].Text.GetString());
            if (string.Equals(candidateText, normalized, StringComparison.Ordinal))
            {
                originalOptionIndex = candidateIndex;
                return true;
            }
        }

        return false;
    }

    internal bool HasInlineOriginalOption(int originalOptionIndex)
    {
        if (originalOptionIndex < 0)
            return _hasInlineRoot;

        return _inlineOptionIndexMap.Contains(originalOptionIndex);
    }

    private bool TryGetInlineMenuConfig(out DetailedFilterMenuConfig menuConfig)
    {
        menuConfig = default;
        if (!_hasInlineRoot || _owner == null)
            return false;

        var maybeConfig = _owner.GetMenuConfig(_lineIndex, _menuId);
        if (!maybeConfig.HasValue)
            return false;

        menuConfig = maybeConfig.Value;
        return true;
    }

    private static string NormalizeInlineOptionText(string text)
    {
        return (text ?? string.Empty).Trim();
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
            _lastRefreshSignature = null;
            RestoreEntryButton();
            SetInlineActive(false);
            return;
        }

        if (ShouldSuppressInlineFilterForMakeList())
        {
            _hasInlineRoot = false;
            _lastRefreshSignature = null;
            HideEntryButton();
            SetInlineActive(false);
            RefreshOwnerSummary();
            return;
        }

        if (!TryGetRootSection(out var section, out var menuConfig))
        {
            _hasInlineRoot = false;
            _lastRefreshSignature = null;
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
            _lastRefreshSignature = null;
            SetInlineActive(false);
            return;
        }

        _hasInlineRoot = true;
        var inlineItemConfigs = BuildInlineItemConfigs(section, itemConfigs);
        var selectedOriginalIndex = _owner.GetInitialSectionState(_lineId, _menuId);
        var rect = _inlineSection.transform as RectTransform;
        var panelWidth = rect == null ? 0f : GetInlineAvailableWidth(rect);
        var refreshSignature = BuildInlineRefreshSignature(section, inlineItemConfigs, selectedOriginalIndex, panelWidth);
        if (_inlineSection.gameObject.activeInHierarchy && refreshSignature == _lastRefreshSignature)
        {
            _inlineSection.SetSelectedIndex(ToDisplayOptionIndex(selectedOriginalIndex), notify: false);
            FilterMultiSelectSupport.ApplySelectionVisuals(_inlineSection, _owner, _lineId, _menuId);
            return;
        }

        RefreshInlineFollowUpOptionSet();
        _inlineSection.gameObject.SetActive(true);
        _inlineSection.Setup(_menuId, string.Empty, inlineItemConfigs, OnInlineSelectionChanged);
        _inlineSection.SetSelectedIndex(ToDisplayOptionIndex(selectedOriginalIndex), notify: false);
        ApplyInlineLayout();
        RequestDelayedLayoutRefresh();
        AfterPanelRefresh();
        RefreshOwnerSummary();
        TuneSummaryAreaLayout();
        _lastRefreshSignature = refreshSignature;
    }

    private void LateUpdate()
    {
        if (_pendingLayoutRefreshFrames <= 0)
            return;

        if (_hasInlineRoot && _inlineSection != null && _inlineSection.gameObject.activeInHierarchy)
        {
            EnsureInlineSection();
            ApplyInlineLayout();
            TuneSummaryAreaLayout();
        }

        _pendingLayoutRefreshFrames--;
    }

    private void RequestDelayedLayoutRefresh()
    {
        _pendingLayoutRefreshFrames = DelayedLayoutRefreshFrames;
    }

    internal void AfterPanelRefresh()
    {
        if (!Plugin.EnableInlineFilterButtons || _owner == null || _filterPanel == null)
            return;

        if (_showRootSectionInPanel)
            ShowPanelRootSection();
        else
            HidePanelRootSection();
        TuneSummaryAreaLayout();
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

    private string BuildInlineRefreshSignature(
        SortAndFilter.SectionViewData section,
        List<FilterDropdownItemConfig> itemConfigs,
        int selectedOriginalIndex,
        float panelWidth)
    {
        var builder = new StringBuilder(128);
        builder.Append(section.LineId).Append('|')
            .Append(section.LineIndex).Append('|')
            .Append(section.MenuId).Append('|')
            .Append(selectedOriginalIndex).Append('|')
            .Append(Mathf.RoundToInt(panelWidth)).Append('|')
            .Append(_hideInlineAllOption ? 1 : 0).Append('|')
            .Append(itemConfigs == null ? 0 : itemConfigs.Count);

        if (itemConfigs != null)
        {
            for (var i = 0; i < itemConfigs.Count; i++)
                builder.Append('|').Append(itemConfigs[i].Text.GetString());
        }

        return builder.ToString();
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
        if (_filterPanel == null)
            _filterPanel = Traverse.Create(_owner).Field("filterPanel").GetValue<FilterPanel>();

        var sectionTemplate = Traverse.Create(_filterPanel).Field("sectionTemplate").GetValue<FilterSection>();
        if (sectionTemplate == null || _entryRect == null)
            return;

        var parent = GetInlineSectionParent(out var siblingIndex);
        if (parent == null)
            return;

        if (_inlineSection == null)
        {
            _inlineSection = Instantiate(sectionTemplate, parent);
            _inlineSection.name = "BetterTaiwuScrollInlineFilterButtons";
        }
        else if (_inlineSection.transform.parent != parent)
        {
            _inlineSection.transform.SetParent(parent, false);
        }

        _inlineSection.transform.SetSiblingIndex(siblingIndex);
        _inlineSection.gameObject.SetActive(true);
    }

    private RectTransform GetInlineSectionParent(out int siblingIndex)
    {
        var entryParent = _entryRect == null ? null : _entryRect.parent as RectTransform;
        siblingIndex = _entryRect == null ? 0 : _entryRect.GetSiblingIndex();
        if (_owner == null || entryParent == null)
            return entryParent;

        var sortButtonGroup = Traverse.Create(_owner).Field("sortButtonGroup").GetValue<Game.Components.SortAndFilter.SortButtonGroup>();
        var sortRect = sortButtonGroup == null ? null : sortButtonGroup.transform as RectTransform;
        if (sortRect != null && sortRect.parent == entryParent && entryParent.parent is RectTransform rowParent)
        {
            siblingIndex = entryParent.GetSiblingIndex();
            return rowParent;
        }

        return entryParent;
    }

    private void ApplyInlineLayout()
    {
        if (_inlineSection == null || _entryRect == null)
            return;

        var rect = _inlineSection.transform as RectTransform;
        if (rect == null)
            return;

        TuneParentLayout(rect.parent as RectTransform);
        var inlineInOriginalRow = rect.parent == _entryRect.parent;
        if (inlineInOriginalRow)
        {
            rect.anchorMin = _entryRect.anchorMin;
            rect.anchorMax = _entryRect.anchorMax;
            rect.pivot = _entryRect.pivot;
            rect.anchoredPosition = _entryRect.anchoredPosition;
        }
        else
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
        }
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
        ApplyInlineFollowUpUnderlines(contentRoot);
        AttachInlineClickRelays(contentRoot);
        FilterMultiSelectSupport.ApplySelectionVisuals(_inlineSection, _owner, _lineId, _menuId);

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
        ForceRebuildInlineLayoutChain();
    }

    private void ForceRebuildInlineLayoutChain()
    {
        Canvas.ForceUpdateCanvases();

        var current = _inlineSection == null ? null : _inlineSection.transform as RectTransform;
        var depth = 0;
        while (current != null && depth < 6)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(current);
            current = current.parent as RectTransform;
            depth++;
        }
    }

    private void TuneSummaryAreaLayout()
    {
        if (_owner == null)
            return;

        var summaryArea = Traverse.Create(_owner).Field("filterSummaryArea").GetValue<RectTransform>();
        if (summaryArea == null || !summaryArea.gameObject.activeSelf)
            return;

        summaryArea.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, CompactSummaryHeight);
        var layoutElement = summaryArea.GetComponent<LayoutElement>() ?? summaryArea.gameObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = CompactSummaryHeight;
        layoutElement.preferredHeight = CompactSummaryHeight;
        layoutElement.flexibleHeight = 0f;

        var summaryRoot = Traverse.Create(_owner).Field("filterSummaryRoot").GetValue<RectTransform>();
        if (summaryRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(summaryRoot);

        LayoutRebuilder.ForceRebuildLayoutImmediate(summaryArea);
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

    private void ApplyInlineFollowUpUnderlines(RectTransform contentRoot)
    {
        if (contentRoot == null)
            return;

        for (var i = 0; i < contentRoot.childCount; i++)
        {
            var child = contentRoot.GetChild(i);
            if (child == null)
                continue;

            var originalOptionIndex = ToOriginalOptionIndex(i - 1);
            var hasFollowUp = originalOptionIndex >= 0 && _inlineOptionsWithFollowUpMenus.Contains(originalOptionIndex);
            foreach (var label in child.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (label == null)
                    continue;

                label.fontStyle = hasFollowUp
                    ? label.fontStyle | FontStyles.Underline
                    : label.fontStyle & ~FontStyles.Underline;
            }
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
            FilterMultiSelectSupport.BindOption(child.gameObject, _owner, _inlineSection, _lineId, _menuId, ToOriginalOptionIndex(i - 1));
        }
    }

    private void RefreshInlineFollowUpOptionSet()
    {
        _inlineOptionsWithFollowUpMenus.Clear();
        if (_owner == null)
            return;

        var menuConfigMap = new Dictionary<int, DetailedFilterMenuConfig>();
        if (_owner.Config?.LineConfigs == null || _lineIndex < 0 || _lineIndex >= _owner.Config.LineConfigs.Count)
            return;

        var lineConfig = _owner.Config.LineConfigs[_lineIndex];
        var menuConfigs = lineConfig.DetailedFilterLineConfig?.Config.MenuConfigs;
        if (menuConfigs == null)
            return;

        foreach (var menuConfig in menuConfigs)
        {
            var maybeConfig = _owner.GetMenuConfig(_lineIndex, menuConfig.Id);
            menuConfigMap[menuConfig.Id] = maybeConfig ?? menuConfig;
        }

        if (menuConfigMap.Count == 0)
            return;

        foreach (var entry in menuConfigMap)
        {
            var dependency = entry.Value.DropdownContext.Dependency;
            if (!dependency.HasValue)
                continue;

            for (var i = 0; i < _inlineOptionIndexMap.Count; i++)
            {
                var originalOptionIndex = _inlineOptionIndexMap[i];
                if (DoesMenuDependOnOption(entry.Value, _menuId, originalOptionIndex, menuConfigMap, null))
                    _inlineOptionsWithFollowUpMenus.Add(originalOptionIndex);
            }
        }

        if (_owner.Config?.LineConfigs == null)
            return;

        for (var i = 0; i < _inlineOptionIndexMap.Count; i++)
        {
            var originalOptionIndex = _inlineOptionIndexMap[i];
            if (SimplifiedFilterToggleVisual.HasLineDependingOnToggle(_owner.Config.LineConfigs, _lineId, originalOptionIndex))
                _inlineOptionsWithFollowUpMenus.Add(originalOptionIndex);
        }
    }

    private static bool DoesMenuDependOnOption(
        DetailedFilterMenuConfig menuConfig,
        int targetMenuId,
        int targetOptionIndex,
        Dictionary<int, DetailedFilterMenuConfig> menuConfigMap,
        HashSet<int> visitingMenuIds)
    {
        var dependency = menuConfig.DropdownContext.Dependency;
        if (!dependency.HasValue)
            return false;

        var value = dependency.Value;
        if (value.MenuId == targetMenuId && value.OptionIndex == targetOptionIndex)
            return true;

        visitingMenuIds ??= new HashSet<int>();
        if (!visitingMenuIds.Add(menuConfig.Id))
            return false;

        var result = menuConfigMap.TryGetValue(value.MenuId, out var parentMenuConfig)
            && DoesMenuDependOnOption(parentMenuConfig, targetMenuId, targetOptionIndex, menuConfigMap, visitingMenuIds);
        visitingMenuIds.Remove(menuConfig.Id);
        return result;
    }

    internal bool IsInlineDisplayOptionSelected(int displayIndex)
    {
        if (_owner == null)
            return false;

        var originalIndex = ToOriginalOptionIndex(displayIndex);
        var selected = FilterMultiSelectSupport.GetSelectedIndices(_owner, _lineId, _menuId);
        return originalIndex < 0 ? selected.Count == 0 : selected.Contains(originalIndex);
    }

    internal void OnInlineDisplayOptionRepeatedClick(int displayIndex)
    {
        if (_owner == null)
            return;

        if (displayIndex < 0)
        {
            _showRootSectionInPanel = true;
            _owner.OpenFilterPanel();
            ShowPanelRootSection();
            return;
        }

        _showRootSectionInPanel = false;
        if (!HasFollowUpSectionsForDisplayOption(displayIndex))
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

        _showRootSectionInPanel = false;
        selectedIndex = ToOriginalOptionIndex(selectedIndex);
        _owner.SetDropdownOption(_lineId, _menuId, selectedIndex);
        _owner.CloseFilterPanel();
    }

    private bool HasFollowUpSectionsForDisplayOption(int displayIndex)
    {
        var originalIndex = ToOriginalOptionIndex(displayIndex);
        if (originalIndex >= 0 && _inlineOptionsWithFollowUpMenus.Contains(originalIndex))
            return true;

        if (_owner?.Config?.LineConfigs != null
            && originalIndex >= 0
            && SimplifiedFilterToggleVisual.HasLineDependingOnToggle(_owner.Config.LineConfigs, _lineId, originalIndex))
            return true;

        return HasActiveFollowUpSections();
    }

    private bool HasActiveFollowUpSections()
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

    private void ShowPanelRootSection()
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
            rootSection.gameObject.SetActive(true);
    }

    private void HideEntryButton()
    {
        if (_owner != null && !_entryForceHiddenByInline)
        {
            _originalEntryForceHidden = Traverse.Create(_owner).Field("_forceHideEntry").GetValue<bool>();
            _owner.SetEntryButtonForceHidden(true);
            _entryForceHiddenByInline = true;
        }

        if (_entryRect != null)
            _entryRect.gameObject.SetActive(false);
    }

    private void RestoreEntryButton()
    {
        if (_owner == null)
            return;

        RestoreParentLayout();
        if (_entryForceHiddenByInline)
        {
            _owner.SetEntryButtonForceHidden(_originalEntryForceHidden);
            _entryForceHiddenByInline = false;
        }

        if (_entryRect == null)
            return;

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
        if (!active)
        {
            _pendingLayoutRefreshFrames = 0;
            _showRootSectionInPanel = false;
        }
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
