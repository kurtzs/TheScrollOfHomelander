#nullable disable
#pragma warning disable CS0105

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using GameData.ArchiveData;
using GameData.Domains;

namespace BetterTaiwuScroll.Backend;

internal static class AdvanceMonthDiagnosticsRecorder
{
    private const string FallbackModPath =
        @"C:\Users\Administrator\AppData\Roaming\TaiwuStudio\Taiwu Studio\data\mods\TheScrollOfHomelander";

    private static readonly object SyncRoot = new object();
    private static readonly List<Metric> ParallelMetrics = new List<Metric>(32);
    private static readonly List<Metric> DomainSaveMetrics = new List<Metric>(32);
    private static readonly List<Metric> InformationPhaseMetrics = new List<Metric>(8);
    private static readonly List<Metric> InformationLookupMetrics = new List<Metric>(8);
    private static readonly List<Metric> MetabolismDetailMetrics = new List<Metric>(8);
    private static readonly List<Dictionary<string, Metric>> ActionPlanningMetricTables =
        new List<Dictionary<string, Metric>>(16);
    private static int _actionPlanningMetricGeneration;

    [ThreadStatic]
    private static Dictionary<string, Metric> _threadActionPlanningMetrics;

    [ThreadStatic]
    private static int _threadActionPlanningMetricGeneration;
    private static int _metabolismDepth;

    private static Session _current;

    internal static long BeginAdvanceMonth()
    {
        if (!AdvanceMonthDiagnosticsSettings.Enabled)
        {
            return 0L;
        }

        try
        {
            lock (SyncRoot)
            {
                ParallelMetrics.Clear();
                DomainSaveMetrics.Clear();
                InformationPhaseMetrics.Clear();
                InformationLookupMetrics.Clear();
                MetabolismDetailMetrics.Clear();
                ActionPlanningMetricTables.Clear();
                _actionPlanningMetricGeneration++;
                _metabolismDepth = 0;
                _current = new Session
                {
                    StartTicks = Stopwatch.GetTimestamp(),
                    CreatedAt = DateTime.Now,
                    DetailLevel = AdvanceMonthDiagnosticsSettings.DetailLevel
                };
                return _current.StartTicks;
            }
        }
        catch
        {
            return 0L;
        }
    }

    internal static void EndAdvanceMonth(long startTicks, Exception exception)
    {
        if (startTicks == 0L)
        {
            return;
        }

        try
        {
            lock (SyncRoot)
            {
                if (_current.StartTicks == 0L)
                {
                    return;
                }

                _current.AdvanceMonthTicks += Stopwatch.GetTimestamp() - startTicks;
                _current.AdvanceMonthException = GetExceptionName(exception);
                if (exception != null)
                {
                    WriteAndResetLocked("advance-month-exception");
                }
            }
        }
        catch
        {
        }
    }

    internal static void EndDisplayedNotifications(bool saveWorld, Exception exception)
    {
        if (!AdvanceMonthDiagnosticsSettings.Enabled)
        {
            return;
        }

        try
        {
            lock (SyncRoot)
            {
                if (_current.StartTicks == 0L)
                {
                    return;
                }

                _current.DisplayedNotificationsException = GetExceptionName(exception);
                if (!saveWorld)
                {
                    WriteAndResetLocked("notifications-finished-without-save");
                }
            }
        }
        catch
        {
        }
    }

    internal static long BeginStep()
    {
        if (!AdvanceMonthDiagnosticsSettings.Enabled)
        {
            return 0L;
        }

        try
        {
            lock (SyncRoot)
            {
                return _current.StartTicks == 0L ? 0L : Stopwatch.GetTimestamp();
            }
        }
        catch
        {
            return 0L;
        }
    }

    internal static void EndAdvanceMonthExecute(long startTicks, Exception exception)
    {
        AddTicks(startTicks, delegate(ref Session session, long ticks)
        {
            session.AdvanceMonthExecuteTicks += ticks;
            session.AdvanceMonthExecuteException = GetExceptionName(exception);
        });
    }

    internal static void EndInformationAdvanceMonth(long startTicks, Exception exception)
    {
        AddTicks(startTicks, delegate(ref Session session, long ticks)
        {
            session.InformationAdvanceMonthTicks += ticks;
            session.InformationAdvanceMonthException = GetExceptionName(exception);
        });
    }

    internal static long BeginInformationPhase()
    {
        return BeginStep();
    }

    internal static void EndInformationPhase(string name, long startTicks, Exception exception)
    {
        AddNamedMetric(InformationPhaseMetrics, name, startTicks, exception);
    }

    internal static long BeginMetabolismSecretInformation()
    {
        long ticks = BeginInformationPhase();
        if (ticks != 0L)
        {
            lock (SyncRoot)
            {
                _metabolismDepth++;
            }
        }

        return ticks;
    }

