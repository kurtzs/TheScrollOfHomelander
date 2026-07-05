#nullable disable
#pragma warning disable CS0105

using System;
using System.Collections.Generic;
using System.Reflection;
using GameData.Common;
using GameData.Dependencies;
using GameData.Domains.Information;
using GameData.Domains.Information.Secret;
using HarmonyLib;
using SecretInformationData = GameData.Domains.Information.Secret.SecretInformation;

namespace BetterTaiwuScroll.Backend;

internal static class AdvanceMonthSecretInformationRemoveBatch
{
    private const int SecretInformationDataId = 6;

    private static readonly FieldInfo SecretInformationField = AccessTools.Field(typeof(InformationDomain), "_secretInformation");
    private static readonly FieldInfo DataStatesField = AccessTools.Field(typeof(BaseGameDataDomain), "DataStates");
    private static readonly FieldInfo CacheInfluencesField = AccessTools.Field(typeof(InformationDomain), "CacheInfluences");
    private static readonly MethodInfo SetModifiedAndInvalidateMethod = AccessTools.Method(
        typeof(BaseGameDataDomain),
        "SetModifiedAndInvalidateInfluencedCache",
        new[] { typeof(int), typeof(byte[]), typeof(DataInfluence[][]), typeof(DataContext) });

    private static bool _lastEnabled;
    private static int _batches;
    private static int _inputIds;
    private static int _removedIds;
    private static int _fallbacks;
    private static string _lastError = string.Empty;

    internal static bool TryRun(InformationDomain domain, IEnumerable<SecretInformationId> idsToRemove, DataContext context)
    {
        _lastEnabled = AdvanceMonthDiagnosticsSettings.SecretInformationRemoveBatchEnabled;
        if (!AdvanceMonthDiagnosticsSettings.SecretInformationRemoveBatchEnabled)
        {
            return false;
        }

        try
        {
            if (domain == null ||
                idsToRemove == null ||
                !TryGetSecretInformationMap(domain, out Dictionary<SecretInformationId, SecretInformationData> secrets) ||
                !TryGetDataStates(domain, out byte[] dataStates) ||
                !TryGetCacheInfluences(out DataInfluence[][] cacheInfluences) ||
                SetModifiedAndInvalidateMethod == null)
            {
                _fallbacks++;
                return false;
            }

            int inputCount = 0;
            int removedCount = 0;
            foreach (SecretInformationId id in idsToRemove)
            {
                inputCount++;
                if (secrets.Remove(id))
                {
                    removedCount++;
                }

                AdvanceMonthMetabolismHolderIndex.OnRemoveSecretInformation(id);
            }

            if (inputCount > 0)
            {
                SetModifiedAndInvalidateMethod.Invoke(
                    domain,
                    new object[] { SecretInformationDataId, dataStates, cacheInfluences, context });
            }

            _batches++;
            _inputIds += inputCount;
            _removedIds += removedCount;
            return true;
        }
        catch (Exception exception)
        {
            _fallbacks++;
            _lastError = exception.GetType().Name;
            return false;
        }
    }

    internal static void GetDiagnostics(
        out bool enabled,
        out int batches,
        out int inputIds,
        out int removedIds,
        out int fallbacks,
        out string lastError)
    {
        enabled = _lastEnabled || AdvanceMonthDiagnosticsSettings.SecretInformationRemoveBatchEnabled;
        batches = _batches;
        inputIds = _inputIds;
        removedIds = _removedIds;
        fallbacks = _fallbacks;
        lastError = _lastError ?? string.Empty;
    }

    internal static void Reset()
    {
        _lastEnabled = false;
        _batches = 0;
        _inputIds = 0;
        _removedIds = 0;
        _fallbacks = 0;
        _lastError = string.Empty;
    }

    private static bool TryGetSecretInformationMap(
        InformationDomain domain,
        out Dictionary<SecretInformationId, SecretInformationData> secrets)
    {
        secrets = domain == null || SecretInformationField == null
            ? null
            : SecretInformationField.GetValue(domain) as Dictionary<SecretInformationId, SecretInformationData>;
        return secrets != null;
    }

    private static bool TryGetDataStates(InformationDomain domain, out byte[] dataStates)
    {
        dataStates = domain == null || DataStatesField == null ? null : DataStatesField.GetValue(domain) as byte[];
        return dataStates != null;
    }

    private static bool TryGetCacheInfluences(out DataInfluence[][] cacheInfluences)
    {
        cacheInfluences = CacheInfluencesField == null ? null : CacheInfluencesField.GetValue(null) as DataInfluence[][];
        return cacheInfluences != null;
    }
}

[HarmonyPatch]
internal static class AdvanceMonthSecretInformationRemoveBatchPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(InformationDomain),
            "RecordSecretInformationRemove",
            new[] { typeof(DataContext), typeof(IEnumerable<SecretInformationId>) });
    }

    [HarmonyPriority(Priority.Last)]
    private static bool Prefix(InformationDomain __instance, DataContext context, IEnumerable<SecretInformationId> idsToRemove)
    {
        return !AdvanceMonthSecretInformationRemoveBatch.TryRun(__instance, idsToRemove, context);
    }
}
