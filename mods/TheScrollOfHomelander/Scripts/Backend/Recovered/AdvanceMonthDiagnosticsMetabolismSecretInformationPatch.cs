using System;
using System.Reflection;
using GameData.Common;
using GameData.Domains.Information;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsMetabolismSecretInformationPatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(InformationDomain), "MetabolismSecretInformation", new Type[1] { typeof(DataContext) });
	}

	private static void Prefix(out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginMetabolismSecretInformation();
		AdvanceMonthMetabolismShadowCompare.Begin(__state != 0);
	}

	private static Exception Finalizer(long __state, Exception __exception)
	{
		AdvanceMonthMetabolismShadowCompare.Finish();
		AdvanceMonthDiagnosticsRecorder.EndMetabolismSecretInformation(__state, __exception);
		return __exception;
	}
}

