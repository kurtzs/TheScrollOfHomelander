using System;
using System.Reflection;
using GameData.Common;
using GameData.Domains.Information;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch]
internal static class AdvanceMonthMetabolismHolderIndexRemoveSecretInformationPatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(InformationDomain), "RemoveElement_SecretInformation", new Type[2]
		{
			typeof(SecretInformationId),
			typeof(DataContext)
		});
	}

	private static void Prefix(SecretInformationId elementId)
	{
		AdvanceMonthMetabolismHolderIndex.OnRemoveSecretInformation(elementId);
	}
}

