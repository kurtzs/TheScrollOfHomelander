#nullable disable
#pragma warning disable CS0612

using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using TMPro;
using UnityEngine;
using FwUi = FrameWork.UISystem.UIElements;
using SysSetting = Game.Views.SystemSetting;
using UGui = UnityEngine.UI;

namespace BetterTaiwuScroll.Frontend;

[Serializable]
internal sealed class ContinuousMakeSettings
{
    public bool ContinuousMakeEnabled = false;
    public bool IncludeInventory = true;
    public bool IncludePrivateStorage = true;
    public bool IncludePublicStorage = true;
    public int HighestMaterialGrade = 1;
    public int LowestMaterialGrade = 9;
    public int ToolGradePriority = 0;
    public bool AllowBareHand = true;
    public bool EnableDurabilityProtection = true;

    internal void Normalize()
    {
        HighestMaterialGrade = Mathf.Clamp(HighestMaterialGrade, 1, 9);
        LowestMaterialGrade = Mathf.Clamp(LowestMaterialGrade, 1, 9);
        if (HighestMaterialGrade > LowestMaterialGrade)
            LowestMaterialGrade = HighestMaterialGrade;

        ToolGradePriority = Mathf.Clamp(ToolGradePriority, 0, 1);
    }
}

internal static class ContinuousMakeSettingsStore
{
    private const string FileName = "ContinuousMakeSettings.json";

    internal static ContinuousMakeSettings Current { get; private set; } = new ContinuousMakeSettings();

    internal static void Load()
    {
        try
        {
            foreach (var path in GetSettingsPathCandidates())
            {
                if (!File.Exists(path))
                    continue;

                Current = JsonUtility.FromJson<ContinuousMakeSettings>(File.ReadAllText(path)) ?? new ContinuousMakeSettings();
                break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to load continuous make settings: " + ex);
            Current = new ContinuousMakeSettings();
        }

        Current.Normalize();
    }

    internal static void Save()
    {
        try
        {
            Current.Normalize();
            var path = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(Current, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save continuous make settings: " + ex);
        }
    }

    internal static bool IsSourceAllowed(GameData.Domains.Taiwu.ItemSourceType sourceType)
    {
        var settings = Current;
        return sourceType switch
        {
            GameData.Domains.Taiwu.ItemSourceType.Inventory => settings.IncludeInventory,
            GameData.Domains.Taiwu.ItemSourceType.Warehouse => settings.IncludePrivateStorage,
            GameData.Domains.Taiwu.ItemSourceType.Treasury => settings.IncludePublicStorage,
            _ => false
        };
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(GetUserDataRoot(), FileName);
    }

    private static IEnumerable<string> GetSettingsPathCandidates()
    {
        yield return GetSettingsPath();
        yield return GetLegacySettingsPath();
    }

    private static string GetUserDataRoot()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appData))
            appData = Application.persistentDataPath;

        return Path.Combine(appData, "TaiwuStudio", "BetterTaiwuScroll", "UserData");
    }

    private static string GetLegacySettingsPath()
    {
        var pluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
        var modRoot = string.IsNullOrEmpty(pluginDir)
            ? Path.Combine(Application.persistentDataPath, "BetterTaiwuScroll")
            : Path.GetFullPath(Path.Combine(pluginDir, "..", ".."));
        return Path.Combine(modRoot, "UserData", FileName);
    }
}

internal static class NativeContinuousMakeSettingsTemplates
{
    internal static SysSetting.BoolSettingItem BoolSettingPrefab;
    internal static SysSetting.EnumSettingItem EnumSettingPrefab;
    internal static SysSetting.SwitchButtonSettingItem SwitchButtonSettingPrefab;

    private static bool _requesting;

    internal static bool Ready => BoolSettingPrefab != null && EnumSettingPrefab != null && SwitchButtonSettingPrefab != null;

    internal static void Request(Action onReady)
    {
        if (TryFind())
        {
            onReady?.Invoke();
            return;
        }

        if (_requesting)
            return;

        _requesting = true;
        UIElement.SystemSetting.PrepareRes(false, go =>
        {
            _requesting = false;
            CacheFrom(go == null ? null : go.GetComponentInChildren<SysSetting.ViewSystemSetting>(true));
            TryFind();
            onReady?.Invoke();
        });
    }

