using System;
using GameData.Domains.Information;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(InformationDomain), "ProcessSecretInformationAdvanceMonth")]
internal static class AdvanceMonthHolderCountCacheLifecyclePatch
{
	private static void Prefix()
	{
		AdvanceMonthSecretInformationHolderCountCache.BeginSecretInformationAdvanceMonth();
	}

	private static Exception Finalizer(Exception __exception)
	{
		AdvanceMonthSecretInformationHolderCountCache.EndSecretInformationAdvanceMonth();
		return __exception;
	}
}

