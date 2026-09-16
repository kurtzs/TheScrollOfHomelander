using GameData.ArchiveData;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(ArchiveFileBase), "Save")]
internal static class AdvanceMonthNoCompressionSavePatch
{
	[HarmonyPriority(800)]
	private static void Prefix(ArchiveFileBase __instance, ref CompressionType compressionType)
	{
		if (AdvanceMonthDiagnosticsSettings.NoCompressionEnabled && __instance is LocalArchiveFile)
		{
			compressionType = CompressionType.NoCompression;
		}
	}
}

