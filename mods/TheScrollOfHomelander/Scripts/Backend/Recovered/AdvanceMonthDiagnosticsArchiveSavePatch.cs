using System;
using GameData.ArchiveData;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(ArchiveFileBase), "Save")]
internal static class AdvanceMonthDiagnosticsArchiveSavePatch
{
	private static void Prefix(ArchiveFileBase __instance, CompressionAlgorithm algorithm, CompressionType compressionType, out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginArchiveSave(__instance, algorithm, compressionType);
	}

	private static Exception Finalizer(long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndArchiveSave(__state, __exception);
		return __exception;
	}
}

