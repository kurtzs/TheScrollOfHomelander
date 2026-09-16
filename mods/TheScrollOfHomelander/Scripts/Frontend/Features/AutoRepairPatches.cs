#nullable disable
#pragma warning disable CS0612

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

[Serializable]
internal sealed class AutoRepairSettings
{
    public bool Enabled = true;
    public bool OnlyBareHandRepair = false;
    public bool AllowBareHandRepair = true;
    public int ToolGradePriority = 1;
    public bool EnableDurabilityProtection = true;

    internal AutoRepairSettings Clone()
    {
        return (AutoRepairSettings)MemberwiseClone();
    }

    internal void Normalize()
    {
        if (OnlyBareHandRepair)
            AllowBareHandRepair = true;
        ToolGradePriority = Mathf.Clamp(ToolGradePriority, 0, 1);
    }
}

internal static class AutoRepairSettingsStore
{
    private const string FileName = "AutoRepairSettings.json";

    internal static AutoRepairSettings Current { get; private set; } = new AutoRepairSettings();

    internal static void Load()
    {
        try
        {
            foreach (var path in ModUserDataPaths.GetFilePathCandidates(FileName))
            {
                if (!File.Exists(path))
                    continue;

                var loaded = new AutoRepairSettings();
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), loaded);
                Current = loaded;
                Current.Normalize();
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to load auto repair settings: " + ex);
        }

        Current = new AutoRepairSettings();
        Current.Normalize();
    }

    internal static void Save()
    {
        try
        {
            Current.Normalize();
            var path = ModUserDataPaths.GetFilePath(FileName);
            AsyncSettingsSaveQueue.Enqueue(path, Current.Clone());
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save auto repair settings: " + ex);
        }
    }
}

[HarmonyPatch(typeof(MakeSubPageRepair), "Awake")]
internal static class MakeSubPageRepairAutoRepairAwakePatch
{
    private static void Postfix(MakeSubPageRepair __instance)
    {
        AutoRepairRepairUiController.GetOrAdd(__instance)?.Refresh();
    }
}

[HarmonyPatch(typeof(MakeSubPageRepair), "Refresh")]
internal static class MakeSubPageRepairAutoRepairRefreshPatch
{
    private static void Postfix(MakeSubPageRepair __instance)
    {
        AutoRepairRepairUiController.GetOrAdd(__instance)?.Refresh();
    }
}

[HarmonyPatch(typeof(MakeSubPageRepair), nameof(MakeSubPageRepair.Init), new[] { typeof(ViewMake) })]
internal static class MakeSubPageRepairAutoRepairInitPatch
{
    private static void Postfix(MakeSubPageRepair __instance)
    {
        AutoRepairRepairUiController.GetOrAdd(__instance)?.Refresh();
    }
}

[HarmonyPatch(typeof(ViewMake), nameof(ViewMake.OnInit), new[] { typeof(ArgumentBox) })]
internal static class ViewMakeAutoRepairUiInitFallbackPatch
{
    private static void Postfix(ViewMake __instance)
    {
        AutoRepairRepairUiController.RefreshForView(__instance);
    }
}

[HarmonyPatch(typeof(ViewMake), "Refresh")]
internal static class ViewMakeAutoRepairUiRefreshFallbackPatch
{
    private static void Postfix(ViewMake __instance)
    {
        AutoRepairRepairUiController.RefreshForView(__instance);
    }
}

internal sealed class AutoRepairRepairUiController : MonoBehaviour
{
    private const float SettingsButtonWidth = 132f;
    private const float SettingsButtonHeight = 52f;
    private const float SettingsButtonGap = 12f;
    private static readonly FieldInfo ConfirmButtonField =
        AccessTools.Field(typeof(MakeSubPageRepair), "confirmBtn");

    private MakeSubPageRepair _page;
    private CButton _settingsButton;
    private CButton[] _buttonComponents;
    private bool _buttonDecorated;
    private Vector2 _confirmButtonBasePosition;
    private bool _confirmButtonBasePositionCaptured;
    private Coroutine _layoutRoutine;

