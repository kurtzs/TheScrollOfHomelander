using System;
using System.Reflection;
using GameData.Common;
using GameData.Domains.Information;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsPlanDisseminateSecretInformationPatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(InformationDomain), "PlanDisseminateSecretInformation", new Type[2]
		{
			typeof(DataContext),
			typeof(int)
		});
	}

	private static void Prefix(out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginInformationPhase();
	}

	private static Exception Finalizer(long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndInformationPhase("PlanDisseminateSecretInformation", __state, __exception);
		return __exception;
	}
}

