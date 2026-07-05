#nullable disable
#pragma warning disable CS0105

using System;
using System.Collections.Generic;
using System.Reflection;
using GameData.Domains.Information;
using GameData.Domains.Information.Secret;
using HarmonyLib;
using ConfigSecretInformation = Config.SecretInformation;
using SecretInformationData = GameData.Domains.Information.Secret.SecretInformation;

namespace BetterTaiwuScroll.Backend;

internal static class AdvanceMonthMetabolismShadowCompare
{
    private static readonly FieldInfo SecretInformationField = AccessTools.Field(typeof(InformationDomain), "_secretInformation");
    private static readonly FieldInfo SecretOccurenceField = AccessTools.Field(typeof(InformationDomain), "_secretOccurence");
    private static readonly FieldInfo CharacterKnownSecretsField = AccessTools.Field(typeof(InformationDomain), "_characterKnownSecrets");

    private static readonly Dictionary<SecretInformationId, int> HolderCountsBySecret = new Dictionary<SecretInformationId, int>(16384);
    private static readonly Dictionary<SecretOccurenceId, HashSet<SecretInformationId>> SecretIdsByOccurence = new Dictionary<SecretOccurenceId, HashSet<SecretInformationId>>(8192);
    private static readonly HashSet<SecretInformationId> PredictedSecretRemovals = new HashSet<SecretInformationId>();
    private static readonly HashSet<SecretOccurenceId> PredictedOccurenceRemovals = new HashSet<SecretOccurenceId>();
    private static readonly HashSet<SecretInformationId> ActualSecretRemovals = new HashSet<SecretInformationId>();
    private static readonly HashSet<SecretOccurenceId> ActualOccurenceRemovals = new HashSet<SecretOccurenceId>();

    private static bool _enabled;
    private static bool _built;
    private static bool _inconclusive;
    private static int _indexedKnownSecretLinks;
    private static int _broadcastCandidates;
    private static string _error;

    internal static void Begin(bool diagnosticsActive)
    {
        Clear();
        _enabled = diagnosticsActive &&
            AdvanceMonthDiagnosticsSettings.Detailed &&
            AdvanceMonthDiagnosticsSettings.MetabolismShadowCompareEnabled;

        if (!_enabled)
        {
            return;
        }

        try
        {
            InformationDomain domain = GameData.Domains.DomainManager.Information;
            if (!TryGetSecretInformationMap(domain, out Dictionary<SecretInformationId, SecretInformationData> secrets) ||
                !TryGetSecretOccurenceMap(domain, out Dictionary<SecretOccurenceId, SecretOccurence> occurences) ||
                !TryGetCharacterKnownSecretsMap(domain, out Dictionary<int, CharacterKnownSecret> knownSecrets))
            {
                _error = "ReflectFieldsFailed";
                return;
            }

            BuildHolderCounts(secrets, knownSecrets);
            BuildPredictedRemovals(secrets, occurences);
            _built = true;
        }
        catch (Exception exception)
        {
            _built = false;
            _error = exception.GetType().Name;
        }
    }

    internal static void CaptureActualSecretRemovals(IEnumerable<SecretInformationId> idsToRemove)
    {
        if (!_enabled || idsToRemove == null)
        {
            return;
        }

        try
        {
            foreach (SecretInformationId id in idsToRemove)
            {
                ActualSecretRemovals.Add(id);
            }
        }
        catch
        {
        }
    }

    internal static void CaptureActualOccurenceRemovals(IEnumerable<SecretOccurenceId> idsToRemove)
    {
        if (!_enabled || idsToRemove == null)
        {
            return;
        }

        try
        {
            foreach (SecretOccurenceId id in idsToRemove)
            {
                ActualOccurenceRemovals.Add(id);
            }
        }
        catch
        {
        }
    }

    internal static void Finish()
    {
        if (!_enabled)
        {
            AdvanceMonthDiagnosticsRecorder.SetMetabolismShadowResult(
                enabled: false,
                built: false,
                matched: false,
                predictedSecretRemovals: 0,
                actualSecretRemovals: 0,
                missingSecretRemovals: 0,
                extraSecretRemovals: 0,
                predictedOccurenceRemovals: 0,
                actualOccurenceRemovals: 0,
                missingOccurenceRemovals: 0,
                extraOccurenceRemovals: 0,
                error: string.Empty);
            Clear();
            return;
        }

        int missingSecretRemovals = CountExcept(PredictedSecretRemovals, ActualSecretRemovals);
        int extraSecretRemovals = CountExcept(ActualSecretRemovals, PredictedSecretRemovals);
        int missingOccurenceRemovals = CountExcept(PredictedOccurenceRemovals, ActualOccurenceRemovals);
        int extraOccurenceRemovals = CountExcept(ActualOccurenceRemovals, PredictedOccurenceRemovals);
        bool matched = _built &&
            !_inconclusive &&
            missingSecretRemovals == 0 &&
            extraSecretRemovals == 0 &&
            missingOccurenceRemovals == 0 &&
            extraOccurenceRemovals == 0;
        string error = BuildErrorText();

        AdvanceMonthDiagnosticsRecorder.SetMetabolismShadowResult(
            enabled: true,
            built: _built,
            matched: matched,
            predictedSecretRemovals: PredictedSecretRemovals.Count,
            actualSecretRemovals: ActualSecretRemovals.Count,
            missingSecretRemovals: missingSecretRemovals,
            extraSecretRemovals: extraSecretRemovals,
            predictedOccurenceRemovals: PredictedOccurenceRemovals.Count,
            actualOccurenceRemovals: ActualOccurenceRemovals.Count,
            missingOccurenceRemovals: missingOccurenceRemovals,
            extraOccurenceRemovals: extraOccurenceRemovals,
            error: error);
        Clear();
    }