    internal static void RefreshForView(ViewMake view)
    {
        if (view == null)
            return;

        var subPages = Traverse.Create(view).Field("subPages").GetValue<MakeSubPage[]>();
        if (subPages == null)
            return;

        foreach (var page in subPages)
        {
            if (page is MakeSubPageRepair repairPage)
                GetOrAdd(repairPage)?.Refresh();
        }
    }

    internal static AutoRepairRepairUiController GetOrAdd(MakeSubPageRepair page)
    {
        if (page == null)
            return null;

        var controller = page.GetComponent<AutoRepairRepairUiController>();
        if (controller == null)
            controller = page.gameObject.AddComponent<AutoRepairRepairUiController>();

        controller._page = page;
        return controller;
    }

    internal void Refresh()
    {
        EnsureSettingsButton();
        if (_settingsButton != null)
        {
            _settingsButton.gameObject.SetActive(true);
            ScheduleLayoutRefresh();
        }
    }

    private void ScheduleLayoutRefresh()
    {
        if (_layoutRoutine != null || !isActiveAndEnabled)
            return;
        _layoutRoutine = StartCoroutine(RefreshLayoutNextFrame());
    }

    private IEnumerator RefreshLayoutNextFrame()
    {
        yield return null;
        _layoutRoutine = null;
        if (_page == null || _settingsButton == null)
            yield break;
        var confirmBtn = ConfirmButtonField?.GetValue(_page) as CButton;
        var confirmRect = confirmBtn == null ? null : confirmBtn.transform as RectTransform;
        if (confirmRect != null)
            ConfigureButton(_settingsButton.gameObject, confirmRect);
    }

    private void EnsureSettingsButton()
    {
        if (_page == null)
            return;

        var confirmBtn = ConfirmButtonField?.GetValue(_page) as CButton;
        var confirmRect = confirmBtn == null ? null : confirmBtn.transform as RectTransform;
        if (confirmRect == null || confirmRect.parent == null)
            return;

        if (_settingsButton != null)
        {
            ConfigureButton(_settingsButton.gameObject, confirmRect);
            return;
        }

        var existing = confirmRect.parent.Find("BetterTaiwuScrollAutoRepairSettingsButton");
        if (existing != null)
        {
            _settingsButton = existing.GetComponent<CButton>() ?? existing.GetComponentInChildren<CButton>(true);
            if (_settingsButton != null)
            {
                ConfigureButton(_settingsButton.gameObject, confirmRect);
                return;
            }
        }

        // The building-management settings template is not guaranteed to be loaded when the
        // repair page first opens.  The repair button itself is already a native, fully loaded
        // button prefab, so clone it to make the settings button available immediately.
        var buttonObj = Instantiate(confirmBtn.gameObject, confirmRect.parent, false);
        buttonObj.name = "BetterTaiwuScrollAutoRepairSettingsButton";
        _settingsButton = buttonObj.GetComponent<CButton>() ?? buttonObj.GetComponentInChildren<CButton>(true);
        if (_settingsButton == null)
        {
            Destroy(buttonObj);
            return;
        }

        ConfigureButton(buttonObj, confirmRect);
        AutoRepairSettingsPanelSupport.Preload();
        Debug.Log("[BetterTaiwuScroll] Auto repair settings button created.");
    }

