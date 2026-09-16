using System;
using GameData.Domains.Information;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(InformationDomain), "MakeSettlementsInformation")]
internal static class AdvanceMonthDiagnosticsMakeSettlementsInformationPatch
{
	private static void Prefix(out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginInformationPhase();
	}

	private static Exception Finalizer(long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndInformationPhase("MakeSettlementsInformation", __state, __exception);
		return __exception;
	}
}

