#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using GameData.Domains.Taiwu.ExchangeSystem;
using ItemListScroll = Game.Components.ListStyleGeneralScroll.Item.ItemListScroll;
using FilterConfig = Game.Components.SortAndFilter.SortAndFilterConfig;
using FilterDetailedLineState = Game.Components.SortAndFilter.DetailedFilterLineState;
using FilterDetailedState = Game.Components.SortAndFilter.DetailedFilterState;
using FilterLineState = Game.Components.SortAndFilter.LineState;
using FilterMenuState = Game.Components.SortAndFilter.DetailedFilterMenuState;
using FilterView = Game.Components.SortAndFilter.SortAndFilter;
using SortButtonGroup = Game.Components.SortAndFilter.SortButtonGroup;
using SortItemState = Game.Components.SortAndFilter.SortItemState;
using SortStateData = Game.Components.SortAndFilter.SortStateData;
using FilterToggleKey = Game.Components.SortAndFilter.ToggleKey;
using DebateView = Game.Views.Debate.ViewDebate;
using LifeSkillCombatBeginView = Game.Views.LifeSkillCombat.ViewLifeSkillCombatBegin;
using ToggleGroup = FrameWork.UISystem.UIElements.CToggleGroup;

namespace BetterTaiwuScroll.Frontend;

[Serializable]
public sealed class MemoryOptimizationSettings
{
    public List<FilterMemoryEntry> FilterMemories = new List<FilterMemoryEntry>();
    public List<SortMemoryEntry> SortMemories = new List<SortMemoryEntry>();
    public List<StrategyPresetMemoryEntry> StrategyPresetMemories = new List<StrategyPresetMemoryEntry>();
    public bool HasLifeSkillAutoMode;
    public bool LifeSkillAutoMode;

    internal void Normalize()
    {
        FilterMemories ??= new List<FilterMemoryEntry>();
        SortMemories ??= new List<SortMemoryEntry>();
        StrategyPresetMemories ??= new List<StrategyPresetMemoryEntry>();

        FilterMemories.RemoveAll(entry => entry == null || string.IsNullOrEmpty(entry.Key));
        foreach (var entry in FilterMemories)
            entry.Normalize();

        SortMemories.RemoveAll(entry => entry == null || string.IsNullOrEmpty(entry.Key));
        foreach (var entry in SortMemories)
            entry.Normalize();

        StrategyPresetMemories.RemoveAll(entry => entry == null);
        foreach (var entry in StrategyPresetMemories)
            entry.PresetIndex = Mathf.Clamp(entry.PresetIndex, 0, 8);
    }
}

[Serializable]
public sealed class FilterMemoryEntry
{
    public string Key;
    public string Signature;
    public List<FilterLineMemory> Lines = new List<FilterLineMemory>();

    internal void Normalize()
    {
        Lines ??= new List<FilterLineMemory>();
        Lines.RemoveAll(line => line == null);
        foreach (var line in Lines)
            line.Normalize();
    }
}

[Serializable]
public sealed class FilterLineMemory
{
    public int LineId;
    public int Type;
    public bool IsActive = true;
    public bool ToggleIsAll = true;
    public int ToggleIndex = -1;
    public List<FilterMenuMemory> Menus = new List<FilterMenuMemory>();

    internal void Normalize()
    {
        Menus ??= new List<FilterMenuMemory>();
        Menus.RemoveAll(menu => menu == null);
        foreach (var menu in Menus)
            menu.Normalize();
    }
}

[Serializable]
public sealed class FilterMenuMemory
{
    public int MenuId;
    public bool IsActive;
    public List<int> SelectedIndices = new List<int>();

    internal void Normalize()
    {
        SelectedIndices ??= new List<int>();
        SelectedIndices.RemoveAll(index => index < 0);
    }
}

[Serializable]
public sealed class SortMemoryEntry
{
    public string Key;
    public string Signature;
    public List<SortItemMemory> Items = new List<SortItemMemory>();

    internal void Normalize()
    {
        Items ??= new List<SortItemMemory>();
        Items.RemoveAll(item => item == null || item.SortId < 0);
    }
}

[Serializable]
public sealed class SortItemMemory
{
    public short SortId;
    public int Direction;
}

[Serializable]
public sealed class StrategyPresetMemoryEntry
{
    public int LifeSkillType;
    public int PresetIndex;
}

internal static class MemoryOptimizationSettingsStore
{
    private const string FileName = "MemoryOptimizationSettings.json";

