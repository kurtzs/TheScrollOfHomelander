using System;
using System.Collections.Generic;
using System.Reflection;
using GameData.Domains.Information;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsMakeSecretBroadcastPatch
{
	private static MethodBase TargetMethod()
	{
		List<MethodInfo> declaredMethods = AccessTools.GetDeclaredMethods(typeof(InformationDomain));
		for (int i = 0; i < declaredMethods.Count; i++)
		{
			MethodInfo methodInfo = declaredMethods[i];
			if (!(methodInfo.Name != "MakeSecretBroadcast") && methodInfo.GetParameters().Length == 6)
			{
				return methodInfo;
			}
		}
		return null;
	}

	private static void Prefix(out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginMetabolismDetail();
	}

	private static Exception Finalizer(long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndMetabolismDetail("MakeSecretBroadcast", __state, __exception);
		return __exception;
	}
}

