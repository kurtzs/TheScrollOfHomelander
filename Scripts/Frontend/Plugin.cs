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
    internal static bool EnableCompactSortButtons = true;
    internal static bool EnableInlineFilterButtons = true;
    internal static bool EnableSimplifyFilterIcons = false;
    internal static bool EnableSimplifiedFilterIconUnderlines = false;
    internal static bool EnableMapTileIconYOffset = true;
    internal static int MapTileIconYOffsetPercent = 35;
    internal static bool EnableContainerCompact = true;
    internal static bool EnableDefaultContainerCardMode = true;
    internal static float InventoryContainerScale = 0.7f;
    internal static float ExchangeContainerScale = 0.72f;
    internal static float SkillContainerScale = 0.7f;
    internal static bool EnableBestTool = true;
    internal static bool EnableMaxProductCount = true;
    internal static bool EnableFood = true;
    internal static bool EnableMedicine = true;
    internal static bool EnablePoison = true;
    internal static bool EnableContinuousMakeUi = true;
    internal static bool EnableAutoSelectFoodMaterial = true;
    internal static bool EnableAutoSelectMedicineMaterial = true;
    internal static bool EnableAutoSelectPoisonMaterial = true;
    internal static bool EnableFilterMemory = true;
    internal static bool EnableSortMemory = true;
    internal static bool EnableStrategyPresetMemory = true;
    internal static bool EnableLifeSkillAutoModeMemory = true;
    internal static bool EnableBulkPurchaseUi = true;
    internal static bool EnableShopStockPage = true;
    internal static bool EnableFastTransfer = true;
    internal static bool InvertFastTransfer = false;

    private Harmony _harmony;

    public override void Initialize()
    {
        LoadSettings(ModIdStr);
        ContinuousMakeSettingsStore.Load();
        MemoryOptimizationSettingsStore.Load();
        PurchaseOptimizationSettingsStore.Load();
        _harmony = new Harmony("taiwu-studio.better-taiwu-scroll.frontend");
        _harmony.PatchAll(typeof(Plugin).Assembly);
    }

    public override void OnModSettingUpdate()
    {
        LoadSettings(ModIdStr);
        ContinuousMakeUiController.RefreshAll();
        ContainerCompactPatches.RefreshAllActive(allowRestore: true);
        SimplifiedFilterToggleVisual.RefreshAllActive();
        PurchaseOptimizationUiController.RefreshAll();
        ShopStockPageSupport.RefreshAllActive();
        if (!EnableMapTileIconYOffset)
            MapTileIconPositionSupport.RestoreAll();
    }

    public override void Dispose()
    {
        MapTileIconPositionSupport.RestoreAll();
        _harmony?.UnpatchSelf();
        _harmony = null;
    }

    private static void LoadSettings(string modIdStr)
    {
        LoadSetting(modIdStr, "compact_sort_buttons", ref EnableCompactSortButtons);
        LoadSetting(modIdStr, "inline_filter_buttons", ref EnableInlineFilterButtons);
        LoadSetting(modIdStr, "simplify_filter_icons", ref EnableSimplifyFilterIcons);
        LoadSetting(modIdStr, "simplified_filter_icon_underlines", ref EnableSimplifiedFilterIconUnderlines);
        LoadSetting(modIdStr, "map_tile_icon_y_offset", ref EnableMapTileIconYOffset);
        LoadIntSetting(modIdStr, "map_tile_icon_y_offset_percent", ref MapTileIconYOffsetPercent);
        MapTileIconYOffsetPercent = Mathf.Clamp(MapTileIconYOffsetPercent, 0, 50);
        LoadSetting(modIdStr, "container_compact", ref EnableContainerCompact);
        LoadSetting(modIdStr, "container_default_card_mode", ref EnableDefaultContainerCardMode);
        LoadScaleSetting(modIdStr, "container_inventory_scale", ref InventoryContainerScale);
        LoadScaleSetting(modIdStr, "container_exchange_scale", ref ExchangeContainerScale);
        LoadScaleSetting(modIdStr, "container_skill_scale", ref SkillContainerScale);
        LoadSetting(modIdStr, "best_tool", ref EnableBestTool);
        LoadSetting(modIdStr, "max_product_count", ref EnableMaxProductCount);
        LoadSetting(modIdStr, "food_enabled", ref EnableFood);
        LoadSetting(modIdStr, "medicine_enabled", ref EnableMedicine);
        LoadSetting(modIdStr, "poison_enabled", ref EnablePoison);
        LoadSetting(modIdStr, "continuous_make_ui", ref EnableContinuousMakeUi);

        var enableAutoSelectMaterial = EnableAutoSelectFoodMaterial
            || EnableAutoSelectMedicineMaterial
            || EnableAutoSelectPoisonMaterial;
        LoadSetting(modIdStr, "auto_select_material", ref enableAutoSelectMaterial);
        EnableAutoSelectFoodMaterial = enableAutoSelectMaterial;
        EnableAutoSelectMedicineMaterial = enableAutoSelectMaterial;
        EnableAutoSelectPoisonMaterial = enableAutoSelectMaterial;

        LoadSetting(modIdStr, "auto_select_food_material", ref EnableAutoSelectFoodMaterial);
        LoadSetting(modIdStr, "auto_select_medicine_material", ref EnableAutoSelectMedicineMaterial);
        LoadSetting(modIdStr, "auto_select_poison_material", ref EnableAutoSelectPoisonMaterial);
        LoadSetting(modIdStr, "memory_filter_enabled", ref EnableFilterMemory);
        LoadSetting(modIdStr, "memory_sort_enabled", ref EnableSortMemory);
        LoadSetting(modIdStr, "memory_strategy_preset_enabled", ref EnableStrategyPresetMemory);
        LoadSetting(modIdStr, "memory_lifeskill_auto_mode_enabled", ref EnableLifeSkillAutoModeMemory);
        LoadSetting(modIdStr, "bulk_purchase_ui", ref EnableBulkPurchaseUi);
        LoadSetting(modIdStr, "shop_show_stock_page", ref EnableShopStockPage);
        LoadSetting(modIdStr, "fast_transfer", ref EnableFastTransfer);
        LoadSetting(modIdStr, "invert_fast_transfer", ref InvertFastTransfer);
    }

    private static void LoadSetting(string modIdStr, string key, ref bool value)
    {
        if (ModManager.GetSetting(modIdStr, key, ref value))
            return;

        var entryValue = GetSettingEntryValue(modIdStr, key);
        if (entryValue != null)
            value = Convert.ToBoolean(entryValue);
    }

    private static void LoadScaleSetting(string modIdStr, string key, ref float value)
    {
        var percent = Mathf.RoundToInt(value * 100f);
        if (!ModManager.GetSetting(modIdStr, key, ref percent))
        {
            var entryValue = GetSettingEntryValue(modIdStr, key);
            if (entryValue != null)
                percent = Convert.ToInt32(entryValue);
        }

        value = ClampContainerScale(percent / 100f);
    }

    private static void LoadIntSetting(string modIdStr, string key, ref int value)
    {
        if (ModManager.GetSetting(modIdStr, key, ref value))
            return;

        var entryValue = GetSettingEntryValue(modIdStr, key);
        if (entryValue != null)
            value = Convert.ToInt32(entryValue);
    }

    private static float ClampContainerScale(float value)
    {
        if (value < 0.3f)
            return 0.3f;
        if (value > 1f)
            return 1f;
        return value;
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

    internal static bool IsEnabledForLifeSkill(sbyte lifeSkillType)
    {
        return lifeSkillType switch
        {
            14 => EnableFood,
            8 => EnableMedicine,
            9 => EnablePoison,
            _ => false,
        };
    }

    internal static bool IsAutoSelectMaterialEnabledForLifeSkill(sbyte lifeSkillType)
    {
        return lifeSkillType switch
        {
            14 => EnableAutoSelectFoodMaterial,
            8 => EnableAutoSelectMedicineMaterial,
            9 => EnableAutoSelectPoisonMaterial,
            _ => false,
        };
    }
}
