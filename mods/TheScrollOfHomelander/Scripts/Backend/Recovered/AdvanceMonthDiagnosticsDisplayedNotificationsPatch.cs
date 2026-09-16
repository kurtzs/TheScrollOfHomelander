using System;
using GameData.Domains.World;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(WorldDomain), "AdvanceMonth_DisplayedMonthlyNotifications")]
internal static class AdvanceMonthDiagnosticsDisplayedNotificationsPatch
{
	private static Exception Finalizer(bool saveWorld, Exception __exception)
	{
		AdvanceMonthDiagnosticsRecorder.EndDisplayedNotifications(saveWorld, __exception);
		return __exception;
	}
}

