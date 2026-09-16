#nullable disable

using HarmonyLib;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(TooltipInvoker), nameof(TooltipInvoker.Refresh), new[] { typeof(bool), typeof(int) })]
internal static class DestroyedTooltipInvokerRefreshPatch
{
    private static bool Prefix(TooltipInvoker __instance)
    {
        // ViewExchangeBase refreshes and rebuilds its rows immediately after moving an item,
        // then asks the old row's tooltip to refresh one frame later. Unity keeps the managed
        // wrapper after the native component is destroyed, so skip only that stale callback.
        return __instance != null;
    }
}
