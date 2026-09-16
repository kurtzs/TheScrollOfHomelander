using System.Reflection;
using GameData.Domains.Information;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch]
internal static class AdvanceMonthHolderCountCacheLookupPatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(InformationDomain), "CalcSecretOccurenceHolderCount");
	}

	[HarmonyPriority(800)]
	private static bool Prefix(SecretOccurenceId occurenceId, ref int __result)
	{
		if (!AdvanceMonthSecretInformationHolderCountCache.TryGetHolderCount(occurenceId, out var holderCount))
		{
			return true;
		}
		__result = holderCount;
		return false;
	}
}

