#nullable disable
#pragma warning disable CS0105

using System;
using System.Collections.Generic;
using System.Reflection;
using GameData.ArchiveData;
using GameData.Common;
using GameData.Domains.Character.Ai.ParallelAdvanceMonth;
using GameData.Domains.Information;
using GameData.Domains.World;
using GameData.GameDataBridge;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

[HarmonyPatch(typeof(WorldDomain), "AdvanceMonth")]
internal static class AdvanceMonthDiagnosticsAdvanceMonthPatch
{
    private static void Prefix(out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginAdvanceMonth();
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndAdvanceMonth(__state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(WorldDomain), "AdvanceMonth_DisplayedMonthlyNotifications")]
internal static class AdvanceMonthDiagnosticsDisplayedNotificationsPatch
{
    private static Exception Finalizer(bool saveWorld, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndDisplayedNotifications(saveWorld, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(WorldDomain), "AdvanceMonth_Execute")]
internal static class AdvanceMonthDiagnosticsAdvanceMonthExecutePatch
{
    private static void Prefix(out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginStep();
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndAdvanceMonthExecute(__state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(ParallelActionManager), "Execute", new[] { typeof(DataMonitorManager), typeof(ICharacterParallelAction) })]
internal static class AdvanceMonthDiagnosticsParallelActionPatch
{
    private static void Prefix(ICharacterParallelAction action, out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginParallelAction(action);
    }

    private static Exception Finalizer(ICharacterParallelAction action, long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndParallelAction(action, __state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(ParallelActionManager), "Execute", new[] { typeof(DataMonitorManager), typeof(ICharacterParallelActionWithTarget) })]
internal static class AdvanceMonthDiagnosticsParallelActionWithTargetPatch
{
    private static void Prefix(ICharacterParallelActionWithTarget action, out long __state)
    {
        __state = AdvanceMonthDiagnosticsSettings.Detailed
            ? AdvanceMonthDiagnosticsRecorder.BeginParallelAction(action)
            : 0L;
    }

    private static Exception Finalizer(ICharacterParallelActionWithTarget action, long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndParallelAction(action, __state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(InformationDomain), "ProcessAdvanceMonth")]
internal static class AdvanceMonthDiagnosticsInformationPatch
{
    private static void Prefix(out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginStep();
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndInformationAdvanceMonth(__state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(InformationDomain), "MakeSettlementsInformation")]
internal static class AdvanceMonthDiagnosticsMakeSettlementsInformationPatch
{
    private static void Prefix(out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginInformationPhase();
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndInformationPhase("MakeSettlementsInformation", __state, __exception);
        return __exception;
    }
}

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsPlanDisseminateSecretInformationPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(InformationDomain),
            "PlanDisseminateSecretInformation",
            new[] { typeof(DataContext), typeof(int) });
    }

    private static void Prefix(out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginInformationPhase();
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndInformationPhase("PlanDisseminateSecretInformation", __state, __exception);
        return __exception;
    }
}

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsMetabolismSecretInformationPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(InformationDomain),
            "MetabolismSecretInformation",
            new[] { typeof(DataContext) });
    }

