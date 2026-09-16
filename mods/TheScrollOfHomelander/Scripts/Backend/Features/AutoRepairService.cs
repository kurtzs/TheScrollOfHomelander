#nullable disable

using System;
using System.Collections.Generic;
namespace BetterTaiwuScroll.Backend;

internal static class AutoRepairService
{
    internal const string MethodName = BetterTaiwuScroll.Shared.ModProtocol.AutoRepair;

    internal static void Register(string modId)
    {
        DomainManager.Mod.AddModMethod(modId, MethodName, Execute);
    }

    private static SerializableModData Execute(DataContext context, SerializableModData parameter)
    {
        try
        {
            if (context == null)
                return CreateResponse(false, "invalid_context", 0, 0, "自动修理缺少有效的数据上下文。");

            if (parameter == null || !parameter.Get(BetterTaiwuScroll.Shared.ModProtocol.VersionKey, out int version)
                || version != BetterTaiwuScroll.Shared.ModProtocol.Version)
                return CreateResponse(false, "invalid_request", 0, 0, "自动修理请求版本无效。");

            var enabled = ReadBool(parameter, "enabled", true);
            if (!enabled)
                return CreateResponse(true, "disabled", 0, 0, "自动修理未启用。");

            var protectDurability = ReadBool(parameter, "protectToolDurability", true);
            var toolGradePriority = ReadInt(parameter, "toolGradePriority", 1);
            var onlyBareHandRepair = ReadBool(parameter, "onlyBareHandRepair", false);
            var allowBareHandRepair = ReadBool(parameter, "allowBareHandRepair", true);
            if (onlyBareHandRepair)
                allowBareHandRepair = true;
            toolGradePriority = Math.Max(0, Math.Min(1, toolGradePriority));

            var taiwuCharId = DomainManager.Taiwu.GetTaiwuCharId();
            if (taiwuCharId < 0)
                return CreateResponse(true, "no_taiwu", 0, 0, "当前没有有效的太吾角色。");

            var taiwu = DomainManager.Character.GetElement_Objects(taiwuCharId);
            if (taiwu == null)
                return CreateResponse(true, "no_taiwu", 0, 0, "当前没有有效的太吾角色。");

            var damagedWeapons = GetDamagedEquippedWeapons(taiwu.GetEquipment());
            if (damagedWeapons.Count == 0)
                return CreateResponse(true, "no_repair_needed", 0, 0, "已装备武器均不需要修理。");

            var repairedCount = 0;
            var skippedCount = 0;
            var skippedNoEligibleToolOrCondition = 0;
            var skippedException = 0;
            string firstExceptionDetail = null;
            foreach (var weaponKey in damagedWeapons)
            {
                var candidate = default(ToolCandidate);
                try
                {
                    if (!onlyBareHandRepair)
                    {
                        candidate = FindBestTool(
                            taiwuCharId,
                            weaponKey,
                            toolGradePriority,
                            protectDurability);
                    }

                    if (!candidate.Valid && allowBareHandRepair)
                        candidate = FindBareHandTool(context, taiwuCharId, weaponKey);

                    if (!candidate.Valid)
                    {
                        skippedCount++;
                        skippedNoEligibleToolOrCondition++;
                        continue;
                    }

                    // FindBestTool has just performed the complete vanilla repair-condition
                    // check for this exact tool/weapon pair. Repeating it here can disagree with
                    // the first query during combat settlement and incorrectly skip the repair.
                    DomainManager.Building.RepairItemOptional(
                        context,
                        taiwuCharId,
                        candidate.ToolKey,
                        weaponKey,
                        (sbyte)candidate.Source);
                    repairedCount++;
                }
                catch (Exception ex)
                {
                    skippedCount++;
                    skippedException++;
                    var detail = "weapon=" + FormatItemKey(weaponKey)
                        + ", tool=" + (candidate.Valid ? FormatItemKey(candidate.ToolKey) : "invalid")
                        + ", mode=" + (candidate.Valid && candidate.IsBareHand ? "bare_hand" : "tool")
                        + ", source=" + (candidate.Valid ? candidate.Source.ToString() : "unknown")
                        + ", exception=" + ex;
                    if (firstExceptionDetail == null)
                        firstExceptionDetail = ex.GetType().Name + ": " + ex.Message;
                    GameData.Utilities.AdaptableLog.Error(
                        "[BetterTaiwuScroll] Auto repair failed for one weapon: " + detail);
                }
            }

            var code = repairedCount == 0 ? "nothing_repaired"
                : skippedCount == 0 ? "ok"
                : "partial";
            var message = "自动修理完成：修理 " + repairedCount + " 把武器，跳过 " + skippedCount
                + " 把武器（无合规工具或资源/造诣不足 " + skippedNoEligibleToolOrCondition
                + "，异常 " + skippedException + "）。"
                + (firstExceptionDetail == null ? string.Empty : " 首个异常：" + firstExceptionDetail);
            return CreateResponse(true, code, repairedCount, skippedCount, message);
        }
        catch (Exception ex)
        {
            return CreateResponse(false, "exception", 0, 0, "自动修理执行失败：" + ex.Message);
        }
    }

    private static string FormatItemKey(ItemKey key)
    {
        return "{type=" + key.ItemType + ", template=" + key.TemplateId + ", id=" + key.Id + "}";
    }

