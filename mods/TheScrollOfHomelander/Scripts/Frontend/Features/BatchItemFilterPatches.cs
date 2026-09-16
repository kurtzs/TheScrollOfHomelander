#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FrameWork;
using FrameWork.UISystem.UIElements;
using Game.Components.Item;
using Game.Components.ListStyleGeneralScroll.Item;
using Game.Components.SortAndFilter;
using GameData.Domains.Item;
using GameData.Domains.Item.Display;
using GameData.Domains.Taiwu.ExchangeSystem;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DetailedFilterLineState = Game.Components.SortAndFilter.DetailedFilterLineState;
using DetailedFilterMenuState = Game.Components.SortAndFilter.DetailedFilterMenuState;
using DetailedFilterState = Game.Components.SortAndFilter.DetailedFilterState;
using ESortAndFilterOneLineType = Game.Components.SortAndFilter.ESortAndFilterOneLineType;
using LineConfig = Game.Components.SortAndFilter.LineConfig;
using LineState = Game.Components.SortAndFilter.LineState;
using ToggleKey = Game.Components.SortAndFilter.ToggleKey;

namespace BetterTaiwuScroll.Frontend;

[Serializable]
internal sealed class BatchItemFilterSettings
{
    public FilterMemoryEntry Filter = new FilterMemoryEntry();

    // Empty means all categories. Values are the top-level item category option indices.
    public List<int> Categories = new List<int>();

    // The selected category whose dependent sub-filters are currently displayed.
    public int EditingCategory = -1;

    // Empty means all grades. Otherwise values are the displayed 一品..九品 ranks (1..9).
    public List<int> Grades = new List<int>();

    internal BatchItemFilterSettings Clone()
    {
        return new BatchItemFilterSettings
        {
            Filter = CloneFilter(Filter),
            Categories = Categories == null ? new List<int>() : new List<int>(Categories),
            EditingCategory = EditingCategory,
            Grades = Grades == null ? new List<int>() : new List<int>(Grades)
        };
    }

    private static FilterMemoryEntry CloneFilter(FilterMemoryEntry source)
    {
        if (source == null)
            return new FilterMemoryEntry();
        return new FilterMemoryEntry
        {
            Key = source.Key,
            Signature = source.Signature,
            Lines = source.Lines == null
                ? new List<FilterLineMemory>()
                : source.Lines.Select(line => new FilterLineMemory
                {
                    LineId = line.LineId,
                    Type = line.Type,
                    IsActive = line.IsActive,
                    ToggleIsAll = line.ToggleIsAll,
                    ToggleIndex = line.ToggleIndex,
                    Menus = line.Menus == null
                        ? new List<FilterMenuMemory>()
                        : line.Menus.Select(menu => new FilterMenuMemory
                        {
                            MenuId = menu.MenuId,
                            IsActive = menu.IsActive,
                            SelectedIndices = menu.SelectedIndices == null
                                ? new List<int>()
                                : new List<int>(menu.SelectedIndices)
                        }).ToList()
                }).ToList()
        };
    }

    internal void Normalize()
    {
        Filter ??= new FilterMemoryEntry();
        Filter.Normalize();
        Categories ??= new List<int>();
        if (Categories.Count == 0 && Filter.Lines != null && Filter.Lines.Count > 0)
        {
            var legacyCategories = Filter.Lines[0].Menus?
                .FirstOrDefault(menu => menu.MenuId == int.MinValue)?
                .SelectedIndices;
            if (legacyCategories != null)
                Categories.AddRange(legacyCategories);
        }
        Categories = Categories.Where(category => category >= 0 && category < 64)
            .Distinct()
            .OrderBy(category => category)
            .ToList();
        if (!Categories.Contains(EditingCategory))
            EditingCategory = Categories.Count > 0 ? Categories[0] : -1;
        Grades ??= new List<int>();
        Grades = Grades.Where(grade => grade >= 1 && grade <= 9).Distinct().OrderBy(grade => grade).ToList();
    }
}

internal static class BatchItemFilterSettingsStore
{
    private const string FileName = "BatchItemFilterSettings.json";
    private static bool _loaded;

    internal static BatchItemFilterSettings Current { get; private set; } = new BatchItemFilterSettings();

    internal static void Load()
    {
        if (_loaded)
            return;

        _loaded = true;
        try
        {
            foreach (var path in ModUserDataPaths.GetFilePathCandidates(FileName))
            {
                if (!File.Exists(path))
                    continue;

                Current = JsonUtility.FromJson<BatchItemFilterSettings>(File.ReadAllText(path))
                    ?? new BatchItemFilterSettings();
                Current.Normalize();
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to load batch item filter settings: " + ex);
        }

        Current = new BatchItemFilterSettings();
        Current.Normalize();
    }

    internal static void Save()
    {
        Load();
        try
        {
            Current.Normalize();
            var path = ModUserDataPaths.GetFilePath(FileName);
            AsyncSettingsSaveQueue.Enqueue(path, Current.Clone());
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save batch item filter settings: " + ex);
        }
    }
}

[HarmonyPatch(typeof(MultiplyItemListScroll), nameof(MultiplyItemListScroll.Init))]
internal static class MultiplyItemListScrollBatchFilterInitPatch
{
    private static void Postfix(MultiplyItemListScroll __instance)
    {
        if (__instance == null)
            return;

        var controller = __instance.GetComponent<BatchItemFilterController>()
            ?? __instance.gameObject.AddComponent<BatchItemFilterController>();
        controller.Setup(__instance);
    }
}

[HarmonyPatch(typeof(MultiplyItemListScroll), nameof(MultiplyItemListScroll.EnterMultiplyMode))]
internal static class MultiplyItemListScrollBatchFilterEnterPatch
{
    private static void Postfix(MultiplyItemListScroll __instance)
    {
        var controller = __instance?.GetComponent<BatchItemFilterController>();
        controller?.SyncFromOwner();
        controller?.ApplySelection();
    }
}

[HarmonyPatch(typeof(MultiplyItemListScroll), nameof(MultiplyItemListScroll.OnItemMultiplyOperationTypeChange))]
internal static class MultiplyItemListScrollBatchFilterOperationChangePatch
{
    private static void Postfix(MultiplyItemListScroll __instance)
    {
        var controller = __instance?.GetComponent<BatchItemFilterController>();
        controller?.SyncFromOwner();
        controller?.ApplySelection();
    }
}

[HarmonyPatch(typeof(MultiplyItemListScroll), nameof(MultiplyItemListScroll.ExitMultiplyMode))]
internal static class MultiplyItemListScrollBatchFilterExitPatch
{
    private static void Postfix(MultiplyItemListScroll __instance)
    {
        __instance?.GetComponent<BatchItemFilterController>()?.SyncFromOwner();
    }
}

[HarmonyPatch(typeof(FilterPanel), "Refresh")]
internal static class BatchItemFilterPanelRefreshPatch
{
    private static void Prefix(FilterPanel __instance)
    {
        BatchItemFilterController.BeforePanelRefresh(__instance);
    }

