using System;
using System.Collections.Generic;
using System.Reflection;
using GameData.Common;
using GameData.Domains.Information;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsRecordSecretOccurenceRemovePatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(InformationDomain), "RecordSecretOccurenceRemove", new Type[2]
		{
			typeof(DataContext),
			typeof(IEnumerable<SecretOccurenceId>)
		});
	}

	private static void Prefix(IEnumerable<SecretOccurenceId> idsToRemove, out long __state)
	{
		AdvanceMonthMetabolismShadowCompare.CaptureActualOccurenceRemovals(idsToRemove);
		__state = AdvanceMonthDiagnosticsRecorder.BeginMetabolismDetail();
	}

	private static Exception Finalizer(long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndMetabolismDetail("RecordSecretOccurenceRemove", __state, __exception);
		return __exception;
	}
}

