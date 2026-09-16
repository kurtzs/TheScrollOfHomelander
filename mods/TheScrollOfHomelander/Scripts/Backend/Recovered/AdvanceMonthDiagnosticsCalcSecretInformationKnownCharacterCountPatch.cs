using System;
using System.Reflection;
using GameData.Domains.Information;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsCalcSecretInformationKnownCharacterCountPatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(InformationDomain), "CalcSecretInformationKnownCharacterCount");
	}

	private static void Prefix(out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginInformationLookup();
	}

	private static Exception Finalizer(long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndInformationLookup("CalcSecretInformationKnownCharacterCount", __state, __exception);
		return __exception;
	}
}

