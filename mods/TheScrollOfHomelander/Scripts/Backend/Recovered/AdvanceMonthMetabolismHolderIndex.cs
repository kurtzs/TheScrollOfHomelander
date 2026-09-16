using System;
using System.Collections.Generic;
using System.Reflection;
using GameData.Domains.Information;
using GameData.Domains.Information.Secret;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

internal static class AdvanceMonthMetabolismHolderIndex
{
	private static readonly FieldInfo CharacterKnownSecretsField = AccessTools.Field(typeof(InformationDomain), "_characterKnownSecrets");

	private static readonly Dictionary<SecretInformationId, HashSet<int>> HoldersBySecret = new Dictionary<SecretInformationId, HashSet<int>>(16384);

	private static readonly Dictionary<int, HashSet<SecretInformationId>> SecretsByCharacter = new Dictionary<int, HashSet<SecretInformationId>>(8192);

	private static bool _active;

	private static bool _transpilerApplied;

	private static int _builds;

	private static int _hits;

	private static int _fallbacks;

	private static int _setCharacterUpdates;

	private static string _lastError = string.Empty;

	internal static void Begin(InformationDomain domain)
	{
		ResetCountersForRun();
		if (!AdvanceMonthDiagnosticsSettings.MetabolismHolderIndexEnabled || !_transpilerApplied)
		{
			return;
		}
		try
		{
			if (!TryGetCharacterKnownSecretsMap(domain, out var knownSecrets))
			{
				_lastError = "ReflectCharacterKnownSecretsFailed";
				return;
			}
			foreach (KeyValuePair<int, CharacterKnownSecret> item in knownSecrets)
			{
				HashSet<SecretInformationId> hashSet = CopySecretSet(item.Value);
				SecretsByCharacter[item.Key] = hashSet;
				foreach (SecretInformationId item2 in hashSet)
				{
					AddHolder(item2, item.Key);
				}
			}
			_active = true;
			_builds++;
		}
		catch (Exception ex)
		{
			_active = false;
			_lastError = ex.GetType().Name;
			ClearMaps();
		}
	}

	internal static void Finish()
	{
		_active = false;
		ClearMaps();
	}

	internal static void SetTranspilerApplied()
	{
		_transpilerApplied = true;
	}

	internal static void FillKnownSecretHolders(InformationDomain domain, HashSet<int> destination, SecretInformationId secretId)
	{
		if (destination == null)
		{
			return;
		}
		if (_active)
		{
			if (HoldersBySecret.TryGetValue(secretId, out var value))
			{
				foreach (int item in value)
				{
					destination.Add(item);
				}
			}
			_hits++;
		}
		else
		{
			_fallbacks++;
			SlowFillKnownSecretHolders(domain, destination, secretId);
		}
	}

	internal static void OnSetCharacterKnownSecrets(int characterId, CharacterKnownSecret value)
	{
		if (!_active)
		{
			return;
		}
		try
		{
			if (!SecretsByCharacter.TryGetValue(characterId, out var value2))
			{
				value2 = new HashSet<SecretInformationId>();
			}
			HashSet<SecretInformationId> hashSet = CopySecretSet(value);
			foreach (SecretInformationId item in value2)
			{
				if (!hashSet.Contains(item))
				{
					RemoveHolder(item, characterId);
				}
			}
			foreach (SecretInformationId item2 in hashSet)
			{
				if (!value2.Contains(item2))
				{
					AddHolder(item2, characterId);
				}
			}
			SecretsByCharacter[characterId] = hashSet;
			_setCharacterUpdates++;
		}
		catch (Exception ex)
		{
			Deactivate(ex.GetType().Name);
		}
	}

	internal static void OnRemoveCharacterKnownSecrets(int characterId)
	{
		if (!_active)
		{
			return;
		}
		try
		{
			if (!SecretsByCharacter.TryGetValue(characterId, out var value))
			{
				return;
			}
			foreach (SecretInformationId item in value)
			{
				RemoveHolder(item, characterId);
			}
			SecretsByCharacter.Remove(characterId);
			_setCharacterUpdates++;
		}
		catch (Exception ex)
		{
			Deactivate(ex.GetType().Name);
		}
	}