    internal static MemoryOptimizationSettings Current { get; private set; } = new MemoryOptimizationSettings();

    internal static void Load()
    {
        try
        {
            foreach (var path in GetSettingsPathCandidates())
            {
                if (!File.Exists(path))
                    continue;

                Current = JsonUtility.FromJson<MemoryOptimizationSettings>(File.ReadAllText(path)) ?? new MemoryOptimizationSettings();
                break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to load memory optimization settings: " + ex);
            Current = new MemoryOptimizationSettings();
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
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save memory optimization settings: " + ex);
        }
    }

    internal static FilterMemoryEntry GetFilterMemory(string key, string signature)
    {
        Current.Normalize();
        return Current.FilterMemories.FirstOrDefault(entry => entry.Key == key && entry.Signature == signature);
    }

    internal static void SetFilterMemory(FilterMemoryEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.Key))
            return;

        Current.Normalize();
        var index = Current.FilterMemories.FindIndex(item => item.Key == entry.Key && item.Signature == entry.Signature);
        if (index >= 0)
            Current.FilterMemories[index] = entry;
        else
            Current.FilterMemories.Add(entry);

        Save();
    }

    internal static SortMemoryEntry GetSortMemory(string key, string signature)
    {
        Current.Normalize();
        return Current.SortMemories.FirstOrDefault(entry => entry.Key == key && entry.Signature == signature);
    }

    internal static void SetSortMemory(SortMemoryEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.Key))
            return;

        Current.Normalize();
        var index = Current.SortMemories.FindIndex(item => item.Key == entry.Key && item.Signature == entry.Signature);
        if (index >= 0)
            Current.SortMemories[index] = entry;
        else
            Current.SortMemories.Add(entry);

        Save();
    }

    internal static int GetStrategyPreset(sbyte lifeSkillType)
    {
        Current.Normalize();
        var entry = Current.StrategyPresetMemories.FirstOrDefault(item => item.LifeSkillType == lifeSkillType);
        return entry != null ? Mathf.Clamp(entry.PresetIndex, 0, 8) : -1;
    }

    internal static void SetStrategyPreset(sbyte lifeSkillType, int presetIndex)
    {
        if (lifeSkillType < 0 || presetIndex < 0)
            return;

        Current.Normalize();
        presetIndex = Mathf.Clamp(presetIndex, 0, 8);
        var entry = Current.StrategyPresetMemories.FirstOrDefault(item => item.LifeSkillType == lifeSkillType);
        if (entry == null)
        {
            entry = new StrategyPresetMemoryEntry { LifeSkillType = lifeSkillType };
            Current.StrategyPresetMemories.Add(entry);
        }

        entry.PresetIndex = presetIndex;
        Save();
    }

    internal static void SetLifeSkillAutoMode(bool isAuto)
    {
        Current.HasLifeSkillAutoMode = true;
        Current.LifeSkillAutoMode = isAuto;
        Save();
    }

    private static string GetSettingsPath()
    {
        return ModUserDataPaths.GetFilePath(FileName);
    }

    private static IEnumerable<string> GetSettingsPathCandidates()
    {
        yield return GetSettingsPath();
    }
}

internal static class FilterMemoryController
{
    private static bool _applying;
    private static readonly MethodInfo ItemListScrollOnSortAndFilterChangedMethod =
        AccessTools.Method(typeof(ItemListScroll), "OnSortAndFilterChanged");

    internal static void TryRestore(FilterView sortAndFilter, ItemListScroll ownerList = null)
    {
        if (!Plugin.EnableFilterMemory || sortAndFilter?.Config?.LineConfigs == null)
            return;

        var key = BuildKey(sortAndFilter);
        var signature = BuildSignature(sortAndFilter.Config);
        var entry = MemoryOptimizationSettingsStore.GetFilterMemory(key, signature);
        if (entry?.Lines == null || entry.Lines.Count == 0)
            return;

        var lineStates = BuildLineStates(sortAndFilter.Config, entry);
        if (lineStates == null || lineStates.Count == 0)
            return;

        try
        {
            _applying = true;
            sortAndFilter.ApplyFilterLineStates(lineStates);
            NotifyRestoredFilterChanged(sortAndFilter, lineStates);
            ForceOwnerListRefresh(sortAndFilter, ownerList);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to restore filter memory: " + ex);
        }
        finally
        {
            _applying = false;
        }
    }