    internal static void EndMetabolismSecretInformation(long startTicks, Exception exception)
    {
        try
        {
            lock (SyncRoot)
            {
                if (_metabolismDepth > 0)
                {
                    _metabolismDepth--;
                }
            }
        }
        catch
        {
        }

        EndInformationPhase("MetabolismSecretInformation", startTicks, exception);
    }

    internal static long BeginMetabolismDetail()
    {
        if (!AdvanceMonthDiagnosticsSettings.Detailed)
        {
            return 0L;
        }

        try
        {
            lock (SyncRoot)
            {
                return _current.StartTicks != 0L && _metabolismDepth > 0 ? Stopwatch.GetTimestamp() : 0L;
            }
        }
        catch
        {
            return 0L;
        }
    }

    internal static void EndMetabolismDetail(string name, long startTicks, Exception exception)
    {
        AddNamedMetric(MetabolismDetailMetrics, name, startTicks, exception);
    }

    internal static long BeginInformationLookup()
    {
        return AdvanceMonthDiagnosticsSettings.Detailed ? BeginStep() : 0L;
    }

    internal static void EndInformationLookup(string name, long startTicks, Exception exception)
    {
        AddNamedMetric(InformationLookupMetrics, name, startTicks, exception);
    }

    internal static void SetMetabolismShadowResult(
        bool enabled,
        bool built,
        bool matched,
        int predictedSecretRemovals,
        int actualSecretRemovals,
        int missingSecretRemovals,
        int extraSecretRemovals,
        int predictedOccurenceRemovals,
        int actualOccurenceRemovals,
        int missingOccurenceRemovals,
        int extraOccurenceRemovals,
        string error)
    {
        if (!AdvanceMonthDiagnosticsSettings.Detailed)
        {
            return;
        }

        try
        {
            lock (SyncRoot)
            {
                if (_current.StartTicks == 0L)
                {
                    return;
                }

                _current.MetabolismShadowEnabled = enabled;
                _current.MetabolismShadowBuilt = built;
                _current.MetabolismShadowMatched = matched;
                _current.MetabolismShadowPredictedSecretRemovals = predictedSecretRemovals;
                _current.MetabolismShadowActualSecretRemovals = actualSecretRemovals;
                _current.MetabolismShadowMissingSecretRemovals = missingSecretRemovals;
                _current.MetabolismShadowExtraSecretRemovals = extraSecretRemovals;
                _current.MetabolismShadowPredictedOccurenceRemovals = predictedOccurenceRemovals;
                _current.MetabolismShadowActualOccurenceRemovals = actualOccurenceRemovals;
                _current.MetabolismShadowMissingOccurenceRemovals = missingOccurenceRemovals;
                _current.MetabolismShadowExtraOccurenceRemovals = extraOccurenceRemovals;
                _current.MetabolismShadowError = error ?? string.Empty;
            }
        }
        catch
        {
        }
    }

    internal static long BeginParallelAction(object action)
    {
        if (!AdvanceMonthDiagnosticsSettings.Enabled)
        {
            return 0L;
        }

        try
        {
            string name = GetActionName(action);
            if (!AdvanceMonthDiagnosticsSettings.Detailed &&
                name != "UpdatePrimaryGoalAndActions" &&
                name != "UpdateSecondaryGoalAndActions")
            {
                return 0L;
            }

            lock (SyncRoot)
            {
                return _current.StartTicks == 0L ? 0L : Stopwatch.GetTimestamp();
            }
        }
        catch
        {
            return 0L;
        }
    }

    internal static void EndParallelAction(object action, long startTicks, Exception exception)
    {
        if (startTicks == 0L)
        {
            return;
        }

        try
        {
            string name = GetActionName(action);
            long ticks = Stopwatch.GetTimestamp() - startTicks;
            lock (SyncRoot)
            {
                if (_current.StartTicks != 0L)
                {
                    AddMetric(ParallelMetrics, name, ticks, GetExceptionName(exception));
                }
            }
        }
        catch
        {
        }
    }

    internal static long BeginActionPlanningDetail()
    {
        if (!AdvanceMonthDiagnosticsSettings.ActionPlanningDetailed)
        {
            return 0L;
        }

        try
        {
            return _current.StartTicks == 0L ? 0L : Stopwatch.GetTimestamp();
        }
        catch
        {
            return 0L;
        }
    }

    internal static void EndActionPlanningDetail(string name, long startTicks, Exception exception)
    {
        if (startTicks == 0L)
        {
            return;
        }

        try
        {
            long ticks = Stopwatch.GetTimestamp() - startTicks;
            Dictionary<string, Metric> metrics = GetThreadActionPlanningMetrics();
            if (metrics == null)
            {
                return;
            }

            AddMetric(metrics, name, ticks, GetExceptionName(exception));
        }
        catch
        {
        }
    }

