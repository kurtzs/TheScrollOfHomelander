#nullable disable

using System;
using Game.Components.Item;
using FrameWork;
using Game.Components.ListStyleGeneralScroll.Item;
using GameData.Domains.Item;
using GameData.Domains.Taiwu.ExchangeSystem;
using HarmonyLib;
using UnityEngine;
using ExchangeContainerView = Game.Views.Exchange.ExchangeContainer;
using ExchangeView = Game.Views.Exchange.ViewExchange;
using ExchangeViewBase = Game.Views.Exchange.ViewExchangeBase;
using SettlementShopView = Game.Views.Exchange.ViewSettlementShop;
using ShopView = Game.Views.Exchange.ViewShop;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(ExchangeViewBase), "OnClickItem")]
internal static class ViewExchangeBaseFastTransferPatch
{
    private static bool Prefix(
        ExchangeViewBase __instance,
        ItemListScroll scroll,
        ITradeableContent itemData,
        RowItemLine rowItemLine,
        Action<RowItemLine, int> action)
    {
        return !FastTransferSupport.TryHandleClickItem(__instance, scroll, itemData, rowItemLine, action);
    }
}

[HarmonyPatch(typeof(ExchangeView), "OnClickItem")]
internal static class ViewExchangeFastTransferPatch
{
    private static bool Prefix(
        ExchangeView __instance,
        ItemListScroll scroll,
        ITradeableContent itemData,
        RowItemLine rowItemLine,
        Action<RowItemLine, int> action)
    {
        return !FastTransferSupport.TryHandleClickItem(__instance, scroll, itemData, rowItemLine, action);
    }
}

[HarmonyPatch(typeof(SettlementShopView), "OnClickItem")]
internal static class ViewSettlementShopFastTransferPatch
{
    private static bool Prefix(
        SettlementShopView __instance,
        ItemListScroll scroll,
        ITradeableContent itemData,
        RowItemLine rowItemLine,
        Action<RowItemLine, int> action)
    {
        return !FastTransferSupport.TryHandleClickItem(__instance, scroll, itemData, rowItemLine, action);
    }
}

[HarmonyPatch(typeof(ShopView), "OnClickTargetItem")]
internal static class ViewShopFastTransferTargetItemPatch
{
    private static bool Prefix(ShopView __instance, ITradeableContent itemData, RowItemLine rowItemLine)
    {
        if (!FastTransferSupport.ShouldUseFastTransfer())
            return true;

        var container = Traverse.Create(__instance).Field("exchangeContainer").GetValue<ExchangeContainerView>();
        var scroll = container?.targetItemList;
        if (scroll == null || __instance?.Exchange == null)
            return true;

        var limitCount = FastTransferSupport.GetShopTargetLimitCount(__instance, itemData);
        var handled = FastTransferSupport.TryHandleClickItem(
            __instance,
            scroll,
            itemData,
            rowItemLine,
            (_, count) => __instance.Exchange.SelectTargetItem(itemData, count),
            limitCount);

        return !handled;
    }
}

[HarmonyPatch(typeof(ItemListScroll), "SetItemToSelectCountMode")]
internal static class MultiplyItemListScrollFastTransferSelectCountPatch
{
    private static bool Prefix(
        ItemListScroll __instance,
        RowItemLine itemView,
        Action<int> onConfirmSetCount,
        int limitCount,
        int minCount)
    {
        return !FastTransferSupport.TryHandleMultiplyItemSelectCount(
            __instance,
            itemView,
            onConfirmSetCount,
            limitCount,
            minCount);
    }
}

internal static class FastTransferSupport
{
    private enum FastTransferMode
    {
        None,
        All,
        Half,
    }

    internal static bool ShouldUseFastTransfer()
    {
        return GetFastTransferMode() != FastTransferMode.None;
    }

    private static FastTransferMode GetFastTransferMode()
    {
        if (!Plugin.EnableFastTransfer)
            return FastTransferMode.None;

        var ctrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (ctrlPressed)
            return FastTransferMode.Half;

        var shiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (Plugin.InvertFastTransfer)
            return shiftPressed ? FastTransferMode.None : FastTransferMode.All;

        return shiftPressed ? FastTransferMode.All : FastTransferMode.None;
    }

