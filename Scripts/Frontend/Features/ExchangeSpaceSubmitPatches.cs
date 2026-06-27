#nullable disable

using System;
using Game.Views;
using Game.Views.Exchange;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(ViewExchangeBase), "Awake")]
internal static class ViewExchangeBaseSpaceSubmitPatch
{
    private static void Postfix(ViewExchangeBase __instance)
    {
        ExchangeSpaceSubmitController.Ensure(__instance);
    }
}

[HarmonyPatch(typeof(ViewShop), "Update")]
internal static class ViewShopSpaceSubmitUpdatePatch
{
    private static void Postfix(ViewShop __instance)
    {
        ExchangeSpaceSubmitSupport.TrySubmitBySpace(__instance);
    }
}

[HarmonyPatch(typeof(ViewWarehouse), "OnEnable")]
internal static class ViewWarehouseSpaceSubmitOnEnablePatch
{
    private static void Postfix(ViewWarehouse __instance)
    {
        ExchangeSpaceSubmitController.Ensure(__instance);
    }
}

[HarmonyPatch(typeof(ViewSettlementShop), "OnEnable")]
internal static class ViewSettlementShopSpaceSubmitOnEnablePatch
{
    private static void Postfix(ViewSettlementShop __instance)
    {
        ExchangeSpaceSubmitController.Ensure(__instance);
    }
}

[HarmonyPatch(typeof(ViewSettlementShop), "OnInit")]
internal static class ViewSettlementShopSpaceSubmitOnInitPatch
{
    private static void Postfix(ViewSettlementShop __instance)
    {
        ExchangeSpaceSubmitController.Ensure(__instance);
    }
}

[HarmonyPatch(typeof(UI_ShopConfirm), "Update")]
internal static class ShopConfirmSpaceConfirmPatch
{
    private static bool Prefix()
    {
        return !ExchangeSpaceSubmitSupport.ShouldSuppressConfirmSpaceThisFrame();
    }
}

[HarmonyPatch(typeof(UI_ShopConfirm), "OnConfirmClick")]
internal static class ShopConfirmClickGuardPatch
{
    private static bool Prefix()
    {
        return !ExchangeSpaceSubmitSupport.ShouldSuppressConfirmAction();
    }
}

[HarmonyPatch(typeof(UI_Dialog), "Update")]
internal static class LegacyDialogSameFrameSpaceGuardPatch
{
    private static bool Prefix()
    {
        return !ExchangeSpaceSubmitSupport.ShouldSuppressConfirmSpaceThisFrame();
    }
}

[HarmonyPatch(typeof(ViewDialog), "Update")]
internal static class ViewDialogSameFrameSpaceGuardPatch
{
    private static bool Prefix()
    {
        return !ExchangeSpaceSubmitSupport.ShouldSuppressConfirmSpaceThisFrame();
    }
}

[HarmonyPatch(typeof(ViewConfirmDialog), "Update")]
internal static class ViewConfirmDialogSameFrameSpaceGuardPatch
{
    private static bool Prefix()
    {
        return !ExchangeSpaceSubmitSupport.ShouldSuppressConfirmSpaceThisFrame();
    }
}

internal sealed class ExchangeSpaceSubmitController : MonoBehaviour
{
    private ViewExchangeBase _view;

    internal static void Ensure(ViewExchangeBase view)
    {
        if (view == null || !ExchangeSpaceSubmitSupport.IsSupported(view))
            return;

        var controller = view.GetComponent<ExchangeSpaceSubmitController>();
        if (controller == null)
            controller = view.gameObject.AddComponent<ExchangeSpaceSubmitController>();

        controller._view = view;
    }

    private void Update()
    {
        ExchangeSpaceSubmitSupport.TrySubmitBySpace(_view);
    }
}

internal static class ExchangeSpaceSubmitSupport
{
    private const float ConfirmActionSuppressSeconds = 0.25f;

    private static int _lastSubmitFrame = -1;
    private static bool _suppressConfirmUntilSpaceReleased;
    private static float _suppressConfirmActionUntilRealtime = -1f;

    internal static bool IsSupported(ViewExchangeBase view)
    {
        return view is ViewShop || view is ViewWarehouse || view is ViewSettlementShop;
    }

    internal static void TrySubmitBySpace(ViewExchangeBase view)
    {
        if (!Plugin.EnableSpaceSubmitExchange)
            return;

        if (view == null || !IsSupported(view) || !view.gameObject.activeInHierarchy)
            return;

        if (!Input.GetKeyDown(KeyCode.Space) || _lastSubmitFrame == Time.frameCount)
            return;

        if (IsTextInputFocused() || IsBlockingOverlayActive() || !CanSubmit(view))
            return;

        try
        {
            _lastSubmitFrame = Time.frameCount;
            SuppressConfirmUntilSpaceReleased();
            Submit(view);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to submit exchange by Space: " + ex);
        }
    }

    internal static bool ShouldSuppressConfirmSpaceThisFrame()
    {
        if (_suppressConfirmUntilSpaceReleased)
        {
            if (Input.GetKey(KeyCode.Space))
                return true;

            _suppressConfirmUntilSpaceReleased = false;
        }

        return Input.GetKeyDown(KeyCode.Space) && _lastSubmitFrame == Time.frameCount;
    }

    internal static bool ShouldSuppressConfirmAction()
    {
        if (_suppressConfirmUntilSpaceReleased)
        {
            if (Input.GetKey(KeyCode.Space))
                return true;

            _suppressConfirmUntilSpaceReleased = false;
        }

        return Time.unscaledTime <= _suppressConfirmActionUntilRealtime ||
               (Input.GetKeyDown(KeyCode.Space) && _lastSubmitFrame == Time.frameCount);
    }

    private static void SuppressConfirmUntilSpaceReleased()
    {
        _suppressConfirmUntilSpaceReleased = Input.GetKey(KeyCode.Space) || Input.GetKeyDown(KeyCode.Space);
        _suppressConfirmActionUntilRealtime = Time.unscaledTime + ConfirmActionSuppressSeconds;
    }

    private static void Submit(ViewExchangeBase view)
    {
        if (view is ViewShop shop && !Plugin.SpaceSubmitExchangeNeedConfirm && CanShopTradeWithoutConfirm(shop))
        {
            shop.SubmitImpl();
            return;
        }

        view.Submit();
    }

    private static bool CanShopTradeWithoutConfirm(ViewShop shop)
    {
        try
        {
            var exchange = shop?.Exchange;
            return exchange != null && exchange.TaiwuValueBase >= exchange.TotalValue;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanSubmit(ViewExchangeBase view)
    {
        try
        {
        if (!view.CanConfirmExchange)
                return false;

            var container = Traverse.Create(view).Field("exchangeContainer").GetValue<ExchangeContainer>();
            return container?.confirm != null &&
                   container.confirm.gameObject.activeInHierarchy &&
                   container.confirm.interactable;
        }
        catch
        {
            return false;
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
        return IsUiElementActive(UIElement.ShopConfirm) ||
               IsUiElementActive(UIElement.Dialog) ||
               IsUiElementActive(UIElement.SetSelectCount);
    }

    private static bool IsUiElementActive(UIElement element)
    {
        var uiBase = element?.UiBase;
        return uiBase != null && uiBase.gameObject.activeInHierarchy;
    }
}