    internal static long BeginArchiveSave(ArchiveFileBase archive, CompressionAlgorithm algorithm, CompressionType compressionType)
    {
        if (!AdvanceMonthDiagnosticsSettings.Enabled || !(archive is LocalArchiveFile))
        {
            return 0L;
        }

        try
        {
            lock (SyncRoot)
            {
                if (_current.StartTicks == 0L)
                {
                    return 0L;
                }

                _current.ArchivePath = GetArchivePath(archive);
                _current.CompressionAlgorithm = algorithm.ToString();
                _current.CompressionType = compressionType.ToString();
                _current.ArchiveSaveCalls++;
                return Stopwatch.GetTimestamp();
            }
        }
        catch
        {
            return 0L;
        }
    }

    internal static void EndArchiveSave(long startTicks, Exception exception)
    {
        if (startTicks == 0L)
        {
            return;
        }

        try
        {
            lock (SyncRoot)
            {
                if (_current.StartTicks == 0L)
                {
                    return;
                }

                _current.ArchiveSaveTicks += Stopwatch.GetTimestamp() - startTicks;
                _current.ArchiveSaveException = GetExceptionName(exception);
                _current.FinalArchiveSizeBytes = TryGetFileSize(_current.ArchivePath);
                WriteAndResetLocked("archive-save-finished");
            }
        }
        catch
        {
        }
    }

    internal static void EndWriteContent(long startTicks, Exception exception)
    {
        AddTicks(startTicks, delegate(ref Session session, long ticks)
        {
            session.WriteContentTicks += ticks;
            session.WriteContentException = GetExceptionName(exception);
        });
    }

    internal static void EndCopyFrom(long startTicks, long length, Exception exception)
    {
        AddTicks(startTicks, delegate(ref Session session, long ticks)
        {
            session.CopyFromTicks += ticks;
            session.CopyFromBytes += Math.Max(0L, length);
            session.CopyFromCalls++;
            session.CopyFromException = GetExceptionName(exception);
        });
    }

    internal static void EndEndCompression(long startTicks, Exception exception)
    {
        AddTicks(startTicks, delegate(ref Session session, long ticks)
        {
            session.EndCompressionTicks += ticks;
            session.EndCompressionException = GetExceptionName(exception);
        });
    }

    internal static void EndDatabaseConnect(long startTicks, Exception exception)
    {
        AddTicks(startTicks, delegate(ref Session session, long ticks)
        {
            session.DatabaseConnectTicks += ticks;
            session.DatabaseConnectCalls++;
            session.DatabaseConnectException = GetExceptionName(exception);
        });
    }

    internal static void EndDatabaseDisconnect(long startTicks, Exception exception)
    {
        AddTicks(startTicks, delegate(ref Session session, long ticks)
        {
            session.DatabaseDisconnectTicks += ticks;
            session.DatabaseDisconnectCalls++;
            session.DatabaseDisconnectException = GetExceptionName(exception);
        });
    }

    internal static long BeginDomainSave()
    {
        if (!AdvanceMonthDiagnosticsSettings.Detailed)
        {
            return 0L;
        }

        try
        {
            lock (SyncRoot)
            {
                return _current.StartTicks == 0L ? 0L : Stopwatch.GetTimestamp();
            }
        }
        catch
        {
            return 0L;
        }
    }

    internal static void EndDomainSave(object domain, long startTicks, Exception exception)
    {
        if (startTicks == 0L)
        {
            return;
        }

        try
        {
            string name = GetTypeDisplayName(domain);
            long ticks = Stopwatch.GetTimestamp() - startTicks;
            lock (SyncRoot)
            {
                if (_current.StartTicks != 0L)
                {
                    AddMetric(DomainSaveMetrics, name, ticks, GetExceptionName(exception));
                }
            }
        }
        catch
        {
        }
    }

    internal static void Reset()
    {
        try
        {
            lock (SyncRoot)
            {
                _current = default(Session);
                ParallelMetrics.Clear();
                DomainSaveMetrics.Clear();
                InformationPhaseMetrics.Clear();
                InformationLookupMetrics.Clear();
                MetabolismDetailMetrics.Clear();
                ActionPlanningMetricTables.Clear();
                _actionPlanningMetricGeneration++;
                _metabolismDepth = 0;
            }
        }
        catch
        {
        }
    }

    private static void AddTicks(long startTicks, StepTickSetter setter)
    {
        if (startTicks == 0L)
        {
            return;
        }

        try
        {
            long ticks = Stopwatch.GetTimestamp() - startTicks;
            lock (SyncRoot)
            {
                if (_current.StartTicks != 0L)
                {
                    setter(ref _current, ticks);
                }
            }
        }
        catch
        {
        }
    }

    private static void AddNamedMetric(List<Metric> metrics, string name, long startTicks, Exception exception)
    {
        if (startTicks == 0L)
        {
            return;
        }

        try
        {
            long ticks = Stopwatch.GetTimestamp() - startTicks;
            lock (SyncRoot)
            {
                if (_current.StartTicks != 0L)
                {
                    AddMetric(metrics, name, ticks, GetExceptionName(exception));
                }
            }
        }
        catch
        {
        }
    }

