#nullable disable
#pragma warning disable CS0105

using GameData.ArchiveData;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(ArchiveFileBase), "Save")]
internal static class AdvanceMonthNoCompressionSavePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(ArchiveFileBase __instance, ref CompressionType compressionType)
    {
        if (!AdvanceMonthDiagnosticsSettings.NoCompressionEnabled || !(__instance is LocalArchiveFile))
        {
            return;
        }

        compressionType = CompressionType.NoCompression;
    }
}
