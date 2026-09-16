using System;
using System.Reflection;
using GameData.Common;
using GameData.Domains.Information;
using GameData.Domains.Information.Secret;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;
using SecretInformation = GameData.Domains.Information.Secret.SecretInformation;

[HarmonyPatch]
internal static class AdvanceMonthHolderCountCacheReceivePatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(InformationDomain), "ReceiveSecretInformation", new Type[5]
		{
			typeof(DataContext),
			typeof(SecretInformation),
			typeof(int),
			typeof(int),
			typeof(SecretInformationId).MakeByRefType()
		});
	}

	private static void Postfix(bool __result, ref SecretInformationId realReceivedSecretId)
	{
		AdvanceMonthSecretInformationHolderCountCache.OnReceiveSecretInformationFinished(__result, realReceivedSecretId);
	}
}