    private static void WriteAndResetLocked(string reason)
    {
        try
        {
            if (_current.StartTicks == 0L)
            {
                return;
            }

            string directory = GetLogDirectory();
            Directory.CreateDirectory(directory);
            string fileName = "advance-month-" + _current.CreatedAt.ToString("yyyyMMdd-HHmmss-fff") + ".log";
            string path = Path.Combine(directory, fileName);
            File.WriteAllText(path, BuildLog(reason), Encoding.UTF8);
        }
        catch
        {
        }
        finally
        {
            _current = default(Session);
            ParallelMetrics.Clear();
            DomainSaveMetrics.Clear();
            InformationPhaseMetrics.Clear();
            InformationLookupMetrics.Clear();
            MetabolismDetailMetrics.Clear();
            ActionPlanningMetricTables.Clear();
            _actionPlanningMetricGeneration++;
            _metabolismDepth = 0;
        }
    }

    private static string BuildLog(string reason)
    {
        StringBuilder builder = new StringBuilder(4096);
        builder.AppendLine("TheScrollOfHomelander Advance Month Diagnostics");
        builder.AppendLine("Reason: " + reason);
        builder.AppendLine("CreatedAt: " + _current.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        builder.AppendLine("DetailLevel: " + (_current.DetailLevel >= 2 ? "Detailed" : "Standard"));
        builder.AppendLine();
        builder.AppendLine("[AdvanceMonth]");
        AppendMetric(builder, "WorldDomain.AdvanceMonth", _current.AdvanceMonthTicks, 1, _current.AdvanceMonthException);
        AppendMetric(builder, "WorldDomain.AdvanceMonth_Execute", _current.AdvanceMonthExecuteTicks, 1, _current.AdvanceMonthExecuteException);
        AppendMetric(builder, "InformationDomain.ProcessAdvanceMonth", _current.InformationAdvanceMonthTicks, 1, _current.InformationAdvanceMonthException);
        AppendText(builder, "DisplayedNotificationsException", _current.DisplayedNotificationsException);
        builder.AppendLine();

        builder.AppendLine("[InformationDomain.ProcessAdvanceMonth]");
        AppendMetrics(builder, InformationPhaseMetrics);
        AppendMetric(builder, "OtherSecretInformationAdvanceMonth", GetOtherInformationTicks(), 0, string.Empty);
        if (_current.DetailLevel >= 2)
        {
            builder.AppendLine("[InformationDomain.Lookups]");
            AppendMetricsWithMax(builder, InformationLookupMetrics);
            AppendHolderCountCacheDiagnostics(builder);
            AppendMetabolismDetails(builder);
            AppendMetabolismHolderIndexDiagnostics(builder);
            AppendSecretInformationRemoveBatchDiagnostics(builder);
            AppendMetabolismShadowDiagnostics(builder);
        }

        builder.AppendLine();

        builder.AppendLine("[ParallelActionManager.Execute]");
        AppendMetrics(builder, ParallelMetrics);
        builder.AppendLine();

        if (_current.DetailLevel >= 2)
        {
            builder.AppendLine("[CharacterActionPlanning]");
            AppendText(builder, "DetailedMetricsEnabled", AdvanceMonthDiagnosticsSettings.ActionPlanningDetailed ? "True" : "False");
            AppendActionPlanningMetrics(builder);
            AppendActionTargetRangeCacheDiagnostics(builder);
            AppendActionRelationCacheDiagnostics(builder);
            builder.AppendLine();
        }

        builder.AppendLine("[SaveWorld]");
        AppendText(builder, "ArchivePath", _current.ArchivePath);
        AppendText(builder, "CompressionAlgorithm", _current.CompressionAlgorithm);
        AppendText(builder, "CompressionType", _current.CompressionType);
        AppendBytes(builder, "FinalArchiveSizeBytes", _current.FinalArchiveSizeBytes);
        AppendMetric(builder, "ArchiveFileBase.Save", _current.ArchiveSaveTicks, _current.ArchiveSaveCalls, _current.ArchiveSaveException);
        AppendMetric(builder, "LocalArchiveFile.WriteContent", _current.WriteContentTicks, 1, _current.WriteContentException);
        AppendMetric(builder, "ArchiveFileBase.CopyFrom", _current.CopyFromTicks, _current.CopyFromCalls, _current.CopyFromException);
        AppendBytes(builder, "ArchiveFileBase.CopyFrom.Bytes", _current.CopyFromBytes);
        AppendText(builder, "ArchiveFileBase.CopyBufferOptimization", AdvanceMonthDiagnosticsSettings.CopyBufferOptimizationEnabled ? "Enabled" : "Disabled");
        AppendBytes(builder, "ArchiveFileBase.CopyBufferBytes", AdvanceMonthDiagnosticsSettings.CopyBufferBytes);
        AppendNumber(builder, "ArchiveFileBase.CopyFrom.EstimatedChunks", EstimateChunks(_current.CopyFromBytes, AdvanceMonthDiagnosticsSettings.CopyBufferBytes));
        AppendMetric(builder, "CompressionStreamFactory.EndCompression", _current.EndCompressionTicks, 1, _current.EndCompressionException);
        AppendMetric(builder, "DatabaseBridge.Connect", _current.DatabaseConnectTicks, _current.DatabaseConnectCalls, _current.DatabaseConnectException);
        AppendMetric(builder, "DatabaseBridge.Disconnect", _current.DatabaseDisconnectTicks, _current.DatabaseDisconnectCalls, _current.DatabaseDisconnectException);
        builder.AppendLine();

        if (_current.DetailLevel >= 2)
        {
            builder.AppendLine("[BaseGameDataDomain.OnSaveWorld]");
            AppendMetrics(builder, DomainSaveMetrics);
        }

        return builder.ToString();
    }

    private static void AppendHolderCountCacheDiagnostics(StringBuilder builder)
    {
        AdvanceMonthSecretInformationHolderCountCache.GetDiagnostics(
            out bool active,
            out int builds,
            out int hits,
            out int misses,
            out int deactivations);
        builder.AppendLine("[InformationDomain.HolderCountCache]");
        AppendText(builder, "Enabled", AdvanceMonthDiagnosticsSettings.HolderCountCacheEnabled ? "True" : "False");
        AppendText(builder, "ActiveAtLogTime", active ? "True" : "False");
        AppendNumber(builder, "Builds", builds);
        AppendNumber(builder, "Hits", hits);
        AppendNumber(builder, "Misses", misses);
        AppendNumber(builder, "Deactivations", deactivations);
    }

    private static void AppendMetabolismDetails(StringBuilder builder)
    {
        builder.AppendLine("[InformationDomain.MetabolismDetails]");
        AppendMetrics(builder, MetabolismDetailMetrics);
        AppendMetric(builder, "UnattributedMetabolismWork", GetUnattributedMetabolismTicks(), 0, string.Empty);
    }

    private static void AppendMetabolismHolderIndexDiagnostics(StringBuilder builder)
    {
        AdvanceMonthMetabolismHolderIndex.GetDiagnostics(
            out bool enabled,
            out bool active,
            out bool transpilerApplied,
            out int builds,
            out int hits,
            out int fallbacks,
            out int setCharacterUpdates,
            out string lastError);
        builder.AppendLine("[InformationDomain.MetabolismHolderIndex]");
        AppendText(builder, "Enabled", enabled ? "True" : "False");
        AppendText(builder, "ActiveAtLogTime", active ? "True" : "False");
        AppendText(builder, "TranspilerApplied", transpilerApplied ? "True" : "False");
        AppendNumber(builder, "Builds", builds);
        AppendNumber(builder, "Hits", hits);
        AppendNumber(builder, "Fallbacks", fallbacks);
        AppendNumber(builder, "SetCharacterUpdates", setCharacterUpdates);
        AppendText(builder, "LastError", lastError);
    }

    private static void AppendSecretInformationRemoveBatchDiagnostics(StringBuilder builder)
    {
        AdvanceMonthSecretInformationRemoveBatch.GetDiagnostics(
            out bool enabled,
            out int batches,
            out int inputIds,
            out int removedIds,
            out int fallbacks,
            out string lastError);
        builder.AppendLine("[InformationDomain.SecretInformationRemoveBatch]");
        AppendText(builder, "Enabled", enabled ? "True" : "False");
        AppendNumber(builder, "Batches", batches);
        AppendNumber(builder, "InputIds", inputIds);
        AppendNumber(builder, "RemovedIds", removedIds);
        AppendNumber(builder, "Fallbacks", fallbacks);
        AppendText(builder, "LastError", lastError);
    }

    private static void AppendMetabolismShadowDiagnostics(StringBuilder builder)
    {
        builder.AppendLine("[InformationDomain.MetabolismShadowCompare]");
        AppendText(builder, "Enabled", _current.MetabolismShadowEnabled ? "True" : "False");
        AppendText(builder, "Built", _current.MetabolismShadowBuilt ? "True" : "False");
        AppendText(builder, "Matched", _current.MetabolismShadowMatched ? "True" : "False");
        AppendNumber(builder, "PredictedSecretRemovals", _current.MetabolismShadowPredictedSecretRemovals);
        AppendNumber(builder, "ActualSecretRemovals", _current.MetabolismShadowActualSecretRemovals);
        AppendNumber(builder, "MissingSecretRemovals", _current.MetabolismShadowMissingSecretRemovals);
        AppendNumber(builder, "ExtraSecretRemovals", _current.MetabolismShadowExtraSecretRemovals);
        AppendNumber(builder, "PredictedOccurenceRemovals", _current.MetabolismShadowPredictedOccurenceRemovals);
        AppendNumber(builder, "ActualOccurenceRemovals", _current.MetabolismShadowActualOccurenceRemovals);
        AppendNumber(builder, "MissingOccurenceRemovals", _current.MetabolismShadowMissingOccurenceRemovals);
        AppendNumber(builder, "ExtraOccurenceRemovals", _current.MetabolismShadowExtraOccurenceRemovals);
        AppendText(builder, "Error", _current.MetabolismShadowError);
    }

    private static void AppendActionPlanningMetrics(StringBuilder builder)
    {
        var merged = new List<Metric>(32);
        for (int i = 0; i < ActionPlanningMetricTables.Count; i++)
        {
            Dictionary<string, Metric> table = ActionPlanningMetricTables[i];
            foreach (KeyValuePair<string, Metric> pair in table)
            {
                Metric metric = pair.Value;
                AddMetric(merged, metric.Name, metric.Ticks, metric.Calls, metric.MaxTicks, metric.ExceptionName);
            }
        }

        AppendMetricsWithMax(builder, merged);
    }

    private static void AppendActionTargetRangeCacheDiagnostics(StringBuilder builder)
    {
        AdvanceMonthActionTargetRangeCache.GetDiagnostics(
            out bool enabled,
            out int hits,
            out int misses,
            out int stores,
            out int clears,
            out int cachedCharacters,
            out int errors,
            out string lastError);
        builder.AppendLine("[CharacterActionPlanning.ActionTargetRangeCache]");
        AppendText(builder, "Enabled", enabled ? "True" : "False");
        AppendNumber(builder, "Hits", hits);
        AppendNumber(builder, "Misses", misses);
        AppendNumber(builder, "Stores", stores);
        AppendNumber(builder, "Clears", clears);
        AppendNumber(builder, "CachedCharacters", cachedCharacters);
        AppendNumber(builder, "Errors", errors);
        AppendText(builder, "LastError", lastError);
    }

    private static void AppendActionRelationCacheDiagnostics(StringBuilder builder)
    {
        AdvanceMonthActionRelationCache.GetDiagnostics(
            out bool enabled,
            out bool ageGroupEnabled,
            out int hits,
            out int misses,
            out int stores,
            out int clears,
            out int scopes,
            out int ageHits,
            out int ageMisses,
            out int transpilerApplications,
            out int tryGetRelationReplacements,
            out int getAgeGroupReplacements,
            out int errors,
            out string lastError);
        builder.AppendLine("[CharacterActionPlanning.ActionRelationCache]");
        AppendText(builder, "Enabled", enabled ? "True" : "False");
        AppendText(builder, "AgeGroupCacheEnabled", ageGroupEnabled ? "True" : "False");
        AppendNumber(builder, "Hits", hits);
        AppendNumber(builder, "Misses", misses);
        AppendNumber(builder, "Stores", stores);
        AppendNumber(builder, "Clears", clears);
        AppendNumber(builder, "Scopes", scopes);
        AppendNumber(builder, "AgeHits", ageHits);
        AppendNumber(builder, "AgeMisses", ageMisses);
        AppendNumber(builder, "TranspilerApplications", transpilerApplications);
        AppendNumber(builder, "TryGetRelationReplacements", tryGetRelationReplacements);
        AppendNumber(builder, "GetAgeGroupReplacements", getAgeGroupReplacements);
        AppendNumber(builder, "Errors", errors);
        AppendText(builder, "LastError", lastError);
    }

    private static long GetOtherInformationTicks()
    {
        long measured = 0L;
        for (int i = 0; i < InformationPhaseMetrics.Count; i++)
        {
            measured += InformationPhaseMetrics[i].Ticks;
        }

        return _current.InformationAdvanceMonthTicks > measured ? _current.InformationAdvanceMonthTicks - measured : 0L;
    }

    private static long GetUnattributedMetabolismTicks()
    {
        long total = 0L;
        for (int i = 0; i < InformationPhaseMetrics.Count; i++)
        {
            Metric metric = InformationPhaseMetrics[i];
            if (metric.Name == "MetabolismSecretInformation")
            {
                total = metric.Ticks;
                break;
            }
        }

        long measured = 0L;
        for (int i = 0; i < MetabolismDetailMetrics.Count; i++)
        {
            measured += MetabolismDetailMetrics[i].Ticks;
        }

        return total > measured ? total - measured : 0L;
    }

    private static void AppendMetrics(StringBuilder builder, List<Metric> metrics)
    {
        if (metrics.Count == 0)
        {
            builder.AppendLine("  (none)");
            return;
        }

        for (int i = 0; i < metrics.Count; i++)
        {
            Metric metric = metrics[i];
            AppendMetric(builder, metric.Name, metric.Ticks, metric.Calls, metric.ExceptionName);
        }
    }

    private static void AppendMetricsWithMax(StringBuilder builder, List<Metric> metrics)
    {
        if (metrics.Count == 0)
        {
            builder.AppendLine("  (none)");
            return;
        }

        for (int i = 0; i < metrics.Count; i++)
        {
            Metric metric = metrics[i];
            builder.Append("  ");
            builder.Append(metric.Name);
            builder.Append(": ");
            builder.Append(TicksToMilliseconds(metric.Ticks).ToString("0.###"));
            builder.Append(" ms, calls=");
            builder.Append(metric.Calls);
            builder.Append(", max=");
            builder.Append(TicksToMilliseconds(metric.MaxTicks).ToString("0.###"));
            builder.Append(" ms");
            if (!string.IsNullOrEmpty(metric.ExceptionName))
            {
                builder.Append(", exception=");
                builder.Append(metric.ExceptionName);
            }

            builder.AppendLine();
        }
    }

    private static void AppendMetric(StringBuilder builder, string name, long ticks, int calls, string exceptionName)
    {
        if (ticks <= 0L && calls <= 0 && string.IsNullOrEmpty(exceptionName))
        {
            return;
        }

        builder.Append("  ");
        builder.Append(name);
        builder.Append(": ");
        builder.Append(TicksToMilliseconds(ticks).ToString("0.###"));
        builder.Append(" ms");
        if (calls > 0)
        {
            builder.Append(", calls=");
            builder.Append(calls);
        }

        if (!string.IsNullOrEmpty(exceptionName))
        {
            builder.Append(", exception=");
            builder.Append(exceptionName);
        }

        builder.AppendLine();
    }

    private static void AppendText(StringBuilder builder, string name, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            builder.Append("  ");
            builder.Append(name);
            builder.Append(": ");
            builder.AppendLine(value);
        }
    }