    private void ConfigureButton(GameObject buttonObj, RectTransform confirmRect)
    {
        if (!_confirmButtonBasePositionCaptured)
        {
            _confirmButtonBasePosition = confirmRect.anchoredPosition;
            _confirmButtonBasePositionCaptured = true;
        }

        var buttonRect = buttonObj.transform as RectTransform;
        if (buttonRect != null)
        {
            if (buttonRect.parent != confirmRect.parent)
                buttonRect.SetParent(confirmRect.parent, false);

            if (buttonRect.anchorMin != confirmRect.anchorMin)
                buttonRect.anchorMin = confirmRect.anchorMin;
            if (buttonRect.anchorMax != confirmRect.anchorMax)
                buttonRect.anchorMax = confirmRect.anchorMax;
            if (buttonRect.pivot != confirmRect.pivot)
                buttonRect.pivot = confirmRect.pivot;
            var targetSize = new Vector2(SettingsButtonWidth, SettingsButtonHeight);
            if (buttonRect.sizeDelta != targetSize)
                buttonRect.sizeDelta = targetSize;
            var confirmWidth = Mathf.Max(Mathf.Abs(confirmRect.rect.width), Mathf.Abs(confirmRect.sizeDelta.x));
            if (confirmWidth < 1f)
                confirmWidth = 210f;

            // Keep the original repair button and the new settings button as one centered group.
            // Placing the settings button to the unmodified repair button's right would overflow
            // the narrow repair panel and be clipped by its viewport.
            var groupCenterOffset = (SettingsButtonWidth + SettingsButtonGap) * 0.5f;
            var confirmPosition = _confirmButtonBasePosition - new Vector2(groupCenterOffset, 0f);
            var buttonPosition = _confirmButtonBasePosition
                + new Vector2(confirmWidth * 0.5f + SettingsButtonGap
                    + SettingsButtonWidth * 0.5f - groupCenterOffset, 0f);
            if (confirmRect.anchoredPosition != confirmPosition)
                confirmRect.anchoredPosition = confirmPosition;
            if (buttonRect.anchoredPosition != buttonPosition)
                buttonRect.anchoredPosition = buttonPosition;
            if (buttonRect.localScale != Vector3.one)
                buttonRect.localScale = Vector3.one;
            buttonRect.SetAsLastSibling();
        }

        _buttonComponents ??= buttonObj.GetComponentsInChildren<CButton>(true);
        foreach (var button in _buttonComponents)
        {
            if (button != null && !button.interactable)
                button.interactable = true;
        }

        if (!_buttonDecorated)
        {
            _settingsButton.ClearAndAddListener(OpenSettings);
            (buttonObj.GetComponent<AutoRepairButtonTextOverride>()
                ?? buttonObj.AddComponent<AutoRepairButtonTextOverride>()).SetText("设置");
            ConfigureTooltip(buttonObj);
            _buttonDecorated = true;
        }
        buttonObj.SetActive(true);
    }

    private void OpenSettings()
    {
        AutoRepairSettingsPanelSupport.Show(_page);
    }

    private static void SetButtonText(GameObject buttonObj, string text)
    {
        foreach (var label in buttonObj.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label == null)
                continue;

            label.SetText(text);
            label.enableAutoSizing = true;
            label.fontSizeMax = Math.Min(label.fontSizeMax <= 0f ? 30f : label.fontSizeMax, 30f);
            label.fontSizeMin = Math.Max(label.fontSizeMin, 18f);
            label.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    private static void ConfigureTooltip(GameObject buttonObj)
    {
        foreach (var tooltip in buttonObj.GetComponentsInChildren<TooltipInvoker>(true))
        {
            if (tooltip == null)
                continue;

            tooltip.enabled = true;
            tooltip.Type = TipType.Simple;
            tooltip.IsLanguageKey = false;
            tooltip.NeedRefresh = false;
            tooltip.PresetParam = new[]
            {
                "自动修理设置",
                "设置战斗结束后使用行囊工具自动修理已装备武器。"
            };
        }
    }

    private sealed class AutoRepairButtonTextOverride : MonoBehaviour
    {
        private string _text;
        private TMP_Text[] _labels;

        internal void SetText(string text)
        {
            if (_text == text && _labels != null)
                return;
            _text = text ?? string.Empty;
            _labels ??= GetComponentsInChildren<TMP_Text>(true);
            Apply();
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_text))
                return;

            Apply();
        }

        private void Apply()
        {
            _labels ??= GetComponentsInChildren<TMP_Text>(true);
            foreach (var label in _labels)
            {
                if (label != null && label.text != _text)
                    label.SetText(_text);
            }
        }
    }
}

[HarmonyPatch(typeof(Game.Views.Item.ItemMultiplyOperationPanel), "Awake")]
internal static class ItemMultiplyOperationPanelAutoRepairAwakePatch
{
    private static void Postfix(Game.Views.Item.ItemMultiplyOperationPanel __instance)
    {
        AutoRepairItemOperationUiController.GetOrAdd(__instance)?.Refresh();
    }
}

