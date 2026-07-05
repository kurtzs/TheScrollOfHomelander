#nullable disable
#pragma warning disable CS0105

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GameData.ActionPlanning.MonthlyAI;
using GameData.Domains.Character;
using GameData.Domains.Character.Relation;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

internal static class AdvanceMonthActionRelationCache
{
    [ThreadStatic]
    private static CharacterPlanningAgent _activeAgent;

    [ThreadStatic]
    private static FilterCache _activeCache;

    [ThreadStatic]
    private static int _scopeDepth;

    private static int _hits;
    private static int _misses;
    private static int _stores;
    private static int _clears;
    private static int _scopes;
    private static int _ageHits;
    private static int _ageMisses;
    private static int _tryGetRelationReplacements;
    private static int _getAgeGroupReplacements;
    private static int _transpilerApplications;
    private static int _errors;
    private static string _lastError = string.Empty;

    internal static bool Enabled => AdvanceMonthDiagnosticsSettings.ActionRelationCacheEnabled;

    internal static bool TryGetOrOriginal(
        CharacterDomain domain,
        int charId,
        int relatedCharId,
        out RelatedCharacter relation)
    {
        FilterCache cache = _activeCache;
        if (Enabled && cache != null)
        {
            long key = MakeRelationKey(charId, relatedCharId);
            if (cache.Relations.TryGetValue(key, out RelationCacheEntry entry))
            {
                relation = entry.Relation;
                _hits++;
                return entry.Found;
            }

            _misses++;
            bool result = domain.TryGetRelation(charId, relatedCharId, out relation);
            cache.Relations[key] = new RelationCacheEntry
            {
                Found = result,
                Relation = relation
            };
            _stores++;
            return result;
        }

        return domain.TryGetRelation(charId, relatedCharId, out relation);
    }

    internal static sbyte GetAgeGroupCached(GameData.Domains.Character.Character character)
    {
        FilterCache cache = _activeCache;
        if (!AdvanceMonthDiagnosticsSettings.ActionAgeGroupCacheEnabled || cache == null || character == null)
        {
            return character == null ? (sbyte)0 : character.GetAgeGroup();
        }

        int charId = character.GetId();
        if (cache.AgeGroups.TryGetValue(charId, out sbyte ageGroup))
        {
            _ageHits++;
            return ageGroup;
        }

        _ageMisses++;
        ageGroup = character.GetAgeGroup();
        cache.AgeGroups[charId] = ageGroup;
        return ageGroup;
    }

    internal static void ClearAgent(CharacterPlanningAgent agent)
    {
        if (agent == null)
        {
            return;
        }

        try
        {
            if (_activeAgent == agent && _activeCache != null)
            {
                _activeCache.Clear();
            }

            _clears++;
        }
        catch (Exception exception)
        {
            RecordError(exception);
        }
    }

    internal static void BeginScope(CharacterPlanningAgent agent)
    {
        if (agent == null ||
            (!AdvanceMonthDiagnosticsSettings.ActionRelationCacheEnabled &&
             !AdvanceMonthDiagnosticsSettings.ActionAgeGroupCacheEnabled))
        {
            return;
        }

        try
        {
            if (_scopeDepth == 0)
            {
                if (_activeCache == null)
                {
                    _activeCache = new FilterCache();
                }

                if (_activeAgent != agent)
                {
                    _activeAgent = agent;
                    _activeCache.Clear();
                }

                _scopes++;
            }

            _scopeDepth++;
        }
        catch (Exception exception)
        {
            _activeAgent = null;
            _activeCache = null;
            _scopeDepth = 0;
            RecordError(exception);
        }
    }

    internal static void EndScope()
    {
        if (_scopeDepth <= 0)
        {
            return;
        }

        _scopeDepth--;
    }

    internal static void Reset()
    {
        _activeAgent = null;
        _activeCache = null;
        _scopeDepth = 0;
        _hits = 0;
        _misses = 0;
        _stores = 0;
        _clears = 0;
        _scopes = 0;
        _ageHits = 0;
        _ageMisses = 0;
        _tryGetRelationReplacements = 0;
        _getAgeGroupReplacements = 0;
        _transpilerApplications = 0;
        _errors = 0;
        _lastError = string.Empty;
    }

    internal static void RecordTranspiler(int relationReplacements, int ageGroupReplacements)
    {
        _transpilerApplications++;
        _tryGetRelationReplacements += relationReplacements;
        _getAgeGroupReplacements += ageGroupReplacements;
    }