    private static void AppendBytes(StringBuilder builder, string name, long value)
    {
        if (value >= 0L)
        {
            builder.Append("  ");
            builder.Append(name);
            builder.Append(": ");
            builder.Append(value);
            builder.AppendLine(" bytes");
        }
    }

    private static void AppendNumber(StringBuilder builder, string name, long value)
    {
        if (value >= 0L)
        {
            builder.Append("  ");
            builder.Append(name);
            builder.Append(": ");
            builder.AppendLine(value.ToString());
        }
    }

    private static long EstimateChunks(long bytes, long chunkBytes)
    {
        if (bytes <= 0L || chunkBytes <= 0L)
        {
            return 0L;
        }

        return (bytes + chunkBytes - 1L) / chunkBytes;
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks <= 0L ? 0.0 : ticks * 1000.0 / Stopwatch.Frequency;
    }

    private static void AddMetric(List<Metric> metrics, string name, long ticks, string exceptionName)
    {
        for (int i = 0; i < metrics.Count; i++)
        {
            Metric metric = metrics[i];
            if (metric.Name == name)
            {
                metric.Ticks += ticks;
                metric.Calls++;
                if (ticks > metric.MaxTicks)
                {
                    metric.MaxTicks = ticks;
                }

                if (!string.IsNullOrEmpty(exceptionName))
                {
                    metric.ExceptionName = exceptionName;
                }

                metrics[i] = metric;
                return;
            }
        }

        metrics.Add(new Metric
        {
            Name = name,
            Ticks = ticks,
            Calls = 1,
            MaxTicks = ticks,
            ExceptionName = exceptionName
        });
    }

