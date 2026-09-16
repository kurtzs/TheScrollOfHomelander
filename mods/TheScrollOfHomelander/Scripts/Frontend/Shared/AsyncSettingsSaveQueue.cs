#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

internal static class AsyncSettingsSaveQueue
{
    private const int DebounceMilliseconds = 1000;
    private const int FlushTimeoutMilliseconds = 10000;

    private sealed class PendingSave
    {
        internal object Snapshot;
        internal DateTime DueUtc;
        internal int Attempts;
    }

    private static readonly object Gate = new object();
    private static readonly Dictionary<string, PendingSave> Pending =
        new Dictionary<string, PendingSave>(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<string> Errors = new Queue<string>();
    private static readonly AutoResetEvent Wake = new AutoResetEvent(false);
    private static readonly ManualResetEventSlim Drained = new ManualResetEventSlim(true);
    private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

    private static Thread _worker;
    private static AsyncSettingsSaveLifecycle _lifecycle;
    private static bool _stopping;
    private static int _activeWrites;

    internal static void Initialize()
    {
        EnsureWorker();
        if (_lifecycle != null)
            return;

        var host = new GameObject("BetterTaiwuScrollAsyncSettingsSaveQueue")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        UnityEngine.Object.DontDestroyOnLoad(host);
        _lifecycle = host.AddComponent<AsyncSettingsSaveLifecycle>();
    }

    internal static void Enqueue(string path, object immutableSnapshot)
    {
        if (string.IsNullOrEmpty(path) || immutableSnapshot == null)
            return;

        lock (Gate)
        {
            EnsureWorker();
            Pending[path] = new PendingSave
            {
                Snapshot = immutableSnapshot,
                DueUtc = DateTime.UtcNow.AddMilliseconds(DebounceMilliseconds)
            };
            Drained.Reset();
        }
        Wake.Set();
    }

    internal static void FlushAll(bool wait = true)
    {
        lock (Gate)
        {
            var now = DateTime.UtcNow;
            foreach (var save in Pending.Values)
            {
                save.DueUtc = now;
                save.Attempts = 0;
            }
            if (Pending.Count == 0 && _activeWrites == 0)
                Drained.Set();
        }

        Wake.Set();
        if (wait) Drained.Wait(FlushTimeoutMilliseconds);
        ReportErrors();
    }

    internal static void Shutdown()
    {
        FlushAll(false);
        Thread worker;
        lock (Gate)
        {
            _stopping = true;
            worker = _worker;
        }
        Wake.Set();
        if (worker != null && worker.IsAlive)
            worker.Join(FlushTimeoutMilliseconds);

        lock (Gate)
        {
            if (ReferenceEquals(_worker, worker) && (worker == null || !worker.IsAlive))
                _worker = null;
        }

        if (_lifecycle != null)
        {
            var host = _lifecycle.gameObject;
            _lifecycle = null;
            if (host != null)
                UnityEngine.Object.Destroy(host);
        }
        ReportErrors();
    }

    private static void EnsureWorker()
    {
        lock (Gate)
        {
            if (_worker != null && _worker.IsAlive)
            {
                _stopping = false;
                return;
            }

            _stopping = false;
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "BetterTaiwuScroll.SettingsSave"
            };
            _worker.Start();
        }
    }

    private static void WorkerLoop()
    {
        while (true)
        {
            List<KeyValuePair<string, PendingSave>> due = null;
            var waitMilliseconds = Timeout.Infinite;
            lock (Gate)
            {
                var now = DateTime.UtcNow;
                DateTime? earliest = null;
                foreach (var pair in Pending)
                {
                    if (pair.Value.Attempts >= 3) continue;
                    if (pair.Value.DueUtc <= now)
                    {
                        due ??= new List<KeyValuePair<string, PendingSave>>();
                        due.Add(pair);
                    }
                    else if (!earliest.HasValue || pair.Value.DueUtc < earliest.Value)
                    {
                        earliest = pair.Value.DueUtc;
                    }
                }

                if (due != null)
                {
                    for (var i = 0; i < due.Count; i++)
                        Pending.Remove(due[i].Key);
                    _activeWrites += due.Count;
                }
                else if (earliest.HasValue)
                {
                    waitMilliseconds = Math.Max(1, (int)(earliest.Value - now).TotalMilliseconds);
                }
                if (_stopping && due == null && !earliest.HasValue && _activeWrites == 0)
                {
                    _worker = null;
                    return;
                }
            }

            if (due == null)
            {
                Wake.WaitOne(waitMilliseconds);
                continue;
            }

            for (var i = 0; i < due.Count; i++)
            {
                var pair = due[i];
                if (!WriteSnapshot(pair.Key, pair.Value.Snapshot))
                {
                    lock (Gate)
                    {
                        pair.Value.Attempts++;
                        pair.Value.DueUtc = DateTime.UtcNow.AddSeconds(pair.Value.Attempts);
                        if (!Pending.ContainsKey(pair.Key)) Pending[pair.Key] = pair.Value;
                    }
                }
            }

            lock (Gate)
            {
                _activeWrites -= due.Count;
                if (Pending.Count == 0 && _activeWrites == 0)
                    Drained.Set();
            }
        }
    }

    private static bool WriteSnapshot(string path, object snapshot)
    {
        var tempPath = path + ".tmp." + Thread.CurrentThread.ManagedThreadId + "." + DateTime.UtcNow.Ticks;
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
            File.WriteAllText(tempPath, json, Utf8NoBom);
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null, true);
            }
            else
            {
                File.Move(tempPath, path);
            }
            return true;
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // Preserve the original write error.
            }

            lock (Gate)
            {
                if (Errors.Count >= 32) Errors.Dequeue();
                Errors.Enqueue("[BetterTaiwuScroll] Failed to save settings '" + path + "': " + ex);
            }
            return false;
        }
    }

    private static void ReportErrors()
    {
        while (true)
        {
            string message;
            lock (Gate)
            {
                if (Errors.Count == 0)
                    return;
                message = Errors.Dequeue();
            }
            Debug.LogWarning(message);
        }
    }
}

internal sealed class AsyncSettingsSaveLifecycle : MonoBehaviour
{
    private void OnApplicationPause(bool paused)
    {
        if (paused)
            AsyncSettingsSaveQueue.FlushAll(false);
    }

    private void OnApplicationQuit()
    {
        AsyncSettingsSaveQueue.FlushAll();
    }
}
