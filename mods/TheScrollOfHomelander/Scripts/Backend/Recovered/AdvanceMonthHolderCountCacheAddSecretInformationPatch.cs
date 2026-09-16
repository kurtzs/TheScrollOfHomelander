using System;
using System.Reflection;
using GameData.Common;
using GameData.Domains.Information;
using GameData.Domains.Information.Secret;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;
using SecretInformation = GameData.Domains.Information.Secret.SecretInformation;

[HarmonyPatch]
internal static class AdvanceMonthHolderCountCacheAddSecretInformationPatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(InformationDomain), "AddElement_SecretInformation", new Type[3]
		{
			typeof(SecretInformationId),
			typeof(SecretInformation),
			typeof(DataContext)
		});
	}

	private static void Postfix(SecretInformationId elementId, SecretInformation value)
	{
		AdvanceMonthSecretInformationHolderCountCache.OnSecretInformationAdded(elementId, value);
	}
}

