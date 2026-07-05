#nullable disable

using System;
using System.Reflection;
using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

[PluginConfig("更好的太吾绘卷", "Taiwu Studio", "0.1.0")]
public sealed class Plugin : TaiwuRemakePlugin
{
    internal static string ModDirectory;

    internal static bool EnableCompactSortButtons = true;
    internal static bool EnableFilterEntryFullRow = true;
    internal static bool EnableInlineFilterButtons = true;
    internal static bool EnableSimplifyFilterIcons = false;
    internal static bool EnableSimplifiedFilterIconUnderlines = false;
    internal static bool EnableMapTileIconYOffset = true;
    internal static int MapTileIconYOffsetPercent = 35;
    internal static bool EnableMapTileMerchantIconBrightColor = true;
    internal static int MapTileMerchantIconBrightnessPercent = 150;
    internal static int MapLargeIconScalePercent = 70;
    internal static bool EnableMapListMerchantIconBrightColor = true;
    internal static int MapListMerchantIconBrightnessPercent = 150;
    internal static int MapListMerchantIconScalePercent = 140;
    internal static bool EnableCustomGradeColors = true;
    internal static bool EnableExchangeFilterCategorySync = true;
    internal static bool EnableShopSelfItemTextColor = true;
    internal static bool EnableShopGiftSelfItemTextColor = true;
    internal static bool EnableSettlementShopSelfItemTextColor = true;
    internal static bool EnableWarehouseSelfItemTextColor = true;
    internal static bool EnableExchangeBookSelfItemTextColor = true;
    internal static bool EnableExchangeSelfItemTextColor = true;
    internal static bool EnableCombatTooltipDetail = false;
    internal static bool EnableInventorySearchBoxOptimization = true;
    internal static bool EnableContainerCompact = true;
    internal static bool EnableDefaultContainerCardMode = true;
    internal static int InventoryContainerLineCount = 10;
    internal static int ExchangeContainerLineCount = 7;
    internal static int SkillContainerLineCount = 4;
    internal static int EquipCombatSkillContainerLineCount = 3;
    internal static bool EnableBestTool = true;
    internal static bool EnableMaxProductCount = true;
    internal static bool EnableFood = true;
    internal static bool EnableMedicine = true;
    internal static bool EnablePoison = true;
    internal static bool EnableForging = true;
    internal static bool EnableWoodworking = true;
    internal static bool EnableWeaving = true;
    internal static bool EnableJade = true;
    internal static bool EnableContinuousMakeUi = true;
    internal static bool EnableAutoSelectFoodMaterial = true;
    internal static bool EnableAutoSelectMedicineMaterial = true;
    internal static bool EnableAutoSelectPoisonMaterial = true;
    internal static bool EnableAutoSelectForgingMaterial = true;
    internal static bool EnableAutoSelectWoodworkingMaterial = true;
    internal static bool EnableAutoSelectWeavingMaterial = true;
    internal static bool EnableAutoSelectJadeMaterial = true;
    internal static bool EnableFilterMemory = true;
    internal static bool EnableSortMemory = true;
    internal static bool EnableStrategyPresetMemory = true;
    internal static bool EnableLifeSkillAutoModeMemory = true;
    internal static bool EnableMakeSubtypeMemory = true;
    internal static bool EnableMakePerfectMemory = true;
    internal static bool EnableBulkPurchaseUi = true;
    internal static bool EnableShopStockPage = true;
    internal static bool EnableFastTransfer = true;
    internal static bool InvertFastTransfer = false;
    internal static bool EnableSpaceSubmitExchange = true;
    internal static bool SpaceSubmitExchangeNeedConfirm = true;
    internal static bool EnableSpaceStartBuilding = true;

    private Harmony _harmony;

    public override void Initialize()
    {
        ModLocalization.Refresh();
        LoadSettings(ModIdStr);
        ModDirectory = ModManager.GetModInfo(ModIdStr)?.DirectoryName;
        ContinuousMakeSettingsStore.Load();
        MemoryOptimizationSettingsStore.Load();
        PurchaseOptimizationSettingsStore.Load();
        _harmony = new Harmony("taiwu-studio.better-taiwu-scroll.frontend");
        _harmony.PatchAll(typeof(Plugin).Assembly);
        GradeColorOptimizationSupport.ApplyOrRestore();
        MapBlockCharListMerchantIconSupport.RefreshAllActive();
    }

