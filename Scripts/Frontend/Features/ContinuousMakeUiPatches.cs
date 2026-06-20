#nullable disable
#pragma warning disable CS0612

using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(ViewMake), "OnInit")]
internal static class ViewMakeContinuousMakeUiPatch
{
    private static void Postfix(ViewMake __instance)
    {
        ContinuousMakeUiController.GetOrAdd(__instance)?.Refresh();
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "RefreshPanel")]
internal static class MakeSubPageMakeContinuousMakeUiPatch
{
    private static void Postfix(MakeSubPageMake __instance)
    {
        var view = MakeSelectMaterialPatch.GetParentView(__instance);
        ContinuousMakeUiController.GetOrAdd(view)?.Refresh(__instance);
    }
}

[HarmonyPatch(typeof(Game.Views.Building.BuildingManage.BuildingManageSubPageShop), "Init")]
internal static class BuildingManageSubPageShopContinuousMakeUiTemplatePatch
{
    private static void Postfix(Game.Views.Building.BuildingManage.BuildingManageSubPageShop __instance)
    {
        NativeContinuousMakeUiTemplates.BuildingManageShopTemplate = __instance;
    }
}

internal static class NativeContinuousMakeUiTemplates
{
    internal static Game.Views.Building.BuildingManage.BuildingManageSubPageShop BuildingManageShopTemplate;
    private static bool _requestingBuildingManageTemplate;

    internal static CButton FindArrangementSettingButtonTemplate()
    {
        var button = FindArrangementSettingButtonTemplate(BuildingManageShopTemplate);
        if (button != null)
            return button;

        foreach (var template in Resources.FindObjectsOfTypeAll<Game.Views.Building.BuildingManage.BuildingManageSubPageShop>())
        {
            button = FindArrangementSettingButtonTemplate(template);
            if (button != null)
            {
                BuildingManageShopTemplate = template;
                return button;
            }
        }

        return null;
    }

    private static CButton FindArrangementSettingButtonTemplate(Game.Views.Building.BuildingManage.BuildingManageSubPageShop template)
    {
        if (template == null)
            return null;

        return Traverse.Create(template).Field("buttonArrangementSetting").GetValue<CButton>();
    }

    internal static void RequestArrangementSettingButtonTemplate(Action<CButton> onReady)
    {
        var button = FindArrangementSettingButtonTemplate();
        if (button != null)
        {
            onReady?.Invoke(button);
            return;
        }

        if (_requestingBuildingManageTemplate)
            return;

        _requestingBuildingManageTemplate = true;
        UIElement.BuildingManage.PrepareRes(false, _ =>
        {
            _requestingBuildingManageTemplate = false;
            FindArrangementSettingButtonTemplate();
            onReady?.Invoke(FindArrangementSettingButtonTemplate());
        });
    }

}

internal sealed class ContinuousMakeUiController : MonoBehaviour
{
    private const float ContinuousToggleWidth = 180f;
    private const float ContinuousToggleHeight = 46f;
    private const float ContinuousToggleOffsetY = 70f;
    private const float SettingsButtonOffsetX = -155f;

    private ViewMake _view;
    private CToggle _continuousToggle;
    private CButton _settingsButton;

    internal static bool IsContinuousMakeEnabled => Plugin.EnableContinuousMakeUi && ContinuousMakeSettingsStore.Current.ContinuousMakeEnabled;

    internal static ContinuousMakeUiController GetOrAdd(ViewMake view)
    {
        if (view == null)
            return null;

        var controller = view.GetComponent<ContinuousMakeUiController>();
        if (controller == null)
            controller = view.gameObject.AddComponent<ContinuousMakeUiController>();

        controller._view = view;
        return controller;
    }

    internal static void RefreshAll()
    {
        foreach (var controller in Resources.FindObjectsOfTypeAll<ContinuousMakeUiController>())
            controller.Refresh();
    }

    internal void Refresh(MakeSubPageMake activeMakePage = null)
    {
        if (_view == null)
            _view = GetComponent<ViewMake>();

        if (!Plugin.EnableContinuousMakeUi)
        {
            SetActive(_continuousToggle, false);
            SetActive(_settingsButton, false);
            return;
        }

        EnsureSettingsButton();
        activeMakePage ??= GetActiveMakePage();
        EnsureContinuousToggle(activeMakePage);
        SetActive(_settingsButton, true);
        SetActive(_continuousToggle, activeMakePage != null && activeMakePage.gameObject.activeInHierarchy);
    }

