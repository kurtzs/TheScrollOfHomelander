using System;
using GameData.Domains.Character.Ai.ParallelAdvanceMonth;
using GameData.GameDataBridge;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(ParallelActionManager), "Execute", new Type[]
{
	typeof(DataMonitorManager),
	typeof(ICharacterParallelAction)
})]
internal static class AdvanceMonthDiagnosticsParallelActionPatch
{
	private static void Prefix(ICharacterParallelAction action, out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginParallelAction(action);
	}

	private static Exception Finalizer(ICharacterParallelAction action, long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndParallelAction(action, __state, __exception);
		return __exception;
	}
}