    internal static void ScheduleRestore(ItemListScroll itemListScroll)
    {
        if (!Plugin.EnableFilterMemory || itemListScroll == null)
            return;

        try
        {
            var sortAndFilter = Traverse.Create(itemListScroll).Field("sortAndFilter").GetValue<FilterView>();
            FilterMemoryRestoreState.GetOrAdd(sortAndFilter)?.Schedule(1, itemListScroll);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to schedule item list filter memory restore: " + ex);
        }
    }

    private static void NotifyRestoredFilterChanged(FilterView sortAndFilter, IReadOnlyList<FilterLineState> lineStates)
    {
        var config = sortAndFilter?.Config;
        if (config?.LineConfigs == null || config.OnFilterChanged == null || lineStates == null)
            return;

        var notified = new HashSet<int>();
        var count = Math.Min(config.LineConfigs.Count, lineStates.Count);
        if (count > 0)
        {
            var lineId = config.LineConfigs[0].Id;
            if (lineId >= 0 && notified.Add(lineId))
                config.OnFilterChanged.Invoke(lineId);
        }

        for (var i = 1; i < count; i++)
        {
            var lineState = lineStates[i];
            if (!HasActiveDetailedSelection(lineState))
                continue;

            var lineId = config.LineConfigs[i].Id;
            if (lineId >= 0 && notified.Add(lineId))
                config.OnFilterChanged.Invoke(lineId);
        }

        if (notified.Count == 0)
            config.OnFilterChanged.Invoke(-1);
    }

    private static bool HasActiveDetailedSelection(FilterLineState lineState)
    {
        var menuStates = lineState.DetailedFilterState?.State.MenuStateDict;
        if (menuStates == null)
            return false;

        foreach (var pair in menuStates)
        {
            if (pair.Value.IsActive && pair.Value.SelectedIndices != null && pair.Value.SelectedIndices.Count > 0)
                return true;
        }

        return false;
    }

