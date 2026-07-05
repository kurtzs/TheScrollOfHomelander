#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Config;
using HarmonyLib;
using UnityEngine;
using GameData.Domains.Item.Display;
using Game.Views.Make;
using GameData.Domains.Taiwu.ExchangeSystem;
using ItemListScroll = Game.Components.ListStyleGeneralScroll.Item.ItemListScroll;
using ExchangeContainerView = Game.Views.Exchange.ExchangeContainer;
using ExchangeViewBase = Game.Views.Exchange.ViewExchangeBase;
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
    public List<MakeSubtypeMemoryEntry> MakeSubtypeMemories = new List<MakeSubtypeMemoryEntry>();
    public List<MakePerfectSelectionMemoryEntry> MakePerfectSelectionMemories = new List<MakePerfectSelectionMemoryEntry>();
    public List<MakePerfectResourceMemoryEntry> MakePerfectResourceMemories = new List<MakePerfectResourceMemoryEntry>();
    public bool HasLifeSkillAutoMode;
    public bool LifeSkillAutoMode;
    public bool HasMakeSubtypeLastSelection;
    public int MakeSubtypeLastLifeSkillType = -1;
    public bool MakeSubtypeLastHasSubtypeId;
    public int MakeSubtypeLastSubtypeId;
    public string MakeSubtypeLastSubtypeName;

    internal void Normalize()
    {
        FilterMemories ??= new List<FilterMemoryEntry>();
        SortMemories ??= new List<SortMemoryEntry>();
        StrategyPresetMemories ??= new List<StrategyPresetMemoryEntry>();
        MakeSubtypeMemories ??= new List<MakeSubtypeMemoryEntry>();
        MakePerfectSelectionMemories ??= new List<MakePerfectSelectionMemoryEntry>();
        MakePerfectResourceMemories ??= new List<MakePerfectResourceMemoryEntry>();

        FilterMemories.RemoveAll(entry => entry == null || string.IsNullOrEmpty(entry.Key));
        foreach (var entry in FilterMemories)
            entry.Normalize();

        SortMemories.RemoveAll(entry => entry == null || string.IsNullOrEmpty(entry.Key));
        foreach (var entry in SortMemories)
            entry.Normalize();

        StrategyPresetMemories.RemoveAll(entry => entry == null);
        foreach (var entry in StrategyPresetMemories)
            entry.PresetIndex = Mathf.Clamp(entry.PresetIndex, 0, 8);

        MakeSubtypeMemories.RemoveAll(entry => entry == null || entry.LifeSkillType < 0 || entry.MakeItemTypeId < 0 || string.IsNullOrEmpty(entry.Signature));
        foreach (var entry in MakeSubtypeMemories)
        {
            entry.SubtypeIndex = Mathf.Clamp(entry.SubtypeIndex, 0, 16);
            if (entry.HasSubtypeId && entry.SubtypeId < 0)
                entry.HasSubtypeId = false;
        }

        MakePerfectSelectionMemories.RemoveAll(entry => entry == null || entry.LifeSkillType < 0 || entry.TargetItemType < 0 || entry.TargetTemplateId < -1);
        foreach (var entry in MakePerfectSelectionMemories)
        {
            entry.MemoryKey ??= string.Empty;
            if (!entry.HasSelection || entry.SelectedSubtypeId < 0 || entry.SelectedTemplateId < -1)
            {
                entry.HasSelection = false;
                entry.SelectedSubtypeId = -1;
                entry.SelectedTemplateId = -1;
            }
        }

        MakePerfectResourceMemories.RemoveAll(entry =>
            entry == null
            || entry.LifeSkillType < 0
            || entry.TargetItemType < 0
            || entry.TargetTemplateId < -1
            || entry.SelectedSubtypeId < 0
            || entry.SelectedTemplateId < -1);
        foreach (var entry in MakePerfectResourceMemories)
        {
            entry.MemoryKey ??= string.Empty;
            entry.Wood = Mathf.Clamp(entry.Wood, 0, 999);
            entry.Metal = Mathf.Clamp(entry.Metal, 0, 999);
            entry.Jade = Mathf.Clamp(entry.Jade, 0, 999);
            entry.Fabric = Mathf.Clamp(entry.Fabric, 0, 999);
        }

        if (HasMakeSubtypeLastSelection && MakeSubtypeLastLifeSkillType < 0)
            HasMakeSubtypeLastSelection = false;
        MakeSubtypeLastSubtypeName ??= string.Empty;
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

[Serializable]
public sealed class MakeSubtypeMemoryEntry
{
    public int LifeSkillType;
    public int MakeItemTypeId;
    public string Signature;
    public int SubtypeIndex;
    public bool HasSubtypeId;
    public int SubtypeId;
    public string SubtypeName;
}

[Serializable]
public sealed class MakePerfectSelectionMemoryEntry
{
    public string MemoryKey;
    public int LifeSkillType;
    public int TargetItemType;
    public int TargetTemplateId;
    public bool PerfectEnabled;
    public bool HasSelection;
    public int SelectedSubtypeId = -1;
    public int SelectedTemplateId = -1;
    public string SelectedName;
}

[Serializable]
public sealed class MakePerfectResourceMemoryEntry
{
    public string MemoryKey;
    public int LifeSkillType;
    public int TargetItemType;
    public int TargetTemplateId;
    public int SelectedSubtypeId;
    public int SelectedTemplateId;
    public string SelectedName;
    public int Wood;
    public int Metal;
    public int Jade;
    public int Fabric;
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

    internal static int GetMakeSubtypeIndex(sbyte lifeSkillType, short makeItemTypeId, string signature)
    {
        var entry = GetMakeSubtypeMemory(lifeSkillType, makeItemTypeId, signature);
        return entry == null ? -1 : entry.SubtypeIndex;
    }

    internal static MakeSubtypeMemoryEntry GetMakeSubtypeMemory(sbyte lifeSkillType, short makeItemTypeId, string signature)
    {
        if (lifeSkillType < 0 || makeItemTypeId < 0 || string.IsNullOrEmpty(signature))
            return null;

        Current.Normalize();
        return Current.MakeSubtypeMemories.FirstOrDefault(item =>
            item.LifeSkillType == lifeSkillType
            && item.MakeItemTypeId == makeItemTypeId
            && item.Signature == signature);
    }

    internal static void SetMakeSubtypeIndex(sbyte lifeSkillType, short makeItemTypeId, string signature, int subtypeIndex, int subtypeId = -1, string subtypeName = null)
    {
        if (lifeSkillType < 0 || makeItemTypeId < 0 || string.IsNullOrEmpty(signature) || subtypeIndex < 0)
            return;

        Current.Normalize();
        var entry = Current.MakeSubtypeMemories.FirstOrDefault(item =>
            item.LifeSkillType == lifeSkillType
            && item.MakeItemTypeId == makeItemTypeId
            && item.Signature == signature);
        if (entry == null)
        {
            entry = new MakeSubtypeMemoryEntry
            {
                LifeSkillType = lifeSkillType,
                MakeItemTypeId = makeItemTypeId,
                Signature = signature
            };
            Current.MakeSubtypeMemories.Add(entry);
        }

        entry.SubtypeIndex = subtypeIndex;
        entry.HasSubtypeId = subtypeId >= 0;
        entry.SubtypeId = subtypeId;
        entry.SubtypeName = subtypeName;
        Current.HasMakeSubtypeLastSelection = true;
        Current.MakeSubtypeLastLifeSkillType = lifeSkillType;
        Current.MakeSubtypeLastHasSubtypeId = subtypeId >= 0;
        Current.MakeSubtypeLastSubtypeId = subtypeId;
        Current.MakeSubtypeLastSubtypeName = subtypeName ?? string.Empty;
        Save();
    }

    internal static MakePerfectSelectionMemoryEntry GetMakePerfectSelection(sbyte lifeSkillType, int productSubtypeId, int productTemplateId)
    {
        if (lifeSkillType < 0)
            return null;

        Current.Normalize();
        if (productSubtypeId >= 0 && productTemplateId >= -1)
        {
            return Current.MakePerfectSelectionMemories.FirstOrDefault(item =>
                item.LifeSkillType == lifeSkillType
                && item.TargetItemType == productSubtypeId
                && item.TargetTemplateId == productTemplateId)
                ?? Current.MakePerfectSelectionMemories.LastOrDefault(item =>
                    item.LifeSkillType == lifeSkillType
                    && item.SelectedSubtypeId == productSubtypeId
                    && item.SelectedTemplateId == productTemplateId);
        }

        return Current.MakePerfectSelectionMemories.LastOrDefault(item =>
            item.LifeSkillType == lifeSkillType);
    }

    internal static MakePerfectSelectionMemoryEntry GetMakePerfectSelection(string memoryKey)
    {
        if (string.IsNullOrEmpty(memoryKey))
            return null;

        Current.Normalize();
        return Current.MakePerfectSelectionMemories.LastOrDefault(item => item.MemoryKey == memoryKey);
    }

    internal static MakePerfectResourceMemoryEntry GetMakePerfectResource(sbyte lifeSkillType, int productSubtypeId, int productTemplateId)
    {
        if (lifeSkillType < 0 || productSubtypeId < 0 || productTemplateId < -1)
            return null;

        Current.Normalize();
        return Current.MakePerfectResourceMemories.FirstOrDefault(item =>
            item.LifeSkillType == lifeSkillType
            && item.TargetItemType == productSubtypeId
            && item.TargetTemplateId == productTemplateId)
            ?? Current.MakePerfectResourceMemories.LastOrDefault(item =>
                item.LifeSkillType == lifeSkillType
                && item.SelectedSubtypeId == productSubtypeId
                && item.SelectedTemplateId == productTemplateId);
    }

    internal static MakePerfectResourceMemoryEntry GetMakePerfectResource(string memoryKey)
    {
        if (string.IsNullOrEmpty(memoryKey))
            return null;

        Current.Normalize();
        return Current.MakePerfectResourceMemories.LastOrDefault(item => item.MemoryKey == memoryKey);
    }

    internal static void SetMakePerfect(MakePerfectSelectionMemoryEntry selection, MakePerfectResourceMemoryEntry resource)
    {
        if (selection == null || selection.LifeSkillType < 0)
            return;

        Current.Normalize();
        var selectionIndex = !string.IsNullOrEmpty(selection.MemoryKey)
            ? Current.MakePerfectSelectionMemories.FindIndex(item => item.MemoryKey == selection.MemoryKey)
            : Current.MakePerfectSelectionMemories.FindIndex(item =>
                item.LifeSkillType == selection.LifeSkillType
                && item.TargetItemType == selection.TargetItemType
                && item.TargetTemplateId == selection.TargetTemplateId);
        if (selectionIndex >= 0)
            Current.MakePerfectSelectionMemories.RemoveAt(selectionIndex);

        Current.MakePerfectSelectionMemories.Add(selection);

        if (resource != null
            && resource.LifeSkillType >= 0
            && resource.SelectedSubtypeId >= 0
            && resource.SelectedTemplateId >= -1
            && resource.TargetItemType >= 0
            && resource.TargetTemplateId >= -1)
        {
            var resourceIndex = !string.IsNullOrEmpty(resource.MemoryKey)
                ? Current.MakePerfectResourceMemories.FindIndex(item => item.MemoryKey == resource.MemoryKey)
                : Current.MakePerfectResourceMemories.FindIndex(item =>
                    item.LifeSkillType == resource.LifeSkillType
                    && item.TargetItemType == resource.TargetItemType
                    && item.TargetTemplateId == resource.TargetTemplateId
                    && item.SelectedSubtypeId == resource.SelectedSubtypeId
                    && item.SelectedTemplateId == resource.SelectedTemplateId);
            if (resourceIndex >= 0)
                Current.MakePerfectResourceMemories[resourceIndex] = resource;
            else
                Current.MakePerfectResourceMemories.Add(resource);
        }

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

[HarmonyPatch(typeof(ExchangeViewBase), "Refresh")]
internal static class ViewExchangeBaseRefreshPreserveFilterStatePatch
{
    private sealed class RefreshFilterState
    {
        internal ItemListScroll SelfList;
        internal ItemListScroll TargetList;
        internal bool SelfSaved;
        internal bool TargetSaved;
    }

    private static void Prefix(ExchangeViewBase __instance, out RefreshFilterState __state)
    {
        __state = Capture(__instance);
    }

    private static void Postfix(RefreshFilterState __state)
    {
        Restore(__state);
    }

    private static RefreshFilterState Capture(ExchangeViewBase view)
    {
        var state = new RefreshFilterState();
        try
        {
            var container = Traverse.Create(view).Field("exchangeContainer").GetValue<ExchangeContainerView>();
            state.SelfList = container?.selfItemList;
            state.TargetList = container?.targetItemList;
            state.SelfSaved = TrySave(state.SelfList);
            state.TargetSaved = TrySave(state.TargetList);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to capture exchange filter state: " + ex);
        }

        return state;
    }

    private static bool TrySave(ItemListScroll list)
    {
        if (list?.SortAndFilterController == null)
            return false;

        try
        {
            list.SortAndFilterController.SaveFilterStateFromUI();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save exchange filter state before refresh: " + ex);
            return false;
        }
    }

    private static void Restore(RefreshFilterState state)
    {
        if (state == null)
            return;

        TryRestore(state.SelfList, state.SelfSaved);
        TryRestore(state.TargetList, state.TargetSaved);
    }

    private static void TryRestore(ItemListScroll list, bool saved)
    {
        if (!saved || list?.SortAndFilterController == null)
            return;

        try
        {
            list.SortAndFilterController.RestoreFilterState();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to restore exchange filter state after refresh: " + ex);
        }
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

    internal static string GetCurrentSignature(FilterView sortAndFilter)
    {
        if (sortAndFilter?.Config?.LineConfigs == null)
            return string.Empty;

        var key = BuildKey(sortAndFilter);
        var configSignature = BuildSignature(sortAndFilter.Config);
        var entry = MemoryOptimizationSettingsStore.GetFilterMemory(key, configSignature);
        return key + "|" + configSignature + "|" + BuildEntrySignature(entry);
    }

    private static string BuildEntrySignature(FilterMemoryEntry entry)
    {
        if (entry?.Lines == null || entry.Lines.Count == 0)
            return "none";

        var builder = new StringBuilder(256);
        foreach (var line in entry.Lines)
        {
            if (line == null)
                continue;

            builder.Append(line.LineId).Append(',')
                .Append(line.Type).Append(',')
                .Append(line.IsActive ? 1 : 0).Append(',')
                .Append(line.ToggleIsAll ? 1 : 0).Append(',')
                .Append(line.ToggleIndex).Append('[');

            if (line.Menus != null)
            {
                foreach (var menu in line.Menus.OrderBy(menu => menu.MenuId))
                {
                    if (menu == null)
                        continue;

                    builder.Append(menu.MenuId).Append(':')
                        .Append(menu.IsActive ? 1 : 0).Append(':');
                    if (menu.SelectedIndices != null)
                    {
                        foreach (var index in menu.SelectedIndices.OrderBy(index => index))
                            builder.Append(index).Append('.');
                    }

                    builder.Append(';');
                }
            }

            builder.Append("]|");
        }

        return builder.ToString();
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
    private string _pendingSignature;
    private string _completedSignature;

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

        var signature = FilterMemoryController.GetCurrentSignature(_owner);
        if (!string.IsNullOrEmpty(signature))
        {
            if (signature == _completedSignature)
                return;

            if (_remainingFrames > 0 && signature == _pendingSignature)
                return;
        }

        _pendingSignature = signature;
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
        _completedSignature = FilterMemoryController.GetCurrentSignature(_owner);
        _pendingSignature = null;
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

[HarmonyPatch(typeof(FilterView), "OnFirstToggleGroupChanged")]
internal static class SortAndFilterFirstToggleGroupChangedMemoryPatch
{
    private static void Postfix(FilterView __instance)
    {
        FilterMemoryController.TrySave(__instance);
    }
}

[HarmonyPatch(typeof(FilterView), "OnSectionSelectionChanged")]
internal static class SortAndFilterSectionSelectionChangedMemoryPatch
{
    private static void Postfix(FilterView __instance)
    {
        FilterMemoryController.TrySave(__instance);
    }
}

[HarmonyPatch(typeof(FilterView), "ClearFilterItem")]
internal static class SortAndFilterClearFilterItemMemoryPatch
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

internal static class MakeSubtypeMemoryController
{
    internal static bool SuppressSave;
    internal static bool IsRefreshingSubtypeToggleGroup;

    internal static void Save(MakeSubPageMake page, int newIndex)
    {
        if (!Plugin.EnableMakeSubtypeMemory || SuppressSave || IsRefreshingSubtypeToggleGroup || page == null || newIndex < 0)
            return;

        try
        {
            if (!TryGetMemoryKey(page, out var lifeSkillType, out var makeItemTypeId, out var signature, out var subtypeIds))
                return;

            if (newIndex >= subtypeIds.Count)
                return;

            var subtypeId = subtypeIds[newIndex];
            MemoryOptimizationSettingsStore.SetMakeSubtypeIndex(lifeSkillType, makeItemTypeId, signature, newIndex, subtypeId, GetSubtypeName(subtypeId));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save make subtype memory: " + ex);
        }
    }

    internal static void Restore(MakeSubPageMake page)
    {
        if (!Plugin.EnableMakeSubtypeMemory || page == null)
            return;

        try
        {
            var toggleGroup = Traverse.Create(page).Field("subTypeToggleGroup").GetValue<ToggleGroup>();
            if (toggleGroup == null || !toggleGroup.gameObject.activeSelf)
                return;

            if (!TryGetMemoryKey(page, out var lifeSkillType, out var makeItemTypeId, out var signature, out var subtypeIds))
                return;

            var memory = GetLatestSubtypeMemory(lifeSkillType)
                ?? MemoryOptimizationSettingsStore.GetMakeSubtypeMemory(lifeSkillType, makeItemTypeId, signature);
            if (memory == null)
                return;

            var rememberedIndex = ResolveRememberedIndex(memory, subtypeIds);
            if (rememberedIndex < 0 || rememberedIndex >= subtypeIds.Count || toggleGroup.GetActiveIndex() == rememberedIndex)
                return;

            SuppressSave = true;
            try
            {
                toggleGroup.Set(rememberedIndex, forceRaiseEvent: true);
            }
            finally
            {
                SuppressSave = false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to restore make subtype memory: " + ex);
        }
    }

    private static bool TryGetMemoryKey(MakeSubPageMake page, out sbyte lifeSkillType, out short makeItemTypeId, out string signature, out List<short> subtypeIds)
    {
        lifeSkillType = -1;
        makeItemTypeId = -1;
        signature = string.Empty;
        subtypeIds = null;

        var view = MakeSelectMaterialPatch.GetParentView(page);
        if (view == null)
            return false;

        subtypeIds = Traverse.Create(page).Field("_makeItemSubtypeIdList").GetValue<List<short>>();
        if (subtypeIds == null || subtypeIds.Count <= 1)
            return false;

        makeItemTypeId = Traverse.Create(page).Field("_makeItemTypeId").GetValue<short>();
        if (makeItemTypeId < 0)
            return false;

        lifeSkillType = view.CurLifeSkillType;
        signature = string.Join(",", subtypeIds.Select(id => id.ToString()).ToArray());
        return !string.IsNullOrEmpty(signature);
    }

    private static int ResolveRememberedIndex(MakeSubtypeMemoryEntry memory, List<short> subtypeIds)
    {
        if (memory == null || subtypeIds == null)
            return -1;

        if (memory.HasSubtypeId)
        {
            var index = subtypeIds.IndexOf((short)memory.SubtypeId);
            if (index >= 0)
                return index;
        }

        if (!string.IsNullOrEmpty(memory.SubtypeName))
        {
            for (var i = 0; i < subtypeIds.Count; i++)
            {
                if (GetSubtypeName(subtypeIds[i]) == memory.SubtypeName)
                    return i;
            }
        }

        return memory.SubtypeIndex;
    }

    private static MakeSubtypeMemoryEntry GetLatestSubtypeMemory(sbyte lifeSkillType)
    {
        var settings = MemoryOptimizationSettingsStore.Current;
        if (!settings.HasMakeSubtypeLastSelection || settings.MakeSubtypeLastLifeSkillType != lifeSkillType)
            return null;

        return new MakeSubtypeMemoryEntry
        {
            LifeSkillType = settings.MakeSubtypeLastLifeSkillType,
            MakeItemTypeId = -1,
            Signature = "latest",
            SubtypeIndex = -1,
            HasSubtypeId = settings.MakeSubtypeLastHasSubtypeId,
            SubtypeId = settings.MakeSubtypeLastSubtypeId,
            SubtypeName = settings.MakeSubtypeLastSubtypeName
        };
    }

    private static string GetSubtypeName(short subtypeId)
    {
        try
        {
            return MakeItemSubType.Instance[subtypeId]?.Name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "SubTypeToggleGroupOnActiveIndexChange")]
internal static class MakeSubtypeSelectionMemorySavePatch
{
    private static void Postfix(MakeSubPageMake __instance, int newIndex)
    {
        MakeSubtypeMemoryController.Save(__instance, newIndex);
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "RefreshSubTypeToggleGroup")]
internal static class MakeSubtypeSelectionMemoryRestorePatch
{
    private static void Prefix()
    {
        MakeSubtypeMemoryController.IsRefreshingSubtypeToggleGroup = true;
    }

    private static void Postfix(MakeSubPageMake __instance)
    {
        MakeSubtypeMemoryController.Restore(__instance);
    }

    private static void Finalizer()
    {
        MakeSubtypeMemoryController.IsRefreshingSubtypeToggleGroup = false;
    }
}

internal static class MakeSubPageMakePerfectMemoryController
{
    private const int NoPerfectEffectId = 0;
    private const int NoPerfectDropdownIndex = -1;
    private const string NoPerfectName = "no-perfect";
    private const int RandomMakeTargetItemTypeBase = 100000;
    private static readonly HashSet<MakeSubPageMake> ApplyingRestore = new HashSet<MakeSubPageMake>();
    private static readonly MethodInfo RefreshPerfectDropdownMethod = AccessTools.Method(typeof(MakeSubPageMake), "RefreshPerfectDropdown", Type.EmptyTypes);
    private static int RefreshingPerfectDropdownDepth;
    private static int HandlingPerfectToggleDepth;

    internal static bool SuppressSave => ApplyingRestore.Count > 0;
    internal static bool IsRefreshingPerfectDropdown => RefreshingPerfectDropdownDepth > 0;
    internal static bool IsHandlingPerfectToggle => HandlingPerfectToggleDepth > 0;

    internal static void BeginRefreshPerfectDropdown()
    {
        RefreshingPerfectDropdownDepth++;
    }

    internal static void EndRefreshPerfectDropdown()
    {
        if (RefreshingPerfectDropdownDepth > 0)
            RefreshingPerfectDropdownDepth--;
    }

    internal static void BeginPerfectToggleChange()
    {
        HandlingPerfectToggleDepth++;
    }

    internal static void EndPerfectToggleChange()
    {
        if (HandlingPerfectToggleDepth > 0)
            HandlingPerfectToggleDepth--;
    }

    internal static void RestoreSelectionAndResources(MakeSubPageMake page)
    {
        if (!Plugin.EnableMakePerfectMemory || page == null || ApplyingRestore.Contains(page))
            return;

        try
        {
            if (!TryGetTargetContext(page, out var lifeSkillType, out var targetItemType, out var targetTemplateId))
                return;

            var selectionKey = BuildSelectionKey(lifeSkillType, targetItemType, targetTemplateId);
            var selection = MemoryOptimizationSettingsStore.GetMakePerfectSelection(selectionKey);
            var targetSlot = GetTargetSlot(page);
            if (targetSlot == null)
                return;

            ApplyingRestore.Add(page);
            try
            {
                var desiredPerfect = selection?.PerfectEnabled ?? false;
                var needRefreshDropdown = targetSlot.IsToggleOn != desiredPerfect;
                targetSlot.IsToggleOn = desiredPerfect;
                if (needRefreshDropdown)
                    InvokeRefreshPerfectDropdown(page);

                if (desiredPerfect && selection?.HasSelection == true)
                    RestoreDropdownSelection(page, selection);

                RestoreResourceCounts(page);
                InvokePrivate(page, "CheckCondition");
            }
            finally
            {
                ApplyingRestore.Remove(page);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to restore make perfect memory for MakeSubPageMake: " + ex);
        }
    }

    internal static void RestoreAfterPerfectDropdownRefresh(MakeSubPageMake page)
    {
        if (!Plugin.EnableMakePerfectMemory || page == null || ApplyingRestore.Contains(page) || IsHandlingPerfectToggle)
            return;

        RestoreSelectionAndResources(page);
    }

    internal static void RestoreResourcesOnly(MakeSubPageMake page)
    {
        if (!Plugin.EnableMakePerfectMemory || page == null || ApplyingRestore.Contains(page))
            return;

        try
        {
            ApplyingRestore.Add(page);
            try
            {
                RestoreResourceCounts(page);
                InvokePrivate(page, "CheckCondition");
            }
            finally
            {
                ApplyingRestore.Remove(page);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to restore make resource memory for MakeSubPageMake: " + ex);
        }
    }

    internal static void SaveSelection(MakeSubPageMake page)
    {
        if (!Plugin.EnableMakePerfectMemory || SuppressSave || IsRefreshingPerfectDropdown || page == null)
            return;

        try
        {
            if (!TryBuildSelectionEntry(page, out var selection))
                return;

            MemoryOptimizationSettingsStore.SetMakePerfect(selection, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save make perfect selection memory for MakeSubPageMake: " + ex);
        }
    }

    internal static void SaveResources(MakeSubPageMake page)
    {
        if (!Plugin.EnableMakePerfectMemory || SuppressSave || page == null)
            return;

        try
        {
            if (!TryBuildSelectionEntry(page, out var selection)
                || !TryBuildResourceEntry(page, out var resource))
                return;

            MemoryOptimizationSettingsStore.SetMakePerfect(selection, resource);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save make resource memory for MakeSubPageMake: " + ex);
        }
    }

    internal static void SaveSelectionThenRestoreResources(MakeSubPageMake page)
    {
        if (!Plugin.EnableMakePerfectMemory || SuppressSave || IsRefreshingPerfectDropdown || page == null)
            return;

        SaveSelection(page);
        RestoreResourcesOnly(page);
    }

    private static bool TryBuildSelectionEntry(MakeSubPageMake page, out MakePerfectSelectionMemoryEntry selection)
    {
        selection = null;
        if (!TryGetTargetContext(page, out var lifeSkillType, out var targetItemType, out var targetTemplateId))
            return false;

        var targetSlot = GetTargetSlot(page);
        if (targetSlot == null)
            return false;

        var perfectEnabled = targetSlot.IsToggleOn;
        var selectedEffectId = -1;
        var selectedDropdownIndex = -1;
        var selectedName = string.Empty;
        var hasSelection = perfectEnabled && TryGetSelectedPerfectEffect(page, out selectedEffectId, out selectedDropdownIndex, out selectedName);

        selection = new MakePerfectSelectionMemoryEntry
        {
            MemoryKey = BuildSelectionKey(lifeSkillType, targetItemType, targetTemplateId),
            LifeSkillType = lifeSkillType,
            TargetItemType = targetItemType,
            TargetTemplateId = targetTemplateId,
            PerfectEnabled = perfectEnabled,
            HasSelection = hasSelection,
            SelectedSubtypeId = hasSelection ? selectedEffectId : -1,
            SelectedTemplateId = hasSelection ? selectedDropdownIndex : -1,
            SelectedName = hasSelection ? selectedName : string.Empty
        };
        return true;
    }

    private static bool TryBuildResourceEntry(MakeSubPageMake page, out MakePerfectResourceMemoryEntry resource)
    {
        resource = null;
        if (!TryGetTargetContext(page, out var lifeSkillType, out var targetItemType, out var targetTemplateId))
            return false;

        GetActivePerfectResourceKey(page, out var effectId, out var dropdownIndex, out var effectName);
        resource = new MakePerfectResourceMemoryEntry
        {
            MemoryKey = BuildResourceKey(lifeSkillType, targetItemType, targetTemplateId, effectId, dropdownIndex, effectName),
            LifeSkillType = lifeSkillType,
            TargetItemType = targetItemType,
            TargetTemplateId = targetTemplateId,
            SelectedSubtypeId = effectId,
            SelectedTemplateId = dropdownIndex,
            SelectedName = effectName,
            Wood = GetCurrentResourceCount(page, 1),
            Metal = GetCurrentResourceCount(page, 2),
            Jade = GetCurrentResourceCount(page, 3),
            Fabric = GetCurrentResourceCount(page, 4)
        };
        return true;
    }

    private static void RestoreDropdownSelection(MakeSubPageMake page, MakePerfectSelectionMemoryEntry selection)
    {
        var dropdown = GetPerfectDropdown(page);
        if (dropdown == null || !dropdown.gameObject.activeSelf)
            return;

        var index = FindPerfectEffectDropdownIndex(page, selection.SelectedSubtypeId, selection.SelectedName);
        if (index < 0)
            return;

        dropdown.SetValueWithoutNotify(index);
        InvokePrivate(page, "OnPerfectDropdownValueChanged", index);
    }

    private static void RestoreResourceCounts(MakeSubPageMake page)
    {
        if (!TryGetTargetContext(page, out var lifeSkillType, out var targetItemType, out var targetTemplateId))
            return;

        GetActivePerfectResourceKey(page, out var effectId, out var dropdownIndex, out var effectName);
        var resourceKey = BuildResourceKey(lifeSkillType, targetItemType, targetTemplateId, effectId, dropdownIndex, effectName);
        var memory = MemoryOptimizationSettingsStore.GetMakePerfectResource(resourceKey);
        var traverse = Traverse.Create(page);
        var currentField = traverse.Field("_curMakeResourceCountInts");
        var lastField = traverse.Field("_lastMakeResourceCountInts");
        var resourceInts = currentField.GetValue<GameData.Domains.Character.ResourceInts>();
        if (memory == null)
        {
            resourceInts.Initialize();
            currentField.SetValue(resourceInts);
            lastField.SetValue(resourceInts);
            return;
        }

        var totalMax = Math.Max(0, (int)traverse.Field("_maxMakeResourceTotalCount").GetValue<short>());
        var runningTotal = 0;

        RestoreResource(1, memory.Wood);
        RestoreResource(2, memory.Metal);
        RestoreResource(3, memory.Jade);
        RestoreResource(4, memory.Fabric);

        currentField.SetValue(resourceInts);
        lastField.SetValue(resourceInts);

        void RestoreResource(sbyte type, int wanted)
        {
            var max = GetResourceMax(page, type);
            var value = Mathf.Clamp(wanted, 0, max);
            if (totalMax > 0)
                value = Mathf.Clamp(value, 0, Math.Max(0, totalMax - runningTotal));

            SetResourceCount(ref resourceInts, type, value);
            runningTotal += value;
        }
    }

    private static bool TryGetTargetContext(MakeSubPageMake page, out sbyte lifeSkillType, out int targetItemType, out int targetTemplateId)
    {
        lifeSkillType = -1;
        targetItemType = -1;
        targetTemplateId = -1;

        var view = MakeSelectMaterialPatch.GetParentView(page);
        var targetSlot = GetTargetSlot(page);
        if (view == null || targetSlot == null || !targetSlot.IsValid || targetSlot.ItemData == null)
            return false;

        lifeSkillType = view.CurLifeSkillType;
        if (lifeSkillType < 0)
            return false;

        var realKey = targetSlot.ItemData.RealKey;
        if (realKey.HasTemplate && realKey.ItemType >= 0)
        {
            targetItemType = realKey.ItemType;
            targetTemplateId = realKey.TemplateId;
            return targetTemplateId >= 0;
        }

        var key = targetSlot.ItemData.Key;
        if (key.HasTemplate)
        {
            targetItemType = key.ItemType >= 0 ? key.ItemType : GetRandomTargetItemType(page);
            targetTemplateId = key.TemplateId;
            return targetItemType >= 0 && targetTemplateId >= 0;
        }

        var makeItemSubTypeId = Traverse.Create(page).Field("_makeItemSubTypeId").GetValue<short>();
        if (makeItemSubTypeId >= 0)
        {
            targetItemType = GetRandomTargetItemType(page);
            targetTemplateId = makeItemSubTypeId;
            return targetItemType >= 0;
        }

        return lifeSkillType >= 0 && targetItemType >= 0 && targetTemplateId >= 0;
    }

    private static int GetRandomTargetItemType(MakeSubPageMake page)
    {
        try
        {
            var makeItemTypeId = Traverse.Create(page).Field("_makeItemTypeId").GetValue<short>();
            return RandomMakeTargetItemTypeBase + Math.Max(0, (int)makeItemTypeId);
        }
        catch
        {
            return RandomMakeTargetItemTypeBase;
        }
    }

    private static void GetActivePerfectResourceKey(MakeSubPageMake page, out int effectId, out int dropdownIndex, out string effectName)
    {
        var targetSlot = GetTargetSlot(page);
        if (targetSlot != null && targetSlot.IsToggleOn && TryGetSelectedPerfectEffect(page, out effectId, out dropdownIndex, out effectName))
            return;

        effectId = NoPerfectEffectId;
        dropdownIndex = NoPerfectDropdownIndex;
        effectName = NoPerfectName;
    }

    private static bool TryGetSelectedPerfectEffect(MakeSubPageMake page, out int effectId, out int dropdownIndex, out string effectName)
    {
        effectId = -1;
        dropdownIndex = -1;
        effectName = string.Empty;

        var dropdown = GetPerfectDropdown(page);
        var effectIds = Traverse.Create(page).Field("_perfectEffectIdList").GetValue<List<short>>();
        if (dropdown == null || effectIds == null || effectIds.Count == 0)
            return false;

        dropdownIndex = Mathf.Clamp(dropdown.value, 0, effectIds.Count - 1);
        effectId = effectIds[dropdownIndex];
        effectName = GetPerfectEffectName(effectId);
        return effectId >= 0;
    }

    private static int FindPerfectEffectDropdownIndex(MakeSubPageMake page, int effectId, string effectName)
    {
        var effectIds = Traverse.Create(page).Field("_perfectEffectIdList").GetValue<List<short>>();
        if (effectIds == null || effectIds.Count == 0)
            return -1;

        if (effectId >= 0)
        {
            var index = effectIds.IndexOf((short)effectId);
            if (index >= 0)
                return index;
        }

        if (!string.IsNullOrEmpty(effectName))
        {
            for (var i = 0; i < effectIds.Count; i++)
            {
                if (GetPerfectEffectName(effectIds[i]) == effectName)
                    return i;
            }
        }

        return -1;
    }

    private static string GetPerfectEffectName(int effectId)
    {
        try
        {
            return effectId >= 0 ? EquipmentEffect.Instance[(short)effectId]?.Name ?? string.Empty : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static MakeTargetSlot GetTargetSlot(MakeSubPageMake page)
    {
        return Traverse.Create(page).Field("targetSlot").GetValue<MakeTargetSlot>();
    }

    private static FrameWork.UISystem.UIElements.CDropdown GetPerfectDropdown(MakeSubPageMake page)
    {
        return Traverse.Create(page).Field("perfectDropdown").GetValue<FrameWork.UISystem.UIElements.CDropdown>();
    }

    private static int GetCurrentResourceCount(MakeSubPageMake page, sbyte resourceType)
    {
        var resourceInts = Traverse.Create(page).Field("_curMakeResourceCountInts").GetValue<GameData.Domains.Character.ResourceInts>();
        return GetResourceCount(resourceInts, resourceType);
    }

    private static int GetResourceMax(MakeSubPageMake page, sbyte resourceType)
    {
        var maxResourceInts = Traverse.Create(page).Field("_maxMakeResourceCountInts").GetValue<GameData.Domains.Character.ResourceInts>();
        return Mathf.Max(0, GetResourceCount(maxResourceInts, resourceType));
    }

    private static int GetResourceCount(GameData.Domains.Character.ResourceInts resourceInts, sbyte resourceType)
    {
        return resourceType >= 0 && resourceType < 8 ? resourceInts.Get(resourceType) : 0;
    }

    private static void SetResourceCount(ref GameData.Domains.Character.ResourceInts resourceInts, sbyte resourceType, int count)
    {
        if (resourceType < 0 || resourceType >= 8)
            return;

        resourceInts.Set(resourceType, count);
    }

    private static string BuildSelectionKey(sbyte lifeSkillType, int targetItemType, int targetTemplateId)
    {
        return $"make-perfect-v2-selection:{lifeSkillType}:{targetItemType}:{targetTemplateId}";
    }

    private static string BuildResourceKey(sbyte lifeSkillType, int targetItemType, int targetTemplateId, int effectId, int dropdownIndex, string effectName)
    {
        return $"make-perfect-v2-resource:{lifeSkillType}:{targetItemType}:{targetTemplateId}:{effectId}:{dropdownIndex}:{effectName ?? string.Empty}";
    }

    private static void InvokeRefreshPerfectDropdown(MakeSubPageMake page)
    {
        RefreshPerfectDropdownMethod?.Invoke(page, Array.Empty<object>());
    }

    private static void InvokePrivate(MakeSubPageMake page, string methodName, params object[] args)
    {
        AccessTools.Method(typeof(MakeSubPageMake), methodName)?.Invoke(page, args);
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "SelectTarget")]
internal static class MakeSubPageMakePerfectMemoryRestoreTargetPatch
{
    private static void Postfix(MakeSubPageMake __instance)
    {
        MakeSubPageMakePerfectMemoryController.RestoreSelectionAndResources(__instance);
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "SelectMaterial")]
internal static class MakeSubPageMakePerfectMemoryRestoreMaterialPatch
{
    private static void Postfix(MakeSubPageMake __instance)
    {
        MakeSubPageMakePerfectMemoryController.RestoreSelectionAndResources(__instance);
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "ResetResourceCount")]
internal static class MakeSubPageMakePerfectMemoryRestoreAfterResourceResetPatch
{
    private static void Postfix(MakeSubPageMake __instance)
    {
        MakeSubPageMakePerfectMemoryController.RestoreResourcesOnly(__instance);
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "RefreshPerfectDropdown", new Type[] { })]
internal static class MakeSubPageMakePerfectMemoryRefreshDropdownPatch
{
    private static void Prefix()
    {
        MakeSubPageMakePerfectMemoryController.BeginRefreshPerfectDropdown();
    }

    private static void Postfix(MakeSubPageMake __instance)
    {
        MakeSubPageMakePerfectMemoryController.RestoreAfterPerfectDropdownRefresh(__instance);
    }

    private static void Finalizer()
    {
        MakeSubPageMakePerfectMemoryController.EndRefreshPerfectDropdown();
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "OnMakePerfectToggleValueChanged")]
internal static class MakeSubPageMakePerfectMemorySaveTogglePatch
{
    private static void Prefix()
    {
        MakeSubPageMakePerfectMemoryController.BeginPerfectToggleChange();
    }

    private static void Postfix(MakeSubPageMake __instance)
    {
        MakeSubPageMakePerfectMemoryController.SaveSelectionThenRestoreResources(__instance);
    }

    private static void Finalizer()
    {
        MakeSubPageMakePerfectMemoryController.EndPerfectToggleChange();
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "OnPerfectDropdownValueChanged")]
internal static class MakeSubPageMakePerfectMemorySaveDropdownPatch
{
    private static void Postfix(MakeSubPageMake __instance)
    {
        MakeSubPageMakePerfectMemoryController.SaveSelectionThenRestoreResources(__instance);
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "OnResourceCountChanged")]
internal static class MakeSubPageMakePerfectMemorySaveResourcePatch
{
    private static void Postfix(MakeSubPageMake __instance)
    {
        MakeSubPageMakePerfectMemoryController.SaveResources(__instance);
    }
}

internal static class MakePerfectMemoryController
{
    private const int MakeTabValue = 1;
    private static readonly Dictionary<UI_Make, Coroutine> SaveCoroutines = new Dictionary<UI_Make, Coroutine>();
    private static readonly Dictionary<UI_Make, Coroutine> SaveSelectionCoroutines = new Dictionary<UI_Make, Coroutine>();
    private static readonly Dictionary<UI_Make, Coroutine> RestoreCoroutines = new Dictionary<UI_Make, Coroutine>();
    private static readonly Dictionary<UI_Make, string> LastObservedStateSignatures = new Dictionary<UI_Make, string>();
    private static readonly Dictionary<UI_Make, int> SuppressObservedSaveUntilFrames = new Dictionary<UI_Make, int>();
    private static readonly Dictionary<UI_Make, int> PendingResourceSaveUntilFrames = new Dictionary<UI_Make, int>();
    private static readonly HashSet<UI_Make> PerfectButtonListeners = new HashSet<UI_Make>();
    private static readonly HashSet<UI_Make> ResourceButtonListeners = new HashSet<UI_Make>();

    internal static bool SuppressSave;

    internal static void AttachPerfectButtonSaveListener(UI_Make view)
    {
        if (!Plugin.EnableMakePerfectMemory || view == null || PerfectButtonListeners.Contains(view))
            return;

        try
        {
            var method = AccessTools.GetDeclaredMethods(typeof(RefersBase))
                .FirstOrDefault(item => item.Name == "CGet"
                    && item.IsGenericMethodDefinition
                    && item.GetParameters().Length == 1
                    && item.GetParameters()[0].ParameterType == typeof(string));
            var button = method?.MakeGenericMethod(typeof(CButtonObsolete)).Invoke(view, new object[] { "ButtonPerfect" }) as CButtonObsolete;
            if (button == null)
                return;

            button.onClick.AddListener(() => RequestSaveAfterUserInteraction(view));
            PerfectButtonListeners.Add(view);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to attach make perfect memory button listener: " + ex);
        }
    }

    internal static void AttachResourceButtonSaveListeners(UI_Make view)
    {
        if (!Plugin.EnableMakePerfectMemory || view == null || ResourceButtonListeners.Contains(view))
            return;

        try
        {
            var resourceList = Traverse.Create(view).Field("_makeRequireResourceList").GetValue<Refers>();
            if (resourceList == null)
                return;

            for (var i = 0; i < resourceList.transform.childCount; i++)
            {
                var refers = resourceList.transform.GetChild(i).GetComponent<Refers>();
                if (refers == null)
                    continue;

                AddResourceSaveListener(refers, "ButtonMore", view);
                AddResourceSaveListener(refers, "ButtonLess", view);
                AddResourceSaveListener(refers, "ButtonMax", view);
                AddResourceSaveListener(refers, "ButtonMin", view);
            }

            ResourceButtonListeners.Add(view);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to attach make perfect resource memory listeners: " + ex);
        }
    }

    private static void AddResourceSaveListener(Refers refers, string buttonName, UI_Make view)
    {
        try
        {
            var button = refers.CGet<CButtonObsolete>(buttonName);
            if (button != null)
                button.onClick.AddListener(() => RequestSaveAfterResourceInteraction(view));
        }
        catch
        {
        }
    }

    internal static void RestoreSelection(UI_Make view)
    {
        if (!Plugin.EnableMakePerfectMemory || view == null || !IsMakeTab(view))
            return;

        RequestRestoreState(view);
    }

    internal static void RequestRestoreState(UI_Make view)
    {
        if (!Plugin.EnableMakePerfectMemory || SuppressSave || view == null || !view.gameObject.activeInHierarchy)
            return;

        SuppressObservedSaveUntilFrames[view] = Time.frameCount + 18;
        if (RestoreCoroutines.TryGetValue(view, out var old) && old != null)
            view.StopCoroutine(old);

        RestoreCoroutines[view] = view.StartCoroutine(RestoreStateLater(view));
    }

    internal static void RequestSave(UI_Make view)
    {
        if (!Plugin.EnableMakePerfectMemory || SuppressSave || view == null || !view.gameObject.activeInHierarchy || !IsMakeTab(view))
            return;

        if (SaveCoroutines.TryGetValue(view, out var old) && old != null)
            view.StopCoroutine(old);

        SaveCoroutines[view] = view.StartCoroutine(SaveLater(view));
    }

    internal static void RequestSaveAfterUserInteraction(UI_Make view)
    {
        if (!Plugin.EnableMakePerfectMemory || SuppressSave || view == null || !view.gameObject.activeInHierarchy || !IsMakeTab(view))
            return;

        if (SaveCoroutines.TryGetValue(view, out var old) && old != null)
            view.StopCoroutine(old);

        SaveCoroutines[view] = view.StartCoroutine(SaveAfterUserInteractionLater(view));
    }

    internal static void RequestSaveAfterResourceInteraction(UI_Make view)
    {
        if (!Plugin.EnableMakePerfectMemory || SuppressSave || view == null || !view.gameObject.activeInHierarchy || !IsMakeTab(view))
            return;

        PendingResourceSaveUntilFrames[view] = Time.frameCount + 180;
        SaveNow(view);

        if (SaveCoroutines.TryGetValue(view, out var old) && old != null)
            view.StopCoroutine(old);

        SaveCoroutines[view] = view.StartCoroutine(SaveAfterResourceInteractionLater(view));
    }

    internal static void RequestSaveSelectionAfterUserInteraction(UI_Make view)
    {
        if (!Plugin.EnableMakePerfectMemory || SuppressSave || view == null || !view.gameObject.activeInHierarchy || !IsMakeTab(view))
            return;

        if (SaveSelectionCoroutines.TryGetValue(view, out var old) && old != null)
            view.StopCoroutine(old);

        SaveSelectionCoroutines[view] = view.StartCoroutine(SaveSelectionAfterUserInteractionLater(view));
    }

    internal static void RequestSaveIfObservedStateChanged(UI_Make view)
    {
        if (!Plugin.EnableMakePerfectMemory || SuppressSave || view == null || !view.gameObject.activeInHierarchy || !IsMakeTab(view))
            return;

        if (SuppressObservedSaveUntilFrames.TryGetValue(view, out var suppressUntilFrame) && Time.frameCount <= suppressUntilFrame)
            return;

        if (!TryBuildStateSignature(view, out var signature))
            return;

        if (LastObservedStateSignatures.TryGetValue(view, out var last) && last == signature)
            return;

        LastObservedStateSignatures[view] = signature;
        SaveNow(view);
    }

    private static IEnumerator RestoreStateLater(UI_Make view)
    {
        for (var i = 0; i < 4; i++)
            yield return null;

        RestoreCoroutines.Remove(view);
        RestoreState(view);
    }

    private static IEnumerator SaveLater(UI_Make view)
    {
        for (var i = 0; i < 2; i++)
            yield return null;

        SaveCoroutines.Remove(view);
        SaveNow(view);
    }

    private static IEnumerator SaveAfterUserInteractionLater(UI_Make view)
    {
        for (var i = 0; i < 6; i++)
            yield return null;

        SaveCoroutines.Remove(view);
        SaveNow(view);
    }

    private static IEnumerator SaveSelectionAfterUserInteractionLater(UI_Make view)
    {
        for (var i = 0; i < 8; i++)
            yield return null;

        SaveSelectionCoroutines.Remove(view);
        SaveSelectionNow(view);
    }

    private static IEnumerator SaveAfterResourceInteractionLater(UI_Make view)
    {
        var checkpoints = new[] { 2, 15, 45, 90 };
        var elapsed = 0;

        foreach (var checkpoint in checkpoints)
        {
            while (elapsed < checkpoint)
            {
                elapsed++;
                yield return null;
            }

            SaveNow(view);
        }

        SaveCoroutines.Remove(view);
    }

    private static void RestoreState(UI_Make view)
    {
        if (!Plugin.EnableMakePerfectMemory || view == null || !IsMakeTab(view))
            return;

        try
        {
            if (!TryGetLifeSkillType(view, out var lifeSkillType))
                return;

            if (!TryGetMakeProduct(view, out var productTypeId, out var productName))
                return;

            var selectionKey = BuildMakePerfectSelectionKey(lifeSkillType, productTypeId, productName);
            var selection = MemoryOptimizationSettingsStore.GetMakePerfectSelection(selectionKey);
            MakePerfectResourceMemoryEntry resource = null;
            if (TryGetSelectedMakeType(view, out var selectedSubtypeId, out var selectedTemplateId, out var selectedName))
            {
                var resourceKey = BuildMakePerfectResourceKey(lifeSkillType, productTypeId, selectedSubtypeId, selectedTemplateId, selectedName);
                resource = MemoryOptimizationSettingsStore.GetMakePerfectResource(resourceKey);
            }

            SuppressSave = true;
            try
            {
                Traverse.Create(view).Field("_makePerfect").SetValue(selection?.PerfectEnabled ?? false);
                AccessTools.Method(typeof(UI_Make), "RefreshButtonPerfect")?.Invoke(view, new object[] { false });

                if (resource != null)
                    RestoreResourceCounts(view, resource);

                AccessTools.Method(typeof(UI_Make), "CheckMakeCondition")?.Invoke(view, new object[] { false, null });
            }
            finally
            {
                SuppressSave = false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to restore make perfect memory: " + ex);
        }
    }

    private static void SaveNow(UI_Make view)
    {
        if (!Plugin.EnableMakePerfectMemory || SuppressSave || view == null || !IsMakeTab(view))
            return;

        try
        {
            if (!TryGetLifeSkillType(view, out var lifeSkillType))
                return;

            if (!TryGetMakeProduct(view, out var productTypeId, out var productName))
                return;

            if (!TryGetSelectedMakeType(view, out var selectedSubtypeId, out var selectedTemplateId, out var selectedName))
                return;

            var selectionKey = BuildMakePerfectSelectionKey(lifeSkillType, productTypeId, productName);
            var resourceKey = BuildMakePerfectResourceKey(lifeSkillType, productTypeId, selectedSubtypeId, selectedTemplateId, selectedName);
            var makePerfect = Traverse.Create(view).Field("_makePerfect").GetValue<bool>();
            var selection = new MakePerfectSelectionMemoryEntry
            {
                MemoryKey = selectionKey,
                LifeSkillType = lifeSkillType,
                TargetItemType = productTypeId,
                TargetTemplateId = -1,
                PerfectEnabled = makePerfect,
                HasSelection = true,
                SelectedSubtypeId = selectedSubtypeId,
                SelectedTemplateId = selectedTemplateId,
                SelectedName = productName
            };

            var resource = new MakePerfectResourceMemoryEntry
            {
                MemoryKey = resourceKey,
                LifeSkillType = lifeSkillType,
                TargetItemType = productTypeId,
                TargetTemplateId = -1,
                SelectedSubtypeId = selectedSubtypeId,
                SelectedTemplateId = selectedTemplateId,
                SelectedName = selectedName,
                Wood = GetCurrentResourceCount(view, 1),
                Metal = GetCurrentResourceCount(view, 2),
                Jade = GetCurrentResourceCount(view, 3),
                Fabric = GetCurrentResourceCount(view, 4)
            };

            MemoryOptimizationSettingsStore.SetMakePerfect(selection, resource);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save make perfect memory: " + ex);
        }
    }

    private static void SaveSelectionNow(UI_Make view)
    {
        if (!Plugin.EnableMakePerfectMemory || SuppressSave || view == null || !IsMakeTab(view))
            return;

        try
        {
            if (!TryGetLifeSkillType(view, out var lifeSkillType))
                return;

            if (!TryGetMakeProduct(view, out var productTypeId, out var productName))
                return;

            var makePerfect = Traverse.Create(view).Field("_makePerfect").GetValue<bool>();
            var selection = new MakePerfectSelectionMemoryEntry
            {
                MemoryKey = BuildMakePerfectSelectionKey(lifeSkillType, productTypeId, productName),
                LifeSkillType = lifeSkillType,
                TargetItemType = productTypeId,
                TargetTemplateId = -1,
                PerfectEnabled = makePerfect,
                HasSelection = true,
                SelectedSubtypeId = -1,
                SelectedTemplateId = -1,
                SelectedName = productName
            };

            MemoryOptimizationSettingsStore.SetMakePerfect(selection, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save make perfect selection memory: " + ex);
        }
    }

    private static bool IsMakeTab(UI_Make view)
    {
        try
        {
            var value = Traverse.Create(view).Field("_curTab").GetValue();
            return Convert.ToInt32(value) == MakeTabValue;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetLifeSkillType(UI_Make view, out sbyte lifeSkillType)
    {
        lifeSkillType = -1;

        lifeSkillType = Traverse.Create(view).Field("_curLifeSkillType").GetValue<sbyte>();
        return lifeSkillType >= 0;
    }

    private static string BuildMakePerfectSelectionKey(sbyte lifeSkillType, int productTypeId, string productName)
    {
        return string.Join("|", "make-perfect-selection-v3", lifeSkillType, productTypeId, productName ?? string.Empty);
    }

    private static string BuildMakePerfectResourceKey(sbyte lifeSkillType, int productTypeId, int subtypeId, int templateId, string name)
    {
        return string.Join("|", "make-perfect-resource-v3", lifeSkillType, productTypeId, subtypeId, templateId, name ?? string.Empty);
    }

    private static bool TryGetMakeProduct(UI_Make view, out int productTypeId, out string productName)
    {
        productTypeId = -1;
        productName = string.Empty;

        try
        {
            productTypeId = Traverse.Create(view).Field("_makeItemTypeId").GetValue<short>();
            if (productTypeId < 0)
                return false;

            productName = MakeItemType.Instance[(short)productTypeId]?.TypeName ?? string.Empty;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetSelectedMakeType(UI_Make view, out int subtypeId, out int templateId, out string name)
    {
        subtypeId = -1;
        templateId = -1;
        name = string.Empty;

        var traverse = Traverse.Create(view);
        var currentSubtypeId = traverse.Field("_makeItemSubTypeId").GetValue<short>();
        var currentTemplateId = traverse.Field("_makeItemTemplateId").GetValue<short>();
        var index = traverse.Field("_selectedMakeDropDownValue").GetValue<int>();

        if (TryGetDropdownDataAt(view, index, out var dropdownSubtypeId, out var dropdownTemplateId, out var dropdownName)
            && dropdownSubtypeId >= 0)
        {
            subtypeId = dropdownSubtypeId;
            templateId = dropdownTemplateId;
            name = dropdownName;
            return true;
        }

        if (currentSubtypeId >= 0)
        {
            subtypeId = currentSubtypeId;
            templateId = currentTemplateId >= -1 ? currentTemplateId : -1;
            name = GetSelectedName(view, subtypeId, templateId);
            return true;
        }

        return false;
    }

    private static int FindDropdownIndex(UI_Make view, int subtypeId, int templateId)
    {
        var dataList = Traverse.Create(view).Field("_curMakeDropdownDataList").GetValue() as System.Collections.IList;
        if (dataList == null)
            return -1;

        for (var i = 0; i < dataList.Count; i++)
        {
            if (!TryReadDropdownData(dataList[i], out var itemSubtypeId, out var itemTemplateId, out _) || itemSubtypeId < 0)
                continue;

            if (itemSubtypeId == subtypeId && (itemTemplateId == templateId || templateId < 0))
                return i;
        }

        return -1;
    }

    private static bool TryGetDropdownDataAt(UI_Make view, int index, out int subtypeId, out int templateId, out string name)
    {
        subtypeId = -1;
        templateId = -1;
        name = string.Empty;

        var dataList = Traverse.Create(view).Field("_curMakeDropdownDataList").GetValue() as System.Collections.IList;
        if (dataList == null || index < 0 || index >= dataList.Count)
            return false;

        return TryReadDropdownData(dataList[index], out subtypeId, out templateId, out name);
    }

    private static bool TryReadDropdownData(object data, out int subtypeId, out int templateId, out string name)
    {
        subtypeId = -1;
        templateId = -1;
        name = string.Empty;
        if (data == null)
            return false;

        var type = data.GetType();
        subtypeId = Convert.ToInt32(type.GetField("Item1")?.GetValue(data) ?? -1);
        templateId = Convert.ToInt32(type.GetField("Item2")?.GetValue(data) ?? -1);
        name = Convert.ToString(type.GetField("Item3")?.GetValue(data) ?? string.Empty);
        return true;
    }

    private static string GetSelectedName(UI_Make view, int subtypeId, int templateId)
    {
        var dataList = Traverse.Create(view).Field("_curMakeDropdownDataList").GetValue() as System.Collections.IList;
        if (dataList != null)
        {
            foreach (var item in dataList)
            {
                if (TryReadDropdownData(item, out var itemSubtypeId, out var itemTemplateId, out var itemName)
                    && itemSubtypeId == subtypeId
                    && (itemTemplateId == templateId || templateId < 0))
                    return itemName;
            }
        }

        try
        {
            return MakeItemSubType.Instance[(short)subtypeId]?.Name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int GetCurrentResourceCount(UI_Make view, sbyte resourceType)
    {
        var resourceInts = Traverse.Create(view).Field("_curMakeResourceCountInts").GetValue<GameData.Domains.Character.ResourceInts>();
        return GetResourceCount(resourceInts, resourceType);
    }

    private static bool TryBuildStateSignature(UI_Make view, out string signature)
    {
        signature = string.Empty;
        try
        {
            if (!TryGetLifeSkillType(view, out var lifeSkillType))
                return false;

            if (!TryGetMakeProduct(view, out var productTypeId, out var productName))
                return false;

            if (!TryGetSelectedMakeType(view, out var selectedSubtypeId, out var selectedTemplateId, out var selectedName))
                return false;

            var selectionKey = BuildMakePerfectSelectionKey(lifeSkillType, productTypeId, productName);
            var resourceKey = BuildMakePerfectResourceKey(lifeSkillType, productTypeId, selectedSubtypeId, selectedTemplateId, selectedName);
            var makePerfect = Traverse.Create(view).Field("_makePerfect").GetValue<bool>();
            signature = string.Join("|",
                selectionKey,
                resourceKey,
                makePerfect ? 1 : 0,
                GetCurrentResourceCount(view, 1),
                GetCurrentResourceCount(view, 2),
                GetCurrentResourceCount(view, 3),
                GetCurrentResourceCount(view, 4));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void RestoreResourceCounts(UI_Make view, MakePerfectResourceMemoryEntry memory)
    {
        var traverse = Traverse.Create(view);
        var currentField = traverse.Field("_curMakeResourceCountInts");
        var resourceInts = currentField.GetValue<GameData.Domains.Character.ResourceInts>();
        var totalMax = Math.Max(0, (int)traverse.Field("_maxMakeResourceTotalCount").GetValue<short>());
        var runningTotal = 0;

        RestoreResource(1, memory.Wood);
        RestoreResource(2, memory.Metal);
        RestoreResource(3, memory.Jade);
        RestoreResource(4, memory.Fabric);
        currentField.SetValue(resourceInts);

        void RestoreResource(sbyte type, int wanted)
        {
            var max = GetResourceMax(view, type);
            var value = Mathf.Clamp(wanted, 0, max);
            if (totalMax > 0)
                value = Mathf.Clamp(value, 0, Math.Max(0, totalMax - runningTotal));

            SetResourceCount(ref resourceInts, type, value);
            runningTotal += value;
        }
    }

    private static int GetResourceMax(UI_Make view, sbyte resourceType)
    {
        var maxResourceInts = Traverse.Create(view).Field("_maxMakeResourceCountInts").GetValue<GameData.Domains.Character.ResourceInts>();
        var max = GetResourceCount(maxResourceInts, resourceType);
        try
        {
            var makeCount = Traverse.Create(view).Field("_makeCount").GetValue<short>();
            var method = AccessTools.Method(typeof(UI_Make), "GetMaxMeetResourceCount");
            if (method != null)
                max = Math.Min(max, Convert.ToInt32(method.Invoke(view, new object[] { makeCount, resourceType })));
        }
        catch
        {
        }

        return Mathf.Max(0, max);
    }

    private static int GetResourceCount(GameData.Domains.Character.ResourceInts resourceInts, sbyte resourceType)
    {
        return resourceType >= 0 && resourceType < 8 ? resourceInts.Get(resourceType) : 0;
    }

    private static void SetResourceCount(ref GameData.Domains.Character.ResourceInts resourceInts, sbyte resourceType, int count)
    {
        if (resourceType < 0 || resourceType >= 8)
            return;

        resourceInts.Set(resourceType, count);
    }
}

[HarmonyPatch(typeof(UI_Make), "RefreshMakeDropDown")]
internal static class MakePerfectMemoryRestoreDropdownPatch
{
    private static void Postfix(UI_Make __instance)
    {
        MakePerfectMemoryController.RestoreSelection(__instance);
    }
}

[HarmonyPatch(typeof(UI_Make), "OnInit")]
internal static class MakePerfectMemoryInitPatch
{
    private static void Postfix(UI_Make __instance)
    {
        MakePerfectMemoryController.AttachResourceButtonSaveListeners(__instance);
    }
}

[HarmonyPatch(typeof(UI_Make), "InitButtonPerfect")]
internal static class MakePerfectMemoryInitButtonPatch
{
    private static void Postfix(UI_Make __instance)
    {
        MakePerfectMemoryController.AttachPerfectButtonSaveListener(__instance);
    }
}

[HarmonyPatch(typeof(UI_Make), "RefreshButtonPerfect")]
internal static class MakePerfectMemoryRestoreAfterPerfectButtonRefreshPatch
{
    private static void Postfix(UI_Make __instance)
    {
        MakePerfectMemoryController.RequestRestoreState(__instance);
    }
}

[HarmonyPatch(typeof(UI_Make), "OnMakeDropdownValueChanged")]
internal static class MakePerfectMemorySaveDropdownPatch
{
    private static void Postfix(UI_Make __instance)
    {
        MakePerfectMemoryController.RequestRestoreState(__instance);
    }
}

[HarmonyPatch(typeof(UI_Make), "SelectMakeItemSubType")]
internal static class MakePerfectMemoryRestoreSubtypePatch
{
    private static void Postfix(UI_Make __instance)
    {
        MakePerfectMemoryController.RequestRestoreState(__instance);
    }
}

[HarmonyPatch(typeof(UI_Make), "OnSelectMakePageIndexChange")]
internal static class MakePerfectMemoryRestoreProductPatch
{
    private static void Postfix(UI_Make __instance)
    {
        MakePerfectMemoryController.RequestRestoreState(__instance);
    }
}

[HarmonyPatch(typeof(UI_Make), "ChangeCurrentTargetOnMake")]
internal static class MakePerfectMemoryRestoreTargetPatch
{
    private static void Postfix(UI_Make __instance)
    {
        MakePerfectMemoryController.RequestRestoreState(__instance);
    }
}

[HarmonyPatch(typeof(UI_Make), "ChangeMakeResourceCount")]
internal static class MakePerfectMemorySaveResourcePatch
{
    private static void Postfix(UI_Make __instance)
    {
        MakePerfectMemoryController.RequestSaveAfterResourceInteraction(__instance);
    }
}

[HarmonyPatch(typeof(UI_Make), "CheckMakeCondition")]
internal static class MakePerfectMemorySaveObservedStatePatch
{
    private static void Postfix(UI_Make __instance)
    {
        MakePerfectMemoryController.RequestSaveIfObservedStateChanged(__instance);
    }
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
