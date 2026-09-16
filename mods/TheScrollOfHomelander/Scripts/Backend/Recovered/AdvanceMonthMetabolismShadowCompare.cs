using System;
using System.Collections.Generic;
using System.Reflection;
using Config;
using GameData.Domains;
using GameData.Domains.Information;
using GameData.Domains.Information.Secret;
using HarmonyLib;

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
		_enabled = diagnosticsActive && AdvanceMonthDiagnosticsSettings.Detailed && AdvanceMonthDiagnosticsSettings.MetabolismShadowCompareEnabled;
		if (!_enabled)
		{
			return;
		}
		try
		{
			InformationDomain information = DomainManager.Information;
			if (!TryGetSecretInformationMap(information, out var secrets) || !TryGetSecretOccurenceMap(information, out var occurences) || !TryGetCharacterKnownSecretsMap(information, out var knownSecrets))
			{
				_error = "ReflectFieldsFailed";
				return;
			}
			BuildHolderCounts(secrets, knownSecrets);
			BuildPredictedRemovals(secrets, occurences);
			_built = true;
		}
		catch (Exception ex)
		{
			_built = false;
			_error = ex.GetType().Name;
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
			foreach (SecretInformationId item in idsToRemove)
			{
				ActualSecretRemovals.Add(item);
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
			foreach (SecretOccurenceId item in idsToRemove)
			{
				ActualOccurenceRemovals.Add(item);
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
			AdvanceMonthDiagnosticsRecorder.SetMetabolismShadowResult(enabled: false, built: false, matched: false, 0, 0, 0, 0, 0, 0, 0, 0, string.Empty);
			Clear();
			return;
		}
		int num = CountExcept(PredictedSecretRemovals, ActualSecretRemovals);
		int num2 = CountExcept(ActualSecretRemovals, PredictedSecretRemovals);
		int num3 = CountExcept(PredictedOccurenceRemovals, ActualOccurenceRemovals);
		int num4 = CountExcept(ActualOccurenceRemovals, PredictedOccurenceRemovals);
		bool matched = _built && !_inconclusive && num == 0 && num2 == 0 && num3 == 0 && num4 == 0;
		string error = BuildErrorText();
		AdvanceMonthDiagnosticsRecorder.SetMetabolismShadowResult(enabled: true, _built, matched, PredictedSecretRemovals.Count, ActualSecretRemovals.Count, num, num2, PredictedOccurenceRemovals.Count, ActualOccurenceRemovals.Count, num3, num4, error);
		Clear();
	}

	private static void BuildHolderCounts(Dictionary<SecretInformationId, GameData.Domains.Information.Secret.SecretInformation> secrets, Dictionary<int, CharacterKnownSecret> knownSecrets)
	{
		foreach (CharacterKnownSecret value2 in knownSecrets.Values)
		{
			if (value2 == null || value2.KnownSecrets == null)
			{
				continue;
			}
			foreach (SecretInformationId knownSecret in value2.KnownSecrets)
			{
				if (secrets.ContainsKey(knownSecret))
				{
					HolderCountsBySecret.TryGetValue(knownSecret, out var value);
					HolderCountsBySecret[knownSecret] = value + 1;
					_indexedKnownSecretLinks++;
				}
			}
		}
	}

	private static void BuildPredictedRemovals(Dictionary<SecretInformationId, GameData.Domains.Information.Secret.SecretInformation> secrets, Dictionary<SecretOccurenceId, SecretOccurence> occurences)
	{
		List<SecretInformationId> list = new List<SecretInformationId>(secrets.Keys);
		Dictionary<SecretInformationId, int> dictionary = new Dictionary<SecretInformationId, int>(list.Count);
		foreach (SecretInformationId item in list)
		{
			if (secrets.TryGetValue(item, out var value) && value != null && occurences.TryGetValue(value.OccurenceId, out var value2) && value2 != null)
			{
				HolderCountsBySecret.TryGetValue(item, out var value3);
				int num = InformationDomain.CalcSecretOccurenceRemainingLifeTime(value2);
				SecretInformationItem secretInformationItem = Config.SecretInformation.Instance[value2.TemplateId];
				if (value3 >= secretInformationItem.MaxPersonAmount)
				{
					_broadcastCandidates++;
					_inconclusive = true;
				}
				if (num <= 0 || value2.InBroadcast)
				{
					value3 = 0;
				}
				if (!SecretIdsByOccurence.TryGetValue(value2.Id, out var value4))
				{
					value4 = new HashSet<SecretInformationId>();
					SecretIdsByOccurence.Add(value2.Id, value4);
				}
				value4.Add(item);
				dictionary[item] = (value2.InBroadcast ? 1 : value3);
			}
		}
		foreach (KeyValuePair<SecretInformationId, int> item2 in dictionary)
		{
			if (item2.Value < 1)
			{
				PredictedSecretRemovals.Add(item2.Key);
			}
		}
		foreach (SecretOccurence value6 in occurences.Values)
		{
			if (value6 != null && InformationDomain.CalcSecretOccurenceRemainingLifeTime(value6) <= 0)
			{
				PredictedOccurenceRemovals.Add(value6.Id);
				if (SecretIdsByOccurence.TryGetValue(value6.Id, out var value5))
				{
					PredictedSecretRemovals.UnionWith(value5);
				}
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
		return AppendPart(text, "IndexedKnownSecretLinks=" + _indexedKnownSecretLinks);
	}

	private static string AppendPart(string text, string part)
	{
		if (!string.IsNullOrEmpty(text))
		{
			return text + "; " + part;
		}
		return part;
	}

	private static int CountExcept<T>(HashSet<T> left, HashSet<T> right)
	{
		int num = 0;
		foreach (T item in left)
		{
			if (!right.Contains(item))
			{
				num++;
			}
		}
		return num;
	}

	private static bool TryGetSecretInformationMap(InformationDomain domain, out Dictionary<SecretInformationId, GameData.Domains.Information.Secret.SecretInformation> secrets)
	{
		secrets = ((SecretInformationField == null) ? null : (SecretInformationField.GetValue(domain) as Dictionary<SecretInformationId, GameData.Domains.Information.Secret.SecretInformation>));
		return secrets != null;
	}

	private static bool TryGetSecretOccurenceMap(InformationDomain domain, out Dictionary<SecretOccurenceId, SecretOccurence> occurences)
	{
		occurences = ((SecretOccurenceField == null) ? null : (SecretOccurenceField.GetValue(domain) as Dictionary<SecretOccurenceId, SecretOccurence>));
		return occurences != null;
	}

	private static bool TryGetCharacterKnownSecretsMap(InformationDomain domain, out Dictionary<int, CharacterKnownSecret> knownSecrets)
	{
		knownSecrets = ((CharacterKnownSecretsField == null) ? null : (CharacterKnownSecretsField.GetValue(domain) as Dictionary<int, CharacterKnownSecret>));
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