    private static void ForceOwnerListRefresh(FilterView sortAndFilter, ItemListScroll ownerList)
    {
        try
        {
            var itemListScroll = ownerList ?? (sortAndFilter == null ? null : sortAndFilter.GetComponentInParent<ItemListScroll>(true));
            var controller = itemListScroll?.SortAndFilterController;
            if (controller != null)
                controller.GenerateFilter();

            ItemListScrollOnSortAndFilterChangedMethod?.Invoke(itemListScroll, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to refresh item list after filter memory restore: " + ex);
        }
    }

    internal static void TrySave(FilterView sortAndFilter)
    {
        if (_applying || !Plugin.EnableFilterMemory || sortAndFilter?.Config?.LineConfigs == null)
            return;

        try
        {
            var state = sortAndFilter.GetStateFromUI();
            if (state.LineStates == null || state.LineStates.Count == 0)
                return;

            var entry = new FilterMemoryEntry
            {
                Key = BuildKey(sortAndFilter),
                Signature = BuildSignature(sortAndFilter.Config),
                Lines = BuildLineMemories(sortAndFilter.Config, state.LineStates)
            };

            MemoryOptimizationSettingsStore.SetFilterMemory(entry);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save filter memory: " + ex);
        }
    }

    private static List<FilterLineMemory> BuildLineMemories(FilterConfig config, IReadOnlyList<FilterLineState> lineStates)
    {
        var result = new List<FilterLineMemory>();
        var count = Math.Min(config.LineConfigs.Count, lineStates.Count);
        for (var i = 0; i < count; i++)
        {
            var lineConfig = config.LineConfigs[i];
            var lineState = lineStates[i];
            var memory = new FilterLineMemory
            {
                LineId = lineConfig.Id,
                Type = (int)lineConfig.Type,
                IsActive = lineState.IsActive,
                ToggleIsAll = lineState.ToggleGroupState.IsAll,
                ToggleIndex = lineState.ToggleGroupState.Index,
                Menus = new List<FilterMenuMemory>()
            };

            var menuStates = lineState.DetailedFilterState?.State.MenuStateDict;
            if (menuStates != null)
            {
                foreach (var pair in menuStates.OrderBy(pair => pair.Key))
                {
                    memory.Menus.Add(new FilterMenuMemory
                    {
                        MenuId = pair.Key,
                        IsActive = pair.Value.IsActive,
                        SelectedIndices = pair.Value.SelectedIndices?.ToList() ?? new List<int>()
                    });
                }
            }

            result.Add(memory);
        }

        return result;
    }

    private static List<FilterLineState> BuildLineStates(FilterConfig config, FilterMemoryEntry entry)
    {
        if (config.LineConfigs.Count != entry.Lines.Count)
            return null;

        var result = new List<FilterLineState>();
        for (var i = 0; i < config.LineConfigs.Count; i++)
        {
            var lineConfig = config.LineConfigs[i];
            var memory = entry.Lines[i];
            if (memory.LineId != lineConfig.Id || memory.Type != (int)lineConfig.Type)
                return null;

            var menuStates = new Dictionary<int, FilterMenuState>();
            foreach (var menu in memory.Menus)
                menuStates[menu.MenuId] = new FilterMenuState(menu.SelectedIndices ?? new List<int>(), menu.IsActive);

            result.Add(new FilterLineState
            {
                IsActive = memory.IsActive,
                Type = lineConfig.Type,
                ToggleGroupState = new FilterToggleKey
                {
                    IsAll = memory.ToggleIsAll,
                    Index = memory.ToggleIsAll ? -1 : memory.ToggleIndex
                },
                DetailedFilterState = new FilterDetailedLineState
                {
                    State = new FilterDetailedState
                    {
                        MenuStateDict = menuStates
                    }
                }
            });
        }

        return result;
    }

    private static string BuildKey(FilterView sortAndFilter)
    {
        var viewName = FindOwnerViewName(sortAndFilter.transform);
        var path = BuildShortPath(sortAndFilter.transform);
        return viewName + "|" + path;
    }

    internal static string FindOwnerViewName(Transform transform)
    {
        var current = transform;
        while (current != null)
        {
            foreach (var component in current.GetComponents<MonoBehaviour>())
            {
                if (component == null)
                    continue;

                var type = component.GetType();
                if (type == typeof(FilterView) || type.Namespace == "Game.Components.SortAndFilter")
                    continue;

                if (type.Namespace != null && type.Namespace.StartsWith("Game.Views", StringComparison.Ordinal))
                    return type.FullName;

                if (type.Name.StartsWith("UI_", StringComparison.Ordinal))
                    return type.FullName;
            }

            current = current.parent;
        }

        return "UnknownView";
    }

    internal static string BuildShortPath(Transform transform)
    {
        var names = new Stack<string>();
        var current = transform;
        while (current != null && names.Count < 5)
        {
            names.Push(current.name.Replace("(Clone)", string.Empty));
            current = current.parent;
        }

        return string.Join("/", names.ToArray());
    }

    private static string BuildSignature(FilterConfig config)
    {
        var parts = new List<string>();
        foreach (var line in config.LineConfigs)
        {
            var menuIds = line.DetailedFilterLineConfig?.Config.MenuConfigs == null
                ? string.Empty
                : string.Join(",", line.DetailedFilterLineConfig.Config.MenuConfigs.Select(menu => menu.Id.ToString()).ToArray());
            parts.Add($"{line.Id}:{(int)line.Type}:{menuIds}");
        }

        return string.Join("|", parts.ToArray());
    }
}

[HarmonyPatch(typeof(SortButtonGroup), "RefreshAll")]
internal static class SortButtonGroupRefreshAllMemoryPatch
{
    private static void Postfix(SortButtonGroup __instance)
    {
        SortMemoryController.ScheduleRestore(__instance);
    }
}

[HarmonyPatch]
internal static class SortButtonGroupClickMemoryPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(SortButtonGroup), "OnClickItem");
    }

    private static void Postfix(SortButtonGroup __instance)
    {
        SortMemoryController.TrySave(__instance);
    }
}

[HarmonyPatch(typeof(SortButtonGroup), "SetSortData")]
internal static class SortButtonGroupSetSortDataMemoryPatch
{
    private static void Postfix(SortButtonGroup __instance)
    {
        SortMemoryController.ScheduleRestore(__instance);
    }
}

internal static class SortMemoryController
{
    internal static bool SuppressSave;

    internal static void ScheduleRestore(SortButtonGroup sortButtonGroup)
    {
        if (!Plugin.EnableSortMemory || sortButtonGroup == null)
            return;

        SortMemoryRestoreState.GetOrAdd(sortButtonGroup)?.Schedule();
    }

