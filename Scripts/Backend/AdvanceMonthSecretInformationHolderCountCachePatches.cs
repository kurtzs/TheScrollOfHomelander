#nullable disable
#pragma warning disable CS0105

using System;
using System.Collections.Generic;
using System.Reflection;
using GameData.Common;
using GameData.Domains.Information;
using GameData.Domains.Information.Secret;
using HarmonyLib;
using SecretInformationData = GameData.Domains.Information.Secret.SecretInformation;

namespace BetterTaiwuScroll.Backend;

internal static class AdvanceMonthSecretInformationHolderCountCache
{
    private static readonly FieldInfo SecretInformationField = AccessTools.Field(typeof(InformationDomain), "_secretInformation");
    private static readonly FieldInfo CharacterKnownSecretsField = AccessTools.Field(typeof(InformationDomain), "_characterKnownSecrets");
    private static readonly Dictionary<SecretInformationId, SecretOccurenceId> SecretToOccurence = new Dictionary<SecretInformationId, SecretOccurenceId>(16384);
    private static readonly Dictionary<SecretOccurenceId, int> HolderCountsByOccurence = new Dictionary<SecretOccurenceId, int>(8192);
    private static readonly HashSet<SecretOccurenceId> CharacterOccurenceScratch = new HashSet<SecretOccurenceId>();

    private static bool _inSecretInformationAdvanceMonth;
    private static bool _active;
    private static int _builds;
    private static int _hits;
    private static int _misses;
    private static int _deactivations;

    internal static void BeginSecretInformationAdvanceMonth()
    {
        _inSecretInformationAdvanceMonth = AdvanceMonthDiagnosticsSettings.HolderCountCacheEnabled;
        _active = false;
        _builds = 0;
        _hits = 0;
        _misses = 0;
        _deactivations = 0;
        Clear();
    }

    internal static void EndSecretInformationAdvanceMonth()
    {
        _inSecretInformationAdvanceMonth = false;
        _active = false;
        Clear();
    }

    internal static void Reset()
    {
        _inSecretInformationAdvanceMonth = false;
        _active = false;
        _builds = 0;
        _hits = 0;
        _misses = 0;
        _deactivations = 0;
        Clear();
    }

    internal static void BuildAfterMakeSettlementsInformation(InformationDomain domain)
    {
        if (!_inSecretInformationAdvanceMonth)
        {
            return;
        }

        if (!TryGetSecretInformationMap(domain, out Dictionary<SecretInformationId, SecretInformationData> secrets) ||
            !TryGetCharacterKnownSecretsMap(domain, out Dictionary<int, CharacterKnownSecret> knownSecrets) ||
            secrets == null ||
            knownSecrets == null)
        {
            Deactivate();
            return;
        }

        Clear();
        foreach (KeyValuePair<SecretInformationId, SecretInformationData> pair in secrets)
        {
            SecretInformationData secret = pair.Value;
            if (secret == null)
            {
                continue;
            }

            SecretToOccurence[pair.Key] = secret.OccurenceId;
            if (!HolderCountsByOccurence.ContainsKey(secret.OccurenceId))
            {
                HolderCountsByOccurence.Add(secret.OccurenceId, 0);
            }
        }

        foreach (CharacterKnownSecret known in knownSecrets.Values)
        {
            if (known == null || known.KnownSecrets == null)
            {
                continue;
            }

            CharacterOccurenceScratch.Clear();
            foreach (SecretInformationId secretId in known.KnownSecrets)
            {
                if (SecretToOccurence.TryGetValue(secretId, out SecretOccurenceId occurenceId))
                {
                    CharacterOccurenceScratch.Add(occurenceId);
                }
            }

            foreach (SecretOccurenceId occurenceId in CharacterOccurenceScratch)
            {
                HolderCountsByOccurence.TryGetValue(occurenceId, out int count);
                HolderCountsByOccurence[occurenceId] = count + 1;
            }
        }

        CharacterOccurenceScratch.Clear();
        _active = true;
        _builds++;
    }

    internal static void DeactivateBeforeMetabolismSecretInformation()
    {
        Deactivate();
    }

    internal static bool TryGetHolderCount(SecretOccurenceId occurenceId, out int holderCount)
    {
        if (_active && HolderCountsByOccurence.TryGetValue(occurenceId, out holderCount))
        {
            _hits++;
            return true;
        }

        holderCount = 0;
        if (_inSecretInformationAdvanceMonth)
        {
            _misses++;
        }

        return false;
    }

    internal static void OnSecretInformationAdded(SecretInformationId secretId, SecretInformationData secret)
    {
        if (!_active || secret == null)
        {
            return;
        }

        SecretToOccurence[secretId] = secret.OccurenceId;
        if (!HolderCountsByOccurence.ContainsKey(secret.OccurenceId))
        {
            HolderCountsByOccurence.Add(secret.OccurenceId, 0);
        }
    }

