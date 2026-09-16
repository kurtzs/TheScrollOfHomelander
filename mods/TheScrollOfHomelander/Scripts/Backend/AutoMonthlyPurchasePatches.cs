#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using Config;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Building;
using GameData.Domains.Character;
using GameData.Domains.Item;
using GameData.Domains.Item.Display;
using GameData.Domains.Map;
using GameData.Domains.Taiwu;
using GameData.Domains.Taiwu.Display;
using GameData.Domains.Taiwu.ExchangeSystem;
using GameData.Domains.World;
using GameData.Utilities;
using HarmonyLib;
using Newtonsoft.Json;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(WorldDomain), nameof(WorldDomain.AdvanceMonth), new[] { typeof(DataContext) })]
internal static class AutoMonthlyPurchaseAdvanceMonthPatch
{
    private static void Postfix(DataContext context)
    {
        AutoMonthlyPurchaseService.Run(context);
    }
}

internal sealed class AutoMonthlyPurchaseSettings
{
    public int LowestPurchaseGrade = 9;
    public int HighestPurchaseGrade = 1;
    public bool SkipPriceIncreasedItems = true;
    public bool SkipOriginalPriceItems;
    public bool IncludeLimitedPurityLockedLevels;
    public bool IncludeMedicineMaterials = true;
    public bool IncludePoisonMaterials;
    public bool AutoPurchaseGroupMerchants;
    public int AutoPurchaseTargetSource = 1;
    public bool AutoPurchaseOnlyOnIndustryTile;
    public bool AutoPurchaseInventoryLoadProtection = true;
    public int AutoPurchaseInventoryLoadThresholdPercent = 90;
    public bool AutoPurchaseSkipInventoryOutsideIndustry = true;

    internal void Normalize()
    {
        HighestPurchaseGrade = Clamp(HighestPurchaseGrade, 1, 9);
        LowestPurchaseGrade = Clamp(LowestPurchaseGrade, 1, 9);
        if (HighestPurchaseGrade > LowestPurchaseGrade)
            LowestPurchaseGrade = HighestPurchaseGrade;
        AutoPurchaseTargetSource = Clamp(AutoPurchaseTargetSource, 0, 2);
        AutoPurchaseInventoryLoadThresholdPercent = Clamp(
            AutoPurchaseInventoryLoadThresholdPercent,
            10,
            100);
    }

    internal ItemSourceType GetTargetItemSource()
    {
        return AutoPurchaseTargetSource switch
        {
            1 => ItemSourceType.Warehouse,
            2 => ItemSourceType.Treasury,
            _ => ItemSourceType.Inventory,
        };
    }

    private static int Clamp(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }
}

internal static class AutoMonthlyPurchaseSettingsStore
{
    private const string FileName = BetterTaiwuScroll.Shared.ModProtocol.PurchaseSettings;

    internal static AutoMonthlyPurchaseSettings Load()
    {
        foreach (var path in GetCandidates())
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                var settings = JsonConvert.DeserializeObject<AutoMonthlyPurchaseSettings>(File.ReadAllText(path))
                               ?? new AutoMonthlyPurchaseSettings();
                settings.Normalize();
                return settings;
            }
            catch (Exception ex)
            {
                AdaptableLog.Warning(
                    "[BetterTaiwuScroll] Failed to load auto monthly purchase settings from "
                    + path + ": " + ex.Message,
                    false);
            }
        }

        var defaults = new AutoMonthlyPurchaseSettings();
        defaults.Normalize();
        return defaults;
    }

    private static IEnumerable<string> GetCandidates()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrEmpty(documents))
            yield return Path.Combine(documents, "TheScrollOfHomelander", FileName);

        var assemblyPath = typeof(Plugin).Assembly.Location;
        if (!string.IsNullOrEmpty(assemblyPath))
        {
            var modRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(assemblyPath), "..", ".."));
            yield return Path.Combine(modRoot, "UserData", FileName);
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(appData))
        {
            yield return Path.Combine(
                appData,
                "TaiwuStudio",
                "Taiwu Studio",
                "data",
                "mods",
                "TheScrollOfHomelander",
                "UserData",
                FileName);
        }
    }
}