    internal static bool TryRestore(SortButtonGroup sortButtonGroup)
    {
        if (!Plugin.EnableSortMemory || sortButtonGroup == null)
            return true;

        if (!TryGetVisibleSortIds(sortButtonGroup, out var validSortIds))
            return false;

        var key = BuildKey(sortButtonGroup.transform);
        var signature = BuildSignature(sortButtonGroup);
        var entry = MemoryOptimizationSettingsStore.GetSortMemory(key, signature);
        if (entry?.Items == null || entry.Items.Count == 0)
        {
            ClearInheritedSortState(sortButtonGroup);
            return true;
        }

        var itemStates = new List<SortItemState>();
        foreach (var item in entry.Items)
        {
            if (item == null || item.SortId < 0 || (validSortIds.Count > 0 && !validSortIds.Contains(item.SortId)))
                continue;

            itemStates.Add(new SortItemState
            {
                SortId = item.SortId,
                SortDirection = (Game.Components.SortAndFilter.ESortDirection)item.Direction
            });
        }

        if (itemStates.Count == 0)
        {
            ClearInheritedSortState(sortButtonGroup);
            return true;
        }

        var currentData = sortButtonGroup.GetSortData();
        if (IsSameSortState(currentData?.ItemStates, itemStates))
            return true;

        try
        {
            SuppressSave = true;
            sortButtonGroup.SetSortData(new SortStateData { ItemStates = itemStates });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to restore sort memory: " + ex);
        }
        finally
        {
            SuppressSave = false;
        }

        return true;
    }

    private static void ClearInheritedSortState(SortButtonGroup sortButtonGroup)
    {
        var currentData = sortButtonGroup.GetSortData();
        if (currentData?.ItemStates == null || currentData.ItemStates.Count == 0)
            return;

        try
        {
            SuppressSave = true;
            sortButtonGroup.SetSortData(new SortStateData { ItemStates = new List<SortItemState>() });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to clear inherited sort memory: " + ex);
        }
        finally
        {
            SuppressSave = false;
        }
    }

    internal static void TrySave(SortButtonGroup sortButtonGroup)
    {
        if (SuppressSave || !Plugin.EnableSortMemory || sortButtonGroup == null)
            return;

        try
        {
            var data = sortButtonGroup.GetSortData();
            if (!TryGetVisibleSortIds(sortButtonGroup, out var visibleSortIds))
                return;

            var entry = new SortMemoryEntry
            {
                Key = BuildKey(sortButtonGroup.transform),
                Signature = BuildSignature(sortButtonGroup),
                Items = (data?.ItemStates ?? new List<SortItemState>())
                    .Where(item => item.SortId >= 0
                        && visibleSortIds.Contains(item.SortId)
                        && item.SortDirection != Game.Components.SortAndFilter.ESortDirection.None)
                    .Select(item => new SortItemMemory
                    {
                        SortId = item.SortId,
                        Direction = (int)item.SortDirection
                    })
                    .ToList()
            };

            MemoryOptimizationSettingsStore.SetSortMemory(entry);
            SortMemoryRestoreState.GetOrAdd(sortButtonGroup)?.MarkUserSaved();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save sort memory: " + ex);
        }
    }

    private static string BuildKey(Transform transform)
    {
        var viewName = FilterMemoryController.FindOwnerViewName(transform);
        var path = FilterMemoryController.BuildShortPath(transform);
        return viewName + "|" + path;
    }

    private static string BuildSignature(SortButtonGroup sortButtonGroup)
    {
        var allSortIds = GetSortIds(sortButtonGroup);
        var visiblePart = TryGetVisibleSortIds(sortButtonGroup, out var visibleSortIds)
            ? JoinSortIds(visibleSortIds)
            : "pending";
        return "all:" + JoinSortIds(allSortIds)
            + "|visible:" + visiblePart
            + "|filter:" + BuildFilterStateSignature(sortButtonGroup);
    }

    internal static string GetCurrentSignature(SortButtonGroup sortButtonGroup)
    {
        return BuildSignature(sortButtonGroup);
    }

    private static string JoinSortIds(IEnumerable<short> sortIds)
    {
        return string.Join(",", sortIds.OrderBy(id => id).Select(id => id.ToString()).ToArray());
    }

