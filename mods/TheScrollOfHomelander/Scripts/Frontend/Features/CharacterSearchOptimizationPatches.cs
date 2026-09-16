#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BetterTaiwuScroll.Shared;
using FrameWork.UISystem.UIElements;
using Game.Components.SortAndFilter;
using Game.Views;
using GameData.Domains.Character;
using GameData.Domains.Character.Display;
using GameData.Domains.Map;
using GameData.GameDataBridge;
using GameData.Serializer;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(ViewTaiwuVillagers), "Awake")]
internal static class CharacterSearchViewAwakePatch
{
    private static void Postfix(ViewTaiwuVillagers __instance)
    {
        CharacterSearchOptimizationController.GetOrAdd(__instance);
    }
}

[HarmonyPatch(typeof(ViewTaiwuVillagers), "OnEnable")]
internal static class CharacterSearchViewOnEnablePatch
{
    private static void Postfix(ViewTaiwuVillagers __instance)
    {
        CharacterSearchOptimizationController.GetOrAdd(__instance)?.OnViewEnabled();
    }
}

[HarmonyPatch(typeof(ViewTaiwuVillagers), nameof(ViewTaiwuVillagers.OnInit), new[] { typeof(ArgumentBox) })]
internal static class CharacterSearchViewOnInitPatch
{
    private static void Postfix(ViewTaiwuVillagers __instance)
    {
        CharacterSearchOptimizationController.GetOrAdd(__instance)?.OnViewInitialized();
    }
}

[HarmonyPatch(typeof(ViewTaiwuVillagers), "OnDisable")]
internal static class CharacterSearchViewOnDisablePatch
{
    private static void Prefix(ViewTaiwuVillagers __instance)
    {
        CharacterSearchOptimizationController.Get(__instance)?.OnViewDisabled();
    }
}

[HarmonyPatch(typeof(ViewTaiwuVillagers), "RefreshListData")]
internal static class CharacterSearchRefreshListPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(ViewTaiwuVillagers __instance, out CharacterSearchRefreshState __state)
    {
        __state = null;
        CharacterSearchOptimizationController.Get(__instance)?.PrepareRefresh(out __state);
    }

    private static void Postfix(CharacterSearchRefreshState __state)
    {
        __state?.Restore();
    }
}

internal sealed class CharacterSearchRefreshState
{
    private readonly ViewTaiwuVillagers _view;
    private readonly TMP_InputField _input;
    private readonly List<VillagerCharDisplayData> _originalData;
    private readonly string _originalText;
    private readonly int _selectionAnchorPosition;
    private readonly int _selectionFocusPosition;

    internal CharacterSearchRefreshState(
        ViewTaiwuVillagers view,
        TMP_InputField input,
        List<VillagerCharDisplayData> originalData,
        string originalText)
    {
        _view = view;
        _input = input;
        _originalData = originalData;
        _originalText = originalText;
        _selectionAnchorPosition = input == null ? 0 : input.selectionAnchorPosition;
        _selectionFocusPosition = input == null ? 0 : input.selectionFocusPosition;
    }

    internal void Restore()
    {
        if (_view == null)
            return;
        CharacterSearchOptimizationController.DataListField.SetValue(_view, _originalData);
        if (_input != null)
        {
            var restoredWithoutUiMutation = CharacterSearchOptimizationController.SetInputTextBacking(
                _input,
                _originalText ?? string.Empty);
            if (!restoredWithoutUiMutation)
            {
                var length = _input.text?.Length ?? 0;
                _input.selectionAnchorPosition = Mathf.Clamp(_selectionAnchorPosition, 0, length);
                _input.selectionFocusPosition = Mathf.Clamp(_selectionFocusPosition, 0, length);
                _input.ForceLabelUpdate();
            }
        }
    }
}

internal sealed class CharacterSearchOptimizationController : MonoBehaviour
{
    internal const string MethodName = BetterTaiwuScroll.Shared.ModProtocol.SearchCharacters;
    private int _timeoutVersion;
    private int _querySession;
    private List<VillagerCharDisplayData> _lastValidData;
    private const float DebounceSeconds = 0.25f;
    private const float ControlHeight = 52f;
    private const float ScopeWidth = 128f;
    private const float AddButtonWidth = 42f;
    private const float Gap = 8f;
    private const float TagViewportWidth = 420f;
    private const float SearchInputMinWidth = 640f;
    private const string UiRootName = "BetterTaiwuScrollCharacterSearchControls";
    private const string ReferenceScopeDropdownName = "BetterSearchTaiwuVillagersScopeDropdown";

    internal static readonly FieldInfo IdsField = AccessTools.Field(typeof(ViewTaiwuVillagers), "_ids");
    internal static readonly FieldInfo DataListField = AccessTools.Field(typeof(ViewTaiwuVillagers), "_dataList");
    private static readonly FieldInfo SearchingFieldField = AccessTools.Field(typeof(ViewTaiwuVillagers), "searchingField");
    private static readonly FieldInfo EntryToggleField = AccessTools.Field(typeof(SortAndFilter), "entryToggle");
    private static readonly FieldInfo InputTextBackingField = AccessTools.Field(typeof(TMP_InputField), "m_Text");
    private static readonly MethodInfo RefreshListDataMethod = AccessTools.Method(typeof(ViewTaiwuVillagers), "RefreshListData");
    private static readonly List<CharacterSearchOptimizationController> Instances = new List<CharacterSearchOptimizationController>();
    private static readonly List<string> PinnedTerms = new List<string>();
    private static readonly List<string> ScopeLabels = new List<string> { "当前格子", "当前区域", "全世界" };

    private ViewTaiwuVillagers _view;
    private TMP_InputField _input;
    private RectTransform _uiRoot;
    private CDropdown _scopeDropdown;
    private CButton _addButton;
    private RectTransform _tagViewport;
    private RectTransform _tagContent;
    private RectTransform _inputOriginalParent;
    private int _inputOriginalSiblingIndex;
    private RectSnapshot _inputOriginalRect;
    private RectSnapshot _textViewportOriginalRect;
    private LayoutElement _inputLayoutElement;
    private bool _inputHadLayoutElement;
    private bool _inputOriginalIgnoreLayout;
    private CharacterSearchLayoutMarker _layoutMarker;
    private SortAndFilter _sortAndFilter;
    private RectTransform _searchRowParent;
    private Canvas _canvas;
    private readonly List<ReferenceCharacterSearchDropdownState> _suppressedReferenceDropdowns = new();
    private Coroutine _debounceRoutine;
    private Coroutine _layoutRoutine;
    private bool _active;
    private int _scope;
    private int _generation;
    private int _requestId;
    private string _pendingKey = string.Empty;
    private string _cachedKey = string.Empty;
    private List<VillagerCharDisplayData> _cachedData;
    private Location _anchor = Location.Invalid;
    private int _lastScreenWidth = -1;
    private float _lastCanvasScale = -1f;
    private float _lastInputRight = float.NaN;
    private Vector2 _lastParentSize = new(float.NaN, float.NaN);

