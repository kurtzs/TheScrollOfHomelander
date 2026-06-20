#nullable disable

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using FrameWork.UISystem.Components;
using Game.Components.ListStyleGeneralScroll.Item;
using HarmonyLib;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch]
internal static class ContainerCompactPatches
{
    private const int MaxParentDepth = 12;

    private static readonly string[] PageViewNames =
    {
        "Game.Views.CharacterMenu.ViewCharacterMenuItems",
        "Game.Views.Exchange.ViewExchangeBase",
    };

    private static readonly FieldInfo CellSizeField = AccessTools.Field(typeof(InfinityScroll), "_cellSize");
    private static readonly FieldInfo ContainerField = AccessTools.Field(typeof(InfinityScroll), "_container");
    private static readonly FieldInfo DirectionField = AccessTools.Field(typeof(InfinityScroll), "scrollDirection");
    private static readonly MethodInfo RefreshStyleMetricsMethod = AccessTools.Method(typeof(InfinityScroll), "RefreshStyleMetrics");

    private static readonly ConditionalWeakTable<InfinityScroll, OriginalInfinityLayout> OriginalLayouts = new();

    private static Type _cardScrollType;
    private static Type _combatSkillSelectType;
    private static readonly Type[] PageViewTypes = new Type[PageViewNames.Length];
    private static bool _typeLookupDone;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InfinityScroll), "RefreshStyleMetrics")]
    private static void RefreshStyleMetricsPostfix(InfinityScroll __instance)
    {
        ApplyOrRestore(__instance, rerender: false);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InfinityScroll), "SetDataCount")]
    private static void SetDataCountPostfix(InfinityScroll __instance)
    {
        ApplyOrRestore(__instance, rerender: true);
    }

    private static void ApplyOrRestore(InfinityScroll instance, bool rerender)
    {
        if (instance == null)
            return;

        var scale = ResolveScale(instance);
        if (!Plugin.EnableContainerCompact || scale <= 0f || scale >= 1f)
        {
            RestoreLayout(instance, rerender);
            return;
        }

        ApplyLayout(instance, scale);
        if (rerender)
            instance.ReRender();
    }

    private static void ApplyLayout(InfinityScroll instance, float scale)
    {
        var original = GetOrCacheOriginal(instance);
        if (instance.srcPrefab != null)
            instance.srcPrefab.transform.localScale = new Vector3(original.PrefabScale.x * scale, original.PrefabScale.y * scale, original.PrefabScale.z);

        if (CellSizeField != null)
            CellSizeField.SetValue(instance, original.CellSize * scale);

        instance.gap = original.Gap * scale;
        instance.padding = original.Padding * scale;
        instance.lineCount = Mathf.Max(1, Mathf.RoundToInt(original.LineCount / scale));
        ResizeCrossAxis(instance);
    }

    private static void RestoreLayout(InfinityScroll instance, bool rerender)
    {
        if (!OriginalLayouts.TryGetValue(instance, out var original))
            return;

        if (instance.srcPrefab != null)
            instance.srcPrefab.transform.localScale = original.PrefabScale;

        if (CellSizeField != null)
            CellSizeField.SetValue(instance, original.CellSize);

        instance.gap = original.Gap;
        instance.padding = original.Padding;
        instance.lineCount = Math.Max(1, original.LineCount);
        ResizeCrossAxis(instance);
        if (rerender)
            instance.ReRender();
    }

    private static void ResizeCrossAxis(InfinityScroll instance)
    {
        if (ContainerField == null || DirectionField == null)
            return;

        var container = ContainerField.GetValue(instance) as RectTransform;
        if (container == null)
            return;

        var viewport = instance.Scroll?.Viewport;
        var maxCrossSize = viewport == null ? float.MaxValue : GetCrossSize(viewport.rect.size, instance);
        var cellSize = CellSizeField != null ? (Vector2)CellSizeField.GetValue(instance) : Vector2.zero;
        var lineCount = Math.Max(1, instance.lineCount);
        var direction = (int)DirectionField.GetValue(instance);
        var verticalMainAxis = direction < 2;
        var cellCrossSize = verticalMainAxis ? cellSize.x : cellSize.y;
        var gapCrossSize = verticalMainAxis ? instance.gap.x : instance.gap.y;
        var paddingCrossSize = verticalMainAxis ? instance.padding.x : instance.padding.y;
        var crossSize = paddingCrossSize * 2f + (gapCrossSize + cellCrossSize) * (lineCount - 1) + cellCrossSize;

        if (crossSize > maxCrossSize && cellCrossSize + gapCrossSize > 0f)
        {
            var maxLineCount = Mathf.Max(1, Mathf.FloorToInt((maxCrossSize - paddingCrossSize * 2f + gapCrossSize) / (cellCrossSize + gapCrossSize)));
            if (maxLineCount < lineCount)
            {
                instance.lineCount = maxLineCount;
                crossSize = paddingCrossSize * 2f + (gapCrossSize + cellCrossSize) * (maxLineCount - 1) + cellCrossSize;
            }
        }

        container.SetSizeWithCurrentAnchors(verticalMainAxis ? RectTransform.Axis.Horizontal : RectTransform.Axis.Vertical, crossSize);
    }

    private static float GetCrossSize(Vector2 size, InfinityScroll instance)
    {
        var direction = DirectionField != null ? (int)DirectionField.GetValue(instance) : 0;
        return direction < 2 ? size.x : size.y;
    }

    private static OriginalInfinityLayout GetOrCacheOriginal(InfinityScroll instance)
    {
        return OriginalLayouts.GetValue(instance, scroll =>
        {
            var cellSize = CellSizeField != null ? (Vector2)CellSizeField.GetValue(scroll) : Vector2.zero;
            if (cellSize == Vector2.zero && scroll.srcPrefab != null)
            {
                var prefabRect = scroll.srcPrefab.GetComponent<RectTransform>();
                if (prefabRect != null)
                    cellSize = prefabRect.rect.size;
            }

            return new OriginalInfinityLayout
            {
                CellSize = cellSize,
                LineCount = Math.Max(1, scroll.lineCount),
                Gap = scroll.gap,
                Padding = scroll.padding,
                PrefabScale = scroll.srcPrefab == null ? Vector3.one : scroll.srcPrefab.transform.localScale,
            };
        });
    }

    private static float ResolveScale(InfinityScroll instance)
    {
        EnsureTypes();
        if (_cardScrollType == null)
            return 0f;

        var current = instance.transform;
        var depth = 0;
        var hasCardScroll = false;
        var hasCombatSkillSelect = false;
        var pageIndex = -1;
        while (current != null && depth < MaxParentDepth)
        {
            if (!hasCardScroll && current.GetComponent(_cardScrollType) != null)
                hasCardScroll = true;

            if (!hasCombatSkillSelect && _combatSkillSelectType != null && current.GetComponent(_combatSkillSelectType) != null)
                hasCombatSkillSelect = true;

            if (pageIndex < 0)
            {
                for (var i = 0; i < PageViewTypes.Length; i++)
                {
                    var pageType = PageViewTypes[i];
                    if (pageType != null && current.GetComponent(pageType) != null)
                    {
                        pageIndex = i;
                        break;
                    }
                }
            }

            current = current.parent;
            depth++;
        }

        if (hasCombatSkillSelect)
            return Plugin.SkillContainerScale;
        if (!hasCardScroll || pageIndex < 0)
            return 0f;

        return pageIndex switch
        {
            0 => Plugin.InventoryContainerScale,
            1 => Plugin.ExchangeContainerScale,
            _ => 0f,
        };
    }

    private static void EnsureTypes()
    {
        if (_typeLookupDone)
            return;

        _typeLookupDone = true;
        _cardScrollType = AccessTools.TypeByName("Game.Components.ListStyleGeneralScroll.Item.CardStyleGeneralScroll");
        _combatSkillSelectType = AccessTools.TypeByName("Game.Components.Common.CombatSkillSelect");
        for (var i = 0; i < PageViewNames.Length; i++)
            PageViewTypes[i] = AccessTools.TypeByName(PageViewNames[i]);
    }

    internal static void RefreshAllActive(bool allowRestore)
    {
        EnsureTypes();
        var scrolls = UnityEngine.Object.FindObjectsOfType<InfinityScroll>();
        foreach (var scroll in scrolls)
        {
            if (scroll == null)
                continue;

            try
            {
                if (Plugin.EnableContainerCompact)
                {
                    RefreshStyleMetricsMethod?.Invoke(scroll, null);
                    scroll.ReRender();
                }
                else if (allowRestore)
                {
                    RestoreLayout(scroll, rerender: true);
                }
            }
            catch
            {
            }
        }
    }

    private sealed class OriginalInfinityLayout
    {
        public Vector2 CellSize;
        public int LineCount;
        public Vector2 Gap;
        public Vector2 Padding;
        public Vector3 PrefabScale;
    }
}

