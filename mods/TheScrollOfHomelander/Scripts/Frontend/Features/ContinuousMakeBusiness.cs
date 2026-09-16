#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using GameData.Domains.Building;
using GameData.Domains.Item.Display;
using GameData.Domains.TaiwuEvent;
using GameData.Serializer;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

internal static partial class ContinuousMakeExecutionController
{
    private static IEnumerator MakeOnceWithoutViewRefresh(MakeSubPageMake page, ItemDisplayData selectedMaterial,
        MakeResult currentMakeResult, Action onSubmitted, Action<DirectMakeResult> setResult)
    {
        var current = MakeExecutionLifetime.Capture(page);
        setResult(DirectMakeResult.Fail);
        var view = MakeSelectMaterialPatch.GetParentView(page);
        if (page == null || view == null || selectedMaterial == null)
            yield break;

        DirectMakeArguments arguments;
        try
        {
            if (!IsSelectedMaterial(page, selectedMaterial)
                || !TryBuildDirectMakeArguments(page, view, currentMakeResult, out arguments))
            {
                Debug.LogWarning("[BetterTaiwuScroll] Continuous make stopped: failed to build native make arguments.");
                yield break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to build continuous make arguments: " + ex.Message);
            yield break;
        }

        var selectedTool = Traverse.Create(page).Field("toolSlot").GetValue<MakeTargetSlot>()?.ItemData;
        var checkDone = false;
        var canMake = false;
        try
        {
            BuildingDomainMethod.AsyncCall.CheckMakeCondition(view, arguments.Condition, (offset, dataPool) =>
            {
                if (!current()) return;
                try
                {
                    Serializer.Deserialize(dataPool, offset, ref canMake);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[BetterTaiwuScroll] Continuous make condition deserialize failed: " + ex.Message);
                }
                finally
                {
                    checkDone = true;
                }
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make condition request failed: " + ex.Message);
            yield break;
        }

        for (var i = 0; i < 120 && !checkDone; i++)
            yield return null;

        if (!checkDone || !canMake)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make stopped: native CheckMakeCondition returned "
                + (checkDone ? "false" : "no response") + ".");
            UIElement.FullScreenMask.Hide();
            yield break;
        }

        if (!ShouldContinue(page))
            yield break;

        try
        {
            BuildingDomainMethod.Call.StartMakeItem(view.Element.GameDataListenerId, arguments.Start);
            onSubmitted();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make start failed: " + ex.Message);
            yield break;
        }

        var itemsDone = false;
        List<ItemDisplayData> itemDataList = null;
        try
        {
            BuildingDomainMethod.AsyncCall.GetMakeItems(view, view.BuildingBlockKey, (offset, pool) =>
            {
                if (!current()) return;
                try
                {
                    Serializer.Deserialize(pool, offset, ref itemDataList);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[BetterTaiwuScroll] Continuous make item deserialize failed: " + ex.Message);
                }
                finally
                {
                    itemsDone = true;
                }
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make item request failed: " + ex.Message);
            yield break;
        }

        for (var i = 0; i < 120 && !itemsDone; i++)
            yield return null;

        if (!itemsDone || itemDataList == null)
            yield break;

        try
        {
            TaiwuEventDomainMethod.Call.OnCollectedMakingSystemItem(view.BuildingBlockKey, view.BlockData.TemplateId, showingGetItem: true);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Continuous make collect event failed: " + ex.Message);
        }

        var collector = GetOrCreateCollector(view);
        collector.ExpectedBatchCount++;
        collector.CapturedBatchCount++;
        collector.LastCaptureFrame = Time.frameCount;
        collector.InWarehouse = arguments.InWarehouse;
        collector.AddItems(itemDataList);
        AddTutorialCloseAction(page, collector, itemDataList);
        SaveLastMakeResourceCounts(page, arguments.ResourceCount);
        setResult(new DirectMakeResult(true, arguments.MakeCount, selectedTool));
    }

    private static bool TryBuildDirectMakeArguments(MakeSubPageMake page, ViewMake view,
        MakeResult currentMakeResult, out DirectMakeArguments arguments)
    {
        arguments = default;
        var traverse = Traverse.Create(page);
        var targetSlot = traverse.Field("targetSlot").GetValue<MakeTargetSlot>();
        var materialSlot = traverse.Field("materialSlot").GetValue<MakeTargetSlot>();
        var toolSlot = traverse.Field("toolSlot").GetValue<MakeTargetSlot>();
        if (targetSlot == null || materialSlot == null || toolSlot == null
            || !targetSlot.IsValid || !materialSlot.IsValid || !toolSlot.IsValid)
            return false;

        var makeCount = Math.Max(1, GetIntField(traverse, "_makeCount", 1));
        if (makeCount > materialSlot.ItemData.Amount)
            return false;
        var makeItemTypeId = (short)GetIntField(traverse, "_makeItemTypeId", -1);
        var makeItemSubTypeId = (short)GetIntField(traverse, "_makeItemSubTypeId", -1);
        if (makeItemTypeId < 0 || makeItemSubTypeId < 0)
            return false;

        var resourceCount = traverse.Field("_curMakeResourceCountInts").GetValue<GameData.Domains.Character.ResourceInts>();
        var needResource = traverse.Field("_makeRequiredResourceInts").GetValue<GameData.Domains.Character.ResourceInts>();
        var isManual = GetBoolField(traverse, "_isManual");
        var isPerfect = MakeGameApi.GetIsPerfect(page);
        var manualFoodTemplateId = (short)(isManual
            && targetSlot.ItemData.RealKey.ItemType == 7
            ? targetSlot.ItemData.RealKey.TemplateId
            : -1);
        var itemList = BuildMakeResultTemplateList(targetSlot, makeCount, currentMakeResult);
        if (itemList == null || itemList.Count == 0)
            return false;

        var condition = new MakeConditionArguments
        {
            BuildingBlockKey = view.BuildingBlockKey,
            CharId = view.TaiwuCharId,
            IsManual = isManual,
            MakeCount = (short)makeCount,
            MakeItemSubTypeId = makeItemSubTypeId,
            MakeItemTypeId = makeItemTypeId,
            MaterialKey = materialSlot.ItemData.RealKey,
            ResourceCount = resourceCount,
            ToolKey = toolSlot.ItemData.RealKey,
            IsPerfect = isPerfect,
            ManulFoodTemplateId = manualFoodTemplateId
        };

        var makeItemSubTypeItem = Config.MakeItemSubType.Instance[makeItemSubTypeId];
        var start = new StartMakeArguments
        {
            CharId = view.TaiwuCharId,
            BuildingBlockKey = view.BuildingBlockKey,
            Tool = toolSlot.ItemData?.Clone(),
            Material = materialSlot.ItemData?.Clone(),
            ItemList = itemList,
            ItemType = makeItemSubTypeItem.Result.ItemType,
            MakeItemSubTypeId = makeItemSubTypeId,
            ResourceCount = resourceCount,
            NeedResource = needResource,
            EquipmentEffectId = GetPerfectEffectId(page, traverse, isPerfect)
        };

        var displayData = traverse.Field("DisplayData").GetValue<BuildingMakeDisplayData>();
        arguments = new DirectMakeArguments(condition, start, resourceCount, makeCount, displayData != null && !displayData.CanTransferItemToWarehouse);
        return true;
    }

    private static List<short> BuildMakeResultTemplateList(MakeTargetSlot targetSlot, int makeCount, MakeResult makeResult)
    {
        var result = new List<short>(makeCount);
        var randomMake = MakeSubPageMakeHelper.CheckIsRandomMake(targetSlot.ItemData);
        if (!randomMake)
        {
            for (var i = 0; i < makeCount; i++)
                result.Add(targetSlot.ItemData.RealKey.TemplateId);
            return result;
        }

        if (!IsValidRandomMakeResult(makeResult))
            return null;
        var stage = makeResult.TargetResultStage;
        for (var i = 0; i < makeCount; i++)
        {
            if (stage.TemplateId >= 0)
            {
                result.Add(stage.TemplateId);
                continue;
            }

            if (stage.TemplateIdList == null || stage.TemplateIdList.Count == 0)
                return null;

            result.Add(stage.TemplateIdList[UnityEngine.Random.Range(0, stage.TemplateIdList.Count)]);
        }

        return result;
    }

    private static short GetPerfectEffectId(MakeSubPageMake page, Traverse traverse, bool isPerfect)
    {
        if (!isPerfect)
            return -1;

        var perfectEffectIdList = traverse.Field("_perfectEffectIdList").GetValue<List<short>>();
        var perfectDropdown = traverse.Field("perfectDropdown").GetValue<CDropdown>();
        if (perfectEffectIdList == null || perfectDropdown == null || perfectEffectIdList.Count == 0)
            return -1;

        var selection = perfectDropdown.value;
        if (selection == 1)
            return perfectEffectIdList[UnityEngine.Random.Range(0, perfectEffectIdList.Count)];
        var index = selection - 2;
        return index >= 0 && index < perfectEffectIdList.Count ? perfectEffectIdList[index] : (short)-1;
    }

}