    internal static CharacterSearchOptimizationController GetOrAdd(ViewTaiwuVillagers view)
    {
        if (view == null)
            return null;
        var controller = view.GetComponent<CharacterSearchOptimizationController>();
        if (controller == null)
            controller = view.gameObject.AddComponent<CharacterSearchOptimizationController>();
        controller._view = view;
        if (!Instances.Contains(controller))
            Instances.Add(controller);
        return controller;
    }

    internal static CharacterSearchOptimizationController Get(ViewTaiwuVillagers view)
    {
        return view == null ? null : view.GetComponent<CharacterSearchOptimizationController>();
    }

    internal static void RefreshAllActive()
    {
        for (var i = Instances.Count - 1; i >= 0; i--)
        {
            var controller = Instances[i];
            if (controller == null)
            {
                Instances.RemoveAt(i);
                continue;
            }
            controller.ApplySetting();
        }
    }

    internal static void DisposeAll()
    {
        for (var i = Instances.Count - 1; i >= 0; i--)
        {
            if (Instances[i] != null)
                Instances[i].DisableAndRemoveUi();
        }
        Instances.Clear();
        PinnedTerms.Clear();
        CharacterSearchUiAssets.Dispose();
    }

    internal void OnViewEnabled()
    {
        if (_view == null || !_view.gameObject.activeInHierarchy)
            return;

        _active = Plugin.EnableSearchOptimization && IdsField.GetValue(_view) != null;
        if (_active)
            EnsureUi();
    }

    internal void OnViewInitialized()
    {
        _generation++;
        CancelUiCoroutines();
        _scope = 0;
        _pendingKey = string.Empty;
        _cachedKey = string.Empty;
        _cachedData = null;
        _anchor = Location.Invalid;
        _lastValidData = null;
        _active = Plugin.EnableSearchOptimization
            && _view != null
            && _view.gameObject.activeInHierarchy
            && IdsField.GetValue(_view) != null;

        if (!_active)
        {
            DisableAndRemoveUi();
            return;
        }

        EnsureUi();
        if (_scopeDropdown != null)
            _scopeDropdown.SetValueWithoutNotify(0);
        if (PinnedTerms.Count > 0)
            RequestRefresh();
    }

    internal void OnViewDisabled()
    {
        SuspendForInactiveView();
    }

    internal void PrepareRefresh(out CharacterSearchRefreshState state)
    {
        state = null;
        if (!CanUseActiveView())
            return;

        EnsureUi();
        CaptureAnchor();
        var terms = GetCurrentTerms();
        if (_scope == 0 && terms.Count == 0)
        {
            if (_pendingKey.Length > 0 || _cachedKey.Length > 0)
            {
                _generation++;
                CancelDebounce();
                _pendingKey = string.Empty;
                _cachedKey = string.Empty;
                _cachedData = null;
            }
            return;
        }

        var key = BuildQueryKey(terms);
        ScheduleQuery(key, terms);
        var originalData = DataListField.GetValue(_view) as List<VillagerCharDisplayData>;
        var originalText = _input == null ? string.Empty : _input.text;
        var replacement = _cachedData != null && _cachedKey == key
            ? _cachedData
            : (_lastValidData ?? originalData ?? new List<VillagerCharDisplayData>());
        state = new CharacterSearchRefreshState(_view, _input, originalData, originalText);
        DataListField.SetValue(_view, replacement);
        SetInputTextBacking(_input, string.Empty);
    }

    private void ApplySetting()
    {
        if (!Plugin.EnableSearchOptimization)
        {
            DisableAndRemoveUi();
            RequestRefresh();
            return;
        }

        if (_view != null && _view.gameObject.activeInHierarchy && IdsField.GetValue(_view) != null)
        {
            _active = true;
            EnsureUi();
            RequestRefresh();
        }
    }

    private void EnsureUi()
    {
        if (!_active || !Plugin.EnableSearchOptimization || _view == null
            || !isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        _input = SearchingFieldField.GetValue(_view) as TMP_InputField;
        EnsureIndependentSearchRow();
        SuppressReferenceScopeDropdowns();
        var inputRect = _input == null ? null : _input.transform as RectTransform;
        if (inputRect == null)
            return;

        if (_uiRoot != null)
        {
            _uiRoot.gameObject.SetActive(true);
            ScheduleLayoutRefresh();
            RebuildTags();
            return;
        }

        var existing = inputRect.Find(UiRootName) as RectTransform;
        if (existing != null)
            Destroy(existing.gameObject);

        var rootObject = new GameObject(UiRootName, typeof(RectTransform), typeof(LayoutElement));
        _uiRoot = rootObject.GetComponent<RectTransform>();
        _uiRoot.SetParent(inputRect, false);
        _uiRoot.anchorMin = new Vector2(1f, 0.5f);
        _uiRoot.anchorMax = new Vector2(1f, 0.5f);
        _uiRoot.pivot = new Vector2(0f, 0.5f);
        _uiRoot.anchoredPosition = new Vector2(Gap, 0f);
        _uiRoot.sizeDelta = new Vector2(ScopeWidth + AddButtonWidth + TagViewportWidth + Gap * 2f, ControlHeight);
        rootObject.GetComponent<LayoutElement>().ignoreLayout = true;

        _scopeDropdown = CreateDropdown(_uiRoot, _input, ScopeLabels);
        var scopeRect = _scopeDropdown.transform as RectTransform;
        PlaceRect(scopeRect, 0f, ScopeWidth);
        _scopeDropdown.SetValueWithoutNotify(_scope);
        _scopeDropdown.onSelect.AddListener(OnScopeChanged);
        _scopeDropdown.onValueChanged.AddListener(OnScopeChanged);

        _addButton = CreateButton(_uiRoot, _input, "+", AddButtonWidth);
        var addRect = _addButton.transform as RectTransform;
        PlaceRect(addRect, ScopeWidth + Gap, AddButtonWidth);
        _addButton.onClick.AddListener(PinCurrentTerm);

        CreateTagViewport(ScopeWidth + Gap + AddButtonWidth + Gap);
        ScheduleLayoutRefresh();
        RebuildTags();
    }

    private void CreateTagViewport(float x)
    {
        var scrollObject = new GameObject("PinnedTags", typeof(RectTransform), typeof(ScrollRect));
        var scrollRect = scrollObject.GetComponent<RectTransform>();
        _tagViewport = scrollRect;
        scrollRect.SetParent(_uiRoot, false);
        PlaceRect(scrollRect, x, TagViewportWidth);

        var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        var viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(scrollRect, false);
        Stretch(viewport, Vector2.zero, Vector2.zero);

        var contentObject = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(ContentSizeFitter));
        _tagContent = contentObject.GetComponent<RectTransform>();
        _tagContent.SetParent(viewport, false);
        _tagContent.anchorMin = new Vector2(0f, 0f);
        _tagContent.anchorMax = new Vector2(0f, 1f);
        _tagContent.pivot = new Vector2(0f, 0.5f);
        _tagContent.anchoredPosition = Vector2.zero;
        _tagContent.sizeDelta = new Vector2(0f, 0f);

        var layout = contentObject.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        var fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        var scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = _tagContent;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
    }