[HarmonyPatch]
internal static class ContainerDefaultCardModePatch
{
    private static readonly FieldInfo IsCardModeField = AccessTools.Field(typeof(ItemListScroll), "_isCardMode");
    private static readonly FieldInfo CardScrollField = AccessTools.Field(typeof(ItemListScroll), "cardScroll");
    private static readonly FieldInfo BtnSwitchCardModeField = AccessTools.Field(typeof(ItemListScroll), "btnSwitchCardMode");
    private static readonly MethodInfo RefreshCardModeMethod = AccessTools.Method(typeof(ItemListScroll), "RefreshCardMode");
    private static readonly MethodInfo ToggleSetWithoutNotifyMethod = AccessTools.Method(AccessTools.TypeByName("FrameWork.UISystem.UIElements.CToggleGroup"), "SetWithoutNotify");
    private static readonly FieldInfo ToggleActiveIndexField = AccessTools.Field(AccessTools.TypeByName("FrameWork.UISystem.UIElements.CToggleGroup"), "_activeIndex");

    private static Type _viewCharacterMenuItemsType;
    private static bool _typeLookupDone;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemListScroll), "Init")]
    private static void InitPostfix(ItemListScroll __instance)
    {
        if (!Plugin.EnableContainerCompact || !Plugin.EnableDefaultContainerCardMode || !IsSupportedPage(__instance))
            return;

        if (IsCardModeField?.GetValue(__instance) is bool isCardMode && isCardMode)
            return;

        if (CardScrollField != null && CardScrollField.GetValue(__instance) == null)
            return;

        IsCardModeField?.SetValue(__instance, true);
        RefreshCardModeMethod?.Invoke(__instance, null);

        var switchToggle = BtnSwitchCardModeField?.GetValue(__instance);
        if (switchToggle != null && ToggleActiveIndexField != null && ToggleSetWithoutNotifyMethod != null && (int)ToggleActiveIndexField.GetValue(switchToggle) == 0)
            ToggleSetWithoutNotifyMethod.Invoke(switchToggle, new object[] { 1 });
    }

    private static bool IsSupportedPage(ItemListScroll instance)
    {
        if (instance == null)
            return false;

        EnsureTypes();
        var current = instance.transform;
        var depth = 0;
        while (current != null && depth < 12)
        {
            if (_viewCharacterMenuItemsType != null && current.GetComponent(_viewCharacterMenuItemsType) != null)
                return true;

            current = current.parent;
            depth++;
        }

        return false;
    }

    private static void EnsureTypes()
    {
        if (_typeLookupDone)
            return;

        _typeLookupDone = true;
        _viewCharacterMenuItemsType = AccessTools.TypeByName("Game.Views.CharacterMenu.ViewCharacterMenuItems");
    }
}

