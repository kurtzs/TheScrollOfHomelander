using System;
using System.Reflection;
using HarmonyLib;
using Game.Views.Make;

namespace BetterTaiwuScroll.Frontend;

internal static class MakeGameApi
{
    internal static readonly MethodInfo CheckCondition = AccessTools.Method(typeof(MakeSubPageMake), "CheckCondition", new[] { typeof(bool) });
    internal static readonly PropertyInfo IsPerfect = AccessTools.Property(typeof(MakeSubPageMake), "IsPerfect");
    internal static readonly FieldInfo Effects = AccessTools.Field(typeof(MakeSubPageMake), "_perfectEffectIdList");
    internal static readonly FieldInfo PerfectDropdown = AccessTools.Field(typeof(MakeSubPageMake), "perfectDropdown");
    internal static readonly FieldInfo ToolSelector = AccessTools.Field(typeof(ViewMake), "_makeToolSelector");
    internal static readonly MethodInfo AutoSelectTool = AccessTools.Method(typeof(MakeToolSelector), "GetAutoSelectTool", Type.EmptyTypes);

    internal static void Validate()
    {
        if (CheckCondition == null || IsPerfect == null || Effects == null || PerfectDropdown == null
            || ToolSelector == null || AutoSelectTool == null)
            throw new MissingMemberException("Installed MakeSubPageMake API does not match the required signatures.");
    }

    internal static bool GetIsPerfect(MakeSubPageMake page) => page != null && (bool)IsPerfect.GetValue(page);
    internal static void Refresh(MakeSubPageMake page, bool request) => CheckCondition.Invoke(page, new object[] { request });
}
