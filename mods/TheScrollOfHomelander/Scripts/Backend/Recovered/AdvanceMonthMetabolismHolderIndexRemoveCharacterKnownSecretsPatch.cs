using System;
using System.Reflection;
using GameData.Common;
using GameData.Domains.Information;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch]
internal static class AdvanceMonthMetabolismHolderIndexRemoveCharacterKnownSecretsPatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(InformationDomain), "RemoveElement_CharacterKnownSecrets", new Type[2]
		{
			typeof(int),
			typeof(DataContext)
		});
	}

	private static void Prefix(int elementId)
	{
		AdvanceMonthMetabolismHolderIndex.OnRemoveCharacterKnownSecrets(elementId);
	}
}

