using System;
using GameData.ActionPlanning.MonthlyAI;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(CharacterActionPlanner), "ReassessPlan")]
internal static class AdvanceMonthDiagnosticsCharacterActionPlannerReassessPlanPatch
{
	private static void Prefix(out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginActionPlanningDetail();
	}

	private static Exception Finalizer(long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndActionPlanningDetail("CharacterActionPlanner.ReassessPlan", __state, __exception);
		return __exception;
	}
}

