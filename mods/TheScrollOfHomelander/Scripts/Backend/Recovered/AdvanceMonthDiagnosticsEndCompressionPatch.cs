using System;
using GameData.ArchiveData;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(CompressionStreamFactory), "EndCompression")]
internal static class AdvanceMonthDiagnosticsEndCompressionPatch
{
	private static void Prefix(out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginStep();
	}

	private static Exception Finalizer(long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndEndCompression(__state, __exception);
		return __exception;
	}
}