    private static void AddMetric(List<Metric> metrics, string name, long ticks, int calls, long maxTicks, string exceptionName)
    {
        for (int i = 0; i < metrics.Count; i++)
        {
            Metric metric = metrics[i];
            if (metric.Name == name)
            {
                metric.Ticks += ticks;
                metric.Calls += calls;
                if (maxTicks > metric.MaxTicks)
                {
                    metric.MaxTicks = maxTicks;
                }

                if (!string.IsNullOrEmpty(exceptionName))
                {
                    metric.ExceptionName = exceptionName;
                }

                metrics[i] = metric;
                return;
            }
        }

        metrics.Add(new Metric
        {
            Name = name,
            Ticks = ticks,
            Calls = calls,
            MaxTicks = maxTicks,
            ExceptionName = exceptionName
        });
    }

    private static void AddMetric(Dictionary<string, Metric> metrics, string name, long ticks, string exceptionName)
    {
        if (metrics.TryGetValue(name, out Metric metric))
        {
            metric.Ticks += ticks;
            metric.Calls++;
            if (ticks > metric.MaxTicks)
            {
                metric.MaxTicks = ticks;
            }

            if (!string.IsNullOrEmpty(exceptionName))
            {
                metric.ExceptionName = exceptionName;
            }

            metrics[name] = metric;
            return;
        }

        metrics[name] = new Metric
        {
            Name = name,
            Ticks = ticks,
            Calls = 1,
            MaxTicks = ticks,
            ExceptionName = exceptionName
        };
    }

