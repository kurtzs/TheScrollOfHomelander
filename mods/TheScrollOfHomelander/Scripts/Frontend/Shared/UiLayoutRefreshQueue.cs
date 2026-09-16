#nullable disable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BetterTaiwuScroll.Frontend;

internal static class UiLayoutRefreshQueue
{
    private static DeferredLayoutRefreshRunner _runner;

    internal static void Request(RectTransform rect, int parentDepth = 0)
    {
        if (rect == null)
            return;

        GetRunner().Request(rect, parentDepth);
    }

    internal static void Clear()
    {
        if (_runner == null)
            return;

        var gameObject = _runner.gameObject;
        _runner = null;
        if (gameObject != null)
            Object.Destroy(gameObject);
    }

    private static DeferredLayoutRefreshRunner GetRunner()
    {
        if (_runner != null)
            return _runner;

        var host = new GameObject("BetterTaiwuScrollUiLayoutRefreshQueue")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        Object.DontDestroyOnLoad(host);
        _runner = host.AddComponent<DeferredLayoutRefreshRunner>();
        return _runner;
    }
}

internal sealed class DeferredLayoutRefreshRunner : MonoBehaviour
{
    private sealed class RequestState
    {
        internal int ParentDepth;
        internal int DueFrame;
    }

    private readonly Dictionary<RectTransform, RequestState> _requests = new Dictionary<RectTransform, RequestState>();
    private readonly List<KeyValuePair<RectTransform, RequestState>> _due = new List<KeyValuePair<RectTransform, RequestState>>();
    private readonly HashSet<RectTransform> _rebuilt = new HashSet<RectTransform>();

    internal void Request(RectTransform rect, int parentDepth)
    {
        if (rect == null)
            return;

        enabled = true;

        var dueFrame = Time.frameCount + 1;
        if (!_requests.TryGetValue(rect, out var state))
        {
            _requests[rect] = new RequestState
            {
                ParentDepth = Mathf.Max(0, parentDepth),
                DueFrame = dueFrame
            };
            return;
        }

        if (parentDepth > state.ParentDepth)
            state.ParentDepth = parentDepth;
        if (dueFrame < state.DueFrame)
            state.DueFrame = dueFrame;
    }

    private void LateUpdate()
    {
        if (_requests.Count == 0)
        {
            enabled = false;
            return;
        }

        _due.Clear();
        foreach (var entry in _requests)
        {
            if (entry.Key == null || entry.Value.DueFrame <= Time.frameCount)
                _due.Add(entry);
        }

        if (_due.Count == 0)
            return;

        for (var i = 0; i < _due.Count; i++)
        {
            _requests.Remove(_due[i].Key);
        }

        _due.Sort((left, right) => GetHierarchyDepth(right.Key).CompareTo(GetHierarchyDepth(left.Key)));
        _rebuilt.Clear();
        for (var i = 0; i < _due.Count; i++)
        {
            var rect = _due[i].Key;
            if (rect == null || !rect.gameObject.activeInHierarchy)
                continue;

            var current = rect;
            var depth = 0;
            var maxDepth = Mathf.Max(0, _due[i].Value.ParentDepth);
            while (current != null)
            {
                if (current.gameObject.activeInHierarchy && _rebuilt.Add(current))
                    LayoutRebuilder.ForceRebuildLayoutImmediate(current);

                if (depth >= maxDepth)
                    break;

                current = current.parent as RectTransform;
                depth++;
            }
        }

        _due.Clear();
        _rebuilt.Clear();
        if (_requests.Count == 0)
            enabled = false;
    }

    private static int GetHierarchyDepth(Transform transform)
    {
        var depth = 0;
        while (transform != null)
        {
            depth++;
            transform = transform.parent;
        }

        return depth;
    }
}