internal static class AutoMonthlyPurchaseService
{
    private const int MaterialItemType = 5;
    private const short MedicineMaterialSubType = 505;
    private const short PoisonMaterialSubType = 506;

    internal static void Run(DataContext context)
    {
        var settings = AutoMonthlyPurchaseSettingsStore.Load();
        if (!settings.AutoPurchaseGroupMerchants)
            return;

        try
        {
            var taiwu = DomainManager.Taiwu.GetTaiwu();
            var location = taiwu.GetLocation();
            var isIndustryTile = IsIndustryTile(location);
            var targetSource = settings.GetTargetItemSource();

            if (settings.AutoPurchaseOnlyOnIndustryTile && !isIndustryTile)
            {
                AdaptableLog.Info("[BetterTaiwuScroll] Auto monthly purchase skipped: not on an industry tile.");
                return;
            }

            if (targetSource == ItemSourceType.Inventory
                && settings.AutoPurchaseSkipInventoryOutsideIndustry
                && !isIndustryTile)
            {
                AdaptableLog.Info("[BetterTaiwuScroll] Auto monthly purchase skipped: inventory target outside an industry tile.");
                return;
            }

            var merchantCount = 0;
            var purchasedCount = 0;
            var inventoryLimitReached = false;
            var groupCharIds = DomainManager.Taiwu.GetGroupCharIds().GetCollection();
            foreach (var charId in groupCharIds)
            {
                if (!IsValidGroupMerchant(context, charId))
                    continue;

                merchantCount++;
                var result = PurchaseFromMerchant(context, charId, targetSource, settings);
                purchasedCount += result.SelectedCount;
                if (!result.InventoryLimitReached)
                    continue;

                inventoryLimitReached = true;
                break;
            }

            AdaptableLog.Info(
                "[BetterTaiwuScroll] Auto monthly purchase finished: merchants=" + merchantCount
                + ", purchased=" + purchasedCount
                + ", target=" + targetSource
                + ", industryTile=" + isIndustryTile
                + ", inventoryLimitReached=" + inventoryLimitReached + ".");
        }
        catch (Exception ex)
        {
            AdaptableLog.Error("[BetterTaiwuScroll] Auto monthly purchase failed: " + ex);
        }
    }

    private static bool IsIndustryTile(Location location)
    {
        if (!location.IsValid())
            return false;

        var rootLocation = DomainManager.Map.GetBlock(location).GetRootBlock().GetLocation();
        return DomainManager.Building.TryGetElement_BuildingAreas(rootLocation, out BuildingAreaData areaData)
               && areaData != null;
    }

    private static bool IsValidGroupMerchant(DataContext context, int charId)
    {
        if (charId == DomainManager.Taiwu.GetTaiwuCharId())
            return false;

        if (!DomainManager.Character.TryGetElement_Objects(
                charId,
                out GameData.Domains.Character.Character character))
            return false;
        if (character.GetAgeGroup() == 0)
            return false;

        var merchantType = DomainManager.Taiwu.GetMerchantType(charId);
        if (merchantType < 0 || merchantType >= 7)
            return false;

        var merchantData = DomainManager.Merchant.GetMerchantData(context, charId);
        return merchantData != null && merchantData.CharId == charId;
    }