    private static HashSet<short> GetSortIds(SortButtonGroup sortButtonGroup)
    {
        var result = new HashSet<short>();
        var sortIds = Traverse.Create(sortButtonGroup).Field("_sortIds").GetValue<List<short>>();
        if (sortIds != null)
        {
            foreach (var id in sortIds)
            {
                if (id >= 0)
                    result.Add(id);
            }
        }

        var displaying = sortButtonGroup.DisplayingSortIds;
        if (displaying != null)
        {
            foreach (var id in displaying)
            {
                if (id >= 0)
                    result.Add(id);
            }
        }

        return result;
    }

    private static bool TryGetVisibleSortIds(SortButtonGroup sortButtonGroup, out HashSet<short> result)
    {
        result = new HashSet<short>();
        var displaying = sortButtonGroup.DisplayingSortIds;
        if (displaying == null || displaying.Count == 0)
            return false;

        foreach (var id in displaying)
        {
            if (id >= 0)
                result.Add(id);
        }

        return result.Count > 0;
    }

    private static string BuildFilterStateSignature(SortButtonGroup sortButtonGroup)
    {
        try
        {
            var owner = sortButtonGroup.GetComponentInParent<FilterView>(true);
            var states = owner?.GetStateFromUI().LineStates;
            if (states == null || states.Count == 0)
                return string.Empty;

            var parts = new List<string>();
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                var toggle = state.ToggleGroupState;
                var menuText = string.Empty;
                var menuStates = state.DetailedFilterState?.State.MenuStateDict;
                if (menuStates != null && menuStates.Count > 0)
                {
                    menuText = string.Join(",", menuStates
                        .OrderBy(pair => pair.Key)
                        .Select(pair =>
                        {
                            var indices = pair.Value.SelectedIndices == null
                                ? string.Empty
                                : string.Join(".", pair.Value.SelectedIndices);
                            return pair.Key + ":" + pair.Value.IsActive + ":" + indices;
                        })
                        .ToArray());
                }

                parts.Add(i + ":" + state.IsActive + ":" + (int)state.Type + ":"
                    + toggle.IsAll + ":" + toggle.Index + ":" + menuText);
            }

            return string.Join("|", parts.ToArray());
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsSameSortState(IReadOnlyList<SortItemState> current, IReadOnlyList<SortItemState> expected)
    {
        if (current == null)
            return expected == null || expected.Count == 0;

        if (expected == null || current.Count != expected.Count)
            return false;

        for (var i = 0; i < current.Count; i++)
        {
            if (current[i].SortId != expected[i].SortId || current[i].SortDirection != expected[i].SortDirection)
                return false;
        }

        return true;
    }
}

internal sealed class SortMemoryRestoreState : MonoBehaviour
{
    private const int RefreshFrames = 20;

    private SortButtonGroup _owner;
    private int _remainingFrames;
    private int _skipUntilFrame;
    private string _completedSignature;

    internal static SortMemoryRestoreState GetOrAdd(SortButtonGroup owner)
    {
        if (owner == null)
            return null;

        var state = owner.GetComponent<SortMemoryRestoreState>();
        if (state == null)
            state = owner.gameObject.AddComponent<SortMemoryRestoreState>();

        state._owner = owner;
        return state;
    }

    internal void Schedule()
    {
        if (_owner != null && Time.frameCount < _skipUntilFrame)
            return;

        var signature = _owner != null ? SortMemoryController.GetCurrentSignature(_owner) : string.Empty;
        if (!string.IsNullOrEmpty(signature) && signature == _completedSignature)
            return;

        _remainingFrames = RefreshFrames;
    }

    internal void MarkUserSaved()
    {
        _completedSignature = _owner != null ? SortMemoryController.GetCurrentSignature(_owner) : _completedSignature;
        _skipUntilFrame = Time.frameCount + 60;
        _remainingFrames = 0;
    }

    private void LateUpdate()
    {
        if (_remainingFrames <= 0)
            return;

        _remainingFrames--;
        if (_owner == null || !_owner.gameObject.activeInHierarchy)
            return;

        if (SortMemoryController.TryRestore(_owner))
        {
            _completedSignature = SortMemoryController.GetCurrentSignature(_owner);
            _remainingFrames = 0;
        }
        else if (_remainingFrames <= 0)
        {
            _remainingFrames = 2;
        }
    }
}

[HarmonyPatch(typeof(FilterView), "Setup")]
internal static class SortAndFilterSetupMemoryPatch
{
    private static void Postfix(FilterView __instance)
    {
        FilterMemoryRestoreState.GetOrAdd(__instance)?.Schedule();
    }
}

internal sealed class FilterMemoryRestoreState : MonoBehaviour
{
    private const int RestoreFrames = 2;