    internal static void OnReceiveSecretInformationFinished(bool success, SecretInformationId realReceivedSecretId)
    {
        if (!_active || !success)
        {
            return;
        }

        if (!SecretToOccurence.TryGetValue(realReceivedSecretId, out SecretOccurenceId occurenceId))
        {
            Deactivate();
            return;
        }

        HolderCountsByOccurence.TryGetValue(occurenceId, out int count);
        HolderCountsByOccurence[occurenceId] = count + 1;
    }

    internal static void OnSecretInformationRemoved()
    {
        if (_active)
        {
            Deactivate();
        }
    }

    internal static void GetDiagnostics(out bool active, out int builds, out int hits, out int misses, out int deactivations)
    {
        active = _active;
        builds = _builds;
        hits = _hits;
        misses = _misses;
        deactivations = _deactivations;
    }

    private static bool TryGetSecretInformationMap(InformationDomain domain, out Dictionary<SecretInformationId, SecretInformationData> secrets)
    {
        secrets = SecretInformationField == null ? null : SecretInformationField.GetValue(domain) as Dictionary<SecretInformationId, SecretInformationData>;
        return secrets != null;
    }

    private static bool TryGetCharacterKnownSecretsMap(InformationDomain domain, out Dictionary<int, CharacterKnownSecret> knownSecrets)
    {
        knownSecrets = CharacterKnownSecretsField == null ? null : CharacterKnownSecretsField.GetValue(domain) as Dictionary<int, CharacterKnownSecret>;
        return knownSecrets != null;
    }

    private static void Deactivate()
    {
        if (_active)
        {
            _deactivations++;
        }

        _active = false;
        Clear();
    }

    private static void Clear()
    {
        SecretToOccurence.Clear();
        HolderCountsByOccurence.Clear();
        CharacterOccurenceScratch.Clear();
    }
}

[HarmonyPatch(typeof(InformationDomain), "ProcessSecretInformationAdvanceMonth")]
internal static class AdvanceMonthHolderCountCacheLifecyclePatch
{
    private static void Prefix()
    {
        AdvanceMonthSecretInformationHolderCountCache.BeginSecretInformationAdvanceMonth();
    }

    private static Exception Finalizer(Exception __exception)
    {
        AdvanceMonthSecretInformationHolderCountCache.EndSecretInformationAdvanceMonth();
        return __exception;
    }
}

[HarmonyPatch(typeof(InformationDomain), "MakeSettlementsInformation")]
internal static class AdvanceMonthHolderCountCacheBuildPatch
{
    private static void Postfix(InformationDomain __instance)
    {
        AdvanceMonthSecretInformationHolderCountCache.BuildAfterMakeSettlementsInformation(__instance);
    }
}

[HarmonyPatch]
internal static class AdvanceMonthHolderCountCacheLookupPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(InformationDomain), "CalcSecretOccurenceHolderCount");
    }

    [HarmonyPriority(Priority.First)]
    private static bool Prefix(SecretOccurenceId occurenceId, ref int __result)
    {
        if (!AdvanceMonthSecretInformationHolderCountCache.TryGetHolderCount(occurenceId, out int holderCount))
        {
            return true;
        }

        __result = holderCount;
        return false;
    }
}

[HarmonyPatch]
internal static class AdvanceMonthHolderCountCacheReceivePatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(InformationDomain),
            "ReceiveSecretInformation",
            new[] { typeof(DataContext), typeof(SecretInformationData), typeof(int), typeof(int), typeof(SecretInformationId).MakeByRefType() });
    }

    private static void Postfix(bool __result, ref SecretInformationId realReceivedSecretId)
    {
        AdvanceMonthSecretInformationHolderCountCache.OnReceiveSecretInformationFinished(__result, realReceivedSecretId);
    }
}

[HarmonyPatch]
internal static class AdvanceMonthHolderCountCacheAddSecretInformationPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(InformationDomain),
            "AddElement_SecretInformation",
            new[] { typeof(SecretInformationId), typeof(SecretInformationData), typeof(DataContext) });
    }

    private static void Postfix(SecretInformationId elementId, SecretInformationData value)
    {
        AdvanceMonthSecretInformationHolderCountCache.OnSecretInformationAdded(elementId, value);
    }
}

[HarmonyPatch]
internal static class AdvanceMonthHolderCountCacheRemoveSecretInformationPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(InformationDomain),
            "RemoveElement_SecretInformation",
            new[] { typeof(SecretInformationId), typeof(DataContext) });
    }

    private static void Postfix()
    {
        AdvanceMonthSecretInformationHolderCountCache.OnSecretInformationRemoved();
    }
}

[HarmonyPatch]
internal static class AdvanceMonthHolderCountCacheBeforeMetabolismPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(InformationDomain),
            "MetabolismSecretInformation",
            new[] { typeof(DataContext) });
    }

    [HarmonyPriority(Priority.First)]
    private static void Prefix()
    {
        AdvanceMonthSecretInformationHolderCountCache.DeactivateBeforeMetabolismSecretInformation();
    }
}
