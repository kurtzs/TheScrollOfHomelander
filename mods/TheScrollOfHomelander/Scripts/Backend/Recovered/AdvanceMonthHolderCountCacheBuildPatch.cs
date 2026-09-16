using GameData.Domains.Information;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(InformationDomain), "MakeSettlementsInformation")]
internal static class AdvanceMonthHolderCountCacheBuildPatch
{
	private static void Postfix(InformationDomain __instance)
	{
		AdvanceMonthSecretInformationHolderCountCache.BuildAfterMakeSettlementsInformation(__instance);
	}
}

