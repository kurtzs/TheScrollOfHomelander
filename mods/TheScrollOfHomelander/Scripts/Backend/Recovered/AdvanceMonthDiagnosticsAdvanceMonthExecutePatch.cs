using System;
using GameData.Domains.World;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(WorldDomain), "AdvanceMonth_Execute")]
internal static class AdvanceMonthDiagnosticsAdvanceMonthExecutePatch
{
	private static void Prefix(out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginStep();
	}

	private static Exception Finalizer(long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndAdvanceMonthExecute(__state, __exception);
		return __exception;
	}
}

