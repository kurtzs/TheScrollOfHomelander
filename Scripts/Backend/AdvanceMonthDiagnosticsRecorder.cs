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

        builder.AppendLine("[ParallelActionManager.Execute]");
        AppendMetrics(builder, ParallelMetrics);
        builder.AppendLine();

        builder.AppendLine("[SaveWorld]");
        AppendText(builder, "ArchivePath", _current.ArchivePath);
        AppendText(builder, "CompressionAlgorithm", _current.CompressionAlgorithm);
        AppendText(builder, "CompressionType", _current.CompressionType);
        AppendBytes(builder, "FinalArchiveSizeBytes", _current.FinalArchiveSizeBytes);
        AppendMetric(builder, "ArchiveFileBase.Save", _current.ArchiveSaveTicks, _current.ArchiveSaveCalls, _current.ArchiveSaveException);
        AppendMetric(builder, "LocalArchiveFile.WriteContent", _current.WriteContentTicks, 1, _current.WriteContentException);
        AppendMetric(builder, "ArchiveFileBase.CopyFrom", _current.CopyFromTicks, _current.CopyFromCalls, _current.CopyFromException);
        AppendBytes(builder, "ArchiveFileBase.CopyFrom.Bytes", _current.CopyFromBytes);
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
            ExceptionName = exceptionName
        });
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
    }

    private struct Metric
    {
        public string Name;
        public long Ticks;
        public int Calls;
        public string ExceptionName;
    }
}
