#nullable disable
#pragma warning disable CS0105

using System;
using System.Collections.Generic;
using System.Reflection;
using GameData.ActionPlanning.Interface;
using GameData.ActionPlanning.MonthlyAI;
using GameData.ActionPlanning.MonthlyAI.Node;
using GameData.ActionPlanning.State;
using GameData.Common;
using HarmonyLib;
using Redzen.Random;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(CharacterActionPlanner), "Plan")]
internal static class AdvanceMonthDiagnosticsCharacterActionPlannerPlanPatch
{
    private static void Prefix(out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginActionPlanningDetail();
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndActionPlanningDetail("CharacterActionPlanner.Plan", __state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(CharacterActionPlanner), "ReassessPlan")]
internal static class AdvanceMonthDiagnosticsCharacterActionPlannerReassessPlanPatch
{
    private static void Prefix(out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginActionPlanningDetail();
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndActionPlanningDetail("CharacterActionPlanner.ReassessPlan", __state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(CharacterPlanningAgent), "SelectActionTarget", new[]
{
    typeof(DataContext),
    typeof(PlanningGoalNode),
    typeof(PlanningActionNode),
    typeof(ContextArgGroupHandle)
})]
internal static class AdvanceMonthDiagnosticsCharacterPlanningAgentSelectActionTargetPatch
{
    private static void Prefix(out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginActionPlanningDetail();
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndActionPlanningDetail("CharacterPlanningAgent.SelectActionTarget", __state, __exception);
        return __exception;
    }
}

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsCharacterPlanningAgentGetCharactersInSelectRangePatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(CharacterPlanningAgent),
            "GetCharactersInSelectRange",
            new[] { typeof(EPlanningActionCharacterSelectRange), typeof(int) });
    }

    private static void Prefix(EPlanningActionCharacterSelectRange range, out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginActionPlanningDetail();
    }

    private static Exception Finalizer(EPlanningActionCharacterSelectRange range, long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndActionPlanningDetail("GetCharactersInSelectRange." + range, __state, __exception);
        return __exception;
    }
}

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsCharacterPlanningAgentFilterActionTargetsPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(CharacterPlanningAgent),
            "FilterActionTargets",
            new[]
            {
                typeof(IRandomSource),
                typeof(IReadOnlyList<GameData.Domains.Character.Character>),
                typeof(ICollection<int>),
                typeof(Predicate<GameData.Domains.Character.Character>),
                typeof(EPlanningActionCharacterSelector)
            });
    }

    private static void Prefix(EPlanningActionCharacterSelector selector, out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginActionPlanningDetail();
    }

    private static Exception Finalizer(EPlanningActionCharacterSelector selector, long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndActionPlanningDetail("FilterActionTargets." + selector, __state, __exception);
        return __exception;
    }
}

