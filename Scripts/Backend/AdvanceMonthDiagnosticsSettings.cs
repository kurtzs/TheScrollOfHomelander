#nullable disable
#pragma warning disable CS0105

using System;
using GameData.Domains;

namespace BetterTaiwuScroll.Backend;

internal static class AdvanceMonthDiagnosticsSettings
{
    internal static string ModId = string.Empty;
    internal static bool OptimizationEnabled = true;
    internal static bool Enabled;
    internal static int DetailLevel = 1;
    internal static bool CopyBufferOptimizationEnabled = true;
    internal static int CopyBufferMegabytes = 4;
    internal static bool HolderCountCacheEnabled = true;
    internal static bool MetabolismHolderIndexEnabled = true;
    internal static bool SecretInformationRemoveBatchEnabled = true;
    internal static bool MetabolismShadowCompareEnabled;
    internal static bool ActionTargetRangeCacheEnabled = true;
    internal static bool ActionRelationCacheEnabled = true;
    internal static bool ActionAgeGroupCacheEnabled = true;
    internal static bool ActionPlanningDetailDiagnosticsEnabled;
    internal static bool NoCompressionEnabled;

    internal static bool Detailed => OptimizationEnabled && Enabled && DetailLevel >= 2;
    internal static bool ActionPlanningDetailed => Detailed && ActionPlanningDetailDiagnosticsEnabled;
    internal static int CopyBufferBytes => OptimizationEnabled && CopyBufferOptimizationEnabled ? CopyBufferMegabytes * 1024 * 1024 : 4096;

    internal static void Load(string modId)
    {
        ModId = modId ?? string.Empty;
        var optimizationEnabled = true;
        var enabled = false;
        var detailLevel = 1;
        var copyBufferOptimizationEnabled = true;
        var copyBufferMegabytes = 4;
        var holderCountCacheEnabled = true;
        var metabolismHolderIndexEnabled = true;
        var secretInformationRemoveBatchEnabled = true;
        var metabolismShadowCompareEnabled = false;
        var actionTargetRangeCacheEnabled = true;
        var actionRelationCacheEnabled = true;
        var actionAgeGroupCacheEnabled = true;
        var actionPlanningDetailDiagnosticsEnabled = false;
        var noCompressionEnabled = false;
        TryGet(modId, "enable_advance_month_optimization", ref optimizationEnabled);
        TryGet(modId, "enable_advance_month_diagnostics", ref enabled);
        TryGet(modId, "advance_month_diagnostics_detail", ref detailLevel);
        TryGet(modId, "enable_advance_month_copy_buffer_optimization", ref copyBufferOptimizationEnabled);
        TryGet(modId, "advance_month_copy_buffer_mb", ref copyBufferMegabytes);
        TryGet(modId, "enable_advance_month_holder_count_cache", ref holderCountCacheEnabled);
        TryGet(modId, "enable_advance_month_metabolism_holder_index", ref metabolismHolderIndexEnabled);
        TryGet(modId, "enable_advance_month_secret_remove_batch", ref secretInformationRemoveBatchEnabled);
        TryGet(modId, "enable_advance_month_metabolism_shadow_compare", ref metabolismShadowCompareEnabled);
        TryGet(modId, "enable_advance_month_action_target_range_cache", ref actionTargetRangeCacheEnabled);
        TryGet(modId, "enable_advance_month_action_relation_cache", ref actionRelationCacheEnabled);
        TryGet(modId, "enable_advance_month_action_age_group_cache", ref actionAgeGroupCacheEnabled);
        TryGet(modId, "enable_advance_month_action_planning_detail_diagnostics", ref actionPlanningDetailDiagnosticsEnabled);
        TryGet(modId, "enable_advance_month_no_compression", ref noCompressionEnabled);
        OptimizationEnabled = optimizationEnabled;
        Enabled = optimizationEnabled && enabled;
        DetailLevel = Math.Clamp(detailLevel, 1, 2);
        CopyBufferOptimizationEnabled = optimizationEnabled && copyBufferOptimizationEnabled;
        CopyBufferMegabytes = NormalizeCopyBufferMegabytes(copyBufferMegabytes);
        HolderCountCacheEnabled = optimizationEnabled && holderCountCacheEnabled;
        MetabolismHolderIndexEnabled = optimizationEnabled && metabolismHolderIndexEnabled;
        SecretInformationRemoveBatchEnabled = optimizationEnabled && secretInformationRemoveBatchEnabled;
        MetabolismShadowCompareEnabled = optimizationEnabled && metabolismShadowCompareEnabled;
        ActionTargetRangeCacheEnabled = optimizationEnabled && actionTargetRangeCacheEnabled;
        ActionRelationCacheEnabled = optimizationEnabled && actionRelationCacheEnabled;
        ActionAgeGroupCacheEnabled = optimizationEnabled && actionAgeGroupCacheEnabled;
        ActionPlanningDetailDiagnosticsEnabled = optimizationEnabled && actionPlanningDetailDiagnosticsEnabled;
        NoCompressionEnabled = optimizationEnabled && noCompressionEnabled;
    }

    private static int NormalizeCopyBufferMegabytes(int value)
    {
        return value switch
        {
            <= 1 => 1,
            <= 4 => 4,
            <= 8 => 8,
            _ => 16
        };
    }

    private static void TryGet(string modId, string key, ref bool value)
    {
        try
        {
            DomainManager.Mod.GetSetting(modId, key, ref value);
        }
        catch
        {
        }
    }

    private static void TryGet(string modId, string key, ref int value)
    {
        try
        {
            DomainManager.Mod.GetSetting(modId, key, ref value);
        }
        catch
        {
        }
    }
}
