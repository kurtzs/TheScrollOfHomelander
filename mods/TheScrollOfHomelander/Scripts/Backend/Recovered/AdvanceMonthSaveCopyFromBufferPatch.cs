using System;
using System.Collections.Generic;
using System.IO;
using GameData.ArchiveData;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(ArchiveFileBase), "CopyFrom", new Type[]
{
	typeof(Stream),
	typeof(long)
})]
internal static class AdvanceMonthSaveCopyFromBufferPatch
{
	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		return AdvanceMonthSaveCopyBufferOptimization.ReplaceOriginalCopyBuffer(instructions);
	}
}