    private void ScheduleLayoutRefresh()
    {
        if (_layoutRoutine != null || !CanUseActiveView())
            return;
        _layoutRoutine = StartCoroutine(RefreshLayoutNextFrame());
    }

    private IEnumerator RefreshLayoutNextFrame()
    {
        yield return null;
        _layoutRoutine = null;
        if (_uiRoot == null || !CanUseActiveView())
            yield break;
        EnsureIndependentSearchRow();
        AlignIndependentSearchRow();
        SuppressReferenceScopeDropdowns();
        UpdateResponsiveWidth();
    }

    internal void OnLayoutDimensionsChanged()
    {
        if (_active)
            ScheduleLayoutRefresh();
    }

    private void EnsureIndependentSearchRow()
    {
        if (_input == null || _view == null || !Plugin.EnableSearchOptimization
            || !_view.gameObject.activeInHierarchy)
            return;

        _sortAndFilter ??= _view.GetComponentInChildren<SortAndFilter>(true);
        var entryToggle = _sortAndFilter == null || EntryToggleField == null
            ? null
            : EntryToggleField.GetValue(_sortAndFilter) as Component;
        var entryRect = entryToggle == null ? null : entryToggle.transform as RectTransform;
        _searchRowParent ??= entryRect == null ? null : entryRect.parent as RectTransform;
        var targetParent = _searchRowParent;
        var inputRect = _input.transform as RectTransform;
        if (targetParent == null || inputRect == null)
            return;

        if (_inputOriginalParent == null)
        {
            _inputOriginalParent = inputRect.parent as RectTransform;
            _inputOriginalSiblingIndex = inputRect.GetSiblingIndex();
            _inputOriginalRect = RectSnapshot.Capture(inputRect);
            _textViewportOriginalRect = RectSnapshot.Capture(_input.textViewport);
            _inputLayoutElement = inputRect.GetComponent<LayoutElement>();
            _inputHadLayoutElement = _inputLayoutElement != null;
            _inputOriginalIgnoreLayout = _inputLayoutElement != null && _inputLayoutElement.ignoreLayout;
        }

        if (!ReferenceEquals(inputRect.parent, targetParent))
            inputRect.SetParent(targetParent, true);

        _inputLayoutElement ??= inputRect.gameObject.AddComponent<LayoutElement>();
        _inputLayoutElement.ignoreLayout = true;
        _layoutMarker = targetParent.GetComponent<CharacterSearchLayoutMarker>()
                        ?? targetParent.gameObject.AddComponent<CharacterSearchLayoutMarker>();
        _layoutMarker.Owner = this;
        _layoutMarker.Active = true;
        _layoutMarker.SearchHeight = Mathf.Max(ControlHeight, ResolveInputHeight(inputRect));
        _input.gameObject.SetActive(true);
        AlignIndependentSearchRow();
        InlineFilterButtonsController.Get(_sortAndFilter)?.RefreshVillagerSearchLayout();
    }

    private void AlignIndependentSearchRow()
    {
        if (_input == null || _layoutMarker == null || !_layoutMarker.Active)
            return;
        var inputRect = _input.transform as RectTransform;
        if (inputRect == null)
            return;

        var width = ResolveInputWidth(inputRect);
        var height = Mathf.Max(ControlHeight, _layoutMarker.SearchHeight);
        inputRect.anchorMin = new Vector2(0f, 1f);
        inputRect.anchorMax = new Vector2(0f, 1f);
        inputRect.pivot = new Vector2(0f, 1f);
        inputRect.anchoredPosition = Vector2.zero;
        inputRect.sizeDelta = new Vector2(width, height);
        inputRect.localScale = Vector3.one;
        inputRect.localRotation = Quaternion.identity;
        AlignInputTextViewport();
    }

    private void AlignInputTextViewport()
    {
        var viewport = _input == null ? null : _input.textViewport;
        if (viewport == null)
            return;

        var bottom = viewport.offsetMin.y;
        var top = viewport.offsetMax.y;
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(56f, bottom);
        viewport.offsetMax = new Vector2(-42f, top);
    }

    internal static bool SetInputTextBacking(TMP_InputField input, string value)
    {
        if (input == null)
            return false;

        if (InputTextBackingField != null)
        {
            InputTextBackingField.SetValue(input, value ?? string.Empty);
            return true;
        }

        input.SetTextWithoutNotify(value ?? string.Empty);
        return false;
    }

    private float ResolveInputWidth(RectTransform inputRect)
    {
        var original = _inputOriginalRect?.SizeDelta.x ?? 0f;
        var current = inputRect.rect.width > 1f ? inputRect.rect.width : inputRect.sizeDelta.x;
        return Mathf.Max(SearchInputMinWidth, original, current);
    }

    private float ResolveInputHeight(RectTransform inputRect)
    {
        var original = _inputOriginalRect?.SizeDelta.y ?? 0f;
        if (original > 20f)
            return original;
        if (inputRect.rect.height > 20f)
            return inputRect.rect.height;
        return ControlHeight;
    }