    private static PurchaseResult PurchaseFromMerchant(
        DataContext context,
        int merchantCharId,
        ItemSourceType targetSource,
        AutoMonthlyPurchaseSettings settings)
    {
        var shopArguments = new OpenShopEventArguments
        {
            Id = merchantCharId,
            MerchantSourceType = 0,
            Refresh = false,
            IgnoreFavorability = false,
            IgnoreWorldProgress = false,
            CurrPage = 0,
        };
        var shopData = DomainManager.Taiwu.GetShopDisplayData(context, shopArguments);
        var exchange = shopData?.Exchange;
        if (exchange == null || exchange.TradeArguments == null)
            return PurchaseResult.Empty;

        exchange.SetItemSource((sbyte)targetSource);
        var selection = new PurchaseSelectionState();
        if (IsInventoryLimitReached(exchange, targetSource, settings))
            return PurchaseResult.InventoryLimit;

        var selectedCount = SelectMatchingItems(
            exchange,
            shopData,
            targetSource,
            settings,
            selection);
        if (selectedCount <= 0)
            return new PurchaseResult(0, selection.InventoryLimitReached);

        DomainManager.Taiwu.ConfirmShopExchange(context, exchange);
        return new PurchaseResult(selectedCount, selection.InventoryLimitReached);
    }

    private static int SelectMatchingItems(
        ShopExchange exchange,
        ShopDisplayData shopData,
        ItemSourceType targetSource,
        AutoMonthlyPurchaseSettings settings,
        PurchaseSelectionState selection)
    {
        var selectedCount = 0;
        for (var page = 0; page <= 6 && !selection.InventoryLimitReached; page++)
        {
            if (!exchange.IsPageShow(page))
                continue;

            var remainingLimitedCount = GetRemainingLimitedCount(
                exchange,
                page,
                settings.IncludeLimitedPurityLockedLevels);
            if (remainingLimitedCount == 0)
                continue;

            var items = shopData[page];
            if (items == null)
                continue;

            foreach (var item in items)
            {
                var count = GetSelectableMaterialCount(
                    exchange,
                    item,
                    page,
                    targetSource,
                    settings,
                    selection,
                    ref remainingLimitedCount);
                if (count <= 0)
                    continue;

                exchange.SelectTargetItem(item, count);
                selectedCount += count;
                if (selection.InventoryLimitReached)
                    break;
            }
        }

        return selectedCount;
    }

    private static int GetSelectableMaterialCount(
        ShopExchange exchange,
        ITradeableContent item,
        int page,
        ItemSourceType targetSource,
        AutoMonthlyPurchaseSettings settings,
        PurchaseSelectionState selection,
        ref int remainingLimitedCount)
    {
        if (item == null || item.Amount <= 0)
            return 0;
        if (!ShopExchange.IsShopItem(item) || item.ItemSourceType - 10 != page)
            return 0;
        if (item.UsingType != ItemDisplayData.ItemUsingType.Invalid || item.ItemSourceType == 0)
            return 0;
        if (item.RealKey.ItemType != MaterialItemType)
            return 0;

        var material = GetMaterial(item);
        if (material == null || !IsMaterialSubTypeAllowed(material.ItemSubType, settings))
            return 0;

        var highestRawGrade = DisplayGradeToRaw(settings.HighestPurchaseGrade);
        var lowestRawGrade = DisplayGradeToRaw(settings.LowestPurchaseGrade);
        if (material.Grade > highestRawGrade || material.Grade < lowestRawGrade)
            return 0;

        var priceChangePercent = exchange.GetPriceChangePercentValue(item, true);
        if (settings.SkipPriceIncreasedItems && priceChangePercent > 0)
            return 0;
        if (settings.SkipOriginalPriceItems && priceChangePercent == 0)
            return 0;

        var remaining = item.Amount - GetSelectedTargetAmount(exchange, item);
        if (remaining <= 0)
            return 0;

        var count = remainingLimitedCount < 0
            ? remaining
            : Math.Min(remaining, remainingLimitedCount);
        if (count <= 0)
            return 0;

        count = LimitByInventoryLoad(exchange, item, count, targetSource, settings, selection);
        if (count <= 0)
            return 0;

        if (remainingLimitedCount >= 0)
            remainingLimitedCount -= count;
        return count;
    }