    private static List<ItemKey> GetDamagedEquippedWeapons(ItemKey[] equipment)
    {
        var result = new List<ItemKey>();
        if (equipment == null)
            return result;

        foreach (var itemKey in equipment)
        {
            if (!itemKey.IsValid() || itemKey.ItemType != ItemType.Weapon)
                continue;

            var item = DomainManager.Item.TryGetBaseItem(itemKey);
            if (item == null || item.GetCurrDurability() >= item.GetMaxDurability())
                continue;

            result.Add(itemKey);
        }

        return result;
    }

    private static ToolCandidate FindBestTool(
        int taiwuCharId,
        ItemKey weaponKey,
        int toolGradePriority,
        bool protectDurability)
    {
        var best = default(ToolCandidate);
        var weaponGrade = ItemTemplateHelper.GetGrade(weaponKey.ItemType, weaponKey.TemplateId);
        var requiredLifeSkillType = ItemTemplateHelper.GetCraftRequiredLifeSkillType(
            weaponKey.ItemType,
            weaponKey.TemplateId);

        const ItemSourceType source = ItemSourceType.Inventory;
        var items = DomainManager.Taiwu.GetItems(source);
        if (items == null)
            return best;

        foreach (var pair in items)
        {
            var toolKey = pair.Key;
            if (pair.Value <= 0 || !toolKey.IsValid() || toolKey.ItemType != ItemType.CraftTool
                || ItemTemplateHelper.IsEmptyTool(toolKey.ItemType, toolKey.TemplateId))
                continue;

            if (!DomainManager.Item.TryGetElement_CraftTools(toolKey.Id, out var tool))
                continue;

            var config = Config.CraftTool.Instance[toolKey.TemplateId];
            if (config == null || config.RequiredLifeSkillTypes == null
                || !config.RequiredLifeSkillTypes.Contains(requiredLifeSkillType))
                continue;

            var durabilityCost = config.DurabilityCost[weaponGrade];
            var durability = tool.GetCurrDurability();
            if (durability <= 0 || protectDurability && durability <= durabilityCost)
                continue;

            if (!DomainManager.Building.CheckRepairConditionIsMeet(
                    taiwuCharId,
                    toolKey,
                    weaponKey,
                    BuildingBlockKey.Invalid))
                continue;

            var grade = ItemTemplateHelper.GetGrade(toolKey.ItemType, toolKey.TemplateId);
            var candidate = new ToolCandidate(
                toolKey,
                source,
                grade,
                durability - durabilityCost);
            if (!best.Valid || IsBetter(candidate, best, toolGradePriority))
                best = candidate;
        }

        return best;
    }

    private static ToolCandidate FindBareHandTool(
        DataContext context,
        int taiwuCharId,
        ItemKey weaponKey)
    {
        var emptyToolKey = DomainManager.Item.GetEmptyToolKey(context);
        if (!emptyToolKey.IsValid()
            || emptyToolKey.ItemType != ItemType.CraftTool
            || !ItemTemplateHelper.IsEmptyTool(emptyToolKey.ItemType, emptyToolKey.TemplateId))
            return default;

        if (!DomainManager.Building.CheckRepairConditionIsMeet(
                taiwuCharId,
                emptyToolKey,
                weaponKey,
                BuildingBlockKey.Invalid))
            return default;

        return new ToolCandidate(
            emptyToolKey,
            ItemSourceType.Inventory,
            ItemTemplateHelper.GetGrade(emptyToolKey.ItemType, emptyToolKey.TemplateId),
            int.MaxValue,
            true);
    }

    private static bool IsBetter(ToolCandidate candidate, ToolCandidate current, int priority)
    {
        if (candidate.Grade != current.Grade)
            return priority == 1
                ? candidate.Grade < current.Grade
                : candidate.Grade > current.Grade;

        if (candidate.RemainingDurability != current.RemainingDurability)
            return candidate.RemainingDurability < current.RemainingDurability;

        if (candidate.Source != current.Source)
            return candidate.Source < current.Source;

        return candidate.ToolKey.Id < current.ToolKey.Id;
    }

    private static bool ReadBool(SerializableModData data, string key, bool fallback)
    {
        return data.Get(key, out bool value) ? value : fallback;
    }

    private static int ReadInt(SerializableModData data, string key, int fallback)
    {
        return data.Get(key, out int value) ? value : fallback;
    }

    private static SerializableModData CreateResponse(
        bool success,
        string code,
        int repairedCount,
        int skippedCount,
        string message)
    {
        var response = new SerializableModData();
        response.Set("success", success);
        response.Set("code", code ?? string.Empty);
        response.Set("repairedCount", repairedCount);
        response.Set("skippedCount", skippedCount);
        response.Set("message", message ?? string.Empty);
        return response;
    }

    private readonly struct ToolCandidate
    {
        internal readonly ItemKey ToolKey;
        internal readonly ItemSourceType Source;
        internal readonly sbyte Grade;
        internal readonly int RemainingDurability;
        internal readonly bool IsBareHand;
        private readonly bool _exists;

        internal bool Valid => _exists
            && Source == ItemSourceType.Inventory
            && ToolKey.IsValid()
            && ToolKey.ItemType == ItemType.CraftTool;

        internal ToolCandidate(
            ItemKey toolKey,
            ItemSourceType source,
            sbyte grade,
            int remainingDurability,
            bool isBareHand = false)
        {
            ToolKey = toolKey;
            Source = source;
            Grade = grade;
            RemainingDurability = remainingDurability;
            IsBareHand = isBareHand;
            _exists = true;
        }
    }
}