    private static void Postfix(FilterPanel __instance)
    {
        BatchItemFilterController.AfterPanelRefresh(__instance);
    }
}

[HarmonyPatch(typeof(FilterPanel), nameof(FilterPanel.ClearAll))]
internal static class BatchItemFilterPanelClearPatch
{
    private static void Postfix(FilterPanel __instance)
    {
        BatchItemFilterController.OnPanelClear(__instance);
    }
}

[HarmonyPatch(typeof(CToggle), nameof(CToggle.OnPointerClick))]
[HarmonyPriority(Priority.First)]
internal static class BatchItemGradeTogglePatch
{
    private static bool Prefix(CToggle __instance, PointerEventData eventData)
    {
        if (__instance == null || eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return true;

        var categoryBinding = __instance.GetComponent<BatchItemCategoryOptionBinding>();
        if (categoryBinding != null)
            return false;

        var subcategoryBinding = __instance.GetComponent<BatchItemSubcategoryOptionBinding>();
        if (subcategoryBinding != null)
            return false;

        var gradeBinding = __instance.GetComponent<BatchItemGradeOptionBinding>();
        if (gradeBinding != null)
            return false;

        return true;
    }
}

internal sealed class BatchItemCategoryOptionBinding : MonoBehaviour, IPointerClickHandler
{
    private BatchItemFilterController _controller;
    private int _categoryIndex;

    internal void Setup(BatchItemFilterController controller, int categoryIndex)
    {
        _controller = controller;
        _categoryIndex = categoryIndex;
    }

    internal void HandleClick(bool toggleMembership)
    {
        _controller?.HandleCategoryClick(_categoryIndex, toggleMembership);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        HandleClick(FilterMultiSelectSupport.IsShiftPressed());
        eventData.Use();
    }
}

internal sealed class BatchItemSubcategoryOptionBinding : MonoBehaviour, IPointerClickHandler
{
    private BatchItemFilterController _controller;
    private FilterSection _section;
    private int _lineId;
    private int _menuId;
    private int _optionIndex;

    internal void Setup(
        BatchItemFilterController controller,
        FilterSection section,
        int lineId,
        int menuId,
        int optionIndex,
        CToggle toggle)
    {
        _controller = controller;
        _section = section;
        _lineId = lineId;
        _menuId = menuId;
        _optionIndex = optionIndex;
        if (toggle != null)
            toggle.interactable = true;
    }

    internal void HandleClick()
    {
        _controller?.HandleSubcategoryClick(_section, _lineId, _menuId, _optionIndex);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        HandleClick();
        eventData.Use();
    }
}

internal sealed class BatchItemGradeOptionBinding : MonoBehaviour, IPointerClickHandler
{
    private BatchItemFilterController _controller;
    private int _grade;
    private CToggle _toggle;

    internal void Setup(BatchItemFilterController controller, int grade, CToggle toggle)
    {
        _controller = controller;
        _grade = grade;
        _toggle = toggle;
        if (_toggle == null)
            return;

        // FilterSection registers every option in a single-select CToggleGroup.
        // Grades are intentionally multi-select, so keep the cloned visuals but
        // detach their click behavior from the vanilla group.
        _toggle.UnRegister();
        _toggle.onValueChanged.RemoveAllListeners();
        _toggle.onValueChanged.AddListener(OnValueChanged);
    }

    internal void HandleClick()
    {
        if (_controller == null)
            return;

        if (_grade == 0)
            _controller.SelectAllGrades();
        else
            _controller.ToggleGrade(_grade);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        HandleClick();
        eventData.Use();
    }

    private void OnValueChanged(bool isOn)
    {
        if (_controller == null)
            return;

        if (_grade == 0)
        {
            if (isOn)
                _controller.SelectAllGrades();
            return;
        }

        _controller.SetGradeSelected(_grade, isOn);
    }

    private void OnDestroy()
    {
        if (_toggle != null)
            _toggle.onValueChanged.RemoveListener(OnValueChanged);
    }
}

internal sealed class BatchItemFilterController : MonoBehaviour
{
    private const string ButtonName = "BetterTaiwuScrollBatchItemFilterButton";
    private const string ViewName = "BetterTaiwuScrollBatchItemFilterView";
    private const string GradeSectionName = "BetterTaiwuScrollBatchItemGradeSection";
    private const string SortSaveKey = "BetterTaiwuScrollBatchItemFilterSort";
    private const float ButtonSpacing = 12f;
    private const float PanelButtonGap = 8f;
    private const int DelayedPanelPositionFrames = 1;

    private static readonly Dictionary<FilterPanel, BatchItemFilterController> PanelOwners =
        new Dictionary<FilterPanel, BatchItemFilterController>();
    private static readonly HashSet<SortAndFilter> BatchFilterViews = new HashSet<SortAndFilter>();

    private MultiplyItemListScroll _owner;
    private CButton _sourceButton;
    private CButton _filterButton;
    private SortAndFilter _filterView;
    private FilterPanel _filterPanel;
    private Game.Components.SortAndFilter.Item.ItemSortAndFilterController _filterController;
    private FilterSection _gradeSection;
    private Transform _gradeParkingRoot;
    private bool _initialized;
    private bool _initializing;
    private bool _refreshingPreview;
    private bool _applyingSelection;
    private bool _handlingCategoryClick;
    private bool _handlingOwnedOptionClick;
    private Coroutine _buttonInitializationRoutine;
    private Coroutine _buttonPositionRoutine;
    private Coroutine _panelPositionRoutine;

