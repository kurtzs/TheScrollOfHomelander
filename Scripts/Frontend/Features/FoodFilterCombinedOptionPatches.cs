#nullable disable

using System.Collections.Generic;
using GameData.Domains.Item;
using GameData.Domains.Item.Display;
using HarmonyLib;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(Game.Components.SortAndFilter.Item.FoodFilterLine), "GetFilterToggleConfigs")]
internal static class FoodFilterLineCombinedOptionConfigPatch
{
    private const string FoodCombinedLabel = "食物";
    internal const int FoodCombinedOptionIndex = 4;

    private static void Postfix(ref List<Game.Components.SortAndFilter.FilterToggleConfig> __result)
    {
        if (__result == null)
            return;

        if (__result.Count > FoodCombinedOptionIndex)
            return;

        __result.Add(new Game.Components.SortAndFilter.FilterToggleConfig("ui9_btn_filter_vegan", StringKey.CreateDirect(FoodCombinedLabel)));
    }
}

[HarmonyPatch(typeof(Game.Components.SortAndFilter.Item.FoodFilterLine), "IsDataMatch")]
internal static class FoodFilterLineCombinedOptionMatchPatch
{
    private static bool Prefix(ITradeableContent data, Game.Components.SortAndFilter.LineState lineState, ref bool __result)
    {
        if (lineState.ToggleGroupState.IsAll || lineState.ToggleGroupState.Index != FoodFilterLineCombinedOptionConfigPatch.FoodCombinedOptionIndex)
            return true;

        var itemSubType = ItemTemplateHelper.GetItemSubType(data.RealKey.ItemType, data.RealKey.TemplateId);
        __result = itemSubType == 700 || itemSubType == 701;
        return false;
    }
}
