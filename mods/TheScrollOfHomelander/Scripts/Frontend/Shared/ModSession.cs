using System;
using System.Collections;
using FrameWork;
using HarmonyLib;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

internal sealed class ModSession : MonoBehaviour
{
    private static ModSession _host;
    internal static int Generation { get; private set; }
    internal static event Action Resetting;
    internal static bool Active => _host != null;
    internal static bool IsCurrent(int generation) => Active && Generation == generation;

    internal static void Initialize()
    {
        if (_host != null) return;
        var host = new GameObject("BetterTaiwuScrollSession") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(host);
        _host = host.AddComponent<ModSession>();
        GEvent.Add(EEvents.OnGameStateChange, OnGameStateChanged);
        Generation++;
    }

    private static void OnGameStateChanged(ArgumentBox args)
    {
        if (GameApp.Instance == null || GameApp.Instance.GetCurrentGameStateName() != EGameState.InGame)
            Reset();
    }

    internal static void Reset()
    {
        Generation++;
        if (_host != null) _host.StopAllCoroutines();
        foreach (var callback in Resetting?.GetInvocationList() ?? Array.Empty<Delegate>())
        {
            try { ((Action)callback)(); }
            catch (Exception ex) { Debug.LogWarning("[BetterTaiwuScroll] Session cleanup failed: " + ex); }
        }
    }

    internal static void Shutdown()
    {
        Reset();
        GEvent.Remove(EEvents.OnGameStateChange, OnGameStateChanged);
        if (_host != null) Destroy(_host.gameObject);
        _host = null;
        Resetting = null;
    }

    internal static void Delay(float seconds, Action action)
    {
        if (_host != null) _host.StartCoroutine(DelayRoutine(Generation, seconds, action));
    }

    private static IEnumerator DelayRoutine(int generation, float seconds, Action action)
    {
        if (seconds <= 0) yield return null;
        else yield return new WaitForSecondsRealtime(seconds);
        if (IsCurrent(generation)) action();
    }
}

[HarmonyPatch(typeof(GlobalOperations), "OnWorldDataReady", new Type[] { })]
internal static class ModSessionWorldReadyPatch
{
    private static void Prefix() => ModSession.Reset();
}
