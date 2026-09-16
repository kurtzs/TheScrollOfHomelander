#nullable disable
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Views.Make;
using GameData.Domains.Building;
using GameData.Domains.Item.Display;
using HarmonyLib;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(MakeSubPageMake), "RequestMakeResult", new Type[] { })]
internal static class MakePreviewRequestScopePatch
{
    [ThreadStatic] internal static MakePreviewRequest Current;

    private static void Prefix(MakeSubPageMake __instance, out MakePreviewRequest __state)
    {
        __state = Current;
        Current = new MakePreviewRequest(__instance);
    }

    private static Exception Finalizer(Exception __exception, MakePreviewRequest __state)
    {
        Current = __state;
        return __exception;
    }
}

internal sealed class MakePreviewRequest
{
    private sealed class Epoch { internal int Value; }
    private static readonly ConditionalWeakTable<MakeSubPageMake, Epoch> Epochs = new();
    private static readonly FieldInfo Material = AccessTools.Field(typeof(MakeSubPageMake), "materialSlot");
    private static readonly FieldInfo Target = AccessTools.Field(typeof(MakeSubPageMake), "targetSlot");
    private static readonly FieldInfo Tool = AccessTools.Field(typeof(MakeSubPageMake), "toolSlot");
    private readonly MakeSubPageMake _page;
    private readonly Epoch _epoch;
    private readonly int _version;
    private readonly Func<bool> _ownerCurrent;
    private readonly ItemDisplayData _material;
    private readonly ItemDisplayData _target;
    private readonly ItemDisplayData _tool;
    private readonly bool _perfect;
    private bool _finishedStale;

    internal MakePreviewRequest(MakeSubPageMake page)
    {
        _page = page;
        _epoch = Epochs.GetValue(page, _ => new Epoch());
        _version = ++_epoch.Value;
        _ownerCurrent = MakeExecutionLifetime.Capture(page);
        _material = Read(Material);
        _target = Read(Target);
        _tool = Read(Tool);
        _perfect = MakeGameApi.GetIsPerfect(page);
    }

    private ItemDisplayData Read(FieldInfo field) => (field.GetValue(_page) as MakeTargetSlot)?.ItemData;

    internal AsyncMethodCallbackDelegate Wrap(AsyncMethodCallbackDelegate callback)
    {
        return (offset, pool) =>
        {
            if (!_ownerCurrent() || _epoch.Value != _version || _finishedStale) return;
            if (_material == null || _material.Amount <= 0 || _target == null
                || !ReferenceEquals(Read(Material), _material)
                || !ReferenceEquals(Read(Target), _target)
                || !ReferenceEquals(Read(Tool), _tool)
                || MakeGameApi.GetIsPerfect(_page) != _perfect)
            {
                // Complete the native wait without reading the now-empty material slot
                // or applying recipe data requested for an earlier selection.
                _finishedStale = true;
                MakeGameApi.Refresh(_page, false);
                return;
            }
            callback(offset, pool);
        };
    }
}

[HarmonyPatch]
internal static class MakePreviewCallbackLifetimePatch
{
    private static MethodBase TargetMethod()
    {
        var methods = typeof(BuildingDomainMethod.AsyncCall).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "GetMakeResult" && method.GetParameters().Length == 10
                && method.GetParameters()[9].ParameterType == typeof(AsyncMethodCallbackDelegate)).ToArray();
        if (methods.Length != 1) throw new MissingMethodException("Native make preview callback signature changed.");
        return methods[0];
    }

    private static void Prefix(ref AsyncMethodCallbackDelegate __9)
    {
        var request = MakePreviewRequestScopePatch.Current;
        if (request != null && __9 != null) __9 = request.Wrap(__9);
    }
}
