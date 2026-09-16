using System;
using GameData.Domains.Character.Ai.ParallelAdvanceMonth;
using GameData.GameDataBridge;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(ParallelActionManager), "Execute", new Type[]
{
	typeof(DataMonitorManager),
	typeof(ICharacterParallelActionWithTarget)
})]
internal static class AdvanceMonthDiagnosticsParallelActionWithTargetPatch
{
	private static void Prefix(ICharacterParallelActionWithTarget action, out long __state)
	{
		__state = (AdvanceMonthDiagnosticsSettings.Detailed ? AdvanceMonthDiagnosticsRecorder.BeginParallelAction(action) : 0);
	}

	private static Exception Finalizer(ICharacterParallelActionWithTarget action, long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndParallelAction(action, __state, __exception);
		return __exception;
	}
}

