#nullable disable

using System.Collections.Generic;
using Config;
using Game.Views.Make;
using GameData.Domains.Item.Display;
using HarmonyLib;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

internal static class MakeFoodTargetSupport
{
    internal const short CombinedFoodTargetSubType = 799;

    internal static bool IsCombinedFoodTarget(short itemSubType)
    {
        return itemSubType == CombinedFoodTargetSubType;
    }

    internal static bool IsFoodRandomSubType(short itemSubType)
    {
        return itemSubType == 700 || itemSubType == 701;
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "Init")]
internal static class MakeSubPageMakeInitFoodTargetPatch
{
    private static void Postfix(MakeSubPageMake __instance, ViewMake parentView)
    {
        if (__instance == null || parentView == null || parentView.CurLifeSkillType != 14)
            return;

        var list = Traverse.Create(__instance).Field("_canMakeItemSubTypeList").GetValue<List<short>>();
        if (list == null || list.Contains(MakeFoodTargetSupport.CombinedFoodTargetSubType))
            return;

        list.Insert(0, MakeFoodTargetSupport.CombinedFoodTargetSubType);
    }
}

[HarmonyPatch(typeof(MakeSubPageMakeHelper), nameof(MakeSubPageMakeHelper.GetRandomMakeIcon))]
internal static class MakeSubPageMakeHelperFoodTargetIconPatch
{
    private static void Postfix(short tempId, ref string __result)
    {
        if (MakeFoodTargetSupport.IsCombinedFoodTarget(tempId))
            __result = "ui9_icon_maketype_all";
    }
}

[HarmonyPatch(typeof(MakeSubPageMakeHelper), nameof(MakeSubPageMakeHelper.GetRandomMakeTypeName))]
internal static class MakeSubPageMakeHelperFoodTargetNamePatch
{
    private static void Postfix(short tempId, ref string __result)
    {
        if (MakeFoodTargetSupport.IsCombinedFoodTarget(tempId))
            __result = "食物";
    }
}

[HarmonyPatch(typeof(MakeSubPageMakeHelper), nameof(MakeSubPageMakeHelper.CheckCanMakeTargetRandomType))]
internal static class MakeSubPageMakeHelperFoodTargetMaterialPatch
{
    private static bool Prefix(short itemSubType, ItemDisplayData materialData, ref bool __result)
    {
        if (!MakeFoodTargetSupport.IsCombinedFoodTarget(itemSubType))
            return true;

        __result = false;
        if (materialData == null)
            return false;

        var craftableItemTypes = Config.Material.Instance[materialData.RealKey.TemplateId].CraftableItemTypes;
        foreach (var makeItemTypeId in craftableItemTypes)
        {
            if (MakeFoodTargetSupport.IsFoodRandomSubType(MakeItemType.Instance[makeItemTypeId].ItemSubType))
            {
                __result = true;
                break;
            }
        }

        return false;
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "RefreshMakeTypeDropdown")]
internal static class MakeSubPageMakeRefreshFoodTargetMakeTypePatch
{
    private static bool Prefix(MakeSubPageMake __instance)
    {
        if (__instance == null)
            return true;

        var traverse = Traverse.Create(__instance);
        var targetSlot = traverse.Field("targetSlot").GetValue<MakeTargetSlot>();
        var materialSlot = traverse.Field("materialSlot").GetValue<MakeTargetSlot>();
        var targetData = targetSlot?.ItemData;
        if (!MakeSubPageMakeHelper.CheckIsRandomMake(targetData)
            || !MakeFoodTargetSupport.IsCombinedFoodTarget(targetData.Key.TemplateId)
            || materialSlot == null
            || !materialSlot.IsValid)
            return true;

        var materialItem = Config.Material.Instance[materialSlot.ItemData.RealKey.TemplateId];
        var makeTypeList = traverse.Field("_makeTypeList").GetValue<List<short>>();
        var makeTypeDict = traverse.Field("_makeTypeDict").GetValue<Dictionary<short, List<short>>>();
        makeTypeList?.Clear();
        makeTypeDict?.Clear();

        short selectedMakeItemTypeId = -1;
        foreach (var makeItemTypeId in materialItem.CraftableItemTypes)
        {
            var makeItemTypeItem = MakeItemType.Instance[makeItemTypeId];
            makeTypeList?.Add(makeItemTypeId);
            if (makeTypeDict != null)
                makeTypeDict[makeItemTypeId] = makeItemTypeItem.MakeItemSubTypes;

            if (selectedMakeItemTypeId < 0 && MakeFoodTargetSupport.IsFoodRandomSubType(makeItemTypeItem.ItemSubType))
                selectedMakeItemTypeId = makeItemTypeId;
        }

        if (selectedMakeItemTypeId < 0)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Combined food target found no matching make item type for material: " + materialItem.TemplateId);
            return false;
        }

        var subTypes = MakeItemType.Instance[selectedMakeItemTypeId].MakeItemSubTypes;
        traverse.Field("_makeItemTypeId").SetValue(selectedMakeItemTypeId);
        traverse.Field("_makeItemSubtypeIdList").SetValue(subTypes);
        traverse.Field("_makeItemSubTypeId").SetValue(subTypes != null && subTypes.Count > 0 ? subTypes[Random.Range(0, subTypes.Count)] : (short)-1);
        traverse.Field("_isManual").SetValue(false);
        targetSlot.Refresh();
        return false;
    }
}