    private void EnsureContinuousToggle(MakeSubPageMake page)
    {
        if (page == null)
            return;

        var confirmButton = Traverse.Create(page).Field("buttonConfirm").GetValue<CButton>();
        var toolSlot = Traverse.Create(page).Field("toolSlot").GetValue<MakeTargetSlot>();
        if (confirmButton == null || toolSlot == null)
            return;

        var sourceToggle = Traverse.Create(toolSlot).Field("toggle").GetValue<CToggle>();
        if (sourceToggle == null)
            return;

        var confirmRect = confirmButton.transform as RectTransform;
        if (confirmRect == null)
            return;

        if (_continuousToggle == null)
        {
            var toggleObj = Instantiate(sourceToggle.gameObject);
            toggleObj.name = "BetterTaiwuScrollContinuousMakeToggle";
            _continuousToggle = toggleObj.GetComponent<CToggle>() ?? toggleObj.AddComponent<CToggle>();
            _continuousToggle.onValueChanged.RemoveAllListeners();
            _continuousToggle.onValueChanged.AddListener(OnContinuousToggleChanged);
            SetToggleLabel(toggleObj, "连续制作");
            ConfigureContinuousToggleTooltip(toggleObj);
        }

        var toggleRect = _continuousToggle.transform as RectTransform;
        if (toggleRect == null)
            return;

        if (toggleRect.parent != confirmRect.parent)
            toggleRect.SetParent(confirmRect.parent, false);

        toggleRect.anchorMin = confirmRect.anchorMin;
        toggleRect.anchorMax = confirmRect.anchorMax;
        toggleRect.pivot = confirmRect.pivot;
        toggleRect.sizeDelta = new Vector2(ContinuousToggleWidth, ContinuousToggleHeight);
        toggleRect.anchoredPosition = confirmRect.anchoredPosition + new Vector2(0f, ContinuousToggleOffsetY);
        toggleRect.localScale = Vector3.one;
        toggleRect.SetAsLastSibling();

        _continuousToggle.SetIsOnWithoutNotify(ContinuousMakeSettingsStore.Current.ContinuousMakeEnabled);
        _continuousToggle.gameObject.SetActive(true);
    }

    private void EnsureSettingsButton()
    {
        if (_view == null || _settingsButton != null)
            return;

        var quickEncyclopedia = Traverse.Create(_view).Field("quickEncyclopedia").GetValue<QuickEncyclopedia>();
        var quickRect = quickEncyclopedia == null ? null : quickEncyclopedia.transform as RectTransform;
        if (quickRect == null || quickRect.parent == null)
            return;

        var nativeTemplate = NativeContinuousMakeUiTemplates.FindArrangementSettingButtonTemplate();
        if (nativeTemplate == null)
        {
            NativeContinuousMakeUiTemplates.RequestArrangementSettingButtonTemplate(_ => EnsureSettingsButton());
            return;
        }

        var buttonObj = Instantiate(nativeTemplate.gameObject, quickRect.parent, false);
        buttonObj.name = "BetterTaiwuScrollContinuousMakeSettingsButton";
        var buttonRect = buttonObj.transform as RectTransform;
        if (buttonRect.parent != quickRect.parent)
            buttonRect.SetParent(quickRect.parent, false);

        buttonRect.anchorMin = quickRect.anchorMin;
        buttonRect.anchorMax = quickRect.anchorMax;
        buttonRect.pivot = quickRect.pivot;
        buttonRect.sizeDelta = new Vector2(132f, 52f);
        buttonRect.anchoredPosition = quickRect.anchoredPosition + new Vector2(SettingsButtonOffsetX, 0f);
        buttonRect.localScale = Vector3.one;
        buttonRect.SetSiblingIndex(Math.Max(quickRect.GetSiblingIndex(), 0));

        _settingsButton = buttonObj.GetComponent<CButton>() ?? buttonObj.GetComponentInChildren<CButton>(true);
        if (_settingsButton == null)
        {
            Destroy(buttonObj);
            return;
        }

        SetButtonInteractable(buttonObj, true);
        _settingsButton.ClearAndAddListener(OpenSettingsPanel);

        SetButtonText(buttonObj, "设置");
        (buttonObj.GetComponent<ButtonTextOverride>() ?? buttonObj.AddComponent<ButtonTextOverride>()).SetText("设置");
        ConfigureSettingsButtonTooltip(buttonObj);
        buttonObj.SetActive(true);
        ContinuousMakeSettingsPanel.Preload();
    }

    private void OpenSettingsPanel()
    {
        ContinuousMakeSettingsPanel.Show(_view);
    }

