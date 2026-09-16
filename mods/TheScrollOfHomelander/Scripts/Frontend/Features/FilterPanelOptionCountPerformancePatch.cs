#nullable disable

using System.Collections.Generic;
using System.Reflection;
using Game.Components.SortAndFilter;
using HarmonyLib;

namespace BetterTaiwuScroll.Frontend;

// Vanilla scans the complete option-count list once per visible filter section.
// Group the same sequence once, then feed each section the same count array.
[HarmonyPatch(typeof(FilterPanel), nameof(FilterPanel.RefreshFilterOptionCounts))]
internal static class FilterPanelOptionCountPerformancePatch
{
    [HarmonyPrepare]
    private static bool Prepare() => GamePerformancePatchLifecycle.AllowInstallation;

    private static readonly FieldInfo SectionMapField =
        AccessTools.Field(typeof(FilterPanel), "_sectionMap");

    private sealed class CountGroup
    {
        internal int AllCount;
        internal bool Found;
        internal readonly List<int> OptionCounts = new();
    }

    private static bool Prefix(FilterPanel __instance, IReadOnlyList<OptionCountData> optionCounts)
    {
        if (!Plugin.IsGameFilterOptionCountOptimizationEnabled
            || __instance == null
            || optionCounts == null)
            return true;

        var sectionMap = SectionMapField?.GetValue(__instance)
            as Dictionary<(int LineId, int MenuId), FilterSection>;
        if (sectionMap == null || sectionMap.Count <= 1)
            return true;

        var groups = new Dictionary<(int LineId, int MenuId), CountGroup>(sectionMap.Count);
        for (var i = 0; i < optionCounts.Count; i++)
        {
            var count = optionCounts[i];
            var key = (count.LineId, count.MenuId);
            if (!sectionMap.ContainsKey(key))
                continue;

            if (!groups.TryGetValue(key, out var group))
            {
                group = new CountGroup();
                groups.Add(key, group);
            }

            group.Found = true;
            if (count.OptionIndex < 0)
                group.AllCount = count.Count;
            else
                group.OptionCounts.Add(count.Count);
        }

        foreach (var entry in sectionMap)
        {
            if (entry.Value == null
                || !groups.TryGetValue(entry.Key, out var group)
                || !group.Found)
            {
                continue;
            }

            var counts = new int[group.OptionCounts.Count + 1];
            counts[0] = group.AllCount;
            for (var i = 0; i < group.OptionCounts.Count; i++)
                counts[i + 1] = group.OptionCounts[i];
            entry.Value.RefreshOptionCounts(counts);
        }

        return false;
    }
}