    private static bool TryFind()
    {
        if (Ready)
            return true;

        foreach (var template in Resources.FindObjectsOfTypeAll<SysSetting.ViewSystemSetting>())
        {
            CacheFrom(template);
            if (Ready)
                return true;
        }

        return false;
    }

    private static void CacheFrom(SysSetting.ViewSystemSetting template)
    {
        if (template == null)
            return;

        var traverse = Traverse.Create(template);
        BoolSettingPrefab ??= traverse.Field("boolSettingPrefab").GetValue<SysSetting.BoolSettingItem>();
        EnumSettingPrefab ??= traverse.Field("enumSettingPrefab").GetValue<SysSetting.EnumSettingItem>();
        SwitchButtonSettingPrefab ??= traverse.Field("switchButtonSettingPrefab").GetValue<SysSetting.SwitchButtonSettingItem>();
    }
}

internal sealed class ContinuousMakeSettingsPanel : MonoBehaviour
{
    private const string PanelName = "BetterTaiwuScrollContinuousMakeSettingsPanel";
    private const string ContentName = "BetterTaiwuScrollContinuousMakeSettingsContent";
    private const string InputBlockerName = "BetterTaiwuScrollContinuousMakeInputBlocker";
    private const float PanelMinWidth = 1220f;
    private const float PanelMinHeight = 820f;
    private const float ContentWidth = 940f;
    private const float ContentHeight = 450f;
    private const float ContentTopOffset = 138f;
    private const float RowHeight = 48f;
    private const float RowSpacing = 8f;
    private const int PanelSortingOrder = 5000;

    private static readonly string[] GradeNames =
    {
        "神·一品",
        "绝·二品",
        "超·三品",
        "极·四品",
        "秘·五品",
        "奇·六品",
        "上·七品",
        "中·八品",
        "下·九品"
    };

    private static readonly string[] ToolPriorityNames =
    {
        "高品",
        "低品"
    };

    private static ContinuousMakeSettingsPanel _current;
    private static bool _requestingArchiveTemplate;
    private static Action _onArchiveTemplateReady;

    private RectTransform _panelRoot;
    private RectTransform _contentRoot;
    private Action _escHandler;
    private readonly List<Action> _controlRebinds = new List<Action>();
    private bool _syncing;
    private bool _masked;

    internal static void Show(ViewMake owner)
    {
        ContinuousMakeSettingsStore.Load();

        if (!NativeContinuousMakeSettingsTemplates.Ready)
        {
            NativeContinuousMakeSettingsTemplates.Request(() => Show(owner));
            return;
        }

        EnsureArchiveTemplate(() => CreateFromRevertArchiveTemplate(UIElement.RevertArchive.UiBase.gameObject));
    }

    internal static void Preload()
    {
        if (!NativeContinuousMakeSettingsTemplates.Ready)
            NativeContinuousMakeSettingsTemplates.Request(null);

        EnsureArchiveTemplate(null);
    }

    private static void EnsureArchiveTemplate(Action onReady)
    {
        if (UIElement.RevertArchive.UiBase != null)
        {
            onReady?.Invoke();
            return;
        }

        if (onReady != null)
            _onArchiveTemplateReady += onReady;

        if (_requestingArchiveTemplate)
            return;

        _requestingArchiveTemplate = true;
        UIElement.RevertArchive.PrepareRes(false, _ =>
        {
            _requestingArchiveTemplate = false;
            var callback = _onArchiveTemplateReady;
            _onArchiveTemplateReady = null;
            callback?.Invoke();
        });
    }

    private static void CreateFromRevertArchiveTemplate(GameObject template)
    {
        if (template == null)
            return;

        if (_current != null)
            _current.Close();

        var sourcePanel = FindRevertArchivePanelSource(template);
        if (sourcePanel == null)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to find revert archive panel source.");
            return;
        }

        Transform parent = UIManager.Instance.GetLayer(UILayer.LayerVeryTop);
        if (parent == null)
            parent = UIManager.Instance.transform;

        var panelObj = Instantiate(sourcePanel.gameObject, parent, false);
        panelObj.name = PanelName;
        panelObj.SetActive(false);

