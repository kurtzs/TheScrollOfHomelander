#nullable disable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

// Cache records and native materials must not outlive their map objects.
internal sealed class MapVisualLifetime : MonoBehaviour
{
    private readonly Dictionary<Action<int>, int> _cleanup = new Dictionary<Action<int>, int>();

    internal static void Register(Component owner, Action<int> cleanup)
    {
        var lifetime = owner.GetComponent<MapVisualLifetime>() ?? owner.gameObject.AddComponent<MapVisualLifetime>();
        lifetime._cleanup[cleanup] = owner.GetInstanceID();
    }

    private void OnDestroy()
    {
        foreach (var cleanup in _cleanup)
            cleanup.Key(cleanup.Value);
        _cleanup.Clear();
    }
}