    private MakeSubPageMake GetActiveMakePage()
    {
        if (_view == null)
            return null;

        var subPages = Traverse.Create(_view).Field("subPages").GetValue<MakeSubPage[]>();
        if (subPages == null)
            return null;

        foreach (var page in subPages)
        {
            if (page is MakeSubPageMake makePage && makePage.gameObject.activeInHierarchy)
                return makePage;
        }

        return null;
    }

    private void OnContinuousToggleChanged(bool isOn)
    {
        ContinuousMakeSettingsStore.Current.ContinuousMakeEnabled = isOn;
        ContinuousMakeSettingsStore.Save();
    }

    private static TextMeshProUGUI CreateText(RectTransform parent, string text, float fontSize, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var obj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = obj.transform as RectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;

        var label = obj.GetComponent<TextMeshProUGUI>();
        CopyFont(label, parent);
        label.SetText(text);
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = new Color(0.82f, 0.86f, 0.80f, 1f);
        label.raycastTarget = false;
        return label;
    }

    private static void CopyFont(TextMeshProUGUI target, Component context)
    {
        var source = context == null ? null : context.GetComponentInParent<ViewMake>(true)?.GetComponentInChildren<TextMeshProUGUI>(true);
        if (source == null)
            return;

        target.font = source.font;
        target.fontSharedMaterial = source.fontSharedMaterial;
    }

    private static void SetToggleLabel(GameObject toggleObj, string text)
    {
        foreach (var label in toggleObj.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (label == null || string.IsNullOrWhiteSpace(label.text))
                continue;

            label.SetText(text);
            label.enableAutoSizing = true;
            label.fontSizeMax = 26f;
            label.fontSizeMin = 18f;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return;
        }
    }

    private static void SetButtonText(GameObject buttonObj, string text)
    {
        if (buttonObj == null)
            return;

        var changed = false;
        foreach (var label in buttonObj.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label == null)
                continue;

            label.SetText(text);
            label.enableAutoSizing = true;
            label.fontSizeMax = Math.Min(label.fontSizeMax <= 0f ? 30f : label.fontSizeMax, 30f);
            label.fontSizeMin = Math.Max(label.fontSizeMin, 18f);
            label.overflowMode = TextOverflowModes.Ellipsis;
            changed = true;
        }

        if (!changed && buttonObj.transform is RectTransform rect)
            CreateText(rect, text, 28f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private static void SetButtonInteractable(GameObject buttonObj, bool interactable)
    {
        foreach (var button in buttonObj.GetComponentsInChildren<CButton>(true))
            button.interactable = interactable;
    }

    private static void ConfigureContinuousToggleTooltip(GameObject toggleObj)
    {
        var tooltips = toggleObj.GetComponentsInChildren<TooltipInvoker>(true);
        foreach (var tooltip in tooltips)
        {
            if (tooltip == null)
                continue;

            tooltip.enabled = true;
            tooltip.Type = TipType.Simple;
            tooltip.IsLanguageKey = false;
            tooltip.NeedRefresh = false;
            tooltip.PresetParam = new[]
            {
                "连续制作",
                "勾选后，制作完成时将按照设置继续进行下一次制作。"
            };
        }
    }

    private static void ConfigureSettingsButtonTooltip(GameObject buttonObj)
    {
        var tooltips = buttonObj.GetComponentsInChildren<TooltipInvoker>(true);
        foreach (var tooltip in tooltips)
        {
            if (tooltip == null)
                continue;

            tooltip.enabled = true;
            tooltip.Type = TipType.Simple;
            tooltip.IsLanguageKey = false;
            tooltip.NeedRefresh = false;
            tooltip.PresetParam = new[]
            {
                "连续制作设置",
                "打开连续制作的设置界面。"
            };
        }
    }

    private static void SetActive(Component component, bool active)
    {
        if (component != null)
            component.gameObject.SetActive(active);
    }

    private sealed class ButtonTextOverride : MonoBehaviour
    {
        private const int ApplyFrames = 8;

        private string _text;
        private int _remainingFrames;

        internal void SetText(string text)
        {
            _text = text;
            _remainingFrames = ApplyFrames;
            enabled = true;
            Apply();
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_text))
                return;

            _remainingFrames = ApplyFrames;
            Apply();
        }

        private void LateUpdate()
        {
            if (string.IsNullOrEmpty(_text))
            {
                enabled = false;
                return;
            }

            Apply();
            _remainingFrames--;
            if (_remainingFrames <= 0)
                enabled = false;
        }

        private void Apply()
        {
            SetButtonText(gameObject, _text);
        }
    }
}
