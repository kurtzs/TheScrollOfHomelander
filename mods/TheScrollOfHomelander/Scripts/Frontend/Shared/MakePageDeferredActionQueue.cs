#nullable disable

using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Views.Make;
using HarmonyLib;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

internal static class MakePageDeferredActionQueue
{
    private static DeferredMakePageActionRunner _runner;

    internal static void RequestCheckCondition(MakeSubPageMake page)
    {
        if (page == null)
            return;

        GetRunner().RequestCheckCondition(page);
    }

    internal static void Clear()
    {
        if (_runner == null)
            return;

        var gameObject = _runner.gameObject;
        _runner = null;
        if (gameObject != null)
            UnityEngine.Object.Destroy(gameObject);
    }

    private static DeferredMakePageActionRunner GetRunner()
    {
        if (_runner != null)
            return _runner;

        var host = new GameObject("BetterTaiwuScrollMakePageDeferredActionQueue")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        UnityEngine.Object.DontDestroyOnLoad(host);
        _runner = host.AddComponent<DeferredMakePageActionRunner>();
        return _runner;
    }
}

internal sealed class DeferredMakePageActionRunner : MonoBehaviour
{
    private static readonly MethodInfo CheckConditionMethod =
        MakeGameApi.CheckCondition;

    private readonly HashSet<MakeSubPageMake> _pendingCheckConditionPages = new HashSet<MakeSubPageMake>();
    private readonly List<MakeSubPageMake> _dueCheckConditionPages = new List<MakeSubPageMake>();

    internal void RequestCheckCondition(MakeSubPageMake page)
    {
        if (page != null)
        {
            _pendingCheckConditionPages.Add(page);
            enabled = true;
        }
    }

    private void LateUpdate()
    {
        if (_pendingCheckConditionPages.Count == 0)
        {
            enabled = false;
            return;
        }

        _dueCheckConditionPages.Clear();
        if (_pendingCheckConditionPages.Count == 0)
            enabled = false;
        foreach (var page in _pendingCheckConditionPages)
            _dueCheckConditionPages.Add(page);
        _pendingCheckConditionPages.Clear();

        for (var i = 0; i < _dueCheckConditionPages.Count; i++)
        {
            var page = _dueCheckConditionPages[i];
            if (page == null || page.gameObject == null || !page.gameObject.activeInHierarchy)
                continue;

            try
            {
                CheckConditionMethod?.Invoke(page, new object[] { true });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BetterTaiwuScroll] Deferred make condition refresh failed: " + ex.Message);
            }
        }

        _dueCheckConditionPages.Clear();
    }
}
