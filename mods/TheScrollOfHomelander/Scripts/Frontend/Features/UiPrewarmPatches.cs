#nullable disable
#pragma warning disable CS0612

using System;
using System.Collections;
using System.Collections.Generic;
using Config;
using Game.Components.Item;
using Game.Views;
using Game.Views.CharacterMenu;
using HarmonyLib;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(GlobalOperations), "OnWorldDataReady", new Type[] { })]
internal static class UiPrewarmWorldDataReadyPatch
{
    private static void Postfix()
    {
        UiPrewarmSupport.RequestAfterWorldDataReady();
    }
}

// This remains as a fallback for enabling/reloading the mod while an archive is
// already open. Normal archive loads use GlobalOperations.OnWorldDataReady.
[HarmonyPatch(typeof(UI_Bottom), "OnInit")]
internal static class UiPrewarmBottomOnInitPatch
{
    private static void Postfix(UI_Bottom __instance)
    {
        UiPrewarmSupport.RequestAfterGameStart(__instance);
    }
}

[HarmonyPatch(typeof(UI_Bottom), "OnEnable")]
internal static class UiPrewarmBottomOnEnablePatch
{
    private static void Postfix(UI_Bottom __instance)
    {
        UiPrewarmSupport.RequestAfterGameStart(__instance);
    }
}

internal sealed class UiPrewarmHost : MonoBehaviour
{
}

internal static class UiPrewarmSupport
{
    // A missing/late prefab must never hold the prewarm coroutine for the whole
    // loading screen. Successful requests continue warming in their callbacks.
    private const float CompletionTimeoutSeconds = 8f;
    private const string HostName = "BetterTaiwuScrollUiPrewarmHost";

    private static UiPrewarmHost _host;
    private static Coroutine _routine;
    private static int _generation;
    private static bool _requestedForCurrentWorld;
    private static int _pendingLoads;
    private static int _successfulLoads;
    private static int _failedLoads;

    internal static void RequestAfterWorldDataReady()
    {
        StartNewGeneration();
    }

    internal static void RequestAfterGameStart(MonoBehaviour owner)
    {
        if (_requestedForCurrentWorld || owner == null || !owner.gameObject.activeInHierarchy)
            return;

        StartNewGeneration();
    }

    internal static void Reset()
    {
        _generation++;
        _requestedForCurrentWorld = false;
        _pendingLoads = 0;
        _successfulLoads = 0;
        _failedLoads = 0;

        if (_host != null && _routine != null)
            _host.StopCoroutine(_routine);
        _routine = null;

        if (_host != null)
            UnityEngine.Object.Destroy(_host.gameObject);
        _host = null;
    }

    private static void StartNewGeneration()
    {
        EnsureHost();
        if (_host == null)
            return;

        _generation++;
        _requestedForCurrentWorld = true;
        _pendingLoads = 0;
        _successfulLoads = 0;
        _failedLoads = 0;

        if (_routine != null)
            _host.StopCoroutine(_routine);
        _routine = _host.StartCoroutine(PrewarmCurrentWorld(_generation));
    }

    private static void EnsureHost()
    {
        if (_host != null)
            return;

        var hostObject = new GameObject(HostName);
        hostObject.hideFlags = HideFlags.HideAndDontSave;
        UnityEngine.Object.DontDestroyOnLoad(hostObject);
        _host = hostObject.AddComponent<UiPrewarmHost>();
    }

    private static IEnumerator PrewarmCurrentWorld(int generation)
    {
        var startedAt = Time.realtimeSinceStartup;

        // Keep disk reads and JSON normalization out of every first-open path.
        // Stores already loaded during plugin initialization return immediately.
        BatchItemFilterSettingsStore.Load();
        ContinuousMakeSettingsStore.Load();
        AutoRepairSettingsStore.Load();
        MemoryOptimizationSettingsStore.Load();
        PurchaseOptimizationSettingsStore.Load();

        yield return null;
        if (generation != _generation)
            yield break;

        if (Plugin.EnableSearchOptimization)
            CharacterSearchUiAssets.Prewarm();

        var entries = BuildPrewarmEntries();
        for (var i = 0; i < entries.Count; i++)
        {
            if (generation != _generation)
                yield break;

            BeginPrepare(entries[i], generation);

            // Starting one background load per frame prevents all prefab
            // instantiation callbacks from being forced into the archive-load frame.
            yield return null;
        }

        while (generation == _generation
               && _pendingLoads > 0
               && Time.realtimeSinceStartup - startedAt < CompletionTimeoutSeconds)
        {
            yield return null;
        }

        if (generation != _generation)
            yield break;

        if (_pendingLoads > 0)
        {
            _failedLoads += _pendingLoads;
            _pendingLoads = 0;
        }

        // These calls now only cache components from the prefabs prepared above;
        // they no longer pay for SystemSetting/RevertArchive/BuildingManage loads.
        if (Plugin.EnableContinuousMakeUi || Plugin.EnableBulkPurchaseUi)
            NativeContinuousMakeUiTemplates.RequestArrangementSettingButtonTemplate(null);
        if (Plugin.EnableContinuousMakeUi)
        {
            ContinuousMakeSettingsPanel.Preload();
            AutoRepairSettingsPanelSupport.Preload();
        }
        if (Plugin.EnableBulkPurchaseUi)
            PurchaseOptimizationSettingsPanel.Preload();

        _routine = null;
        Debug.Log("[BetterTaiwuScroll] Archive UI prewarm complete: loaded="
            + _successfulLoads + ", failed=" + _failedLoads + ", elapsedMs="
            + Mathf.RoundToInt((Time.realtimeSinceStartup - startedAt) * 1000f) + ".");
    }

