#nullable disable

using UnityEngine;
using UnityEngine.UI;

namespace BetterTaiwuScroll.Frontend;

internal static class UiLayoutRefreshQueue
{
    internal static void Request(RectTransform rect, int parentDepth = 0, bool forceCanvas = false)
    {
        if (rect == null)
            return;

        var updater = rect.GetComponent<DeferredLayoutRefresh>();
        if (updater == null)
            updater = rect.gameObject.AddComponent<DeferredLayoutRefresh>();

        updater.Request(parentDepth, forceCanvas);
    }
}

internal sealed class DeferredLayoutRefresh : MonoBehaviour
{
    private RectTransform _rect;
    private int _frames;
    private int _parentDepth;
    private bool _forceCanvas;

    internal void Request(int parentDepth, bool forceCanvas)
    {
        _rect = transform as RectTransform;
        _frames = 1;
        if (parentDepth > _parentDepth)
            _parentDepth = parentDepth;
        _forceCanvas |= forceCanvas;
    }

    private void LateUpdate()
    {
        if (_frames > 0)
        {
            _frames--;
            return;
        }

        if (_forceCanvas)
            Canvas.ForceUpdateCanvases();

        var current = _rect;
        var depth = 0;
        while (current != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(current);
            if (depth >= _parentDepth)
                break;

            current = current.parent as RectTransform;
            depth++;
        }

        Destroy(this);
    }
}