[HarmonyPatch(typeof(Game.Views.Item.ItemMultiplyOperationPanel), nameof(Game.Views.Item.ItemMultiplyOperationPanel.Show), new[] { typeof(ArgumentBox) })]
internal static class ItemMultiplyOperationPanelAutoRepairShowPatch
{
    private static void Postfix(Game.Views.Item.ItemMultiplyOperationPanel __instance)
    {
        AutoRepairItemOperationUiController.GetOrAdd(__instance)?.Refresh();
    }
}

[HarmonyPatch(typeof(Game.Views.Item.ItemMultiplyOperationPanel), "RefreshPage")]
internal static class ItemMultiplyOperationPanelAutoRepairRefreshPagePatch
{
    private static void Postfix(Game.Views.Item.ItemMultiplyOperationPanel __instance)
    {
        AutoRepairItemOperationUiController.GetOrAdd(__instance)?.Refresh();
    }
}

[HarmonyPatch(typeof(Game.Views.Item.ItemMultiplyOperationPanel), "RefreshRepairPage")]
internal static class ItemMultiplyOperationPanelAutoRepairRefreshRepairPagePatch
{
    private static void Postfix(Game.Views.Item.ItemMultiplyOperationPanel __instance)
    {
        AutoRepairItemOperationUiController.GetOrAdd(__instance)?.Refresh();
    }
}

internal sealed class AutoRepairItemOperationUiController : MonoBehaviour
{
    private const float SettingsButtonWidth = 132f;
    private const float SettingsButtonHeight = 52f;
    private const float SettingsButtonGap = 12f;
    private static readonly FieldInfo ConfirmButtonField = AccessTools.Field(
        typeof(Game.Views.Item.ItemMultiplyOperationPanel), "buttonConfirm");
    private static readonly FieldInfo OperationTypeField = AccessTools.Field(
        typeof(Game.Views.Item.ItemMultiplyOperationPanel), "_curOperationType");

    private Game.Views.Item.ItemMultiplyOperationPanel _panel;
    private CButton _settingsButton;
    private CButton[] _buttonComponents;
    private Vector2 _confirmButtonBasePosition;
    private bool _confirmButtonBasePositionCaptured;
    private Coroutine _layoutRoutine;

    internal static AutoRepairItemOperationUiController GetOrAdd(Game.Views.Item.ItemMultiplyOperationPanel panel)
    {
        if (panel == null)
            return null;

        var controller = panel.GetComponent<AutoRepairItemOperationUiController>();
        if (controller == null)
            controller = panel.gameObject.AddComponent<AutoRepairItemOperationUiController>();

        controller._panel = panel;
        return controller;
    }

    internal void Refresh()
    {
        if (_panel == null)
            return;

        var confirmButton = ConfirmButtonField?.GetValue(_panel) as CButton;
        var confirmRect = confirmButton == null ? null : confirmButton.transform as RectTransform;
        if (confirmRect == null || confirmRect.parent == null)
            return;

        var operationType = OperationTypeField?.GetValue(_panel);
        var isRepairPage = operationType != null
            && string.Equals(operationType.ToString(), "Repair", StringComparison.Ordinal);
        if (!isRepairPage)
        {
            if (_confirmButtonBasePositionCaptured)
                confirmRect.anchoredPosition = _confirmButtonBasePosition;
            if (_settingsButton != null)
                _settingsButton.gameObject.SetActive(false);
            return;
        }

        EnsureSettingsButton(confirmButton, confirmRect);
        if (_settingsButton == null)
            return;

        ConfigureButton(_settingsButton.gameObject, confirmRect);
        _settingsButton.gameObject.SetActive(true);
        ScheduleLayoutRefresh();
    }

