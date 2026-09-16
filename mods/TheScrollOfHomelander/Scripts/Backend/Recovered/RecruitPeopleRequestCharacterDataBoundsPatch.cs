using System;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Building;
using GameData.Domains.Extra;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(ExtraDomain), "RequestRecruitCharacterData", new Type[]
{
	typeof(DataContext),
	typeof(BuildingBlockKey),
	typeof(int),
	typeof(bool)
})]
internal static class RecruitPeopleRequestCharacterDataBoundsPatch
{
	private static bool Prefix(BuildingBlockKey buildingBlockKey, int earningDataIndex, ref RecruitCharacterData __result)
	{
		if (earningDataIndex < 0)
		{
			__result = null;
			return false;
		}
		if (!DomainManager.Building.TryGetElement_CollectBuildingEarningsData(buildingBlockKey, out var value) || value == null || value.RecruitLevelList == null || value.RecruitLevelList.Count == 0)
		{
			return true;
		}
		if (earningDataIndex < value.RecruitLevelList.Count)
		{
			return true;
		}
		__result = null;
		return false;
	}
}