    internal static void GetDiagnostics(
        out bool enabled,
        out bool ageGroupEnabled,
        out int hits,
        out int misses,
        out int stores,
        out int clears,
        out int scopes,
        out int ageHits,
        out int ageMisses,
        out int transpilerApplications,
        out int tryGetRelationReplacements,
        out int getAgeGroupReplacements,
        out int errors,
        out string lastError)
    {
        enabled = Enabled;
        ageGroupEnabled = AdvanceMonthDiagnosticsSettings.ActionAgeGroupCacheEnabled;
        hits = _hits;
        misses = _misses;
        stores = _stores;
        clears = _clears;
        scopes = _scopes;
        ageHits = _ageHits;
        ageMisses = _ageMisses;
        transpilerApplications = _transpilerApplications;
        tryGetRelationReplacements = _tryGetRelationReplacements;
        getAgeGroupReplacements = _getAgeGroupReplacements;
        errors = _errors;
        lastError = _lastError ?? string.Empty;
    }

    private static long MakeRelationKey(int charId, int relatedCharId)
    {
        return ((long)charId << 32) ^ (uint)relatedCharId;
    }

    private static void RecordError(Exception exception)
    {
        _errors++;
        _lastError = exception == null ? string.Empty : exception.GetType().Name + ": " + exception.Message;
    }

    private sealed class FilterCache
    {
        public readonly Dictionary<long, RelationCacheEntry> Relations =
            new Dictionary<long, RelationCacheEntry>(256);

        public readonly Dictionary<int, sbyte> AgeGroups =
            new Dictionary<int, sbyte>(128);

        public void Clear()
        {
            Relations.Clear();
            AgeGroups.Clear();
        }
    }

    private struct RelationCacheEntry
    {
        public bool Found;
        public RelatedCharacter Relation;
    }
}

[HarmonyPatch]
internal static class AdvanceMonthActionRelationCacheFilterActionTargetsPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(CharacterPlanningAgent),
            "FilterActionTargets",
            new[]
            {
                typeof(Redzen.Random.IRandomSource),
                typeof(IReadOnlyList<GameData.Domains.Character.Character>),
                typeof(ICollection<int>),
                typeof(Predicate<GameData.Domains.Character.Character>),
                typeof(EPlanningActionCharacterSelector)
            });
    }

    private static void Prefix(CharacterPlanningAgent __instance)
    {
        AdvanceMonthActionRelationCache.BeginScope(__instance);
    }

    private static Exception Finalizer(Exception __exception)
    {
        AdvanceMonthActionRelationCache.EndScope();
        return __exception;
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo relationReplacement = AccessTools.Method(
            typeof(AdvanceMonthActionRelationCache),
            nameof(AdvanceMonthActionRelationCache.TryGetOrOriginal));
        MethodInfo ageGroupReplacement = AccessTools.Method(
            typeof(AdvanceMonthActionRelationCache),
            nameof(AdvanceMonthActionRelationCache.GetAgeGroupCached));

        int relationReplacements = 0;
        int ageGroupReplacements = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (IsTryGetRelationCall(instruction))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = relationReplacement;
                relationReplacements++;
            }
            else if (IsGetAgeGroupCall(instruction))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = ageGroupReplacement;
                ageGroupReplacements++;
            }

            yield return instruction;
        }

        AdvanceMonthActionRelationCache.RecordTranspiler(relationReplacements, ageGroupReplacements);
    }

    private static bool IsTryGetRelationCall(CodeInstruction instruction)
    {
        if (instruction.operand is not MethodInfo method || method.Name != "TryGetRelation")
        {
            return false;
        }

        if (method.DeclaringType?.FullName != typeof(CharacterDomain).FullName)
        {
            return false;
        }

        ParameterInfo[] parameters = method.GetParameters();
        return parameters.Length == 3 &&
               parameters[0].ParameterType == typeof(int) &&
               parameters[1].ParameterType == typeof(int) &&
               parameters[2].ParameterType.IsByRef &&
               parameters[2].ParameterType.GetElementType()?.FullName == typeof(RelatedCharacter).FullName;
    }

    private static bool IsGetAgeGroupCall(CodeInstruction instruction)
    {
        if (instruction.operand is not MethodInfo method || method.Name != "GetAgeGroup")
        {
            return false;
        }

        return method.GetParameters().Length == 0 &&
               method.ReturnType == typeof(sbyte) &&
               method.DeclaringType?.FullName == typeof(GameData.Domains.Character.Character).FullName;
    }
}