    private static Dictionary<string, Metric> GetThreadActionPlanningMetrics()
    {
        if (_current.StartTicks == 0L)
        {
            return null;
        }

        int generation = _actionPlanningMetricGeneration;
        Dictionary<string, Metric> metrics = _threadActionPlanningMetrics;
        if (metrics != null && _threadActionPlanningMetricGeneration == generation)
        {
            return metrics;
        }

        lock (SyncRoot)
        {
            if (_current.StartTicks == 0L)
            {
                return null;
            }

            metrics = _threadActionPlanningMetrics;
            if (metrics == null)
            {
                metrics = new Dictionary<string, Metric>(16);
                _threadActionPlanningMetrics = metrics;
            }
            else
            {
                metrics.Clear();
            }

            _threadActionPlanningMetricGeneration = generation;
            ActionPlanningMetricTables.Add(metrics);
            return metrics;
        }
    }

    private static string GetLogDirectory()
    {
        string modPath = null;
        try
        {
            modPath = DomainManager.Mod.GetModDirectory(AdvanceMonthDiagnosticsSettings.ModId);
        }
        catch
        {
        }

        if (string.IsNullOrEmpty(modPath) || !Directory.Exists(modPath))
        {
            modPath = FallbackModPath;
        }

        if (string.IsNullOrEmpty(modPath) || !Directory.Exists(modPath))
        {
            modPath = Directory.GetCurrentDirectory();
        }

        return Path.Combine(modPath, "UserData", "AdvanceMonthDiagnostics");
    }

