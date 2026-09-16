using System;
using System.Collections.Generic;
using System.Reflection;
using GameData.ActionPlanning.MonthlyAI;
using GameData.Domains.Character;
using HarmonyLib;
using Redzen.Random;

namespace BetterTaiwuScroll.Backend;
using Character = GameData.Domains.Character.Character;

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsCharacterPlanningAgentFilterActionTargetsPatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(CharacterPlanningAgent), "FilterActionTargets", new Type[5]
		{
			typeof(IRandomSource),
			typeof(IReadOnlyList<Character>),
			typeof(ICollection<int>),
			typeof(Predicate<Character>),
			typeof(EPlanningActionCharacterSelector)
		});
	}

	private static void Prefix(EPlanningActionCharacterSelector selector, out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginActionPlanningDetail();
	}

	private static Exception Finalizer(EPlanningActionCharacterSelector selector, long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndActionPlanningDetail("FilterActionTargets." + selector, __state, __exception);
		return __exception;
	}
}

