using System;
using GameData.ActionPlanning.MonthlyAI;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(CharacterActionPlanner), "Plan")]
internal static class AdvanceMonthDiagnosticsCharacterActionPlannerPlanPatch
{
	private static void Prefix(out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginActionPlanningDetail();
	}

	private static Exception Finalizer(long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndActionPlanningDetail("CharacterActionPlanner.Plan", __state, __exception);
		return __exception;
	}
}

