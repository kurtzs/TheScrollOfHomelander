using System;
using System.Reflection;
using GameData.Common;
using GameData.Domains.Information;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch]
internal static class AdvanceMonthMetabolismHolderIndexLifecyclePatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(InformationDomain), "MetabolismSecretInformation", new Type[1] { typeof(DataContext) });
	}

	[HarmonyPriority(800)]
	private static void Prefix(InformationDomain __instance)
	{
		AdvanceMonthMetabolismHolderIndex.Begin(__instance);
	}

	private static Exception Finalizer(Exception __exception)
	{
		AdvanceMonthMetabolismHolderIndex.Finish();
		return __exception;
	}
}