[HarmonyPatch]
internal static class ContainerCompactHighlightPatch
{
    private static Type _viewCharacterMenuItemsType;
    private static Type _viewExchangeBaseType;
    private static readonly FieldInfo IsCardModeField = AccessTools.Field(typeof(ItemListScroll), "_isCardMode");
    private static bool _typeLookupDone;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemListScroll), "HighLightItemView")]
    private static void HighLightPostfix(ItemListScroll __instance, object itemView)
    {
        if (!Plugin.EnableContainerCompact || IsCardModeField?.GetValue(__instance) is bool isCardMode && !isCardMode)
            return;

        if (itemView is not Component component)
            return;

        var scale = ResolveScaleForItem(component.transform);
        if (scale <= 0f || scale >= 1f)
            return;

        component.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private static float ResolveScaleForItem(Transform start)
    {
        EnsureTypes();
        var current = start;
        var depth = 0;
        while (current != null && depth < 12)
        {
            if (_viewCharacterMenuItemsType != null && current.GetComponent(_viewCharacterMenuItemsType) != null)
                return Plugin.InventoryContainerScale;
            if (_viewExchangeBaseType != null && current.GetComponent(_viewExchangeBaseType) != null)
                return Plugin.ExchangeContainerScale;

            current = current.parent;
            depth++;
        }

        return 0f;
    }

    private static void EnsureTypes()
    {
        if (_typeLookupDone)
            return;

        _typeLookupDone = true;
        _viewCharacterMenuItemsType = AccessTools.TypeByName("Game.Views.CharacterMenu.ViewCharacterMenuItems");
        _viewExchangeBaseType = AccessTools.TypeByName("Game.Views.Exchange.ViewExchangeBase");
    }
}