    internal static bool IsBatchFilterView(SortAndFilter view)
    {
        return view != null && BatchFilterViews.Contains(view);
    }

    internal static bool IsBatchFilterPanel(FilterPanel panel)
    {
        return panel != null && PanelOwners.ContainsKey(panel);
    }

    internal void Setup(MultiplyItemListScroll owner)
    {
        _owner = owner;
        BatchItemFilterSettingsStore.Load();
        if (!TryCreateButton() && _buttonInitializationRoutine == null)
        {
            if (CanStartUiCoroutine())
                _buttonInitializationRoutine = StartCoroutine(InitializeButtonWhenReady());
        }
    }

    internal bool Prewarm()
    {
        if (_owner == null)
            _owner = GetComponent<MultiplyItemListScroll>();
        if (_owner == null)
            return false;

        BatchItemFilterSettingsStore.Load();
        return EnsureFilterClone(refreshPreview: false);
    }

    private void OnDestroy()
    {
        AsyncSettingsSaveQueue.FlushAll();
        StopUiCoroutines();
        _filterController?.UninitForReplace();
        if (_filterPanel != null)
        {
            PanelOwners.Remove(_filterPanel);
            if (_filterView == null || !_filterPanel.transform.IsChildOf(_filterView.transform))
                Destroy(_filterPanel.gameObject);
        }
        if (_filterView != null)
            BatchFilterViews.Remove(_filterView);
    }

    private void OnDisable()
    {
        StopUiCoroutines();
        if (_filterPanel != null && _filterPanel.gameObject.activeSelf)
            _filterPanel.gameObject.SetActive(false);
    }

    private IEnumerator InitializeButtonWhenReady()
    {
        for (var frame = 0; frame < 8 && _filterButton == null && _owner != null; frame++)
        {
            yield return null;
            if (!CanStartUiCoroutine())
                break;
            if (TryCreateButton())
                break;
        }

        _buttonInitializationRoutine = null;
    }

    private bool TryCreateButton()
    {
        if (_filterButton != null)
            return true;
        if (_owner == null)
            return false;

        _sourceButton = _owner.BtnMultiplySelect;
        if (_sourceButton == null)
            return false;

        try
        {
            CreateButton();
            SyncButton();
            if (CanStartUiCoroutine() && _buttonPositionRoutine == null)
                _buttonPositionRoutine = StartCoroutine(PositionButtonNextFrame());
            return _filterButton != null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to initialize batch item filter button: " + ex);
            CleanupFailedClone();
            return false;
        }
    }

    private IEnumerator PositionButtonNextFrame()
    {
        yield return null;
        _buttonPositionRoutine = null;
        if (!CanStartUiCoroutine())
            yield break;
        PositionButton();
        SyncButton();
    }

    private bool EnsureFilterClone(bool refreshPreview = true)
    {
        if (_initialized)
            return true;
        if (!TryCreateButton())
            return false;

        var sourceController = _owner?.CurMultiplyScrollView?.SortAndFilterController;
        var sourceView = sourceController == null
            ? null
            : Traverse.Create(sourceController).Field("_sortAndFilter").GetValue() as SortAndFilter;
        if (sourceView == null)
            return false;

        try
        {
            CreateFilterClone(sourceView, refreshPreview);
            _initialized = _filterView != null && _filterController != null;
            if (_initialized && refreshPreview)
                RefreshPreview();
            return _initialized;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to create batch item filter panel: " + ex);
            CleanupFailedClone(destroyButton: false);
            return false;
        }
    }

    private void CreateButton()
    {
        var existing = _sourceButton.transform.parent?.Find(ButtonName);
        if (existing != null)
            Destroy(existing.gameObject);

        var buttonObject = Instantiate(_sourceButton.gameObject, _sourceButton.transform.parent, false);
        buttonObject.name = ButtonName;
        _filterButton = buttonObject.GetComponent<CButton>();
        if (_filterButton == null)
        {
            Destroy(buttonObject);
            return;
        }

        var layout = buttonObject.GetComponent<LayoutElement>() ?? buttonObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;
        _filterButton.ClearAndAddListener(OpenFilterPanel);
        SetButtonText(buttonObject, "批量物品筛选");
        PositionButton();
    }

    private void CreateFilterClone(SortAndFilter sourceView, bool refreshPreview)
    {
        var cloneObject = Instantiate(sourceView.gameObject, sourceView.transform.parent, false);
        cloneObject.name = ViewName;
        _filterView = cloneObject.GetComponent<SortAndFilter>();
        if (_filterView == null)
            throw new InvalidOperationException("Cloned object does not contain SortAndFilter.");

        _filterPanel = Traverse.Create(_filterView).Field("filterPanel").GetValue<FilterPanel>();
        var sourcePanel = Traverse.Create(sourceView).Field("filterPanel").GetValue<FilterPanel>();
        if (_filterPanel == null || _filterPanel == sourcePanel || !_filterPanel.transform.IsChildOf(cloneObject.transform))
            throw new InvalidOperationException("The original filter panel is not contained by the cloned filter view.");

        PanelOwners[_filterPanel] = this;
        BatchFilterViews.Add(_filterView);
        DisableClonedInlineFilterLayout();

        _initializing = true;
        _filterController = new Game.Components.SortAndFilter.Item.ItemSortAndFilterController(_filterView);
        _filterController.Init(OnFilterStateChanged, SortSaveKey);
        ApplySavedFilterState();
        SyncCategorySelectionToView(refresh: refreshPreview);
        _initializing = false;

        KeepOnlyPanelVisible(cloneObject.transform, _filterPanel.transform);
        EnsureGradeSection();
        _filterView.CloseFilterPanel();
        MoveFilterPanelToOverlay();
    }