    private static string GetArchivePath(ArchiveFileBase archive)
    {
        if (archive == null)
        {
            return string.Empty;
        }

        try
        {
            FieldInfo field = typeof(ArchiveFileBase).GetField("Path", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            object value = field == null ? null : field.GetValue(archive);
            return value as string ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static long TryGetFileSize(string path)
    {
        try
        {
            return string.IsNullOrEmpty(path) || !File.Exists(path) ? -1L : new FileInfo(path).Length;
        }
        catch
        {
            return -1L;
        }
    }

    private static string GetActionName(object action)
    {
        if (action == null)
        {
            return "(null)";
        }

        Type type = action.GetType();
        if (type.IsGenericType)
        {
            Type[] args = type.GetGenericArguments();
            if (args.Length == 1)
            {
                return GetTypeDisplayName(args[0]);
            }
        }

        return GetTypeDisplayName(type);
    }

    private static string GetTypeDisplayName(object value)
    {
        return value == null ? "(null)" : GetTypeDisplayName(value.GetType());
    }

    private static string GetTypeDisplayName(Type type)
    {
        if (type == null)
        {
            return "(null)";
        }

        string name = type.Name;
        int tick = name.IndexOf('`');
        return tick > 0 ? name.Substring(0, tick) : name;
    }

    private static string GetExceptionName(Exception exception)
    {
        return exception == null ? string.Empty : GetTypeDisplayName(exception.GetType());
    }

    private delegate void StepTickSetter(ref Session session, long ticks);

    private struct Session
    {
        public long StartTicks;
        public DateTime CreatedAt;
        public int DetailLevel;
        public long AdvanceMonthTicks;
        public string AdvanceMonthException;
        public long AdvanceMonthExecuteTicks;
        public string AdvanceMonthExecuteException;
        public long InformationAdvanceMonthTicks;
        public string InformationAdvanceMonthException;
        public string DisplayedNotificationsException;
        public int ArchiveSaveCalls;
        public long ArchiveSaveTicks;
        public string ArchiveSaveException;
        public string ArchivePath;
        public string CompressionAlgorithm;
        public string CompressionType;
        public long FinalArchiveSizeBytes;
        public long WriteContentTicks;
        public string WriteContentException;
        public long CopyFromTicks;
        public long CopyFromBytes;
        public int CopyFromCalls;
        public string CopyFromException;
        public long EndCompressionTicks;
        public string EndCompressionException;
        public long DatabaseConnectTicks;
        public int DatabaseConnectCalls;
        public string DatabaseConnectException;
        public long DatabaseDisconnectTicks;
        public int DatabaseDisconnectCalls;
        public string DatabaseDisconnectException;
        public bool MetabolismShadowEnabled;
        public bool MetabolismShadowBuilt;
        public bool MetabolismShadowMatched;
        public int MetabolismShadowPredictedSecretRemovals;
        public int MetabolismShadowActualSecretRemovals;
        public int MetabolismShadowMissingSecretRemovals;
        public int MetabolismShadowExtraSecretRemovals;
        public int MetabolismShadowPredictedOccurenceRemovals;
        public int MetabolismShadowActualOccurenceRemovals;
        public int MetabolismShadowMissingOccurenceRemovals;
        public int MetabolismShadowExtraOccurenceRemovals;
        public string MetabolismShadowError;
    }

    private struct Metric
    {
        public string Name;
        public long Ticks;
        public int Calls;
        public long MaxTicks;
        public string ExceptionName;
    }
}