    private FilterView _owner;
    private ItemListScroll _ownerList;
    private int _remainingFrames;

    internal static FilterMemoryRestoreState GetOrAdd(FilterView owner)
    {
        if (owner == null)
            return null;

        var state = owner.GetComponent<FilterMemoryRestoreState>();
        if (state == null)
            state = owner.gameObject.AddComponent<FilterMemoryRestoreState>();

        state._owner = owner;
        return state;
    }

    internal void Schedule(int frames = RestoreFrames, ItemListScroll ownerList = null)
    {
        if (ownerList != null)
            _ownerList = ownerList;

        _remainingFrames = Math.Max(1, frames);
    }

    private void LateUpdate()
    {
        if (_remainingFrames <= 0)
            return;

        _remainingFrames--;
        if (_remainingFrames > 0)
            return;

        if (_owner == null || !_owner.gameObject.activeInHierarchy)
            return;

        FilterMemoryController.TryRestore(_owner, _ownerList);
    }
}

[HarmonyPatch(typeof(ItemListScroll), "SetItemList", new[] { typeof(IReadOnlyList<ITradeableContent>) })]
internal static class ItemListScrollSetItemListFilterMemoryPatch
{
    private static void Postfix(ItemListScroll __instance)
    {
        FilterMemoryController.ScheduleRestore(__instance);
    }
}

[HarmonyPatch(typeof(ItemListScroll), "SetItemList", new[] { typeof(IReadOnlyList<ITradeableContent>), typeof(int) })]
internal static class ItemListScrollSetItemListWithSelectedIndexFilterMemoryPatch
{
    private static void Postfix(ItemListScroll __instance)
    {
        FilterMemoryController.ScheduleRestore(__instance);
    }
}

[HarmonyPatch(typeof(FilterView), "SetDropdownOption")]
internal static class SortAndFilterSetDropdownOptionMemoryPatch
{
    private static void Postfix(FilterView __instance)
    {
        FilterMemoryController.TrySave(__instance);
    }
}

[HarmonyPatch(typeof(FilterView), "SetToggleIsOn")]
internal static class SortAndFilterSetToggleIsOnMemoryPatch
{
    private static void Postfix(FilterView __instance)
    {
        FilterMemoryController.TrySave(__instance);
    }
}

[HarmonyPatch(typeof(FilterView), "SetToggleIsOnWithoutNotify")]
internal static class SortAndFilterSetToggleIsOnWithoutNotifyMemoryPatch
{
    private static void Postfix(FilterView __instance)
    {
        // Internal sync calls should not overwrite the last user-selected filter memory.
    }
}

[HarmonyPatch(typeof(FilterView), "ApplyFilterLineStates")]
internal static class SortAndFilterApplyFilterLineStatesMemoryPatch
{
    private static void Postfix(FilterView __instance)
    {
        FilterMemoryController.TrySave(__instance);
    }
}

[HarmonyPatch(typeof(FilterView), "ClearAllFilter")]
internal static class SortAndFilterClearAllFilterMemoryPatch
{
    private static void Postfix(FilterView __instance)
    {
        FilterMemoryController.TrySave(__instance);
    }
}

[HarmonyPatch(typeof(LifeSkillCombatBeginView), "OnPresetStrategyToggleGroupActiveIndexChange")]
internal static class LifeSkillCombatPresetStrategySavePatch
{
    private static void Postfix(LifeSkillCombatBeginView __instance, int newIndex)
    {
        if (!Plugin.EnableStrategyPresetMemory || LifeSkillCombatPresetStrategyMemoryController.SuppressSave || __instance == null || newIndex < 0)
            return;

        try
        {
            var lifeSkillType = Traverse.Create(__instance).Field("_selectedSkillType").GetValue<sbyte>();
            MemoryOptimizationSettingsStore.SetStrategyPreset(lifeSkillType, newIndex);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save life skill strategy preset memory: " + ex);
        }
    }
}

[HarmonyPatch(typeof(LifeSkillCombatBeginView), "OnCurrentStageChanged")]
internal static class LifeSkillCombatPresetStrategyRestorePatch
{
    private static void Prefix()
    {
        LifeSkillCombatPresetStrategyMemoryController.SuppressSave = true;
    }

