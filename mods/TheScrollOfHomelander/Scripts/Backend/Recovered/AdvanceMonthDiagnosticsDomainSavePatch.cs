using System;
using System.Collections.Generic;
using System.Reflection;
using GameData.Common;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsDomainSavePatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		Type baseType = typeof(BaseGameDataDomain);
		Type[] types = baseType.Assembly.GetTypes();
		foreach (Type type in types)
		{
			if (!type.IsAbstract && baseType.IsAssignableFrom(type))
			{
				MethodInfo method = type.GetMethod("OnSaveWorld", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (method != null && method.DeclaringType == type)
				{
					yield return method;
				}
			}
		}
	}

	private static void Prefix(out long __state)
	{
		__state = AdvanceMonthDiagnosticsRecorder.BeginDomainSave();
	}

	private static Exception Finalizer(BaseGameDataDomain __instance, long __state, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndDomainSave(__instance, __state, __exception);
		return __exception;
	}
}

