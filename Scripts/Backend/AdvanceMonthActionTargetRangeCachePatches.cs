#nullable disable
#pragma warning disable CS0105

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using GameData.ActionPlanning.MonthlyAI;
using GameData.Common;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

internal static class AdvanceMonthActionTargetRangeCache
{
    private static ConditionalWeakTable<CharacterPlanningAgent, AgentCache> _caches =
        new ConditionalWeakTable<CharacterPlanningAgent, AgentCache>();

    private static int _hits;
    private static int _misses;
    private static int _stores;
    private static int _clears;
    private static int _cachedCharacters;
    private static int _errors;
    private static string _lastError = string.Empty;

    internal static bool Enabled => AdvanceMonthDiagnosticsSettings.ActionTargetRangeCacheEnabled;

    internal static void ClearAgent(CharacterPlanningAgent agent)
    {
        if (agent == null)
        {
            return;
        }

        try
        {
            AgentCache cache = _caches.GetValue(agent, _ => new AgentCache());
            cache.Ranges.Clear();

            _clears++;
        }
        catch (Exception exception)
        {
            RecordError(exception);
        }
    }

    internal static bool TryGet(
        CharacterPlanningAgent agent,
        EPlanningActionCharacterSelectRange range,
        int rangeValue,
        out IReadOnlyList<GameData.Domains.Character.Character> characters)
    {
        characters = null;
        if (!Enabled || agent == null)
        {
            return false;
        }

        try
        {
            AgentCache cache = _caches.GetValue(agent, _ => new AgentCache());
            long key = MakeKey(range, rangeValue);
            if (cache.Ranges.TryGetValue(key, out characters))
            {
                _hits++;
                return true;
            }

            _misses++;
        }
        catch (Exception exception)
        {
            RecordError(exception);
        }

        return false;
    }

    internal static void Store(
        CharacterPlanningAgent agent,
        EPlanningActionCharacterSelectRange range,
        int rangeValue,
        IReadOnlyList<GameData.Domains.Character.Character> characters)
    {
        if (!Enabled || agent == null || characters == null)
        {
            return;
        }

        try
        {
            AgentCache cache = _caches.GetValue(agent, _ => new AgentCache());
            long key = MakeKey(range, rangeValue);
            cache.Ranges[key] = characters;

            _stores++;
            _cachedCharacters += characters.Count;
        }
        catch (Exception exception)
        {
            RecordError(exception);
        }
    }

    internal static void Reset()
    {
        _caches = new ConditionalWeakTable<CharacterPlanningAgent, AgentCache>();
        _hits = 0;
        _misses = 0;
        _stores = 0;
        _clears = 0;
        _cachedCharacters = 0;
        _errors = 0;
        _lastError = string.Empty;
    }

    internal static void GetDiagnostics(
        out bool enabled,
        out int hits,
        out int misses,
        out int stores,
        out int clears,
        out int cachedCharacters,
        out int errors,
        out string lastError)
    {
        enabled = Enabled;
        hits = _hits;
        misses = _misses;
        stores = _stores;
        clears = _clears;
        cachedCharacters = _cachedCharacters;
        errors = _errors;
        lastError = _lastError ?? string.Empty;
    }

    private static long MakeKey(EPlanningActionCharacterSelectRange range, int rangeValue)
    {
        return ((long)(int)range << 32) ^ (uint)rangeValue;
    }

    private static void RecordError(Exception exception)
    {
        _errors++;
        _lastError = exception == null ? string.Empty : exception.GetType().Name + ": " + exception.Message;
    }

    private sealed class AgentCache
    {
        public readonly Dictionary<long, IReadOnlyList<GameData.Domains.Character.Character>> Ranges =
            new Dictionary<long, IReadOnlyList<GameData.Domains.Character.Character>>(8);
    }
}

[HarmonyPatch(typeof(CharacterPlanningAgent), "Initialize", new[]
{
    typeof(DataContext),
    typeof(GameData.Domains.Character.Character),
    typeof(GameData.ActionPlanning.Interface.IGoal<GameData.Domains.Character.Character, GameData.ActionPlanning.MonthlyAI.StateKey>)
})]
internal static class AdvanceMonthActionTargetRangeCacheInitializePatch
{
    private static void Prefix(CharacterPlanningAgent __instance)
    {
        AdvanceMonthActionTargetRangeCache.ClearAgent(__instance);
        AdvanceMonthActionRelationCache.ClearAgent(__instance);
    }
}

[HarmonyPatch]
internal static class AdvanceMonthActionTargetRangeCacheGetCharactersInSelectRangePatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(CharacterPlanningAgent),
            "GetCharactersInSelectRange",
            new[] { typeof(EPlanningActionCharacterSelectRange), typeof(int) });
    }

    private static bool Prefix(
        CharacterPlanningAgent __instance,
        EPlanningActionCharacterSelectRange range,
        int rangeValue,
        ref IReadOnlyList<GameData.Domains.Character.Character> __result,
        out bool __state)
    {
        __state = false;
        if (AdvanceMonthActionTargetRangeCache.TryGet(__instance, range, rangeValue, out var cached))
        {
            __result = cached;
            return false;
        }

        __state = AdvanceMonthActionTargetRangeCache.Enabled;
        return true;
    }

    private static void Postfix(
        CharacterPlanningAgent __instance,
        EPlanningActionCharacterSelectRange range,
        int rangeValue,
        ref IReadOnlyList<GameData.Domains.Character.Character> __result,
        bool __state)
    {
        if (!__state || __result == null)
        {
            return;
        }

        var copy = new List<GameData.Domains.Character.Character>(__result);
        __result = copy;
        AdvanceMonthActionTargetRangeCache.Store(__instance, range, rangeValue, copy);
    }
}