    private static void BuildHolderCounts(
        Dictionary<SecretInformationId, SecretInformationData> secrets,
        Dictionary<int, CharacterKnownSecret> knownSecrets)
    {
        foreach (CharacterKnownSecret known in knownSecrets.Values)
        {
            if (known == null || known.KnownSecrets == null)
            {
                continue;
            }

            foreach (SecretInformationId secretId in known.KnownSecrets)
            {
                if (!secrets.ContainsKey(secretId))
                {
                    continue;
                }

                HolderCountsBySecret.TryGetValue(secretId, out int count);
                HolderCountsBySecret[secretId] = count + 1;
                _indexedKnownSecretLinks++;
            }
        }
    }

    private static void BuildPredictedRemovals(
        Dictionary<SecretInformationId, SecretInformationData> secrets,
        Dictionary<SecretOccurenceId, SecretOccurence> occurences)
    {
        List<SecretInformationId> secretIds = new List<SecretInformationId>(secrets.Keys);
        Dictionary<SecretInformationId, int> secretHolderCounts = new Dictionary<SecretInformationId, int>(secretIds.Count);

        foreach (SecretInformationId secretId in secretIds)
        {
            if (!secrets.TryGetValue(secretId, out SecretInformationData secret) ||
                secret == null ||
                !occurences.TryGetValue(secret.OccurenceId, out SecretOccurence occurence) ||
                occurence == null)
            {
                continue;
            }

            HolderCountsBySecret.TryGetValue(secretId, out int holderCount);
            int remainingLifeTime = InformationDomain.CalcSecretOccurenceRemainingLifeTime(occurence);
            Config.SecretInformationItem config = ConfigSecretInformation.Instance[occurence.TemplateId];
            if (holderCount >= config.MaxPersonAmount)
            {
                _broadcastCandidates++;
                _inconclusive = true;
            }

            if (remainingLifeTime <= 0 || occurence.InBroadcast)
            {
                holderCount = 0;
            }

            if (!SecretIdsByOccurence.TryGetValue(occurence.Id, out HashSet<SecretInformationId> idsByOccurence))
            {
                idsByOccurence = new HashSet<SecretInformationId>();
                SecretIdsByOccurence.Add(occurence.Id, idsByOccurence);
            }

            idsByOccurence.Add(secretId);
            secretHolderCounts[secretId] = occurence.InBroadcast ? 1 : holderCount;
        }

        foreach (KeyValuePair<SecretInformationId, int> pair in secretHolderCounts)
        {
            if (pair.Value < 1)
            {
                PredictedSecretRemovals.Add(pair.Key);
            }
        }

        foreach (SecretOccurence occurence in occurences.Values)
        {
            if (occurence == null || InformationDomain.CalcSecretOccurenceRemainingLifeTime(occurence) > 0)
            {
                continue;
            }

            PredictedOccurenceRemovals.Add(occurence.Id);
            if (SecretIdsByOccurence.TryGetValue(occurence.Id, out HashSet<SecretInformationId> idsByOccurence))
            {
                PredictedSecretRemovals.UnionWith(idsByOccurence);
            }
        }
    }

    private static string BuildErrorText()
    {
        string text = _error ?? string.Empty;
        if (_broadcastCandidates > 0)
        {
            text = AppendPart(text, "BroadcastCandidates=" + _broadcastCandidates);
        }

        text = AppendPart(text, "IndexedKnownSecretLinks=" + _indexedKnownSecretLinks);
        return text;
    }

    private static string AppendPart(string text, string part)
    {
        return string.IsNullOrEmpty(text) ? part : text + "; " + part;
    }

    private static int CountExcept<T>(HashSet<T> left, HashSet<T> right)
    {
        int count = 0;
        foreach (T value in left)
        {
            if (!right.Contains(value))
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryGetSecretInformationMap(
        InformationDomain domain,
        out Dictionary<SecretInformationId, SecretInformationData> secrets)
    {
        secrets = SecretInformationField == null ? null : SecretInformationField.GetValue(domain) as Dictionary<SecretInformationId, SecretInformationData>;
        return secrets != null;
    }

    private static bool TryGetSecretOccurenceMap(
        InformationDomain domain,
        out Dictionary<SecretOccurenceId, SecretOccurence> occurences)
    {
        occurences = SecretOccurenceField == null ? null : SecretOccurenceField.GetValue(domain) as Dictionary<SecretOccurenceId, SecretOccurence>;
        return occurences != null;
    }

    private static bool TryGetCharacterKnownSecretsMap(
        InformationDomain domain,
        out Dictionary<int, CharacterKnownSecret> knownSecrets)
    {
        knownSecrets = CharacterKnownSecretsField == null ? null : CharacterKnownSecretsField.GetValue(domain) as Dictionary<int, CharacterKnownSecret>;
        return knownSecrets != null;
    }

    private static void Clear()
    {
        _enabled = false;
        _built = false;
        _inconclusive = false;
        _indexedKnownSecretLinks = 0;
        _broadcastCandidates = 0;
        _error = string.Empty;
        HolderCountsBySecret.Clear();
        SecretIdsByOccurence.Clear();
        PredictedSecretRemovals.Clear();
        PredictedOccurenceRemovals.Clear();
        ActualSecretRemovals.Clear();
        ActualOccurenceRemovals.Clear();
    }
}