    private static List<UiPrewarmEntry> BuildPrewarmEntries()
    {
        var entries = new List<UiPrewarmEntry>(24);

        var needsSettingsTemplates = Plugin.EnableContinuousMakeUi || Plugin.EnableBulkPurchaseUi;
        if (needsSettingsTemplates)
        {
            Add(entries, "SystemSetting", UIElement.SystemSetting);
            Add(entries, "RevertArchive", UIElement.RevertArchive);
            Add(entries, "BuildingManage", UIElement.BuildingManage);
        }

        // Batch item filtering is always available in multiply mode, so its owner
        // and operation view are prewarmed even if the optional list tweaks are off.
        {
            Add(entries, "CharacterMenu", UIElement.CharacterMenu);
            Add(entries, "CharacterMenuItems", UIElement.CharacterMenuItems);
            Add(entries, "CharacterMenuLifeSkill", UIElement.CharacterMenuLifeSkill);
            Add(entries, "CharacterMenuCombatSkill", UIElement.CharacterMenuCombatSkill);
            Add(entries, "CharacterMenuEquipCombatSkill", UIElement.CharacterMenuEquipCombatSkill);
            Add(entries, "ItemMultiplyOperation", UIElement.ItemMultiplyOperation);
        }

        var needsMake = Plugin.EnableContinuousMakeUi
            || Plugin.EnableBestTool
            || Plugin.EnableMaxProductCount
            || Plugin.EnableMakeSubtypeMemory
            || Plugin.EnableMakePerfectMemory
            || AutoRepairSettingsStore.Current.Enabled;
        if (needsMake)
        {
            Add(entries, "Make", UIElement.Make);
            // Product selection is now part of ViewMake; the legacy UI was removed.
        }

        var needsExchange = Plugin.EnableFastTransfer
            || Plugin.EnableSpaceSubmitExchange
            || Plugin.EnableExchangeFilterCategorySync
            || Plugin.EnableExchangeSelfItemTextColor
            || Plugin.EnableExchangeBookSelfItemTextColor
            || Plugin.EnableWarehouseSelfItemTextColor
            || Plugin.EnableInventorySearchBoxOptimization;
        if (needsExchange)
        {
            Add(entries, "Warehouse", UIElement.Warehouse);
            Add(entries, "Exchange", UIElement.Exchange);
            Add(entries, "ExchangeBook", UIElement.ExchangeBook);
        }

        var needsShop = Plugin.EnableBulkPurchaseUi
            || Plugin.EnableShopStockPage
            || Plugin.EnableLifeSkillBookOwnedMarker
            || Plugin.EnableShopSelfItemTextColor
            || Plugin.EnableShopGiftSelfItemTextColor
            || Plugin.EnableSettlementShopSelfItemTextColor;
        if (needsShop)
        {
            Add(entries, "NewShop", UIElement.NewShop);
            Add(entries, "NewShopGift", UIElement.NewShopGift);
            Add(entries, "SettlementShop", UIElement.SettlementShop);
        }

        if (Plugin.EnableAdvanceMonthOptimization
            || Plugin.EnableAutoHarvestAfterAdvanceMonth
            || Plugin.EnableSpaceStartBuilding
            || Plugin.EnableContinuousMakeUi)
        {
            Add(entries, "BuildingArea", UIElement.BuildingArea);
        }

        if (Plugin.EnableContainerCompact || Plugin.EnableFilterMemory || Plugin.EnableSortMemory)
        {
            Add(entries, "LegendaryBook", UIElement.LegendaryBook);
            Add(entries, "BuildingTeachBook", UIElement.BuildingTeachBook);
        }

        return entries;
    }

    private static void Add(List<UiPrewarmEntry> entries, string name, UIElement element, Action<GameObject> afterReady = null)
    {
        if (element != null)
            entries.Add(new UiPrewarmEntry(name, element, afterReady));
    }

    private static void BeginPrepare(UiPrewarmEntry entry, int generation)
    {
        _pendingLoads++;
        var callbackInvoked = false;
        try
        {
            entry.Element.PrepareRes(false, gameObject =>
            {
                if (callbackInvoked)
                    return;
                callbackInvoked = true;
                if (generation != _generation)
                    return;

                _pendingLoads = Math.Max(0, _pendingLoads - 1);
                if (gameObject == null)
                {
                    _failedLoads++;
                    return;
                }

                _successfulLoads++;
                try
                {
                    entry.AfterReady?.Invoke(gameObject);
                }
                catch (Exception ex)
                {
                    _failedLoads++;
                    Debug.LogWarning("[BetterTaiwuScroll] UI prewarm post-step failed for "
                        + entry.Name + ": " + ex);
                }
            }, true);
        }
        catch (Exception ex)
        {
            if (!callbackInvoked)
            {
                callbackInvoked = true;
                _pendingLoads = Math.Max(0, _pendingLoads - 1);
            }
            _failedLoads++;
            Debug.LogWarning("[BetterTaiwuScroll] UI prewarm failed to start for "
                + entry.Name + ": " + ex);
        }
    }

    private sealed class UiPrewarmEntry
    {
        internal readonly string Name;
        internal readonly UIElement Element;
        internal readonly Action<GameObject> AfterReady;

        internal UiPrewarmEntry(string name, UIElement element, Action<GameObject> afterReady)
        {
            Name = name;
            Element = element;
            AfterReady = afterReady;
        }
    }
}