        var panel = panelObj.AddComponent<ContinuousMakeSettingsPanel>();
        panel._panelRoot = panelObj.transform as RectTransform;
        panel.ConfigureClone();
        panel.Build();
        panel.ConfigureInputLayer();
        panel.EnablePanelRendering();

        panelObj.SetActive(true);
        panel.RebindControlEvents();
        panelObj.transform.SetAsLastSibling();
        panel.Mask();
        _current = panel;
    }

    private void ConfigureClone()
    {
        if (_panelRoot != null)
        {
            _panelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRoot.pivot = new Vector2(0.5f, 0.5f);
            _panelRoot.anchoredPosition = Vector2.zero;
            _panelRoot.localScale = Vector3.one;
            SetMinSize(_panelRoot, PanelMinWidth, PanelMinHeight);
        }

        var view = GetComponent<Game.Views.RecordSelect.ViewRevertArchive>();
        TextMeshProUGUI title = null;
        Component scroll = null;
        CButton confirmBtn = null;

        if (view != null)
        {
            var traverse = Traverse.Create(view);
            title = traverse.Field("revertTitle").GetValue<TextMeshProUGUI>();
            scroll = traverse.Field("scroll").GetValue<Component>();
            confirmBtn = traverse.Field("confirmBtn").GetValue<CButton>();
            view.enabled = false;
        }

        if (title == null)
            title = FindTitleText();
        if (scroll == null)
            scroll = FindComponentByTypeName("InfinityScroll");
        if (confirmBtn == null)
            confirmBtn = FindDeep(transform, "EnterGame")?.GetComponent<CButton>();
        if (title != null)
            title.SetText("连续制作设置");

        if (scroll != null)
            scroll.gameObject.SetActive(false);
        if (confirmBtn != null)
            confirmBtn.gameObject.SetActive(false);

        HideByName(transform, "EnterGame");
        HideArchiveHeaderRow();
        BindCloseButton();
        _contentRoot = CreateContentRoot(scroll == null ? null : scroll.transform as RectTransform);
    }

    private void EnablePanelRendering()
    {
        foreach (var group in GetComponentsInChildren<CanvasGroup>(true))
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        var rootCanvas = GetComponent<Canvas>();
        if (rootCanvas != null)
        {
            rootCanvas.enabled = true;
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = 200;
            rootCanvas.worldCamera = UIManager.Instance.UiCamera;
        }

        foreach (var canvas in GetComponentsInChildren<Canvas>(true))
        {
            canvas.enabled = true;
            if (canvas != rootCanvas)
                canvas.worldCamera = UIManager.Instance.UiCamera;
        }

        foreach (var raycaster in GetComponentsInChildren<UGui.GraphicRaycaster>(true))
            raycaster.enabled = true;

        foreach (var graphic in GetComponentsInChildren<UGui.Graphic>(true))
            graphic.enabled = true;
    }

    private static RectTransform FindRevertArchivePanelSource(GameObject template)
    {
        var view = template.GetComponent<Game.Views.RecordSelect.ViewRevertArchive>();
        Transform title = null;
        Transform scroll = null;
        Transform confirm = null;
        Transform close = FindDeep(template.transform, "BtnClose");

        if (view != null)
        {
            var traverse = Traverse.Create(view);
            title = traverse.Field("revertTitle").GetValue<TextMeshProUGUI>()?.transform;
            scroll = traverse.Field("scroll").GetValue<Component>()?.transform;
            confirm = traverse.Field("confirmBtn").GetValue<CButton>()?.transform;
        }

        title ??= FindTitleText(template.transform)?.transform;
        scroll ??= FindComponentByTypeName(template.transform, "InfinityScroll")?.transform;
        confirm ??= FindDeep(template.transform, "EnterGame");

        var panel = CommonAncestor(title, scroll, confirm, close) as RectTransform;
        if (panel == null || panel == template.transform)
            panel = PreferPanelAncestor(title, scroll, confirm, close);

        return panel;
    }

    private static RectTransform PreferPanelAncestor(params Transform[] nodes)
    {
        foreach (var node in nodes)
        {
            var current = node;
            while (current != null)
            {
                var rect = current as RectTransform;
                if (rect != null)
                {
                    var width = Mathf.Abs(rect.rect.width);
                    var height = Mathf.Abs(rect.rect.height);
                    if (width >= 700f && width <= 1800f && height >= 400f && height <= 1200f)
                        return rect;
                }

                current = current.parent;
            }
        }

        return null;
    }

    private static Transform CommonAncestor(params Transform[] nodes)
    {
        var first = Array.Find(nodes, node => node != null);
        if (first == null)
            return null;

        var current = first;
        while (current != null)
        {
            var allInside = true;
            foreach (var node in nodes)
            {
                if (node == null)
                    continue;

                if (!IsAncestorOf(current, node))
                {
                    allInside = false;
                    break;
                }
            }

            if (allInside)
                return current;

            current = current.parent;
        }

        return null;
    }

    private static bool IsAncestorOf(Transform ancestor, Transform node)
    {
        var current = node;
        while (current != null)
        {
            if (current == ancestor)
                return true;
            current = current.parent;
        }

        return false;
    }

    private RectTransform CreateContentRoot(RectTransform oldScrollRect)
    {
        var host = FindContentHost(oldScrollRect);
        if (host == null)
            host = _panelRoot;
        if (host == null)
            return null;

        var old = host.Find(ContentName);
        if (old != null)
            Destroy(old.gameObject);

        var contentObj = new GameObject(ContentName, typeof(RectTransform), typeof(UGui.VerticalLayoutGroup));
        var contentRect = contentObj.transform as RectTransform;
        contentRect.SetParent(host, false);
        contentRect.anchorMin = new Vector2(0.5f, 1f);
        contentRect.anchorMax = new Vector2(0.5f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = new Vector2(0f, -ContentTopOffset);
        contentRect.sizeDelta = new Vector2(ContentWidth, ContentHeight);
        contentRect.localScale = Vector3.one;
        contentRect.SetAsLastSibling();

        var layout = contentObj.GetComponent<UGui.VerticalLayoutGroup>();
        layout.spacing = RowSpacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return contentRect;
    }

    private void ConfigureInputLayer()
    {
        if (_panelRoot == null)
            return;

        var canvas = gameObject.GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = PanelSortingOrder;
        canvas.worldCamera = UIManager.Instance.UiCamera;

        var raycaster = gameObject.GetComponent<ConchShipGraphicRaycaster>() ?? gameObject.AddComponent<ConchShipGraphicRaycaster>();
        raycaster.TargetCamera = UIManager.Instance.UiCamera;
        raycaster.enabled = true;

        var group = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;

        EnsureInputBlocker();
        DisableDecorativeRaycasts();
    }

    private void EnsureInputBlocker()
    {
        var blocker = transform.Find(InputBlockerName) as RectTransform;
        UGui.Image blockerImage;
        if (blocker == null)
        {
            var blockerObj = new GameObject(InputBlockerName, typeof(RectTransform), typeof(UGui.Image));
            blocker = blockerObj.transform as RectTransform;
            blocker.SetParent(_panelRoot, false);
            blockerImage = blockerObj.GetComponent<UGui.Image>();
        }
        else
        {
            blockerImage = blocker.GetComponent<UGui.Image>() ?? blocker.gameObject.AddComponent<UGui.Image>();
        }

        blocker.anchorMin = new Vector2(0.5f, 0.5f);
        blocker.anchorMax = new Vector2(0.5f, 0.5f);
        blocker.pivot = new Vector2(0.5f, 0.5f);
        blocker.anchoredPosition = Vector2.zero;
        blocker.sizeDelta = new Vector2(4096f, 4096f);
        blocker.localScale = Vector3.one;
        blocker.SetAsFirstSibling();

        blockerImage.color = new Color(0f, 0f, 0f, 0f);
        blockerImage.raycastTarget = true;
    }

    private void DisableDecorativeRaycasts()
    {
        foreach (var graphic in GetComponentsInChildren<UGui.Graphic>(true))
        {
            if (graphic == null || graphic.gameObject.name == InputBlockerName)
                continue;

            if (graphic.GetComponentInParent<UGui.Selectable>() != null)
                continue;

            var text = graphic as TextMeshProUGUI;
            if (text != null)
            {
                text.raycastTarget = false;
                continue;
            }

            var rect = graphic.transform as RectTransform;
            if (rect == null)
                continue;

            if (Mathf.Abs(rect.rect.width) >= 360f || Mathf.Abs(rect.rect.height) >= 120f)
                graphic.raycastTarget = false;
        }
    }

    private void Build()
    {
        if (_contentRoot == null)
            return;

        AddBoolRow("是否包括行囊", () => ContinuousMakeSettingsStore.Current.IncludeInventory, value => ContinuousMakeSettingsStore.Current.IncludeInventory = value);
        AddBoolRow("是否包括私库", () => ContinuousMakeSettingsStore.Current.IncludePrivateStorage, value => ContinuousMakeSettingsStore.Current.IncludePrivateStorage = value);
        AddBoolRow("是否包括公库", () => ContinuousMakeSettingsStore.Current.IncludePublicStorage, value => ContinuousMakeSettingsStore.Current.IncludePublicStorage = value);
        AddDropdownRow("允许使用的最高引子品级", GradeNames, () => GradeToIndex(ContinuousMakeSettingsStore.Current.HighestMaterialGrade), OnHighestGradeChanged);
        AddDropdownRow("允许使用的最低引子品级", GradeNames, () => GradeToIndex(ContinuousMakeSettingsStore.Current.LowestMaterialGrade), OnLowestGradeChanged);
        AddSwitchRow("优先使用工具的品级", ToolPriorityNames, () => ContinuousMakeSettingsStore.Current.ToolGradePriority, value => ContinuousMakeSettingsStore.Current.ToolGradePriority = value);
        AddBoolRow("是否允许徒手制作", () => ContinuousMakeSettingsStore.Current.AllowBareHand, value => ContinuousMakeSettingsStore.Current.AllowBareHand = value);
        AddBoolRow("是否开启耐久保护", () => ContinuousMakeSettingsStore.Current.EnableDurabilityProtection, value => ContinuousMakeSettingsStore.Current.EnableDurabilityProtection = value);

        RefreshValues();
    }

    private void RebindControlEvents()
    {
        foreach (var rebind in _controlRebinds)
            rebind();

        RefreshValues();
    }

    private void AddBoolRow(string label, Func<bool> getter, Action<bool> setter)
    {
        var item = Instantiate(NativeContinuousMakeSettingsTemplates.BoolSettingPrefab, _contentRoot);
        item.gameObject.name = "Setting_" + label;
        PrepareRow(item.gameObject, label);

        var toggle = Traverse.Create(item).Field("toggle").GetValue<FwUi.CToggle>();
        if (toggle == null)
            return;

        void Bind()
        {
            toggle.onValueChanged = new UGui.Toggle.ToggleEvent();
            DisableTooltips(toggle.gameObject);
            toggle.SetIsOnWithoutNotify(getter());
            toggle.onValueChanged.AddListener(value =>
            {
                if (_syncing)
                    return;

                setter(value);
                SaveAndRefresh();
            });
        }

        _controlRebinds.Add(Bind);
        Bind();
    }

    private void AddDropdownRow(string label, IReadOnlyList<string> options, Func<int> getter, Action<int> setter)
    {
        var item = Instantiate(NativeContinuousMakeSettingsTemplates.EnumSettingPrefab, _contentRoot);
        item.gameObject.name = "Setting_" + label;
        PrepareRow(item.gameObject, label);

        var dropdown = Traverse.Create(item).Field("dropdown").GetValue<FwUi.CDropdown>();
        if (dropdown == null)
            return;

        void Bind()
        {
            dropdown.onSelect = new FwUi.CDropdown.DropdownEvent();
            dropdown.onValueChanged = new FwUi.CDropdown.DropdownEvent();
            DisableTooltips(dropdown.gameObject);
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(options));
            dropdown.SetValueWithoutNotify(Mathf.Clamp(getter(), 0, options.Count - 1));
            dropdown.onValueChanged.AddListener(index =>
            {
                if (_syncing)
                    return;

                setter(index);
                SaveAndRefresh();
            });
        }

        _controlRebinds.Add(Bind);
        Bind();
    }

    private void AddSwitchRow(string label, IReadOnlyList<string> options, Func<int> getter, Action<int> setter)
    {
        var item = Instantiate(NativeContinuousMakeSettingsTemplates.SwitchButtonSettingPrefab, _contentRoot);
        item.gameObject.name = "Setting_" + label;
        PrepareRow(item.gameObject, label);

        var traverse = Traverse.Create(item);
        var leftBtn = traverse.Field("leftBtn").GetValue<FwUi.CButton>();
        var rightBtn = traverse.Field("rightBtn").GetValue<FwUi.CButton>();
        var valueText = traverse.Field("valueText").GetValue<TextMeshProUGUI>();

        var value = Mathf.Clamp(getter(), 0, options.Count - 1);
        void ApplyValue(int next)
        {
            value = Mathf.Clamp(next, 0, options.Count - 1);
            if (valueText != null)
                valueText.SetText(options[value]);
            if (leftBtn != null)
                leftBtn.interactable = value > 0;
            if (rightBtn != null)
                rightBtn.interactable = value < options.Count - 1;
        }

        void Bind()
        {
            if (leftBtn != null)
                leftBtn.onClick = new UGui.Button.ButtonClickedEvent();
            if (rightBtn != null)
                rightBtn.onClick = new UGui.Button.ButtonClickedEvent();

            value = Mathf.Clamp(getter(), 0, options.Count - 1);
            leftBtn?.onClick.AddListener(() =>
            {
                if (_syncing)
                    return;

                ApplyValue(value - 1);
                setter(value);
                SaveAndRefresh();
            });
            rightBtn?.onClick.AddListener(() =>
            {
                if (_syncing)
                    return;

                ApplyValue(value + 1);
                setter(value);
                SaveAndRefresh();
            });

            ApplyValue(value);
        }

        _controlRebinds.Add(Bind);
        Bind();
    }

    private void PrepareRow(GameObject row, string label)
    {
        row.SetActive(true);
        var rect = row.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, RowHeight);
            rect.localScale = Vector3.one;
        }

        var layoutElement = row.GetComponent<UGui.LayoutElement>() ?? row.AddComponent<UGui.LayoutElement>();
        layoutElement.minWidth = ContentWidth;
        layoutElement.preferredWidth = ContentWidth;
        layoutElement.flexibleWidth = 0f;
        layoutElement.minHeight = RowHeight;
        layoutElement.preferredHeight = RowHeight;
        layoutElement.flexibleHeight = 0f;

        var labelText = Traverse.Create(row.GetComponent<SysSetting.SettingItemBase>()).Field("labelText").GetValue<TextMeshProUGUI>();
        if (labelText != null)
        {
            labelText.SetText(label);
            labelText.enableAutoSizing = true;
            labelText.fontSizeMin = 18f;
            labelText.fontSizeMax = 26f;
            labelText.overflowMode = TextOverflowModes.Ellipsis;
        }

        DisableTooltips(row);
    }

    private void RefreshValues()
    {
        _syncing = true;
        ContinuousMakeSettingsStore.Current.Normalize();

        foreach (var item in GetComponentsInChildren<SysSetting.BoolSettingItem>(true))
        {
            var label = Traverse.Create(item).Field("labelText").GetValue<TextMeshProUGUI>()?.text;
            var toggle = Traverse.Create(item).Field("toggle").GetValue<FwUi.CToggle>();
            if (toggle == null)
                continue;

            toggle.SetIsOnWithoutNotify(label switch
            {
                "是否包括行囊" => ContinuousMakeSettingsStore.Current.IncludeInventory,
                "是否包括私库" => ContinuousMakeSettingsStore.Current.IncludePrivateStorage,
                "是否包括公库" => ContinuousMakeSettingsStore.Current.IncludePublicStorage,
                "是否允许徒手制作" => ContinuousMakeSettingsStore.Current.AllowBareHand,
                "是否开启耐久保护" => ContinuousMakeSettingsStore.Current.EnableDurabilityProtection,
                _ => toggle.isOn
            });
        }

        foreach (var item in GetComponentsInChildren<SysSetting.EnumSettingItem>(true))
        {
            var label = Traverse.Create(item).Field("labelText").GetValue<TextMeshProUGUI>()?.text;
            var dropdown = Traverse.Create(item).Field("dropdown").GetValue<FwUi.CDropdown>();
            if (dropdown == null)
                continue;

            dropdown.SetValueWithoutNotify(label switch
            {
                "允许使用的最高引子品级" => GradeToIndex(ContinuousMakeSettingsStore.Current.HighestMaterialGrade),
                "允许使用的最低引子品级" => GradeToIndex(ContinuousMakeSettingsStore.Current.LowestMaterialGrade),
                _ => dropdown.value
            });
        }

        foreach (var item in GetComponentsInChildren<SysSetting.SwitchButtonSettingItem>(true))
        {
            var label = Traverse.Create(item).Field("labelText").GetValue<TextMeshProUGUI>()?.text;
            if (label != "优先使用工具的品级")
                continue;

            var value = Mathf.Clamp(ContinuousMakeSettingsStore.Current.ToolGradePriority, 0, ToolPriorityNames.Length - 1);
            var traverse = Traverse.Create(item);
            var valueText = traverse.Field("valueText").GetValue<TextMeshProUGUI>();
            var leftBtn = traverse.Field("leftBtn").GetValue<FwUi.CButton>();
            var rightBtn = traverse.Field("rightBtn").GetValue<FwUi.CButton>();
            if (valueText != null)
                valueText.SetText(ToolPriorityNames[value]);
            if (leftBtn != null)
                leftBtn.interactable = value > 0;
            if (rightBtn != null)
                rightBtn.interactable = value < ToolPriorityNames.Length - 1;
        }

        _syncing = false;
    }

    private void OnHighestGradeChanged(int index)
    {
        var grade = IndexToGrade(index);
        ContinuousMakeSettingsStore.Current.HighestMaterialGrade = grade;
        if (ContinuousMakeSettingsStore.Current.LowestMaterialGrade < grade)
            ContinuousMakeSettingsStore.Current.LowestMaterialGrade = grade;
    }

    private void OnLowestGradeChanged(int index)
    {
        var grade = IndexToGrade(index);
        ContinuousMakeSettingsStore.Current.LowestMaterialGrade = grade;
        if (ContinuousMakeSettingsStore.Current.HighestMaterialGrade > grade)
            ContinuousMakeSettingsStore.Current.HighestMaterialGrade = grade;
    }

    private void SaveAndRefresh()
    {
        ContinuousMakeSettingsStore.Save();
        RefreshValues();
    }

    private void BindCloseButton()
    {
        var close = FindDeep(transform, "BtnClose");
        if (close == null)
            return;

        var hasButton = false;
        foreach (var button in close.GetComponentsInChildren<CButton>(true))
        {
            button.ClearAndAddListener(Close);
            hasButton = true;
        }
        foreach (var button in close.GetComponentsInChildren<CButtonObsolete>(true))
        {
            button.ClearAndAddListener(Close);
            hasButton = true;
        }

        if (hasButton)
            return;

        foreach (var button in close.GetComponentsInParent<CButton>(true))
            button.ClearAndAddListener(Close);
        foreach (var button in close.GetComponentsInParent<CButtonObsolete>(true))
            button.ClearAndAddListener(Close);
    }

    private void Mask()
    {
        if (_panelRoot == null || _masked)
            return;

        UIManager.Instance.MaskComponent(_panelRoot);
        RegisterEscHandler();
        _masked = true;
    }

    private void Close()
    {
        UnregisterEscHandler();
        if (_masked && _panelRoot != null)
        {
            UIManager.Instance.UnMaskComponent(_panelRoot);
            _masked = false;
        }

        if (_current == this)
            _current = null;

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        UnregisterEscHandler();
        if (_masked && _panelRoot != null && UIManager.Instance != null)
        {
            UIManager.Instance.UnMaskComponent(_panelRoot);
            _masked = false;
        }

        if (_current == this)
            _current = null;
    }

    private void RegisterEscHandler()
    {
        if (UIManager.Instance == null)
            return;

        _escHandler ??= Close;
        UIManager.Instance.SetEscHandler(_escHandler);
    }

    private void UnregisterEscHandler()
    {
        if (UIManager.Instance == null || _escHandler == null)
            return;

        if (UIManager.Instance.CheckEscHandler(_escHandler))
            UIManager.Instance.SetEscHandler(null);
    }

    private TextMeshProUGUI FindTitleText()
    {
        return FindTitleText(transform);
    }

    private Component FindComponentByTypeName(string typeName)
    {
        return FindComponentByTypeName(transform, typeName);
    }

    private static TextMeshProUGUI FindTitleText(Transform root)
    {
        if (root == null)
            return null;

        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text == null)
                continue;

            var name = text.gameObject.name;
            if (!string.IsNullOrEmpty(name) && name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0)
                return text;
        }

        return null;
    }

    private static Component FindComponentByTypeName(Transform root, string typeName)
    {
        if (root == null || string.IsNullOrEmpty(typeName))
            return null;

        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null)
                continue;

            var type = component.GetType();
            if (type.Name == typeName || type.FullName == typeName)
                return component;
        }

        return null;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;

        for (var i = 0; i < root.childCount; i++)
        {
            var found = FindDeep(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static void HideByName(Transform root, string name)
    {
        var target = FindDeep(root, name);
        if (target != null)
            target.gameObject.SetActive(false);
    }

    private void HideArchiveHeaderRow()
    {
        var headerTexts = new List<Transform>();
        foreach (var text in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text == null)
                continue;

            switch (text.text)
            {
                case "头像":
                case "名字":
                case "第几世":
                case "存档时间":
                case "所在地点":
                    headerTexts.Add(text.transform);
                    break;
            }
        }

        if (headerTexts.Count == 0)
            return;

        var header = FindHeaderRowAncestor(headerTexts);
        if (header != null)
        {
            header.gameObject.SetActive(false);
            return;
        }

        foreach (var text in headerTexts)
            text.gameObject.SetActive(false);
    }

    private RectTransform FindContentHost(RectTransform oldScrollRect)
    {
        var best = FindPanelGraphicHost();
        if (best != null)
            return best;

        var oldParent = oldScrollRect == null ? null : oldScrollRect.parent as RectTransform;
        return oldParent != null && oldParent != _panelRoot ? oldParent : _panelRoot;
    }

    private RectTransform FindPanelGraphicHost()
    {
        if (_panelRoot == null)
            return null;

        RectTransform best = null;
        var bestScore = float.MinValue;
        foreach (var graphic in _panelRoot.GetComponentsInChildren<UGui.Graphic>(true))
        {
            if (graphic == null || graphic is TextMeshProUGUI)
                continue;

            var rect = graphic.transform as RectTransform;
            if (rect == null || rect == _panelRoot)
                continue;

            var width = Mathf.Abs(rect.rect.width);
            var height = Mathf.Abs(rect.rect.height);
            if (width < 900f || width > 1400f || height < 520f || height > 950f)
                continue;

            var score = width * height - Mathf.Abs(width - PanelMinWidth) * 250f - Mathf.Abs(height - PanelMinHeight) * 250f;
            if (score <= bestScore)
                continue;

            bestScore = score;
            best = rect;
        }

        return best;
    }

    private static Transform FindHeaderRowAncestor(IReadOnlyList<Transform> headerTexts)
    {
        var nodes = new Transform[headerTexts.Count];
        for (var i = 0; i < headerTexts.Count; i++)
            nodes[i] = headerTexts[i];

        var common = CommonAncestor(nodes) as RectTransform;
        var current = common;
        while (current != null)
        {
            var height = Mathf.Abs(current.rect.height);
            var width = Mathf.Abs(current.rect.width);
            if (height > 16f && height <= 90f && width >= 700f)
                return current;

            current = current.parent as RectTransform;
        }

        return null;
    }

    private static void SetMinSize(RectTransform rect, float width, float height)
    {
        if (rect == null)
            return;

        var currentWidth = Mathf.Abs(rect.rect.width);
        var currentHeight = Mathf.Abs(rect.rect.height);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(currentWidth, width));
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(currentHeight, height));
    }

    private static int GradeToIndex(int grade)
    {
        return Mathf.Clamp(grade, 1, 9) - 1;
    }

    private static int IndexToGrade(int index)
    {
        return Mathf.Clamp(index, 0, 8) + 1;
    }

    private static void DisableTooltips(GameObject obj)
    {
        foreach (var tooltip in obj.GetComponentsInChildren<TooltipInvoker>(true))
            tooltip.enabled = false;
    }
}