    private void CleanupFailedClone(bool destroyButton = true)
    {
        if (_filterPanel != null)
            PanelOwners.Remove(_filterPanel);
        if (_filterView != null)
        {
            BatchFilterViews.Remove(_filterView);
            Destroy(_filterView.gameObject);
        }
        if (destroyButton && _filterButton != null)
            Destroy(_filterButton.gameObject);

        _filterPanel = null;
        _filterView = null;
        _filterController = null;
        if (destroyButton)
            _filterButton = null;
        _initialized = false;
    }

    private void OpenFilterPanel()
    {
        if (!CanStartUiCoroutine())
            return;
        if (!EnsureFilterClone())
            return;

        RefreshPreview();
        EnsureGradeSection();
        _filterView.OpenFilterPanel();
        _filterPanel.transform.SetAsLastSibling();
        RequestPanelPosition();
    }

    private void DisableClonedInlineFilterLayout()
    {
        var inlineController = _filterView.GetComponent<InlineFilterButtonsController>();
        if (inlineController != null)
        {
            inlineController.Restore();
            inlineController.enabled = false;
            Destroy(inlineController);
        }

        var fullRowLayout = _filterView.GetComponent<FilterEntryFullRowLayoutState>();
        if (fullRowLayout != null)
        {
            fullRowLayout.Restore();
            fullRowLayout.enabled = false;
            Destroy(fullRowLayout);
        }
    }

    private void MoveFilterPanelToOverlay()
    {
        if (_filterPanel == null || _sourceButton == null)
            return;

        var canvas = _sourceButton.GetComponentInParent<Canvas>(true)?.rootCanvas;
        var overlayRoot = canvas == null ? null : canvas.transform as RectTransform;
        if (overlayRoot == null || _filterPanel.transform.parent == overlayRoot)
            return;

        _filterPanel.transform.SetParent(overlayRoot, true);
        _filterPanel.transform.SetAsLastSibling();
    }

    private void RequestPanelPosition()
    {
        if (!CanStartUiCoroutine() || _filterPanel == null || !_filterPanel.gameObject.activeInHierarchy)
            return;
        if (_panelPositionRoutine != null)
            StopCoroutine(_panelPositionRoutine);
        _panelPositionRoutine = StartCoroutine(PositionPanelAfterLayout());
    }

    private IEnumerator PositionPanelAfterLayout()
    {
        for (var frame = 0; frame < DelayedPanelPositionFrames; frame++)
            yield return null;
        if (!CanStartUiCoroutine())
        {
            _panelPositionRoutine = null;
            yield break;
        }
        PositionFilterPanel();
        _panelPositionRoutine = null;
    }

    private bool CanStartUiCoroutine()
    {
        return isActiveAndEnabled
            && gameObject.activeInHierarchy
            && _owner != null
            && _owner.gameObject.activeInHierarchy;
    }

    private void StopUiCoroutines()
    {
        if (_buttonInitializationRoutine != null)
            StopCoroutine(_buttonInitializationRoutine);
        if (_buttonPositionRoutine != null)
            StopCoroutine(_buttonPositionRoutine);
        if (_panelPositionRoutine != null)
            StopCoroutine(_panelPositionRoutine);
        _buttonInitializationRoutine = null;
        _buttonPositionRoutine = null;
        _panelPositionRoutine = null;
    }

    private void PositionFilterPanel()
    {
        var buttonRect = _filterButton?.transform as RectTransform;
        var panelRect = _filterPanel?.transform as RectTransform;
        var canvasRect = panelRect?.parent as RectTransform;
        if (buttonRect == null || panelRect == null || canvasRect == null
            || !_filterPanel.gameObject.activeInHierarchy)
            return;

        var buttonCorners = new Vector3[4];
        var panelCorners = new Vector3[4];
        var canvasCorners = new Vector3[4];
        buttonRect.GetWorldCorners(buttonCorners);
        panelRect.GetWorldCorners(panelCorners);
        canvasRect.GetWorldCorners(canvasCorners);

        var gap = PanelButtonGap * Mathf.Max(Mathf.Abs(panelRect.lossyScale.y), 0.01f);
        var targetLeft = buttonCorners.Min(corner => corner.x);
        var targetBottom = buttonCorners.Max(corner => corner.y) + gap;
        var panelLeft = panelCorners.Min(corner => corner.x);
        var panelBottom = panelCorners.Min(corner => corner.y);
        panelRect.position += new Vector3(targetLeft - panelLeft, targetBottom - panelBottom, 0f);

        panelRect.GetWorldCorners(panelCorners);
        var canvasLeft = canvasCorners.Min(corner => corner.x);
        var canvasRight = canvasCorners.Max(corner => corner.x);
        panelLeft = panelCorners.Min(corner => corner.x);
        var panelRight = panelCorners.Max(corner => corner.x);
        var horizontalCorrection = panelLeft < canvasLeft
            ? canvasLeft - panelLeft
            : panelRight > canvasRight
                ? canvasRight - panelRight
                : 0f;
        if (!Mathf.Approximately(horizontalCorrection, 0f))
            panelRect.position += new Vector3(horizontalCorrection, 0f, 0f);

        _filterPanel.transform.SetAsLastSibling();
    }

    private void OnFilterStateChanged()
    {
        if (_initializing || _refreshingPreview || _handlingOwnedOptionClick
            || _filterView?.Config?.LineConfigs == null)
            return;

        RefreshPreview();
        SaveFilterState();
    }

    private void ApplySavedFilterState()
    {
        var entry = BatchItemFilterSettingsStore.Current.Filter;
        if (entry?.Lines == null || entry.Lines.Count == 0)
            return;

        var states = FilterMemoryController.BuildLineStates(_filterView.Config, entry);
        if (states == null || states.Count == 0)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Stored batch filter no longer matches the game config; using All.");
            return;
        }

        _filterView.ApplyFilterLineStates(states);
    }

