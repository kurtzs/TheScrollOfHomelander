using System;
using System.Reflection;
using GameData.ActionPlanning.MonthlyAI;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsCharacterPlanningAgentGetCharactersInSelectRangePatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(CharacterPlanningAgent), "GetCharactersInSelectRange", new Type[2]
		{
			typeof(EPlanningActionCharacterSelectRange),
			typeof(int)
		});
	}

	private static void Prefix(EPlanningActionCharacterSelectRange range, out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginActionPlanningDetail();
	}

	private static Exception Finalizer(EPlanningActionCharacterSelectRange range, long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndActionPlanningDetail("GetCharactersInSelectRange." + range, __state, __exception);
		return __exception;
	}
}