    private static void Postfix(LifeSkillCombatBeginView __instance)
    {
        try
        {
            if (Plugin.EnableStrategyPresetMemory && __instance != null)
                Restore(__instance);
        }
        finally
        {
            LifeSkillCombatPresetStrategyMemoryController.SuppressSave = false;
        }
    }

    private static void Restore(LifeSkillCombatBeginView __instance)
    {
        if (!Plugin.EnableStrategyPresetMemory || __instance == null)
            return;

        try
        {
            var traverse = Traverse.Create(__instance);
            var selectStrategyHolder = traverse.Field("selectStrategyHolder").GetValue<RectTransform>();
            if (selectStrategyHolder == null || !selectStrategyHolder.gameObject.activeSelf)
                return;

            var lifeSkillType = traverse.Field("_selectedSkillType").GetValue<sbyte>();
            var rememberedIndex = MemoryOptimizationSettingsStore.GetStrategyPreset(lifeSkillType);
            if (rememberedIndex < 0)
                return;

            var toggleGroup = traverse.Field("presetStrategyToggleGroup").GetValue<ToggleGroup>();
            if (toggleGroup == null || rememberedIndex >= toggleGroup.Count() || toggleGroup.GetActiveIndex() == rememberedIndex)
                return;

            toggleGroup.Set(rememberedIndex);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to restore life skill strategy preset memory: " + ex);
        }
    }
}

internal static class LifeSkillCombatPresetStrategyMemoryController
{
    internal static bool SuppressSave;
}

internal static class LifeSkillAutoModeMemoryController
{
    internal static void SaveCurrent()
    {
        if (!Plugin.EnableLifeSkillAutoModeMemory)
            return;

        try
        {
            var model = SingletonObject.getInstance<LifeSkillCombatModel>();
            if (model != null)
                MemoryOptimizationSettingsStore.SetLifeSkillAutoMode(model.IsAuto);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save life skill auto mode memory: " + ex);
        }
    }

    internal static void RestoreCurrent(object view, MethodInfo updateAutoFightMarkMethod, MethodInfo refreshButtonInteractableMethod = null)
    {
        if (!Plugin.EnableLifeSkillAutoModeMemory
            || view == null
            || !MemoryOptimizationSettingsStore.Current.HasLifeSkillAutoMode)
            return;

        try
        {
            var model = SingletonObject.getInstance<LifeSkillCombatModel>();
            if (model == null)
                return;

            model.IsAuto = MemoryOptimizationSettingsStore.Current.LifeSkillAutoMode;
            updateAutoFightMarkMethod?.Invoke(view, new object[] { model.IsAuto });
            refreshButtonInteractableMethod?.Invoke(view, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to restore life skill auto mode memory: " + ex);
        }
    }
}

[HarmonyPatch(typeof(UI_LifeSkillCombat2), "OnClickAutoFight")]
internal static class LifeSkillCombatAutoModeSavePatch
{
    private static void Postfix()
    {
        LifeSkillAutoModeMemoryController.SaveCurrent();
    }
}

[HarmonyPatch(typeof(UI_LifeSkillCombat2), "OnInit")]
internal static class LifeSkillCombatAutoModeRestorePatch
{
    private static readonly MethodInfo UpdateAutoFightMarkMethod =
        AccessTools.Method(typeof(UI_LifeSkillCombat2), "UpdateAutoFightMark", new[] { typeof(bool) });

    private static void Postfix(UI_LifeSkillCombat2 __instance)
    {
        LifeSkillAutoModeMemoryController.RestoreCurrent(__instance, UpdateAutoFightMarkMethod);
    }
}

[HarmonyPatch(typeof(DebateView), "OnClickButtonAutoFight")]
internal static class DebateViewAutoModeSavePatch
{
    private static void Postfix()
    {
        LifeSkillAutoModeMemoryController.SaveCurrent();
    }
}

[HarmonyPatch(typeof(DebateView), "OnInit")]
internal static class DebateViewAutoModeRestorePatch
{
    private static readonly MethodInfo UpdateAutoFightMarkMethod =
        AccessTools.Method(typeof(DebateView), "UpdateAutoFightMark", new[] { typeof(bool) });

    private static readonly MethodInfo RefreshButtonInteractableMethod =
        AccessTools.Method(typeof(DebateView), "RefreshButtonInteractable");

    private static void Postfix(DebateView __instance)
    {
        LifeSkillAutoModeMemoryController.RestoreCurrent(__instance, UpdateAutoFightMarkMethod, RefreshButtonInteractableMethod);
    }
}
