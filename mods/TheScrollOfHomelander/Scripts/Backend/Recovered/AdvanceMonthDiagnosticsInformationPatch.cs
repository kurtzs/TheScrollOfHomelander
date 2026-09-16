using System;
using GameData.Domains.Information;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(InformationDomain), "ProcessAdvanceMonth")]
internal static class AdvanceMonthDiagnosticsInformationPatch
{
	private static void Prefix(out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginStep();
	}

	private static Exception Finalizer(long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndInformationAdvanceMonth(__state, __exception);
		return __exception;
	}
}

