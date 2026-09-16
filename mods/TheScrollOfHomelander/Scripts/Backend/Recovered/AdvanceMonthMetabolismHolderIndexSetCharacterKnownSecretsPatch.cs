using System;
using System.Reflection;
using GameData.Common;
using GameData.Domains.Information;
using GameData.Domains.Information.Secret;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch]
internal static class AdvanceMonthMetabolismHolderIndexSetCharacterKnownSecretsPatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(InformationDomain), "SetElement_CharacterKnownSecrets", new Type[3]
		{
			typeof(int),
			typeof(CharacterKnownSecret),
			typeof(DataContext)
		});
	}

	private static void Postfix(int elementId, CharacterKnownSecret value)
	{
		AdvanceMonthMetabolismHolderIndex.OnSetCharacterKnownSecrets(elementId, value);
	}
}