    private void UpdateResponsiveWidth(bool force = false)
    {
        if (_uiRoot == null || _tagViewport == null || _input == null)
            return;

        var inputRect = _input.transform as RectTransform;
        _canvas ??= _input.GetComponentInParent<Canvas>();
        var canvas = _canvas;
        if (inputRect == null || canvas == null)
            return;

        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var inputRight = inputRect.TransformPoint(new Vector3(inputRect.rect.xMax, 0f, 0f));
        var screenRight = RectTransformUtility.WorldToScreenPoint(camera, inputRight).x;
        var scaleFactor = Mathf.Max(0.01f, canvas.scaleFactor);
        var parentSize = _searchRowParent == null ? Vector2.zero : _searchRowParent.rect.size;
        if (!force
            && _lastScreenWidth == Screen.width
            && Mathf.Approximately(_lastCanvasScale, scaleFactor)
            && Mathf.Abs(_lastInputRight - screenRight) < 0.5f
            && Vector2.SqrMagnitude(_lastParentSize - parentSize) < 0.25f)
            return;

        _lastScreenWidth = Screen.width;
        _lastCanvasScale = scaleFactor;
        _lastInputRight = screenRight;
        _lastParentSize = parentSize;
        var fixedWidth = ScopeWidth + AddButtonWidth + Gap * 2f;
        var availableWidth = (Screen.width - screenRight - 24f) / scaleFactor;
        var tagWidth = Mathf.Clamp(availableWidth - fixedWidth, 140f, TagViewportWidth);
        var tagSize = new Vector2(tagWidth, ControlHeight);
        var rootSize = new Vector2(fixedWidth + tagWidth, ControlHeight);
        if (Vector2.SqrMagnitude(_tagViewport.sizeDelta - tagSize) > 0.01f)
            _tagViewport.sizeDelta = tagSize;
        if (Vector2.SqrMagnitude(_uiRoot.sizeDelta - rootSize) > 0.01f)
            _uiRoot.sizeDelta = rootSize;
    }

    private void RebuildTags()
    {
        if (_tagContent == null)
            return;
        for (var i = _tagContent.childCount - 1; i >= 0; i--)
            Destroy(_tagContent.GetChild(i).gameObject);

        for (var i = 0; i < PinnedTerms.Count; i++)
        {
            var term = PinnedTerms[i];
            var width = Mathf.Clamp(34f + term.Length * 20f, 82f, 220f);
            var button = CreateButton(_tagContent, _input, term + " ×", width);
            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.minHeight = ControlHeight;
            layout.preferredHeight = ControlHeight;
            button.onClick.AddListener(() => RemovePinnedTerm(term));
        }
    }

    private void PinCurrentTerm()
    {
        if (_input == null)
            return;
        var term = (_input.text ?? string.Empty).Trim();
        if (term.Length == 0)
            return;
        if (ContainsPinnedTerm(term))
        {
            _input.SetTextWithoutNotify(string.Empty);
            InvalidateAndRefresh();
            return;
        }

        PinnedTerms.Add(term);
        _input.SetTextWithoutNotify(string.Empty);
        RebuildTags();
        InvalidateAndRefresh();
    }

    private void RemovePinnedTerm(string term)
    {
        for (var i = PinnedTerms.Count - 1; i >= 0; i--)
        {
            if (string.Equals(PinnedTerms[i], term, StringComparison.OrdinalIgnoreCase))
                PinnedTerms.RemoveAt(i);
        }
        RebuildTags();
        InvalidateAndRefresh();
    }