    private static void Prefix(out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginMetabolismSecretInformation();
        AdvanceMonthMetabolismShadowCompare.Begin(__state != 0L);
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthMetabolismShadowCompare.Finish();
        AdvanceMonthDiagnosticsRecorder.EndMetabolismSecretInformation(__state, __exception);
        return __exception;
    }
}

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsMakeSecretBroadcastPatch
{
    private static MethodBase TargetMethod()
    {
        List<MethodInfo> methods = AccessTools.GetDeclaredMethods(typeof(InformationDomain));
        for (int i = 0; i < methods.Count; i++)
        {
            MethodInfo method = methods[i];
            if (method.Name != "MakeSecretBroadcast" || method.GetParameters().Length != 6)
            {
                continue;
            }

            return method;
        }

        return null;
    }

    private static void Prefix(out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginMetabolismDetail();
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndMetabolismDetail("MakeSecretBroadcast", __state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(InformationDomain), "DiscardSecretInformation", new[] { typeof(DataContext), typeof(int), typeof(SecretInformationId) })]
internal static class AdvanceMonthDiagnosticsDiscardSecretInformationPatch
{
    private static void Prefix(out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginMetabolismDetail();
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndMetabolismDetail("DiscardSecretInformation", __state, __exception);
        return __exception;
    }
}

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsRecordSecretInformationRemovePatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(InformationDomain),
            "RecordSecretInformationRemove",
            new[] { typeof(DataContext), typeof(IEnumerable<SecretInformationId>) });
    }

    private static void Prefix(IEnumerable<SecretInformationId> idsToRemove, out long __state)
    {
        AdvanceMonthMetabolismShadowCompare.CaptureActualSecretRemovals(idsToRemove);
        __state = AdvanceMonthDiagnosticsRecorder.BeginMetabolismDetail();
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndMetabolismDetail("RecordSecretInformationRemove", __state, __exception);
        return __exception;
    }
}

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsRecordSecretOccurenceRemovePatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(InformationDomain),
            "RecordSecretOccurenceRemove",
            new[] { typeof(DataContext), typeof(IEnumerable<SecretOccurenceId>) });
    }

    private static void Prefix(IEnumerable<SecretOccurenceId> idsToRemove, out long __state)
    {
        AdvanceMonthMetabolismShadowCompare.CaptureActualOccurenceRemovals(idsToRemove);
        __state = AdvanceMonthDiagnosticsRecorder.BeginMetabolismDetail();
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndMetabolismDetail("RecordSecretOccurenceRemove", __state, __exception);
        return __exception;
    }
}

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsCalcSecretOccurenceHolderCountPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(InformationDomain),
            "CalcSecretOccurenceHolderCount");
    }

    private static void Prefix(out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginInformationLookup();
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndInformationLookup("CalcSecretOccurenceHolderCount", __state, __exception);
        return __exception;
    }
}

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsCalcSecretInformationKnownCharacterCountPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(InformationDomain),
            "CalcSecretInformationKnownCharacterCount");
    }

    private static void Prefix(out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginInformationLookup();
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndInformationLookup("CalcSecretInformationKnownCharacterCount", __state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(ArchiveFileBase), "Save")]
internal static class AdvanceMonthDiagnosticsArchiveSavePatch
{
    private static void Prefix(ArchiveFileBase __instance, CompressionAlgorithm algorithm, CompressionType compressionType, out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginArchiveSave(__instance, algorithm, compressionType);
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndArchiveSave(__state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(LocalArchiveFile), "WriteContent")]
internal static class AdvanceMonthDiagnosticsWriteContentPatch
{
    private static void Prefix(out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginStep();
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndWriteContent(__state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(ArchiveFileBase), "CopyFrom", new[] { typeof(System.IO.Stream), typeof(long) })]
internal static class AdvanceMonthDiagnosticsCopyFromPatch
{
    private static void Prefix(long length, out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginStep();
    }

    private static Exception Finalizer(long length, long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndCopyFrom(__state, length, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(CompressionStreamFactory), "EndCompression")]
internal static class AdvanceMonthDiagnosticsEndCompressionPatch
{
    private static void Prefix(out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginStep();
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndEndCompression(__state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(DatabaseBridge), "Connect")]
internal static class AdvanceMonthDiagnosticsDatabaseConnectPatch
{
    private static void Prefix(out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginStep();
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndDatabaseConnect(__state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(DatabaseBridge), "Disconnect")]
internal static class AdvanceMonthDiagnosticsDatabaseDisconnectPatch
{
    private static void Prefix(out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginStep();
    }

    private static Exception Finalizer(long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndDatabaseDisconnect(__state, __exception);
        return __exception;
    }
}

[HarmonyPatch]
internal static class AdvanceMonthDiagnosticsDomainSavePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        Type baseType = typeof(BaseGameDataDomain);
        Type[] types = baseType.Assembly.GetTypes();
        for (int i = 0; i < types.Length; i++)
        {
            Type type = types[i];
            if (type.IsAbstract || !baseType.IsAssignableFrom(type))
            {
                continue;
            }

            MethodInfo method = type.GetMethod(
                "OnSaveWorld",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null && method.DeclaringType == type)
            {
                yield return method;
            }
        }
    }

    private static void Prefix(out long __state)
    {
        __state = AdvanceMonthDiagnosticsRecorder.BeginDomainSave();
    }

    private static Exception Finalizer(BaseGameDataDomain __instance, long __state, Exception __exception)
    {
        AdvanceMonthDiagnosticsRecorder.EndDomainSave(__instance, __state, __exception);
        return __exception;
    }
}
