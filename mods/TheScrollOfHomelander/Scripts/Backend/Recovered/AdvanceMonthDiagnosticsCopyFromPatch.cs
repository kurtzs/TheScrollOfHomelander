using System;
using System.IO;
using GameData.ArchiveData;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(ArchiveFileBase), "CopyFrom", new Type[]
{
	typeof(Stream),
	typeof(long)
})]
internal static class AdvanceMonthDiagnosticsCopyFromPatch
{
	private static void Prefix(long length, out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginStep();
	}

	private static Exception Finalizer(long length, long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndCopyFrom(__state, length, __exception);
		return __exception;
	}
}