    public override void OnModSettingUpdate()
    {
        ModLocalization.Refresh();
        LoadSettings(ModIdStr);
        ContinuousMakeUiController.RefreshAll();
        ContainerCompactPatches.RefreshAllActive(allowRestore: true);
        FilterEntryFullRowLayoutSupport.RefreshAllActive(allowRestore: true);
        InlineFilterButtonsController.RefreshAllActive(allowRestore: true);
        SimplifiedFilterToggleVisual.RefreshAllActive();
        PurchaseOptimizationUiController.RefreshAll();
        ExchangeFilterCategorySyncSupport.OnSettingChanged();
        InventorySearchBoxOptimizationSupport.RefreshAllActive();
        ExchangeSearchBoxOptimizationSupport.RefreshAllActive();
        ShopStockPageSupport.RefreshAllActive();
        GradeColorOptimizationSupport.ApplyOrRestore();
        MapMerchantIconColorSupport.RefreshAllActive();
        MapLargeIconScaleSupport.RefreshAllActive();
        MapBlockCharListMerchantIconSupport.RefreshAllActive();
        if (!EnableMapTileIconYOffset)
            MapTileIconPositionSupport.RestoreAll();
    }

    public override void Dispose()
    {
        GradeColorOptimizationSupport.Restore();
        MapMerchantIconColorSupport.RestoreAll();
        MapLargeIconScaleSupport.RestoreAll();
        MapBlockCharListMerchantIconSupport.RestoreAll();
        MapTileIconPositionSupport.RestoreAll();
        InventorySearchBoxOptimizationSupport.RestoreAll();
        ExchangeSearchBoxOptimizationSupport.RestoreAll();
        InlineFilterButtonsController.RestoreAll();
        FilterEntryFullRowLayoutSupport.RestoreAll();
        TooltipGradeBackgroundSpriteSupport.Clear();
        _harmony?.UnpatchSelf();
        _harmony = null;
    }