    internal void HandleCategoryClick(int categoryIndex, bool toggleMembership)
    {
        if (_handlingCategoryClick)
            return;

        _handlingCategoryClick = true;
        try
        {
            var settings = BatchItemFilterSettingsStore.Current;
            var categories = GetSelectedCategories();

            if (categoryIndex < 0)
            {
                categories.Clear();
                settings.EditingCategory = -1;
            }
            else if (toggleMembership)
            {
                if (categories.Contains(categoryIndex))
                {
                    categories.Remove(categoryIndex);
                    if (settings.EditingCategory == categoryIndex)
                        settings.EditingCategory = categories.Count > 0 ? categories[0] : -1;
                }
                else
                {
                    categories.Add(categoryIndex);
                    settings.EditingCategory = categoryIndex;
                }
            }
            else if (categories.Contains(categoryIndex))
            {
                settings.EditingCategory = categoryIndex;
            }
            else
            {
                categories.Clear();
                categories.Add(categoryIndex);
                settings.EditingCategory = categoryIndex;
            }

            categories.Sort();
            settings.Categories = categories;
            NormalizeEditingCategory(categories);
            SyncCategorySelectionToView(refresh: true);
            RefreshPreview();
            SaveFilterState();
        }
        finally
        {
            _handlingCategoryClick = false;
        }
    }

    private void SaveFilterState()
    {
        if (_filterView?.Config?.LineConfigs == null)
            return;

        try
        {
            var state = _filterView.GetStateFromUI();
            BatchItemFilterSettingsStore.Current.Filter = new FilterMemoryEntry
            {
                Key = "BatchItemFilter",
                Signature = string.Empty,
                Lines = FilterMemoryController.BuildLineMemories(
                    _filterView,
                    _filterView.Config,
                    state.LineStates)
            };
            BatchItemFilterSettingsStore.Save();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save batch item filter state: " + ex);
        }
    }

    private void RefreshPreview()
    {
        if (_filterController == null || _refreshingPreview)
            return;

        try
        {
            _refreshingPreview = true;
            var items = GetCurrentSourceItems().Cast<ITradeableContent>().ToList();
            var matchedItems = GetItemsMatchingSelectedCategories(items);
            // Keep the vanilla controller's option-count state in sync with the
            // category currently shown in the panel. GenerateFilter only reads
            // the view here; category union matching above is deliberately pure.
            _filterController.GenerateFilter();
            _filterController.AfterFilter(items);
            _filterController.SetFilteredCount(matchedItems.Count);
            RefreshGradeCounts(matchedItems);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to refresh batch filter preview: " + ex);
        }
        finally
        {
            _refreshingPreview = false;
        }
    }

    internal void SetGradeSelected(int grade, bool selected)
    {
        var grades = BatchItemFilterSettingsStore.Current.Grades;
        if (grade < 1 || grade > 9)
            return;

        if (selected)
        {
            if (!grades.Contains(grade))
                grades.Add(grade);
        }
        else
        {
            grades.Remove(grade);
        }

        grades.Sort();
        RefreshGradeVisuals();
        BatchItemFilterSettingsStore.Save();
        RefreshPreview();
    }

    internal void ToggleGrade(int grade)
    {
        var grades = BatchItemFilterSettingsStore.Current.Grades;
        SetGradeSelected(grade, !grades.Contains(grade));
    }

    internal void HandleSubcategoryClick(
        FilterSection section,
        int lineId,
        int menuId,
        int optionIndex)
    {
        if (_handlingOwnedOptionClick || section == null || _filterView == null)
            return;

        _handlingOwnedOptionClick = true;
        try
        {
            section.SetSelectedIndex(optionIndex, notify: false);
            _filterView.OnSectionSelectionChanged(lineId, menuId, optionIndex);
            RefreshPreview();
            SaveFilterState();
        }
        finally
        {
            _handlingOwnedOptionClick = false;
        }
    }

    internal void SelectAllGrades()
    {
        var grades = BatchItemFilterSettingsStore.Current.Grades;
        if (grades.Count > 0)
        {
            grades.Clear();
            RefreshGradeVisuals();
            BatchItemFilterSettingsStore.Save();
            RefreshPreview();
        }
        else
        {
            // Keep the all-option selected when it is clicked again.
            RefreshGradeVisuals();
        }
    }

    internal void ResetGrades()
    {
        BatchItemFilterSettingsStore.Current.Grades.Clear();
        RefreshGradeVisuals();
        BatchItemFilterSettingsStore.Save();
        RefreshPreview();
    }

    internal void ApplySelection()
    {
        if (_applyingSelection || !_initialized || _owner == null || !_owner.IsMultiItemSelect
            || _owner.CurrItemOperation != ItemOperationType.EItemOperationType.Disassemble)
            return;

        try
        {
            _applyingSelection = true;
            var candidates = Traverse.Create(_owner)
                .Field("_canOperateItems")
                .GetValue<List<ItemDisplayData>>()
                ?? new List<ItemDisplayData>();

            var selectedGrades = BatchItemFilterSettingsStore.Current.Grades;
            var filtered = GetItemsMatchingSelectedCategories(candidates).Where(item =>
                item != null
                && (selectedGrades.Count == 0
                    || selectedGrades.Contains(ToDisplayGrade(
                        ItemTemplateHelper.GetGrade(item.Key.ItemType, item.Key.TemplateId)))))
                .ToList();

            _owner.CurMultiplyScrollView.SetItemList(filtered);
            Traverse.Create(_owner).Method("SelectAll", true).GetValue();
            Debug.Log("[BetterTaiwuScroll] Batch item filter preselected "
                + _owner.SelectedMultiplyItemDict.Count + " item stacks from " + filtered.Count + " candidates.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to apply batch item filter selection: " + ex);
        }
        finally
        {
            try
            {
                _owner.RefreshMultiplyCanOperateItems();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BetterTaiwuScroll] Failed to restore batch item list after preselection: " + ex);
            }
            _applyingSelection = false;
        }
    }

    private List<ItemDisplayData> GetCurrentSourceItems()
    {
        if (_owner == null)
            return new List<ItemDisplayData>();

        var itemDict = Traverse.Create(_owner)
            .Field("_itemDict")
            .GetValue<Dictionary<ItemSourceType, List<ItemDisplayData>>>();
        return itemDict != null && itemDict.TryGetValue(_owner.ItemSourceType, out var items) && items != null
            ? items
            : new List<ItemDisplayData>();
    }