	internal static void OnRemoveSecretInformation(SecretInformationId secretId)
	{
		if (!_active || !HoldersBySecret.TryGetValue(secretId, out var value))
		{
			return;
		}
		foreach (int item in value)
		{
			if (SecretsByCharacter.TryGetValue(item, out var value2))
			{
				value2.Remove(secretId);
			}
		}
		HoldersBySecret.Remove(secretId);
	}

	internal static void GetDiagnostics(out bool enabled, out bool active, out bool transpilerApplied, out int builds, out int hits, out int fallbacks, out int setCharacterUpdates, out string lastError)
	{
		enabled = AdvanceMonthDiagnosticsSettings.MetabolismHolderIndexEnabled;
		active = _active;
		transpilerApplied = _transpilerApplied;
		builds = _builds;
		hits = _hits;
		fallbacks = _fallbacks;
		setCharacterUpdates = _setCharacterUpdates;
		lastError = _lastError ?? string.Empty;
	}

	internal static void Reset()
	{
		_active = false;
		_builds = 0;
		_hits = 0;
		_fallbacks = 0;
		_setCharacterUpdates = 0;
		_lastError = string.Empty;
		ClearMaps();
	}

	private static void ResetCountersForRun()
	{
		_active = false;
		_builds = 0;
		_hits = 0;
		_fallbacks = 0;
		_setCharacterUpdates = 0;
		_lastError = string.Empty;
		ClearMaps();
	}

	private static HashSet<SecretInformationId> CopySecretSet(CharacterKnownSecret knownSecret)
	{
		HashSet<SecretInformationId> hashSet = new HashSet<SecretInformationId>();
		if (knownSecret?.KnownSecrets == null)
		{
			return hashSet;
		}
		foreach (SecretInformationId knownSecret2 in knownSecret.KnownSecrets)
		{
			hashSet.Add(knownSecret2);
		}
		return hashSet;
	}

	private static void AddHolder(SecretInformationId secretId, int characterId)
	{
		if (!HoldersBySecret.TryGetValue(secretId, out var value))
		{
			value = new HashSet<int>();
			HoldersBySecret.Add(secretId, value);
		}
		value.Add(characterId);
	}

	private static void RemoveHolder(SecretInformationId secretId, int characterId)
	{
		if (HoldersBySecret.TryGetValue(secretId, out var value))
		{
			value.Remove(characterId);
			if (value.Count == 0)
			{
				HoldersBySecret.Remove(secretId);
			}
		}
	}

	private static void SlowFillKnownSecretHolders(InformationDomain domain, HashSet<int> destination, SecretInformationId secretId)
	{
		if (domain == null || !TryGetCharacterKnownSecretsMap(domain, out var knownSecrets))
		{
			return;
		}
		foreach (KeyValuePair<int, CharacterKnownSecret> item in knownSecrets)
		{
			if (item.Value?.KnownSecrets != null && item.Value.KnownSecrets.Contains(secretId))
			{
				destination.Add(item.Key);
			}
		}
	}

	private static bool TryGetCharacterKnownSecretsMap(InformationDomain domain, out Dictionary<int, CharacterKnownSecret> knownSecrets)
	{
		knownSecrets = ((domain == null || CharacterKnownSecretsField == null) ? null : (CharacterKnownSecretsField.GetValue(domain) as Dictionary<int, CharacterKnownSecret>));
		return knownSecrets != null;
	}

	private static void Deactivate(string error)
	{
		_active = false;
		_lastError = error ?? string.Empty;
		ClearMaps();
	}

	private static void ClearMaps()
	{
		HoldersBySecret.Clear();
		SecretsByCharacter.Clear();
	}
}

