using System;
using System.Collections.Generic;
using System.Reflection;
using GameData.Common;
using GameData.Domains.Information;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsRecordSecretInformationRemovePatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(InformationDomain), "RecordSecretInformationRemove", new Type[2]
		{
			typeof(DataContext),
			typeof(IEnumerable<SecretInformationId>)
		});
	}

	private static void Prefix(IEnumerable<SecretInformationId> idsToRemove, out long __state)
	{
		AdvanceMonthMetabolismShadowCompare.CaptureActualSecretRemovals(idsToRemove);
		__state = AdvanceMonthDiagnosticsRecorder.BeginMetabolismDetail();
	}

	private static Exception Finalizer(long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndMetabolismDetail("RecordSecretInformationRemove", __state, __exception);
		return __exception;
	}
}

