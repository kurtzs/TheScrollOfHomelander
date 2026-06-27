#nullable disable
#pragma warning disable CS0105

using System;
using GameData.Domains;

namespace BetterTaiwuScroll.Backend;

internal static class AdvanceMonthDiagnosticsSettings
{
    internal static string ModId = string.Empty;
    internal static bool Enabled;
    internal static int DetailLevel = 1;

    internal static bool Detailed => Enabled && DetailLevel >= 2;

    internal static void Load(string modId)
    {
        ModId = modId ?? string.Empty;
        var enabled = false;
        var detailLevel = 1;
        TryGet(modId, "enable_advance_month_diagnostics", ref enabled);
        TryGet(modId, "advance_month_diagnostics_detail", ref detailLevel);
        Enabled = enabled;
        DetailLevel = Math.Clamp(detailLevel, 1, 2);
    }

    private static void TryGet(string modId, string key, ref bool value)
    {
        try
        {
            DomainManager.Mod.GetSetting(modId, key, ref value);
        }
        catch
        {
        }
    }

    private static void TryGet(string modId, string key, ref int value)
    {
        try
        {
            DomainManager.Mod.GetSetting(modId, key, ref value);
        }
        catch
        {
        }
    }
}