    private static bool ContainsPinnedTerm(string term)
    {
        for (var i = 0; i < PinnedTerms.Count; i++)
        {
            if (string.Equals(PinnedTerms[i], term, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private void OnScopeChanged(int value)
    {
        value = Mathf.Clamp(value, 0, 2);
        if (_scope == value)
            return;
        _scope = value;
        InvalidateAndRefresh();
    }

    private void InvalidateAndRefresh()
    {
        _generation++;
        CancelDebounce();
        _pendingKey = string.Empty;
        _cachedKey = string.Empty;
        _cachedData = null;
        RequestRefresh();
    }

    private void RequestRefresh()
    {
        if (!CanUseActiveView() || RefreshListDataMethod == null)
            return;
        try
        {
            RefreshListDataMethod.Invoke(_view, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to refresh character search: " + ex);
        }
    }

    private void CaptureAnchor()
    {
        var data = DataListField.GetValue(_view) as List<VillagerCharDisplayData>;
        if (data != null && data.Count > 0 && data[0] != null && data[0].Location.IsValid())
        {
            _anchor = data[0].Location;
            return;
        }
        if (_anchor.IsValid())
            return;
        try
        {
            var model = SingletonObject.getInstance<WorldMapModel>();
            if (model != null)
                _anchor = model.CurrentLocation;
        }
        catch
        {
            _anchor = Location.Invalid;
        }
    }

    private List<string> GetCurrentTerms()
    {
        var terms = new List<string>(PinnedTerms);
        var current = (_input == null ? string.Empty : _input.text ?? string.Empty).Trim();
        if (current.Length > 0 && !ContainsTerm(terms, current))
            terms.Add(current);
        return terms;
    }

    private static bool ContainsTerm(List<string> terms, string term)
    {
        for (var i = 0; i < terms.Count; i++)
        {
            if (string.Equals(terms[i], term, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private string BuildQueryKey(List<string> terms)
    {
        var key = _scope + "|" + _anchor.AreaId + "|" + _anchor.BlockId;
        for (var i = 0; i < terms.Count; i++)
            key += "|" + terms[i].Length + ":" + terms[i];
        return key;
    }

    private void ScheduleQuery(string key, List<string> terms)
    {
        if (!CanUseActiveView())
            return;
        if (_cachedData != null && _cachedKey == key)
            return;
        if (_pendingKey == key)
            return;

        CancelDebounce();
        _pendingKey = key;
        var generation = ++_generation;
        _debounceRoutine = StartCoroutine(DebounceAndQuery(generation, key, new List<string>(terms)));
    }

    private IEnumerator DebounceAndQuery(int generation, string key, List<string> terms)
    {
        yield return new WaitForSecondsRealtime(DebounceSeconds);
        _debounceRoutine = null;
        if (!CanUseActiveView() || generation != _generation || key != BuildQueryKey(GetCurrentTerms()))
            yield break;
        SendQuery(generation, key, terms);
    }

    private void SendQuery(int generation, string key, List<string> terms)
    {
        if (!CanUseActiveView() || generation != _generation || string.IsNullOrEmpty(Plugin.ModId))
            return;
        var requestId = ++_requestId;
        _querySession = ModSession.Generation;
        ArmTimeout(generation, key, requestId);
        var parameter = new SerializableModData();
        parameter.Set(BetterTaiwuScroll.Shared.ModProtocol.VersionKey, BetterTaiwuScroll.Shared.ModProtocol.Version);
        parameter.Set(BetterTaiwuScroll.Shared.ModProtocol.RequestIdKey, requestId);
        parameter.Set("scope", _scope);
        parameter.Set("areaId", _anchor.AreaId);
        parameter.Set("blockId", _anchor.BlockId);
        parameter.Set("termCount", terms.Count);
        for (var i = 0; i < terms.Count; i++)
            parameter.Set("term" + i, terms[i]);

        try
        {
            ModDomainMethod.AsyncCall.CallModMethodWithParamAndRet(
                _view,
                Plugin.ModId,
                MethodName,
                parameter,
                (offset, pool) => HandleSearchResponse(generation, key, requestId, offset, pool));
        }
        catch (Exception ex)
        {
            CompleteWithFailure(generation, key, "request_exception", ex.ToString());
        }
    }

    private void HandleSearchResponse(
        int generation,
        string key,
        int requestId,
        int offset,
        RawDataPool pool)
    {
        if (!CanUseActiveView() || generation != _generation || !ModSession.IsCurrent(_querySession))
            return;

        try
        {
            SerializableModData response = null;
            Serializer.Deserialize(pool, offset, ref response);
            if (response == null)
            {
                CompleteWithFailure(generation, key, "empty_response", "人物搜索返回空响应。");
                return;
            }
            response.Get("requestId", out int returnedRequestId);
            response.Get("success", out bool success);
            response.Get("code", out string code);
            response.Get("message", out string message);
            if (returnedRequestId != requestId || generation != _generation || !CanUseActiveView())
                return;
            if (!success || !response.Get("result", out CharacterSearchResultData result))
            {
                CompleteWithFailure(generation, key, code, message);
                return;
            }

            var ids = result.CharacterIds ?? new List<int>();
            if (ids.Count == 0)
            {
                CompleteQuery(generation, key, new List<VillagerCharDisplayData>());
                return;
            }

            if (!CanUseActiveView())
                return;

            ArmTimeout(generation, key, requestId);
            CharacterDomainMethod.AsyncCall.GetCharDisplayDataListAsVillager(
                _view,
                ids,
                (displayOffset, displayPool) =>
                {
                    try
                    {
                        var displayData = new List<VillagerCharDisplayData>();
                        Serializer.Deserialize(displayPool, displayOffset, ref displayData);
                        CompleteQuery(generation, key, displayData ?? new List<VillagerCharDisplayData>());
                    }
                    catch (Exception ex)
                    {
                        CompleteWithFailure(generation, key, "display_data_exception", ex.ToString());
                    }
                });
        }
        catch (Exception ex)
        {
            CompleteWithFailure(generation, key, "decode_exception", ex.ToString());
        }
    }

    private void CompleteQuery(int generation, string key, List<VillagerCharDisplayData> data)
    {
        if (!CanUseActiveView() || generation != _generation || key != BuildQueryKey(GetCurrentTerms())
            || !ModSession.IsCurrent(_querySession))
            return;
        _pendingKey = string.Empty;
        _cachedKey = key;
        _cachedData = data ?? new List<VillagerCharDisplayData>();
        _lastValidData = _cachedData;
        _timeoutVersion++;
        RequestRefresh();
    }

    private void CompleteWithFailure(int generation, string key, string code, string message)
    {
        if (generation != _generation || !CanUseActiveView())
            return;
        Debug.LogWarning("[BetterTaiwuScroll] Character search failed: code="
                         + (code ?? string.Empty) + ", message=" + (message ?? string.Empty));
        _generation++;
        _pendingKey = string.Empty;
    }

    private void ArmTimeout(int generation, string key, int requestId)
    {
        var session = ModSession.Generation;
        var timeout = ++_timeoutVersion;
        ModSession.Delay(30f, () =>
        {
            if (ModSession.IsCurrent(session) && generation == _generation && timeout == _timeoutVersion
                && requestId == _requestId && _pendingKey == key)
                CompleteWithFailure(generation, key, "timeout", "Character search request timed out.");
        });
    }

    private void CancelDebounce()
    {
        if (_debounceRoutine == null)
            return;
        StopCoroutine(_debounceRoutine);
        _debounceRoutine = null;
    }

    private void CancelUiCoroutines()
    {
        CancelDebounce();
        if (_layoutRoutine == null)
            return;
        StopCoroutine(_layoutRoutine);
        _layoutRoutine = null;
    }

    private bool CanUseActiveView()
    {
        return _active
            && Plugin.EnableSearchOptimization
            && _view != null
            && isActiveAndEnabled
            && gameObject.activeInHierarchy
            && _view.gameObject.activeInHierarchy;
    }

    private void SuspendForInactiveView()
    {
        var shouldInvalidate = _active
            || _debounceRoutine != null
            || _layoutRoutine != null
            || _pendingKey.Length > 0
            || _cachedKey.Length > 0;
        _active = false;
        if (shouldInvalidate)
            _generation++;
        CancelUiCoroutines();
        _pendingKey = string.Empty;
        _cachedKey = string.Empty;
        _cachedData = null;
        if (_uiRoot != null)
            _uiRoot.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        SuspendForInactiveView();
    }

    private void OnDestroy()
    {
        SuspendForInactiveView();
        RestoreReferenceScopeDropdowns();
        Instances.Remove(this);
    }

    private void DisableAndRemoveUi()
    {
        _active = false;
        _generation++;
        CancelUiCoroutines();
        _pendingKey = string.Empty;
        _cachedKey = string.Empty;
        _cachedData = null;
        if (_uiRoot != null)
            Destroy(_uiRoot.gameObject);
        RestoreReferenceScopeDropdowns();
        RestoreOriginalSearchRow();
        _uiRoot = null;
        _scopeDropdown = null;
        _addButton = null;
        _tagViewport = null;
        _tagContent = null;
        _sortAndFilter = null;
        _searchRowParent = null;
        _canvas = null;
        _lastScreenWidth = -1;
        _lastCanvasScale = -1f;
        _lastInputRight = float.NaN;
        _lastParentSize = new Vector2(float.NaN, float.NaN);
    }

    private void RestoreOriginalSearchRow()
    {
        if (_input == null || _inputOriginalParent == null)
            return;

        var inputRect = _input.transform as RectTransform;
        var sortAndFilter = _view == null ? null : _view.GetComponentInChildren<SortAndFilter>(true);
        if (_layoutMarker != null)
        {
            _layoutMarker.Active = false;
            _layoutMarker.Owner = null;
            Destroy(_layoutMarker);
            _layoutMarker = null;
        }

        if (inputRect != null)
        {
            inputRect.SetParent(_inputOriginalParent, false);
            inputRect.SetSiblingIndex(Mathf.Clamp(_inputOriginalSiblingIndex, 0, _inputOriginalParent.childCount - 1));
            _inputOriginalRect?.Restore(inputRect);
            _textViewportOriginalRect?.Restore(_input.textViewport);
        }

        if (_inputLayoutElement != null)
        {
            if (_inputHadLayoutElement)
                _inputLayoutElement.ignoreLayout = _inputOriginalIgnoreLayout;
            else
                Destroy(_inputLayoutElement);
        }

        _inputOriginalParent = null;
        _inputOriginalRect = null;
        _textViewportOriginalRect = null;
        _inputLayoutElement = null;
        _inputHadLayoutElement = false;
        InlineFilterButtonsController.Get(sortAndFilter)?.RefreshVillagerSearchLayout();
    }

    private void SuppressReferenceScopeDropdowns()
    {
        if (_view == null)
            return;

        foreach (var dropdown in _view.GetComponentsInChildren<CDropdown>(true))
        {
            if (dropdown == null || dropdown == _scopeDropdown
                || !string.Equals(dropdown.gameObject.name, ReferenceScopeDropdownName, StringComparison.Ordinal))
                continue;

            var state = dropdown.GetComponent<ReferenceCharacterSearchDropdownState>()
                        ?? dropdown.gameObject.AddComponent<ReferenceCharacterSearchDropdownState>();
            state.Suppress(dropdown);
            if (!_suppressedReferenceDropdowns.Contains(state))
                _suppressedReferenceDropdowns.Add(state);
        }
    }

    private void RestoreReferenceScopeDropdowns()
    {
        for (var i = _suppressedReferenceDropdowns.Count - 1; i >= 0; i--)
        {
            var state = _suppressedReferenceDropdowns[i];
            if (state != null)
                state.Restore();
        }
        _suppressedReferenceDropdowns.Clear();
    }

    private static CDropdown CreateDropdown(RectTransform parent, TMP_InputField input, List<string> options)
    {
        var root = new GameObject("Scope", typeof(RectTransform), typeof(CImage), typeof(CDropdown));
        root.layer = 5;
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        var image = root.GetComponent<CImage>();
        CharacterSearchUiAssets.ApplyPanel(image);
        var dropdown = root.GetComponent<CDropdown>();
        dropdown.targetGraphic = image;

        var caption = CreateText(rect, input, "Label", string.Empty, TextAlignmentOptions.MidlineLeft);
        caption.rectTransform.offsetMin = new Vector2(10f, 0f);
        caption.rectTransform.offsetMax = new Vector2(-30f, 0f);
        dropdown.captionText = caption;
        CreateDropdownArrow(rect);

        var template = CreateDropdownTemplate(rect, input, out var itemText);
        dropdown.template = template;
        dropdown.itemText = itemText;
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(0);
        template.gameObject.SetActive(false);
        return dropdown;
    }

    private static void CreateDropdownArrow(RectTransform parent)
    {
        var arrowObject = new GameObject("Arrow", typeof(RectTransform), typeof(CImage));
        arrowObject.layer = 5;
        var rect = arrowObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-10f, 0f);
        rect.sizeDelta = new Vector2(16f, 10f);
        var image = arrowObject.GetComponent<CImage>();
        image.sprite = CharacterSearchUiAssets.DropdownArrowSprite;
        image.type = Image.Type.Simple;
        image.color = CharacterSearchUiAssets.TextColor;
        image.raycastTarget = false;
    }

    private static RectTransform CreateDropdownTemplate(
        RectTransform parent,
        TMP_InputField input,
        out TMP_Text itemText)
    {
        var templateObject = new GameObject("Template", typeof(RectTransform));
        templateObject.layer = 5;
        templateObject.SetActive(false);
        var template = templateObject.GetComponent<RectTransform>();
        template.SetParent(parent, false);
        template.anchorMin = new Vector2(0f, 0f);
        template.anchorMax = new Vector2(1f, 0f);
        template.pivot = new Vector2(0.5f, 1f);
        template.anchoredPosition = Vector2.zero;
        template.sizeDelta = new Vector2(0f, ControlHeight * 3f);

        var scrollViewObject = new GameObject("VerticalScrollView", typeof(RectTransform), typeof(CImage));
        scrollViewObject.SetActive(false);
        scrollViewObject.layer = 5;
        var scrollView = scrollViewObject.GetComponent<RectTransform>();
        scrollView.SetParent(template, false);
        Stretch(scrollView, Vector2.zero, Vector2.zero);
        CharacterSearchUiAssets.ApplyList(scrollViewObject.GetComponent<CImage>());

        var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewportObject.layer = 5;
        var viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(scrollView, false);
        Stretch(viewport, Vector2.zero, new Vector2(-8f, 0f));

        var contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.layer = 5;
        var content = contentObject.GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, ControlHeight);

        var itemObject = new GameObject("Item", typeof(RectTransform), typeof(CImage), typeof(Toggle), typeof(LayoutElement));
        itemObject.layer = 5;
        var itemRect = itemObject.GetComponent<RectTransform>();
        itemRect.SetParent(content, false);
        itemRect.anchorMin = new Vector2(0f, 1f);
        itemRect.anchorMax = new Vector2(1f, 1f);
        itemRect.pivot = new Vector2(0.5f, 1f);
        itemRect.anchoredPosition = Vector2.zero;
        itemRect.sizeDelta = new Vector2(0f, ControlHeight);
        var itemImage = itemObject.GetComponent<CImage>();
        CharacterSearchUiAssets.ApplyItem(itemImage);
        itemObject.GetComponent<Toggle>().targetGraphic = itemImage;
        var itemLayout = itemObject.GetComponent<LayoutElement>();
        itemLayout.minHeight = ControlHeight;
        itemLayout.preferredHeight = ControlHeight;
        itemText = CreateText(itemRect, input, "Item Label", string.Empty, TextAlignmentOptions.MidlineLeft);
        itemText.rectTransform.offsetMin = new Vector2(8f, 0f);
        itemText.rectTransform.offsetMax = new Vector2(-8f, 0f);

        var lineObject = new GameObject("line", typeof(RectTransform), typeof(CImage));
        lineObject.layer = 5;
        var lineRect = lineObject.GetComponent<RectTransform>();
        lineRect.SetParent(itemRect, false);
        lineRect.anchorMin = new Vector2(0f, 0f);
        lineRect.anchorMax = new Vector2(1f, 0f);
        lineRect.pivot = new Vector2(0.5f, 0f);
        lineRect.anchoredPosition = Vector2.zero;
        lineRect.sizeDelta = new Vector2(0f, 2f);
        CharacterSearchUiAssets.ApplyLine(lineObject.GetComponent<CImage>());

        var scroll = scrollViewObject.AddComponent<CScrollRect>();
        scroll.Direction = CScrollRect.ScrollDirection.Vertical;
        scroll.Viewport = viewport;
        scroll.Content = content;
        scroll.ScrollBar = CreateDropdownScrollbar(scrollView);
        scrollViewObject.SetActive(true);
        return template;
    }

    private static Scrollbar CreateDropdownScrollbar(RectTransform parent)
    {
        var root = new GameObject("VerticalScrollbar", typeof(RectTransform), typeof(CImage), typeof(Scrollbar));
        root.layer = 5;
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(8f, 0f);
        CharacterSearchUiAssets.ApplyScrollbar(root.GetComponent<CImage>(), false);

        var slidingObject = new GameObject("Sliding Area", typeof(RectTransform));
        slidingObject.layer = 5;
        var slidingRect = slidingObject.GetComponent<RectTransform>();
        slidingRect.SetParent(rect, false);
        Stretch(slidingRect, Vector2.one, -Vector2.one);

        var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(CImage));
        handleObject.layer = 5;
        var handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.SetParent(slidingRect, false);
        Stretch(handleRect, Vector2.zero, Vector2.zero);
        var handleImage = handleObject.GetComponent<CImage>();
        CharacterSearchUiAssets.ApplyScrollbar(handleImage, true);

        var scrollbar = root.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handleRect;
        scrollbar.size = 1f;
        scrollbar.value = 0f;
        return scrollbar;
    }

    private static CButton CreateButton(RectTransform parent, TMP_InputField input, string text, float width)
    {
        var root = new GameObject("Button", typeof(RectTransform), typeof(CImage), typeof(CButton));
        root.layer = 5;
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(width, ControlHeight);
        var image = root.GetComponent<CImage>();
        CharacterSearchUiAssets.ApplyPanel(image);
        var button = root.GetComponent<CButton>();
        button.targetGraphic = image;
        CreateText(rect, input, "Label", text, TextAlignmentOptions.Center);
        return button;
    }

    private static TextMeshProUGUI CreateText(
        RectTransform parent,
        TMP_InputField input,
        string name,
        string value,
        TextAlignmentOptions alignment)
    {
        var textObject = new GameObject(name, typeof(RectTransform));
        textObject.SetActive(false);
        textObject.layer = 5;
        var rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Stretch(rect, Vector2.zero, Vector2.zero);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        var source = input == null ? null : input.textComponent;
        if (source != null)
        {
            text.font = source.font;
            text.fontSharedMaterial = source.fontSharedMaterial;
            text.color = CharacterSearchUiAssets.TextColor;
        }
        else
            text.color = CharacterSearchUiAssets.TextColor;
        text.SetText(value ?? string.Empty);
        text.alignment = alignment;
        text.fontSize = source == null ? 18f : Mathf.Clamp(source.fontSize, 16f, 22f);
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 20f;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        textObject.SetActive(true);
        return text;
    }

    private static void PlaceRect(RectTransform rect, float x, float width)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(width, ControlHeight);
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private sealed class RectSnapshot
    {
        internal Vector2 AnchorMin;
        internal Vector2 AnchorMax;
        internal Vector2 Pivot;
        internal Vector2 AnchoredPosition;
        internal Vector2 SizeDelta;
        internal Vector3 LocalScale;
        internal Quaternion LocalRotation;

        internal static RectSnapshot Capture(RectTransform rect)
        {
            return rect == null ? null : new RectSnapshot
            {
                AnchorMin = rect.anchorMin,
                AnchorMax = rect.anchorMax,
                Pivot = rect.pivot,
                AnchoredPosition = rect.anchoredPosition,
                SizeDelta = rect.sizeDelta,
                LocalScale = rect.localScale,
                LocalRotation = rect.localRotation,
            };
        }

        internal void Restore(RectTransform rect)
        {
            if (rect == null)
                return;
            rect.anchorMin = AnchorMin;
            rect.anchorMax = AnchorMax;
            rect.pivot = Pivot;
            rect.anchoredPosition = AnchoredPosition;
            rect.sizeDelta = SizeDelta;
            rect.localScale = LocalScale;
            rect.localRotation = LocalRotation;
        }
    }
}

internal sealed class CharacterSearchLayoutMarker : MonoBehaviour
{
    internal const float RowGap = 4f;
    internal bool Active;
    internal float SearchHeight = 42f;
    internal CharacterSearchOptimizationController Owner;

    internal float GetCombinedHeight(float inlineHeight)
    {
        return Active ? SearchHeight + RowGap + Mathf.Max(0f, inlineHeight) : inlineHeight;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (Active)
            Owner?.OnLayoutDimensionsChanged();
    }
}

internal sealed class ReferenceCharacterSearchDropdownState : MonoBehaviour
{
    private CDropdown _dropdown;
    private CanvasGroup _canvasGroup;
    private bool _captured;
    private bool _hadCanvasGroup;
    private bool _originalInteractable;
    private float _originalAlpha;
    private bool _originalBlocksRaycasts;
    private bool _originalCanvasInteractable;
    private bool _originalIgnoreParentGroups;

    internal void Suppress(CDropdown dropdown)
    {
        if (!_captured)
        {
            _dropdown = dropdown;
            _originalInteractable = dropdown != null && dropdown.interactable;
            _canvasGroup = GetComponent<CanvasGroup>();
            _hadCanvasGroup = _canvasGroup != null;
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _originalAlpha = _canvasGroup.alpha;
            _originalBlocksRaycasts = _canvasGroup.blocksRaycasts;
            _originalCanvasInteractable = _canvasGroup.interactable;
            _originalIgnoreParentGroups = _canvasGroup.ignoreParentGroups;
            _captured = true;
        }

        if (_dropdown != null)
            _dropdown.interactable = false;
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }
    }

    internal void Restore()
    {
        if (!_captured)
            return;
        if (_dropdown != null)
            _dropdown.interactable = _originalInteractable;
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = _originalAlpha;
            _canvasGroup.blocksRaycasts = _originalBlocksRaycasts;
            _canvasGroup.interactable = _originalCanvasInteractable;
            _canvasGroup.ignoreParentGroups = _originalIgnoreParentGroups;
            if (!_hadCanvasGroup)
                Destroy(_canvasGroup);
        }
        _captured = false;
        Destroy(this);
    }
}

internal static class CharacterSearchUiAssets
{
    private const string PanelSpritePath = "RemakeResources/UIGraphics5.0/Ui9Common/ui9_back_menu_bg_0";
    private const string ListSpritePath = "RemakeResources/UIGraphics5.0/Ui9Common/ui9_sp_btn_screening";
    private const string LineSpritePath = "RemakeResources/UIGraphics5.0/Ui9Common/ui9_line_horizontal_1";
    private const string ScrollbarSpritePath = "RemakeResources/UIGraphics5.0/Ui9Common/ui9_btn_scroll_base_0";

    internal static readonly Color TextColor = new(0.93f, 0.87f, 0.72f, 1f);
    private static readonly Color PanelFallback = new(0.115f, 0.098f, 0.078f, 0.98f);
    private static readonly Color ScrollbarFallback = new(0.52f, 0.35f, 0.11f, 1f);
    private static Sprite _panelSprite;
    private static Sprite _listSprite;
    private static Sprite _lineSprite;
    private static Sprite _scrollbarSprite;
    private static Sprite _dropdownArrowSprite;
    private static Texture2D _dropdownArrowTexture;

    internal static Sprite DropdownArrowSprite
    {
        get
        {
            if (_dropdownArrowSprite == null)
                CreateDropdownArrow();
            return _dropdownArrowSprite;
        }
    }

    internal static void Prewarm()
    {
        _panelSprite ??= LoadSprite(PanelSpritePath);
        _listSprite ??= LoadSprite(ListSpritePath);
        _lineSprite ??= LoadSprite(LineSpritePath);
        _scrollbarSprite ??= LoadSprite(ScrollbarSpritePath);
        if (_dropdownArrowSprite == null)
            CreateDropdownArrow();
    }

    internal static void ApplyPanel(CImage image)
    {
        _panelSprite ??= LoadSprite(PanelSpritePath);
        Apply(image, _panelSprite, PanelFallback, true);
    }

    internal static void ApplyList(CImage image)
    {
        _listSprite ??= LoadSprite(ListSpritePath);
        Apply(image, _listSprite, PanelFallback, true);
    }

    internal static void ApplyItem(CImage image)
    {
        _panelSprite ??= LoadSprite(PanelSpritePath);
        Apply(image, _panelSprite, PanelFallback, true);
    }

    internal static void ApplyLine(CImage image)
    {
        _lineSprite ??= LoadSprite(LineSpritePath);
        Apply(image, _lineSprite, new Color(TextColor.r, TextColor.g, TextColor.b, 0.22f), false);
    }

    internal static void ApplyScrollbar(CImage image, bool handle)
    {
        _scrollbarSprite ??= LoadSprite(ScrollbarSpritePath);
        Apply(image, _scrollbarSprite, handle ? ScrollbarFallback : PanelFallback, true);
    }

    internal static void Dispose()
    {
        if (_dropdownArrowSprite != null)
            UnityEngine.Object.Destroy(_dropdownArrowSprite);
        if (_dropdownArrowTexture != null)
            UnityEngine.Object.Destroy(_dropdownArrowTexture);
        _dropdownArrowSprite = null;
        _dropdownArrowTexture = null;
        _panelSprite = null;
        _listSprite = null;
        _lineSprite = null;
        _scrollbarSprite = null;
    }

    private static void Apply(CImage image, Sprite sprite, Color fallback, bool raycastTarget)
    {
        if (image == null)
            return;
        image.sprite = sprite;
        image.type = sprite == null ? Image.Type.Simple : Image.Type.Sliced;
        image.color = sprite == null ? fallback : Color.white;
        image.raycastTarget = raycastTarget;
    }

    private static Sprite LoadSprite(string path)
    {
        try
        {
            return ResLoader.SyncLoad<Sprite>(path);
        }
        catch
        {
            return null;
        }
    }

    private static void CreateDropdownArrow()
    {
        _dropdownArrowTexture = new Texture2D(16, 10, TextureFormat.RGBA32, false);
        var pixels = new Color32[160];
        var clear = new Color32(255, 255, 255, 0);
        var solid = new Color32(255, 255, 255, 255);
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = clear;
        for (var y = 0; y < 10; y++)
        {
            var halfWidth = Mathf.RoundToInt((y + 1) * 0.68f);
            for (var x = 0; x < 16; x++)
            {
                if (Mathf.Abs(x - 7.5f) <= halfWidth)
                    pixels[y * 16 + x] = solid;
            }
        }
        _dropdownArrowTexture.SetPixels32(pixels);
        _dropdownArrowTexture.Apply();
        _dropdownArrowTexture.name = "BetterTaiwuScrollSearchDropdownArrow";
        _dropdownArrowTexture.hideFlags = HideFlags.HideAndDontSave;
        _dropdownArrowSprite = Sprite.Create(
            _dropdownArrowTexture,
            new Rect(0f, 0f, 16f, 10f),
            new Vector2(0.5f, 0.5f),
            10f);
        _dropdownArrowSprite.name = "BetterTaiwuScrollSearchDropdownArrow";
        _dropdownArrowSprite.hideFlags = HideFlags.HideAndDontSave;
    }
}
