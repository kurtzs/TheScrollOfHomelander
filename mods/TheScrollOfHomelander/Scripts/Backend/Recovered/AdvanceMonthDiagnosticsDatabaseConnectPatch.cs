using System;
using GameData.ArchiveData;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(DatabaseBridge), "Connect")]
internal static class AdvanceMonthDiagnosticsDatabaseConnectPatch
{
	private static void Prefix(out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginStep();
	}

	private static Exception Finalizer(long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndDatabaseConnect(__state, __exception);
		return __exception;
	}
}

