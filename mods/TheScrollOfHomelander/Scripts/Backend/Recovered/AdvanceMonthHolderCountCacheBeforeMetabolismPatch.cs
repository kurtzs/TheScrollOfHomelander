using System;
using System.Reflection;
using GameData.Common;
using GameData.Domains.Information;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch]
internal static class AdvanceMonthHolderCountCacheBeforeMetabolismPatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(InformationDomain), "MetabolismSecretInformation", new Type[1] { typeof(DataContext) });
	}

	[HarmonyPriority(800)]
	private static void Prefix()
	{
		AdvanceMonthSecretInformationHolderCountCache.DeactivateBeforeMetabolismSecretInformation();
	}
}

