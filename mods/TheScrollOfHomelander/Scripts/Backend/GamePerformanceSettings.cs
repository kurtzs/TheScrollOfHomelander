#nullable disable

using GameData.Domains;

namespace BetterTaiwuScroll.Backend;

internal static class GamePerformanceSettings
{
    internal static bool OptimizationEnabled = true;
    internal static bool ActionTargetRangeCacheEnabled = true;
    internal static bool ActionRelationCacheEnabled = true;
    internal static bool ActionAgeGroupCacheEnabled = true;

    internal static void Load(string modId)
    {
        var optimizationEnabled = true;
        var actionTargetRangeCacheEnabled = true;
        var actionRelationCacheEnabled = true;
        var actionAgeGroupCacheEnabled = true;

        TryGet(modId, "enable_game_performance_optimization", ref optimizationEnabled);
        if (!TryGet(modId, "enable_game_action_target_range_cache", ref actionTargetRangeCacheEnabled))
            TryGet(modId, "enable_advance_month_action_target_range_cache", ref actionTargetRangeCacheEnabled);
        if (!TryGet(modId, "enable_game_action_relation_cache", ref actionRelationCacheEnabled))
            TryGet(modId, "enable_advance_month_action_relation_cache", ref actionRelationCacheEnabled);
        if (!TryGet(modId, "enable_game_action_age_group_cache", ref actionAgeGroupCacheEnabled))
            TryGet(modId, "enable_advance_month_action_age_group_cache", ref actionAgeGroupCacheEnabled);

        OptimizationEnabled = optimizationEnabled;
        ActionTargetRangeCacheEnabled = optimizationEnabled && actionTargetRangeCacheEnabled;
        ActionRelationCacheEnabled = optimizationEnabled && actionRelationCacheEnabled;
        ActionAgeGroupCacheEnabled = optimizationEnabled && actionAgeGroupCacheEnabled;
    }

    private static bool TryGet(string modId, string key, ref bool value)
    {
        try
        {
            return DomainManager.Mod.GetSetting(modId, key, ref value);
        }
        catch
        {
            return false;
        }
    }
}
