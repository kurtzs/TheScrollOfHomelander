using System.Collections.Generic;
using System.Reflection;
using GameData.Domains.Information;
using GameData.Domains.Information.Secret;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;
using SecretInformation = GameData.Domains.Information.Secret.SecretInformation;

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
		if (!TryGetSecretInformationMap(domain, out var secrets) || !TryGetCharacterKnownSecretsMap(domain, out var knownSecrets) || secrets == null || knownSecrets == null)
		{
			Deactivate();
			return;
		}
		Clear();
		foreach (KeyValuePair<SecretInformationId, SecretInformation> item in secrets)
		{
			SecretInformation value = item.Value;
			if (value != null)
			{
				SecretToOccurence[item.Key] = value.OccurenceId;
				if (!HolderCountsByOccurence.ContainsKey(value.OccurenceId))
				{
					HolderCountsByOccurence.Add(value.OccurenceId, 0);
				}
			}
		}
		foreach (CharacterKnownSecret value4 in knownSecrets.Values)
		{
			if (value4 == null || value4.KnownSecrets == null)
			{
				continue;
			}
			CharacterOccurenceScratch.Clear();
			foreach (SecretInformationId knownSecret in value4.KnownSecrets)
			{
				if (SecretToOccurence.TryGetValue(knownSecret, out var value2))
				{
					CharacterOccurenceScratch.Add(value2);
				}
			}
			foreach (SecretOccurenceId item2 in CharacterOccurenceScratch)
			{
				HolderCountsByOccurence.TryGetValue(item2, out var value3);
				HolderCountsByOccurence[item2] = value3 + 1;
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

	internal static void OnSecretInformationAdded(SecretInformationId secretId, SecretInformation secret)
	{
		if (_active && secret != null)
		{
			SecretToOccurence[secretId] = secret.OccurenceId;
			if (!HolderCountsByOccurence.ContainsKey(secret.OccurenceId))
			{
				HolderCountsByOccurence.Add(secret.OccurenceId, 0);
			}
		}
	}

	internal static void OnReceiveSecretInformationFinished(bool success, SecretInformationId realReceivedSecretId)
	{
		if (_active && success)
		{
			if (!SecretToOccurence.TryGetValue(realReceivedSecretId, out var value))
			{
				Deactivate();
				return;
			}
			HolderCountsByOccurence.TryGetValue(value, out var value2);
			HolderCountsByOccurence[value] = value2 + 1;
		}
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

	private static bool TryGetSecretInformationMap(InformationDomain domain, out Dictionary<SecretInformationId, SecretInformation> secrets)
	{
		secrets = ((SecretInformationField == null) ? null : (SecretInformationField.GetValue(domain) as Dictionary<SecretInformationId, SecretInformation>));
		return secrets != null;
	}

	private static bool TryGetCharacterKnownSecretsMap(InformationDomain domain, out Dictionary<int, CharacterKnownSecret> knownSecrets)
	{
		knownSecrets = ((CharacterKnownSecretsField == null) ? null : (CharacterKnownSecretsField.GetValue(domain) as Dictionary<int, CharacterKnownSecret>));
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