    internal static bool TryHandleClickItem(
        ExchangeViewBase view,
        ItemListScroll scroll,
        ITradeableContent itemData,
        RowItemLine rowItemLine,
        Action<RowItemLine, int> action,
        int limitCount = 0)
    {
        var mode = GetFastTransferMode();
        if (mode == FastTransferMode.None)
            return false;

        if (view == null || scroll == null || itemData == null || rowItemLine == null || action == null)
            return false;

        scroll.HandleClickItem(itemData, rowItemLine, clickedRow =>
        {
            var count = GetSelectableCount(itemData, limitCount, mode);
            if (count <= 0)
            {
                rowItemLine.SetSelected(selected: false);
                return;
            }

            action(clickedRow, count);
            Refresh(view);
            RefreshTipNextFrame(rowItemLine);
        });

        return true;
    }

    internal static bool TryHandleMultiplyItemSelectCount(
        ItemListScroll scroll,
        RowItemLine itemView,
        Action<int> onConfirmSetCount,
        int limitCount,
        int minCount)
    {
        var mode = GetFastTransferMode();
        if (mode == FastTransferMode.None)
            return false;

        if (scroll == null || itemView?.RowItemMain?.Data == null || onConfirmSetCount == null)
            return false;

        var multiplyList = scroll.GetComponentInParent<MultiplyItemListScroll>();
        if (multiplyList == null ||
            !multiplyList.IsMultiItemSelect ||
            multiplyList.CurrItemOperation != ItemOperationType.EItemOperationType.Disassemble)
        {
            return false;
        }

        var count = GetSelectableCount(itemView.RowItemMain.Data.Amount, limitCount, minCount, mode);
        if (count <= 0)
            return false;

        try
        {
            onConfirmSetCount(count);
            RefreshTipNextFrame(itemView);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to apply fast transfer in multiply item list: " + ex);
            return false;
        }
    }

    internal static int GetShopTargetLimitCount(ShopView view, ITradeableContent itemData)
    {
        try
        {
            var exchange = view?.Exchange as ShopExchange;
            var levelDataArray = exchange?.TradeArguments?.OverFavorData?.MerchantOverFavorLevelDataArray;
            var level = (itemData?.ItemSourceType ?? 0) - 10;
            if (levelDataArray == null || level < 0 || level >= levelDataArray.Length)
                return 0;

            var levelData = levelDataArray[level];
            if (levelData == null || levelData.BuyCount == short.MaxValue)
                return 0;

            return levelData.BuyCount;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to read shop purchase limit: " + ex);
            return 0;
        }
    }

    private static int GetSelectableCount(ITradeableContent itemData, int limitCount, FastTransferMode mode)
    {
        return itemData == null ? 0 : GetSelectableCount(itemData.Amount, limitCount, 1, mode);
    }

    private static int GetSelectableCount(int amount, int limitCount, int minCount, FastTransferMode mode)
    {
        if (amount <= 0)
            return 0;

        var maxCount = limitCount > 0 ? Mathf.Min(amount, limitCount) : amount;
        if (maxCount <= 0)
            return 0;

        var safeMin = Mathf.Clamp(minCount, 1, maxCount);
        var count = mode == FastTransferMode.Half ? Mathf.Max(safeMin, (maxCount + 1) / 2) : maxCount;
        return Mathf.Clamp(count, safeMin, maxCount);
    }

    private static void Refresh(ExchangeViewBase view)
    {
        try
        {
            Traverse.Create(view).Method("Refresh").GetValue();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to refresh exchange view after fast transfer: " + ex);
        }
    }

    private static void RefreshTipNextFrame(RowItemLine rowItemLine)
    {
        if (rowItemLine?.TipDisplayer == null)
            return;

        try
        {
            SingletonObject.getInstance<YieldHelper>().DelayFrameDo(1u, () =>
            {
                if (rowItemLine?.TipDisplayer != null)
                    rowItemLine.TipDisplayer.Refresh(forceUseHideAndShow: true);
            });
        }
        catch
        {
        }
    }
}