    private List<T> GetItemsMatchingSelectedCategories<T>(IReadOnlyList<T> items)
        where T : class, ITradeableContent
    {
        var result = new List<T>();
        if (_filterController == null || _filterView == null || items == null || items.Count == 0)
            return result;

        var categories = GetSelectedCategories();
        IEnumerable<int> categoryIndices = categories.Count == 0 ? new[] { -1 } : categories;
        var predicates = categoryIndices
            .Select(CreateCategoryPredicate)
            .Where(predicate => predicate != null)
            .ToList();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item != null && predicates.Any(predicate => predicate(item)))
                result.Add(items[i]);
        }
        return result;
    }

    private Func<ITradeableContent, bool> CreateCategoryPredicate(int categoryIndex)
    {
        var configs = _filterView?.Config?.LineConfigs;
        var sourceStates = _filterView?.GetStateFromUI().LineStates;
        var filterLines = _filterController?.FilterLines;
        if (configs == null || sourceStates == null || filterLines == null
            || configs.Count == 0 || sourceStates.Count != configs.Count)
            return null;

        var lineIndexById = new Dictionary<int, int>();
        var states = new List<LineState>(sourceStates.Count);
        for (var i = 0; i < sourceStates.Count; i++)
        {
            lineIndexById[configs[i].Id] = i;
            var source = sourceStates[i];
            if (i == 0 && source.Type == ESortAndFilterOneLineType.ToggleGroup)
            {
                source.ToggleGroupState = categoryIndex < 0
                    ? ToggleKey.AllKey
                    : ToggleKey.CreateIndexKey(categoryIndex);
            }
            states.Add(source);
        }

        var activeCache = new Dictionary<int, bool>();
        for (var i = 0; i < states.Count; i++)
        {
            var state = states[i];
            state.IsActive = IsSimulatedLineActive(
                i,
                configs,
                states,
                lineIndexById,
                activeCache,
                new HashSet<int>());
            states[i] = state;
        }

        for (var i = 0; i < states.Count; i++)
        {
            var state = states[i];
            var sourceMenus = state.DetailedFilterState?.State.MenuStateDict;
            if (sourceMenus == null)
                continue;

            var simulatedMenus = new Dictionary<int, DetailedFilterMenuState>();
            foreach (var menu in sourceMenus)
            {
                var isActive = state.IsActive && IsSimulatedMenuActive(
                    i,
                    menu.Key,
                    sourceMenus,
                    new HashSet<int>());
                simulatedMenus[menu.Key] = new DetailedFilterMenuState(
                    menu.Value.SelectedIndices,
                    isActive);
            }

            state.DetailedFilterState = new DetailedFilterLineState
            {
                State = new DetailedFilterState { MenuStateDict = simulatedMenus }
            };
            states[i] = state;
        }

        return item =>
        {
            foreach (var filterLine in filterLines)
            {
                if (!lineIndexById.TryGetValue(filterLine.Id, out var lineIndex))
                    continue;

                var state = states[lineIndex];
                if (state.IsActive && !filterLine.IsDataMatch(item, state))
                    return false;
            }
            return true;
        };
    }

    private static bool IsSimulatedLineActive(
        int lineIndex,
        IReadOnlyList<LineConfig> configs,
        IReadOnlyList<LineState> states,
        IReadOnlyDictionary<int, int> lineIndexById,
        IDictionary<int, bool> activeCache,
        ISet<int> visiting)
    {
        if (activeCache.TryGetValue(lineIndex, out var cached))
            return cached;
        if (!visiting.Add(lineIndex))
            return false;

        var config = configs[lineIndex];
        var condition = config.ActiveCondition;
        var active = !condition.HasValue
            ? config.DefaultActive
            : condition.Value.ActiveDependsOn != null
              && condition.Value.ActiveDependsOn.Any(dependency =>
                  lineIndexById.TryGetValue(dependency.LineId, out var dependencyIndex)
                  && IsSimulatedLineActive(
                      dependencyIndex,
                      configs,
                      states,
                      lineIndexById,
                      activeCache,
                      visiting)
                  && (dependency.ToggleKey.IsAll
                      || (!states[dependencyIndex].ToggleGroupState.IsAll
                          && states[dependencyIndex].ToggleGroupState.Index == dependency.ToggleKey.Index)));

        visiting.Remove(lineIndex);
        activeCache[lineIndex] = active;
        return active;
    }

    private bool IsSimulatedMenuActive(
        int lineIndex,
        int menuId,
        IReadOnlyDictionary<int, DetailedFilterMenuState> menuStates,
        ISet<int> visiting)
    {
        if (!_filterView.ShouldShowMenu(lineIndex, menuId) || !visiting.Add(menuId))
            return false;

        var config = _filterView.GetMenuConfig(lineIndex, menuId);
        if (!config.HasValue)
        {
            visiting.Remove(menuId);
            return false;
        }

        var dependency = config.Value.DropdownContext.Dependency;
        var active = true;
        if (dependency.HasValue)
        {
            var parentId = dependency.Value.MenuId;
            active = menuStates.TryGetValue(parentId, out var parentState)
                && IsSimulatedMenuActive(lineIndex, parentId, menuStates, visiting)
                && parentState.SelectedIndices != null
                && parentState.SelectedIndices.Contains(dependency.Value.OptionIndex);
        }

        visiting.Remove(menuId);
        return active;
    }

    private List<int> GetSelectedCategories()
    {
        var settings = BatchItemFilterSettingsStore.Current;
        var optionCount = GetCategoryOptionCount();
        settings.Categories ??= new List<int>();
        settings.Categories = settings.Categories
            .Where(category => category >= 0 && category < optionCount)
            .Distinct()
            .OrderBy(category => category)
            .ToList();
        NormalizeEditingCategory(settings.Categories);
        return new List<int>(settings.Categories);
    }

    private int GetCategoryOptionCount()
    {
        var lineConfigs = _filterView?.Config?.LineConfigs;
        if (lineConfigs == null || lineConfigs.Count == 0)
            return 0;

        var toggleGroupLineConfig = lineConfigs[0].ToggleGroupLineConfig;
        return toggleGroupLineConfig?.Config.FilterToggleConfigs?.Count ?? 0;
    }

    private void NormalizeEditingCategory(IReadOnlyList<int> categories)
    {
        var settings = BatchItemFilterSettingsStore.Current;
        if (categories == null || categories.Count == 0)
        {
            settings.EditingCategory = -1;
            return;
        }

        if (!categories.Contains(settings.EditingCategory))
            settings.EditingCategory = categories[0];
    }

    private void SyncCategorySelectionToView(bool refresh)
    {
        var categories = GetSelectedCategories();
        SetViewCategoryOrder(BatchItemFilterSettingsStore.Current.EditingCategory);
        if (refresh)
            _filterView?.UpdateLineActive(0);
        RefreshCategoryVisuals();
    }

    private void SetViewCategoryOrder(int editingCategory)
    {
        if (_filterView?.Config?.LineConfigs == null || _filterView.Config.LineConfigs.Count == 0)
            return;

        var selectedIndices = Traverse.Create(_filterView)
            .Field("_selectedIndices")
            .GetValue<Dictionary<int, List<int>>>();
        if (selectedIndices == null)
            return;

        var categories = BatchItemFilterSettingsStore.Current.Categories ?? new List<int>();
        var selectionKey = GetSelectionKey(_filterView.Config.LineConfigs[0].Id, int.MinValue);
        if (categories.Count == 0 || editingCategory < 0)
        {
            selectedIndices.Remove(selectionKey);
            return;
        }

        var ordered = new List<int> { editingCategory };
        ordered.AddRange(categories.Where(category => category != editingCategory).OrderBy(category => category));
        selectedIndices[selectionKey] = ordered;
    }

    private void EnsureGradeSection()
    {
        if (_filterPanel == null)
            return;

        var sectionRoot = Traverse.Create(_filterPanel).Field("sectionRoot").GetValue<RectTransform>();
        var sectionTemplate = Traverse.Create(_filterPanel).Field("sectionTemplate").GetValue<FilterSection>();
        if (sectionRoot == null || sectionTemplate == null)
            return;

        if (_gradeSection == null)
        {
            var gradeObject = Instantiate(sectionTemplate.gameObject, sectionRoot, false);
            gradeObject.name = GradeSectionName;
            _gradeSection = gradeObject.GetComponent<FilterSection>();
            var configs = Enumerable.Range(1, 9)
                .Select(grade => new FilterDropdownItemConfig(GetGradeName(grade)))
                .ToList();
            _gradeSection.Setup(int.MaxValue, "品级", configs, _ => { });

            var contentRoot = _gradeSection.GetContentRoot();
            for (var i = 0; i < contentRoot.childCount; i++)
            {
                var option = contentRoot.GetChild(i)?.GetComponent<FilterSectionOption>();
                if (option == null)
                    continue;

                var binding = option.gameObject.GetComponent<BatchItemGradeOptionBinding>()
                    ?? option.gameObject.AddComponent<BatchItemGradeOptionBinding>();
                option.Toggle.interactable = true;
                binding.Setup(this, i == 0 ? 0 : i, option.Toggle);
                FilterMultiSelectSupport.RemoveBinding(option.gameObject);
            }
        }

        _gradeSection.transform.SetParent(sectionRoot, false);
        _gradeSection.transform.SetAsLastSibling();
        _gradeSection.gameObject.SetActive(true);
        RefreshGradeVisuals();
    }

    private void BindCategorySection()
    {
        if (_filterPanel == null || _filterView?.Config?.LineConfigs == null || _filterView.Config.LineConfigs.Count == 0)
            return;

        var sectionMap = Traverse.Create(_filterPanel)
            .Field("_sectionMap")
            .GetValue<Dictionary<(int LineId, int MenuId), FilterSection>>();
        var lineId = _filterView.Config.LineConfigs[0].Id;
        if (sectionMap == null || !sectionMap.TryGetValue((lineId, int.MinValue), out var section) || section == null)
            return;

        var contentRoot = section.GetContentRoot();
        if (contentRoot == null)
            return;

        for (var i = 0; i < contentRoot.childCount; i++)
        {
            var option = contentRoot.GetChild(i)?.GetComponent<FilterSectionOption>();
            if (option == null)
                continue;

            var binding = option.gameObject.GetComponent<BatchItemCategoryOptionBinding>()
                ?? option.gameObject.AddComponent<BatchItemCategoryOptionBinding>();
            binding.Setup(this, i - 1);
            FilterMultiSelectSupport.RemoveBinding(option.gameObject);
        }
        RefreshCategoryVisuals(section);
    }

    private void BindSubcategorySections()
    {
        if (_filterPanel == null)
            return;

        var sectionMap = Traverse.Create(_filterPanel)
            .Field("_sectionMap")
            .GetValue<Dictionary<(int LineId, int MenuId), FilterSection>>();
        if (sectionMap == null)
            return;

        foreach (var entry in sectionMap)
        {
            if (entry.Key.MenuId == int.MinValue || entry.Value == null)
                continue;

            var contentRoot = entry.Value.GetContentRoot();
            if (contentRoot == null)
                continue;

            for (var i = 0; i < contentRoot.childCount; i++)
            {
                var option = contentRoot.GetChild(i)?.GetComponent<FilterSectionOption>();
                if (option == null)
                    continue;

                var binding = option.gameObject.GetComponent<BatchItemSubcategoryOptionBinding>()
                    ?? option.gameObject.AddComponent<BatchItemSubcategoryOptionBinding>();
                binding.Setup(this, entry.Value, entry.Key.LineId, entry.Key.MenuId, i - 1, option.Toggle);
                FilterMultiSelectSupport.RemoveBinding(option.gameObject);
            }
        }
    }

    private void RefreshCategoryVisuals(FilterSection knownSection = null)
    {
        if (_filterPanel == null || _filterView?.Config?.LineConfigs == null || _filterView.Config.LineConfigs.Count == 0)
            return;

        var section = knownSection;
        if (section == null)
        {
            var sectionMap = Traverse.Create(_filterPanel)
                .Field("_sectionMap")
                .GetValue<Dictionary<(int LineId, int MenuId), FilterSection>>();
            var lineId = _filterView.Config.LineConfigs[0].Id;
            sectionMap?.TryGetValue((lineId, int.MinValue), out section);
        }

        var contentRoot = section?.GetContentRoot();
        if (contentRoot == null)
            return;

        var categories = BatchItemFilterSettingsStore.Current.Categories ?? new List<int>();
        for (var i = 0; i < contentRoot.childCount; i++)
        {
            var option = contentRoot.GetChild(i)?.GetComponent<FilterSectionOption>();
            if (option != null)
                option.SetIsOnWithoutNotify(i == 0 ? categories.Count == 0 : categories.Contains(i - 1));
        }
    }

    private void ParkGradeSection()
    {
        if (_gradeSection == null || _filterPanel == null)
            return;

        if (_gradeParkingRoot == null)
        {
            var parking = new GameObject("BetterTaiwuScrollBatchGradeParking", typeof(RectTransform));
            parking.transform.SetParent(_filterPanel.transform, false);
            parking.SetActive(false);
            _gradeParkingRoot = parking.transform;
        }

        _gradeSection.transform.SetParent(_gradeParkingRoot, false);
    }

    private void RefreshGradeVisuals()
    {
        var contentRoot = _gradeSection?.GetContentRoot();
        if (contentRoot == null)
            return;

        var grades = BatchItemFilterSettingsStore.Current.Grades;
        for (var i = 0; i < contentRoot.childCount; i++)
        {
            var option = contentRoot.GetChild(i)?.GetComponent<FilterSectionOption>();
            if (option != null)
                option.SetIsOnWithoutNotify(i == 0 ? grades.Count == 0 : grades.Contains(i));
        }
    }

    private void RefreshGradeCounts(IReadOnlyList<ITradeableContent> items)
    {
        if (_gradeSection == null || _filterController == null)
            return;

        var predicate = _filterController.GenerateFilter();
        var counts = new int[10];
        foreach (var item in items)
        {
            if (item == null || !predicate(item))
                continue;

            counts[0]++;
            var grade = ToDisplayGrade(ItemTemplateHelper.GetGrade(item.Key.ItemType, item.Key.TemplateId));
            if (grade >= 1 && grade <= 9)
                counts[grade]++;
        }
        _gradeSection.RefreshOptionCounts(counts);
    }

    private static int ToDisplayGrade(int internalGrade)
    {
        return internalGrade >= 0 && internalGrade <= 8 ? 9 - internalGrade : -1;
    }

    private static int GetSelectionKey(int lineId, int menuId)
    {
        return lineId * 1000 + menuId;
    }

    internal void SyncFromOwner()
    {
        if (_filterButton == null)
            TryCreateButton();
        SyncButton();
    }

    private void SyncButton()
    {
        if (_filterButton == null || _sourceButton == null || _owner == null)
            return;

        PositionButton();
        var active = _sourceButton.gameObject.activeSelf && !_owner.IsMultiItemSelect;
        if (_filterButton.gameObject.activeSelf != active)
            _filterButton.gameObject.SetActive(active);
        _filterButton.interactable = _sourceButton.interactable;
    }

    private void PositionButton()
    {
        var sourceRect = _sourceButton?.transform as RectTransform;
        var buttonRect = _filterButton?.transform as RectTransform;
        if (sourceRect == null || buttonRect == null)
            return;

        buttonRect.anchorMin = sourceRect.anchorMin;
        buttonRect.anchorMax = sourceRect.anchorMax;
        buttonRect.pivot = sourceRect.pivot;
        buttonRect.localScale = sourceRect.localScale;
        buttonRect.sizeDelta = sourceRect.sizeDelta;
        var width = Mathf.Max(Mathf.Abs(sourceRect.rect.width), Mathf.Abs(sourceRect.sizeDelta.x));
        buttonRect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(-width - ButtonSpacing, 0f);
    }

    private static void SetButtonText(GameObject buttonObject, string text)
    {
        var labels = buttonObject.GetComponentsInChildren<TextMeshProUGUI>(true);
        var label = labels.FirstOrDefault(item => item != null && item.text.Contains("批量"))
            ?? labels.OrderByDescending(item => item?.rectTransform.rect.width ?? 0f).FirstOrDefault();
        if (label == null)
            return;

        label.SetText(text);
        label.enableAutoSizing = true;
        label.fontSizeMin = 16f;
    }

    private static string GetGradeName(int grade)
    {
        return grade switch
        {
            1 => "一品",
            2 => "二品",
            3 => "三品",
            4 => "四品",
            5 => "五品",
            6 => "六品",
            7 => "七品",
            8 => "八品",
            _ => "九品"
        };
    }

    private static void KeepOnlyPanelVisible(Transform cloneRoot, Transform panel)
    {
        var current = panel;
        while (current != null && current != cloneRoot)
        {
            var parent = current.parent;
            if (parent == null)
                break;

            for (var i = 0; i < parent.childCount; i++)
            {
                var sibling = parent.GetChild(i);
                if (sibling != current)
                    sibling.gameObject.SetActive(false);
            }

            current = parent;
        }
    }

    internal static void BeforePanelRefresh(FilterPanel panel)
    {
        if (panel != null && PanelOwners.TryGetValue(panel, out var owner))
            owner.ParkGradeSection();
    }

    internal static void AfterPanelRefresh(FilterPanel panel)
    {
        if (panel != null && PanelOwners.TryGetValue(panel, out var owner))
        {
            owner.BindCategorySection();
            owner.BindSubcategorySections();
            owner.EnsureGradeSection();
            owner.RequestPanelPosition();
        }
    }

    internal static void OnPanelClear(FilterPanel panel)
    {
        if (panel != null && PanelOwners.TryGetValue(panel, out var owner))
            owner.ResetAllBatchFilters();
    }

    private void ResetAllBatchFilters()
    {
        var settings = BatchItemFilterSettingsStore.Current;
        settings.Categories.Clear();
        settings.EditingCategory = -1;
        settings.Grades.Clear();
        SyncCategorySelectionToView(refresh: true);
        RefreshGradeVisuals();
        RefreshPreview();
        SaveFilterState();
    }
}