    private void EnsureSettingsButton(CButton confirmButton, RectTransform confirmRect)
    {
        if (_settingsButton != null)
            return;

        var existing = confirmRect.parent.Find("BetterTaiwuScrollAutoRepairItemSettingsButton");
        if (existing != null)
        {
            _settingsButton = existing.GetComponent<CButton>()
                ?? existing.GetComponentInChildren<CButton>(true);
            return;
        }

        var buttonObj = Instantiate(confirmButton.gameObject, confirmRect.parent, false);
        buttonObj.name = "BetterTaiwuScrollAutoRepairItemSettingsButton";
        _settingsButton = buttonObj.GetComponent<CButton>()
            ?? buttonObj.GetComponentInChildren<CButton>(true);
        if (_settingsButton == null)
        {
            Destroy(buttonObj);
            return;
        }

        _settingsButton.ClearAndAddListener(OpenSettings);
        (buttonObj.GetComponent<ItemOperationButtonTextOverride>()
            ?? buttonObj.AddComponent<ItemOperationButtonTextOverride>()).SetText("设置");
        ConfigureTooltip(buttonObj);
        AutoRepairSettingsPanelSupport.Preload();
        Debug.Log("[BetterTaiwuScroll] Auto repair settings button created on ItemMultiplyOperationPanel.");
    }

    private void ConfigureButton(GameObject buttonObj, RectTransform confirmRect)
    {
        if (!_confirmButtonBasePositionCaptured)
        {
            _confirmButtonBasePosition = confirmRect.anchoredPosition;
            _confirmButtonBasePositionCaptured = true;
        }

        var buttonRect = buttonObj.transform as RectTransform;
        if (buttonRect == null)
            return;

        if (buttonRect.parent != confirmRect.parent)
            buttonRect.SetParent(confirmRect.parent, false);

        if (buttonRect.anchorMin != confirmRect.anchorMin)
            buttonRect.anchorMin = confirmRect.anchorMin;
        if (buttonRect.anchorMax != confirmRect.anchorMax)
            buttonRect.anchorMax = confirmRect.anchorMax;
        if (buttonRect.pivot != confirmRect.pivot)
            buttonRect.pivot = confirmRect.pivot;
        var targetSize = new Vector2(SettingsButtonWidth, SettingsButtonHeight);
        if (buttonRect.sizeDelta != targetSize)
            buttonRect.sizeDelta = targetSize;

        var confirmWidth = Mathf.Max(Mathf.Abs(confirmRect.rect.width), Mathf.Abs(confirmRect.sizeDelta.x));
        if (confirmWidth < 1f)
            confirmWidth = 210f;

        var groupCenterOffset = (SettingsButtonWidth + SettingsButtonGap) * 0.5f;
        var confirmPosition = _confirmButtonBasePosition - new Vector2(groupCenterOffset, 0f);
        var buttonPosition = _confirmButtonBasePosition
            + new Vector2(confirmWidth * 0.5f + SettingsButtonGap
                + SettingsButtonWidth * 0.5f - groupCenterOffset, 0f);
        if (confirmRect.anchoredPosition != confirmPosition)
            confirmRect.anchoredPosition = confirmPosition;
        if (buttonRect.anchoredPosition != buttonPosition)
            buttonRect.anchoredPosition = buttonPosition;
        if (buttonRect.localScale != Vector3.one)
            buttonRect.localScale = Vector3.one;
        buttonRect.SetAsLastSibling();

        _buttonComponents ??= buttonObj.GetComponentsInChildren<CButton>(true);
        foreach (var button in _buttonComponents)
        {
            if (button != null && !button.interactable)
                button.interactable = true;
        }
    }

    private void ScheduleLayoutRefresh()
    {
        if (_layoutRoutine != null || !isActiveAndEnabled)
            return;
        _layoutRoutine = StartCoroutine(RefreshLayoutNextFrame());
    }

    private IEnumerator RefreshLayoutNextFrame()
    {
        yield return null;
        _layoutRoutine = null;
        if (_panel == null || _settingsButton == null)
            yield break;
        var confirmButton = ConfirmButtonField?.GetValue(_panel) as CButton;
        var confirmRect = confirmButton == null ? null : confirmButton.transform as RectTransform;
        if (confirmRect != null)
            ConfigureButton(_settingsButton.gameObject, confirmRect);
    }

