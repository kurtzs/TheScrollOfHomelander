using System;
using System.Collections.Generic;
using System.Reflection;
using GameData.Common;
using GameData.Domains.Information;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch]
internal static class AdvanceMonthSecretInformationRemoveBatchPatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(InformationDomain), "RecordSecretInformationRemove", new Type[2]
		{
			typeof(DataContext),
			typeof(IEnumerable<SecretInformationId>)
		});
	}

	[HarmonyPriority(0)]
	private static void Prefix(ref IEnumerable<SecretInformationId> idsToRemove)
	{
		AdvanceMonthSecretInformationRemoveBatch.Prepare(ref idsToRemove);
	}
}