    private static int LimitByInventoryLoad(
        ShopExchange exchange,
        ITradeableContent item,
        int count,
        ItemSourceType targetSource,
        AutoMonthlyPurchaseSettings settings,
        PurchaseSelectionState selection)
    {
        if (targetSource != ItemSourceType.Inventory || !settings.AutoPurchaseInventoryLoadProtection)
            return count;

        var effectiveMax = GetEffectiveInventoryMaxLoad(exchange, settings);
        var remainingLoad = effectiveMax - exchange.TaiwuInventoryCurLoadPreview;
        if (remainingLoad <= 0)
        {
            selection.InventoryLimitReached = true;
            return 0;
        }

        var itemWeight = ItemTemplateHelper.GetBaseWeight(item.RealKey.ItemType, item.RealKey.TemplateId);
        if (itemWeight <= 0)
            return count;

        var capacityCount = remainingLoad / itemWeight;
        if (capacityCount <= 0)
        {
            selection.InventoryLimitReached = true;
            return 0;
        }
        if (capacityCount >= count)
            return count;

        selection.InventoryLimitReached = true;
        return capacityCount;
    }

    private static bool IsInventoryLimitReached(
        ShopExchange exchange,
        ItemSourceType targetSource,
        AutoMonthlyPurchaseSettings settings)
    {
        return targetSource == ItemSourceType.Inventory
               && settings.AutoPurchaseInventoryLoadProtection
               && exchange.TaiwuInventoryCurLoadPreview >= GetEffectiveInventoryMaxLoad(exchange, settings);
    }

    private static int GetEffectiveInventoryMaxLoad(
        ShopExchange exchange,
        AutoMonthlyPurchaseSettings settings)
    {
        return (int)((long)exchange.TaiwuInventoryMaxLoadPreview
                     * settings.AutoPurchaseInventoryLoadThresholdPercent / 100L);
    }

    private static MaterialItem GetMaterial(ITradeableContent item)
    {
        try
        {
            return Config.Material.Instance[item.RealKey.TemplateId];
        }
        catch
        {
            return null;
        }
    }

    private static bool IsMaterialSubTypeAllowed(
        short subType,
        AutoMonthlyPurchaseSettings settings)
    {
        return subType switch
        {
            MedicineMaterialSubType => settings.IncludeMedicineMaterials,
            PoisonMaterialSubType => settings.IncludePoisonMaterials,
            _ => true,
        };
    }

    private static int GetRemainingLimitedCount(
        ShopExchange exchange,
        int page,
        bool includeLimitedLevels)
    {
        if (exchange.MinDebtLevel >= page)
            return -1;
        if (!includeLimitedLevels)
            return 0;

        var levels = exchange.TradeArguments?.OverFavorData?.MerchantOverFavorLevelDataArray;
        if (levels == null || !levels.CheckIndex(page))
            return 0;

        var level = levels[page];
        if (level == null)
            return 0;
        if (level.BuyCount == short.MaxValue)
            return -1;
        return Math.Max(0, (int)level.BuyCount);
    }

    private static int GetSelectedTargetAmount(ShopExchange exchange, ITradeableContent item)
    {
        var selected = 0;
        foreach (var selectedItem in exchange.TargetContentList)
        {
            if (selectedItem == null)
                continue;
            if (selectedItem.RealKey.Equals(item.RealKey)
                && selectedItem.CharacterId == item.CharacterId
                && selectedItem.ItemSourceType == item.ItemSourceType)
            {
                selected += selectedItem.Amount;
            }
        }
        return selected;
    }

    private static int DisplayGradeToRaw(int displayGrade)
    {
        displayGrade = displayGrade < 1 ? 1 : displayGrade > 9 ? 9 : displayGrade;
        return 9 - displayGrade;
    }

    private sealed class PurchaseSelectionState
    {
        internal bool InventoryLimitReached;
    }

    private readonly struct PurchaseResult
    {
        internal static readonly PurchaseResult Empty = new PurchaseResult(0, false);
        internal static readonly PurchaseResult InventoryLimit = new PurchaseResult(0, true);

        internal readonly int SelectedCount;
        internal readonly bool InventoryLimitReached;

        internal PurchaseResult(int selectedCount, bool inventoryLimitReached)
        {
            SelectedCount = selectedCount;
            InventoryLimitReached = inventoryLimitReached;
        }
    }
}