    private void OpenSettings()
    {
        AutoRepairSettingsPanelSupport.Show(_panel);
    }

    private static void ConfigureTooltip(GameObject buttonObj)
    {
        foreach (var tooltip in buttonObj.GetComponentsInChildren<TooltipInvoker>(true))
        {
            tooltip.enabled = true;
            tooltip.Type = TipType.Simple;
            tooltip.IsLanguageKey = false;
            tooltip.NeedRefresh = false;
            tooltip.PresetParam = new[]
            {
                "自动修理设置",
                "设置战斗结束后使用行囊工具自动修理已装备武器。"
            };
        }
    }

    private sealed class ItemOperationButtonTextOverride : MonoBehaviour
    {
        private string _text;
        private TMP_Text[] _labels;

        internal void SetText(string text)
        {
            if (_text == text && _labels != null)
                return;
            _text = text ?? string.Empty;
            _labels ??= GetComponentsInChildren<TMP_Text>(true);
            Apply();
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_text))
                return;
            Apply();
        }

        private void Apply()
        {
            if (string.IsNullOrEmpty(_text))
                return;
            _labels ??= GetComponentsInChildren<TMP_Text>(true);
            foreach (var label in _labels)
            {
                if (label != null && label.text != _text)
                    label.SetText(_text);
            }
        }
    }
}

internal static class AutoRepairSettingsPanelSupport
{
    internal static void Show(MonoBehaviour owner)
    {
        ContinuousMakeSettingsPanel.ShowAutoRepair(owner);
    }

    internal static void Preload()
    {
        ContinuousMakeSettingsPanel.Preload();
    }
}

[HarmonyPatch(typeof(Game.Views.Combat.ViewCombatResult), nameof(Game.Views.Combat.ViewCombatResult.OnInit), new[] { typeof(ArgumentBox) })]
internal static class ViewCombatResultAutoRepairInitPatch
{
    private static void Postfix(Game.Views.Combat.ViewCombatResult __instance)
    {
        AutoRepairCombatController.Prepare(__instance);
    }
}

[HarmonyPatch(typeof(Game.Views.Combat.ViewCombatResult), "OnShowed")]
internal static class ViewCombatResultAutoRepairShowedPatch
{
    private static void Postfix(Game.Views.Combat.ViewCombatResult __instance)
    {
        AutoRepairCombatController.RequestWhenResultReady(__instance);
    }
}

internal static class AutoRepairCombatController
{
    internal const string MethodName = BetterTaiwuScroll.Shared.ModProtocol.AutoRepair;
    private static readonly HashSet<int> RequestedViewIds = new HashSet<int>();

    internal static void Prepare(Game.Views.Combat.ViewCombatResult view)
    {
        if (view == null)
            return;

        RequestedViewIds.Remove(view.GetInstanceID());
        if (!IsEnabled())
            return;
        AutoRepairCombatResultRequestController.GetOrAdd(view)?.ResetRequest();
    }

    internal static void RequestWhenResultReady(Game.Views.Combat.ViewCombatResult view)
    {
        if (!IsEnabled())
            return;
        AutoRepairCombatResultRequestController.GetOrAdd(view)?.Begin();
    }

    internal static void Reset()
    {
        RequestedViewIds.Clear();
    }

    internal static void Close(Game.Views.Combat.ViewCombatResult view)
    {
        if (view != null) RequestedViewIds.Remove(view.GetInstanceID());
    }

    internal static void TryRequest(Game.Views.Combat.ViewCombatResult view)
    {
        var settings = AutoRepairSettingsStore.Current;
        if (view == null || settings == null || !Plugin.EnableAutoRepairEquippedWeapons
            || !settings.Enabled || string.IsNullOrEmpty(Plugin.ModId))
            return;

        settings.Normalize();
        var viewId = view.GetInstanceID();
        var session = ModSession.Generation;
        if (!RequestedViewIds.Add(viewId))
            return;

        var parameter = new SerializableModData();
        parameter.Set("version", 1);
        parameter.Set("enabled", settings.Enabled);
        parameter.Set("onlyBareHandRepair", settings.OnlyBareHandRepair);
        parameter.Set("allowBareHandRepair", settings.AllowBareHandRepair);
        parameter.Set("toolGradePriority", settings.ToolGradePriority);
        parameter.Set("protectToolDurability", settings.EnableDurabilityProtection);

        try
        {
            ModDomainMethod.AsyncCall.CallModMethodWithParamAndRet(
                view,
                Plugin.ModId,
                MethodName,
                parameter,
                (offset, pool) => { if (ModSession.IsCurrent(session)) HandleResponse(offset, pool); });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to request auto repair: " + ex);
        }
    }

