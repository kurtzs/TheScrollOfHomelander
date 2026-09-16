using System;
using GameData.Common;
using GameData.Domains.Information;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(InformationDomain), "DiscardSecretInformation", new Type[]
{
	typeof(DataContext),
	typeof(int),
	typeof(SecretInformationId)
})]
internal static class AdvanceMonthDiagnosticsDiscardSecretInformationPatch
{
	private static void Prefix(out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginMetabolismDetail();
	}

	private static Exception Finalizer(long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndMetabolismDetail("DiscardSecretInformation", __state, __exception);
		return __exception;
	}
}

