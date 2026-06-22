#nullable disable

using System;
using FrameWork;
using Game.Components.ListStyleGeneralScroll.Item;
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
        if (itemData == null || itemData.Amount <= 0)
            return 0;

        var maxCount = limitCount > 0 ? Mathf.Min(itemData.Amount, limitCount) : itemData.Amount;
        return mode == FastTransferMode.Half ? Mathf.Max(1, (maxCount + 1) / 2) : maxCount;
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
