using System;
using GameData.Domains.World;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(WorldDomain), "AdvanceMonth")]
internal static class AdvanceMonthDiagnosticsAdvanceMonthPatch
{
	private static void Prefix(out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginAdvanceMonth();
	}

	private static Exception Finalizer(long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndAdvanceMonth(__state, __exception);
		return __exception;
	}
}