    private static bool IsEnabled()
    {
        return Plugin.EnableAutoRepairEquippedWeapons
            && AutoRepairSettingsStore.Current != null
            && AutoRepairSettingsStore.Current.Enabled
            && !string.IsNullOrEmpty(Plugin.ModId);
    }

    private static void HandleResponse(int offset, RawDataPool pool)
    {
        try
        {
            SerializableModData response = null;
            Serializer.Deserialize(pool, offset, ref response);
            if (response == null)
            {
                Debug.LogWarning("[BetterTaiwuScroll] Auto repair returned an empty response.");
                return;
            }

            response.Get("success", out bool success);
            response.Get("code", out string code);
            response.Get("message", out string message);
            response.Get("repairedCount", out int repairedCount);
            response.Get("skippedCount", out int skippedCount);
            Debug.Log("[BetterTaiwuScroll] Auto repair result: success=" + success
                + ", code=" + (code ?? string.Empty)
                + ", repaired=" + repairedCount
                + ", skipped=" + skippedCount
                + ", message=" + (message ?? string.Empty));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to decode auto repair response: " + ex);
        }
    }
}

internal sealed class AutoRepairCombatResultRequestController : MonoBehaviour
{
    private const int MaxLootPageWaitFrames = 180;

    private Game.Views.Combat.ViewCombatResult _view;
    private Coroutine _requestRoutine;
    private int _session;

    internal static AutoRepairCombatResultRequestController GetOrAdd(
        Game.Views.Combat.ViewCombatResult view)
    {
        if (view == null)
            return null;

        var controller = view.GetComponent<AutoRepairCombatResultRequestController>();
        if (controller == null)
            controller = view.gameObject.AddComponent<AutoRepairCombatResultRequestController>();
        controller._view = view;
        return controller;
    }

    internal void ResetRequest()
    {
        if (_requestRoutine != null)
        {
            StopCoroutine(_requestRoutine);
            _requestRoutine = null;
        }
    }

    internal void Begin()
    {
        if (_view == null || !isActiveAndEnabled || _requestRoutine != null)
            return;

        _session = ModSession.Generation;
        _requestRoutine = StartCoroutine(RequestAfterResultIsReady());
    }

    private void OnDisable()
    {
        ResetRequest();
        AutoRepairCombatController.Close(_view);
    }

    private IEnumerator RequestAfterResultIsReady()
    {
        // ViewCombatResult.OnInit still has one asynchronous resource-display request.  The
        // loot page becomes active in that callback, which is the first reliable indication
        // that the visible settlement screen (and its backend reads) has completed setup.
        var waitFrames = 0;
        var ready = false;
        while (_view != null && ModSession.IsCurrent(_session) && waitFrames++ < MaxLootPageWaitFrames)
        {
            var lootPage = Traverse.Create(_view).Field("lootPage").GetValue<CombatResultLootPage>();
            if (lootPage != null && lootPage.gameObject.activeInHierarchy)
            {
                ready = true;
                break;
            }
            yield return null;
        }

        // Let all callbacks queued by the result-page initialization finish before mutating
        // equipment durability in the backend.
        yield return new WaitForEndOfFrame();
        yield return null;

        if (ready && _view != null && _view.gameObject.activeInHierarchy && ModSession.IsCurrent(_session))
        {
            Debug.Log("[BetterTaiwuScroll] Combat result is ready; requesting auto repair.");
            AutoRepairCombatController.TryRequest(_view);
        }
        _requestRoutine = null;
    }
}
