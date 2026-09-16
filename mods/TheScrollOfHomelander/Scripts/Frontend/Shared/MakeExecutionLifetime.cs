using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

internal sealed class MakeExecutionLifetime : MonoBehaviour
{
    private static readonly HashSet<MakeExecutionLifetime> Instances = new();
    private int _epoch;
    private Coroutine _routine;
    private MakeSubPageMake _page;

    internal static Func<bool> Capture(MakeSubPageMake page)
    {
        var owner = Get(page);
        var epoch = owner._epoch;
        var session = ModSession.Generation;
        return () => owner != null && owner.isActiveAndEnabled && owner._epoch == epoch
            && ModSession.IsCurrent(session);
    }

    private static MakeExecutionLifetime Get(MakeSubPageMake page)
    {
        var owner = page.GetComponent<MakeExecutionLifetime>();
        if (owner == null) owner = page.gameObject.AddComponent<MakeExecutionLifetime>();
        owner._page = page;
        Instances.Add(owner);
        return owner;
    }

    internal static void Run(MakeSubPageMake page, IEnumerator routine)
    {
        var owner = Get(page);
        owner._routine = owner.StartCoroutine(Guard(routine, Capture(page)));
    }

    private static IEnumerator Guard(IEnumerator routine, Func<bool> current)
    {
        try
        {
            while (current() && routine.MoveNext())
            {
                if (routine.Current is IEnumerator nested) yield return Guard(nested, current);
                else yield return routine.Current;
            }
        }
        finally { (routine as IDisposable)?.Dispose(); }
    }

    internal static void CancelAll()
    {
        foreach (var owner in new List<MakeExecutionLifetime>(Instances))
            if (owner != null) owner.Cancel();
        Instances.Clear();
    }

    private void Cancel()
    {
        _epoch++;
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
    }

    private void OnDisable()
    {
        Cancel();
        ContinuousMakeExecutionController.Close(_page);
    }

    private void OnDestroy() { Instances.Remove(this); }
}