    private static void LoadSettings(string modIdStr)
    {
        LoadSetting(modIdStr, "compact_sort_buttons", ref EnableCompactSortButtons);
        LoadSetting(modIdStr, "filter_entry_full_row", ref EnableFilterEntryFullRow);
        LoadSettingDefault(modIdStr, "inline_filter_buttons", ref EnableInlineFilterButtons, true);
        LoadSetting(modIdStr, "simplify_filter_icons", ref EnableSimplifyFilterIcons);
        LoadSetting(modIdStr, "simplified_filter_icon_underlines", ref EnableSimplifiedFilterIconUnderlines);
        LoadSetting(modIdStr, "map_tile_icon_y_offset", ref EnableMapTileIconYOffset);
        LoadIntSetting(modIdStr, "map_tile_icon_y_offset_percent", ref MapTileIconYOffsetPercent);
        MapTileIconYOffsetPercent = Mathf.Clamp(MapTileIconYOffsetPercent, 0, 50);
        LoadSetting(modIdStr, "map_tile_merchant_icon_bright_color", ref EnableMapTileMerchantIconBrightColor);
        LoadIntSetting(modIdStr, "map_tile_merchant_icon_brightness_percent", ref MapTileMerchantIconBrightnessPercent);
        MapTileMerchantIconBrightnessPercent = Mathf.Clamp(MapTileMerchantIconBrightnessPercent, 100, 400);
        LoadIntSetting(modIdStr, "map_large_icon_scale_percent", ref MapLargeIconScalePercent);
        MapLargeIconScalePercent = Mathf.Clamp(MapLargeIconScalePercent, 40, 120);
        LoadSetting(modIdStr, "map_list_merchant_icon_bright_color", ref EnableMapListMerchantIconBrightColor);
        LoadIntSetting(modIdStr, "map_list_merchant_icon_brightness_percent", ref MapListMerchantIconBrightnessPercent);
        MapListMerchantIconBrightnessPercent = Mathf.Clamp(MapListMerchantIconBrightnessPercent, 100, 400);
        LoadIntSetting(modIdStr, "map_list_merchant_icon_scale_percent", ref MapListMerchantIconScalePercent);
        MapListMerchantIconScalePercent = Mathf.Clamp(MapListMerchantIconScalePercent, 50, 200);
        LoadSetting(modIdStr, "custom_grade_colors", ref EnableCustomGradeColors);
        LoadSetting(modIdStr, "exchange_filter_category_sync", ref EnableExchangeFilterCategorySync);
        LoadSetting(modIdStr, "shop_self_item_text_color", ref EnableShopSelfItemTextColor);
        LoadSetting(modIdStr, "shop_gift_self_item_text_color", ref EnableShopGiftSelfItemTextColor);
        LoadSetting(modIdStr, "settlement_shop_self_item_text_color", ref EnableSettlementShopSelfItemTextColor);
        LoadSetting(modIdStr, "warehouse_self_item_text_color", ref EnableWarehouseSelfItemTextColor);
        LoadSetting(modIdStr, "exchange_book_self_item_text_color", ref EnableExchangeBookSelfItemTextColor);
        LoadSetting(modIdStr, "exchange_self_item_text_color", ref EnableExchangeSelfItemTextColor);
        LoadSetting(modIdStr, "combat_tooltip_detail_without_shift", ref EnableCombatTooltipDetail);
        LoadSetting(modIdStr, "inventory_search_box_optimization", ref EnableInventorySearchBoxOptimization);
        LoadSetting(modIdStr, "container_compact", ref EnableContainerCompact);
        LoadSetting(modIdStr, "container_default_card_mode", ref EnableDefaultContainerCardMode);
        LoadIntSetting(modIdStr, "container_inventory_line_count", ref InventoryContainerLineCount);
        InventoryContainerLineCount = Mathf.Clamp(InventoryContainerLineCount, 7, 12);
        LoadIntSetting(modIdStr, "container_exchange_line_count", ref ExchangeContainerLineCount);
        ExchangeContainerLineCount = Mathf.Clamp(ExchangeContainerLineCount, 5, 10);
        LoadIntSetting(modIdStr, "container_skill_line_count", ref SkillContainerLineCount);
        SkillContainerLineCount = Mathf.Clamp(SkillContainerLineCount, 3, 7);
        LoadIntSetting(modIdStr, "container_equip_combat_skill_line_count", ref EquipCombatSkillContainerLineCount);
        EquipCombatSkillContainerLineCount = Mathf.Clamp(EquipCombatSkillContainerLineCount, 3, 7);
        LoadSetting(modIdStr, "best_tool", ref EnableBestTool);
        LoadSetting(modIdStr, "max_product_count", ref EnableMaxProductCount);
        LoadSetting(modIdStr, "food_enabled", ref EnableFood);
        LoadSetting(modIdStr, "medicine_enabled", ref EnableMedicine);
        LoadSetting(modIdStr, "poison_enabled", ref EnablePoison);
        LoadSetting(modIdStr, "forging_enabled", ref EnableForging);
        LoadSetting(modIdStr, "woodworking_enabled", ref EnableWoodworking);
        LoadSetting(modIdStr, "weaving_enabled", ref EnableWeaving);
        LoadSetting(modIdStr, "jade_enabled", ref EnableJade);
        LoadSetting(modIdStr, "continuous_make_ui", ref EnableContinuousMakeUi);

        var enableAutoSelectMaterial = EnableAutoSelectFoodMaterial
            || EnableAutoSelectMedicineMaterial
            || EnableAutoSelectPoisonMaterial
            || EnableAutoSelectForgingMaterial
            || EnableAutoSelectWoodworkingMaterial
            || EnableAutoSelectWeavingMaterial
            || EnableAutoSelectJadeMaterial;
        LoadSetting(modIdStr, "auto_select_material", ref enableAutoSelectMaterial);
        EnableAutoSelectFoodMaterial = enableAutoSelectMaterial;
        EnableAutoSelectMedicineMaterial = enableAutoSelectMaterial;
        EnableAutoSelectPoisonMaterial = enableAutoSelectMaterial;
        EnableAutoSelectForgingMaterial = enableAutoSelectMaterial;
        EnableAutoSelectWoodworkingMaterial = enableAutoSelectMaterial;
        EnableAutoSelectWeavingMaterial = enableAutoSelectMaterial;
        EnableAutoSelectJadeMaterial = enableAutoSelectMaterial;

        LoadSetting(modIdStr, "auto_select_food_material", ref EnableAutoSelectFoodMaterial);
        LoadSetting(modIdStr, "auto_select_medicine_material", ref EnableAutoSelectMedicineMaterial);
        LoadSetting(modIdStr, "auto_select_poison_material", ref EnableAutoSelectPoisonMaterial);
        LoadSetting(modIdStr, "auto_select_forging_material", ref EnableAutoSelectForgingMaterial);
        LoadSetting(modIdStr, "auto_select_woodworking_material", ref EnableAutoSelectWoodworkingMaterial);
        LoadSetting(modIdStr, "auto_select_weaving_material", ref EnableAutoSelectWeavingMaterial);
        LoadSetting(modIdStr, "auto_select_jade_material", ref EnableAutoSelectJadeMaterial);
        LoadSetting(modIdStr, "memory_filter_enabled", ref EnableFilterMemory);
        LoadSetting(modIdStr, "memory_sort_enabled", ref EnableSortMemory);
        LoadSetting(modIdStr, "memory_strategy_preset_enabled", ref EnableStrategyPresetMemory);
        LoadSetting(modIdStr, "memory_lifeskill_auto_mode_enabled", ref EnableLifeSkillAutoModeMemory);
        LoadSetting(modIdStr, "memory_make_subtype_enabled", ref EnableMakeSubtypeMemory);
        LoadSettingDefault(modIdStr, "memory_make_perfect_enabled", ref EnableMakePerfectMemory, true);
        LoadSetting(modIdStr, "bulk_purchase_ui", ref EnableBulkPurchaseUi);
        LoadSetting(modIdStr, "shop_show_stock_page", ref EnableShopStockPage);
        LoadSetting(modIdStr, "fast_transfer", ref EnableFastTransfer);
        LoadSetting(modIdStr, "invert_fast_transfer", ref InvertFastTransfer);
        LoadSetting(modIdStr, "space_submit_exchange", ref EnableSpaceSubmitExchange);
        LoadSettingDefault(modIdStr, "space_submit_exchange_need_confirm", ref SpaceSubmitExchangeNeedConfirm, true);
        LoadSettingDefault(modIdStr, "space_start_building", ref EnableSpaceStartBuilding, true);
    }

    private static void LoadSetting(string modIdStr, string key, ref bool value)
    {
        if (ModManager.GetSetting(modIdStr, key, ref value))
            return;

        var entryValue = GetSettingEntryValue(modIdStr, key);
        if (entryValue != null)
            value = Convert.ToBoolean(entryValue);
    }

    private static void LoadSettingDefault(string modIdStr, string key, ref bool value, bool defaultValue)
    {
        value = defaultValue;
        if (ModManager.GetSetting(modIdStr, key, ref value))
            return;

        var entryDefaultValue = GetSettingEntryDefaultValue(modIdStr, key);
        if (entryDefaultValue != null)
            value = Convert.ToBoolean(entryDefaultValue);
    }

    private static void LoadIntSetting(string modIdStr, string key, ref int value)
    {
        if (ModManager.GetSetting(modIdStr, key, ref value))
            return;

        var entryValue = GetSettingEntryValue(modIdStr, key);
        if (entryValue != null)
            value = Convert.ToInt32(entryValue);
    }

    private static object GetSettingEntryValue(string modIdStr, string key)
    {
        var info = ModManager.GetModInfo(modIdStr);
        if (info?.ModSettingEntries == null)
            return null;

        foreach (var entry in info.ModSettingEntries)
        {
            if (entry == null)
                continue;

            var entryType = entry.GetType();
            var entryKey = entryType.GetField("Key")?.GetValue(entry) as string;
            if (entryKey != key)
                continue;

            return entryType.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(entry, null)
                ?? entryType.GetField("<Value>k__BackingField", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(entry);
        }

        return null;
    }

    private static object GetSettingEntryDefaultValue(string modIdStr, string key)
    {
        var info = ModManager.GetModInfo(modIdStr);
        if (info?.ModSettingEntries == null)
            return null;

        foreach (var entry in info.ModSettingEntries)
        {
            if (entry == null)
                continue;

            var entryType = entry.GetType();
            var entryKey = entryType.GetField("Key")?.GetValue(entry) as string;
            if (entryKey != key)
                continue;

            return entryType.GetProperty("DefaultValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(entry, null)
                ?? entryType.GetField("DefaultValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(entry)
                ?? entryType.GetField("<DefaultValue>k__BackingField", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(entry);
        }

        return null;
    }

    internal static bool IsEnabledForLifeSkill(sbyte lifeSkillType)
    {
        return lifeSkillType switch
        {
            6 => EnableForging,
            7 => EnableWoodworking,
            14 => EnableFood,
            8 => EnableMedicine,
            9 => EnablePoison,
            10 => EnableWeaving,
            11 => EnableJade,
            _ => false,
        };
    }

    internal static bool IsAutoSelectMaterialEnabledForLifeSkill(sbyte lifeSkillType)
    {
        return lifeSkillType switch
        {
            6 => EnableAutoSelectForgingMaterial,
            7 => EnableAutoSelectWoodworkingMaterial,
            14 => EnableAutoSelectFoodMaterial,
            8 => EnableAutoSelectMedicineMaterial,
            9 => EnableAutoSelectPoisonMaterial,
            10 => EnableAutoSelectWeavingMaterial,
            11 => EnableAutoSelectJadeMaterial,
            _ => false,
        };
    }
}
