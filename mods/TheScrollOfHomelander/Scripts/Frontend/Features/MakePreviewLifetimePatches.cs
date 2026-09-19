#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Views.Make;
using GameData.Domains.Building;
using GameData.Domains.Item.Display;
using GameData.Serializer;
using HarmonyLib;
using UnityEngine;

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

    private static void Postfix(MakeSubPageMake __instance)
    {
        // With no materials the native loop sends no callbacks, so it never
        // resets _coroutineRequestMakeResult or redraws the disabled controls.
        var traverse = Traverse.Create(__instance);
        if (traverse.Field("_makeItemSubtypeIdList").GetValue<List<short>>() != null
            && traverse.Field("_allMatchMaterialList").GetValue<List<ItemDisplayData>>()?.Count == 0)
            MakeGameApi.Refresh(__instance, false);
    }
}

internal sealed class MakePreviewRequest
{
    private sealed class Epoch { internal int Value; internal MakePreviewRequest Request; }
    private static readonly ConditionalWeakTable<MakeSubPageMake, Epoch> Epochs = new();
    private static readonly FieldInfo Material = AccessTools.Field(typeof(MakeSubPageMake), "materialSlot");
    private static readonly FieldInfo Target = AccessTools.Field(typeof(MakeSubPageMake), "targetSlot");
    private static readonly FieldInfo Results = AccessTools.Field(typeof(MakeSubPageMake), "_materialMakeResultDict");
    private static readonly PropertyInfo CurrentResult = AccessTools.Property(typeof(MakeSubPageMake), "CurMakeResult");
    private static readonly FieldInfo PendingCoroutine = AccessTools.Field(typeof(MakeSubPageMake), "_coroutineRequestMakeResult");
    private readonly MakeSubPageMake _page;
    private readonly Epoch _epoch;
    private readonly int _version;
    private readonly Func<bool> _ownerCurrent;
    private readonly ItemDisplayData _material;
    private readonly ItemDisplayData _target;
    private readonly MakeRecipeSnapshot _recipe;
    private bool _finishedStale;
    private int _pendingCallbacks;

    internal MakePreviewRequest(MakeSubPageMake page)
    {
        _page = page;
        _epoch = Epochs.GetValue(page, _ => new Epoch());
        _version = ++_epoch.Value;
        _epoch.Request = this;
        _ownerCurrent = MakeExecutionLifetime.Capture(page);
        _material = Read(Material);
        _target = Read(Target);
        _recipe = new MakeRecipeSnapshot(page);
    }

    private ItemDisplayData Read(FieldInfo field) => (field.GetValue(_page) as MakeTargetSlot)?.ItemData;

    internal static void Cancel(MakeSubPageMake page)
    {
        if (Epochs.TryGetValue(page, out var epoch))
            epoch.Value++;
        Epochs.Remove(page);
        if (PendingCoroutine.GetValue(page) is Coroutine routine)
            page.StopCoroutine(routine);
        PendingCoroutine.SetValue(page, null);
        (Results.GetValue(page) as Dictionary<int, Dictionary<int, MakeResult>>)?.Clear();
    }

    internal AsyncMethodCallbackDelegate Wrap(AsyncMethodCallbackDelegate callback)
    {
        _pendingCallbacks++;
        return (offset, pool) =>
        {
            if (!_ownerCurrent() || _epoch.Value != _version || _finishedStale) return;
            if (!_recipe.IsCurrent() || (MakeSubPageMakeHelper.CheckIsRandomMake(_target)
                && (_material == null || _material.Amount <= 0)))
            {
                _finishedStale = true;
                (Results.GetValue(_page) as Dictionary<int, Dictionary<int, MakeResult>>)?.Clear();
                MakeGameApi.Refresh(_page, false);
                // Refresh(false) only redraws the UI; it does not refill the cache.
                if (Read(Material)?.Amount > 0)
                    MakePageDeferredActionQueue.RequestCheckCondition(_page);
                return;
            }
            _pendingCallbacks--;
            callback(offset, pool);
        };
    }

    internal static bool CanSubmit(MakeSubPageMake page)
    {
        var target = (Target.GetValue(page) as MakeTargetSlot)?.ItemData;
        if (!MakeSubPageMakeHelper.CheckIsRandomMake(target))
            return true;

        if (!Epochs.TryGetValue(page, out var epoch) || epoch.Request == null)
            return false;
        var request = epoch.Request;
        if (request._finishedStale || request._pendingCallbacks != 0 || !request._recipe.IsCurrent())
            return false;
        var traverse = Traverse.Create(page);
        var subTypes = traverse.Field("_makeItemSubtypeIdList").GetValue<List<short>>();
        var subType = traverse.Field("_makeItemSubTypeId").GetValue<short>();
        var config = subType < 0 ? null : Config.MakeItemSubType.Instance[subType];
        if (config == null || request._material == null || request._material.Amount <= 0)
            return false;
        var manual = traverse.Field("_isManual").GetValue<bool>();
        return MakeResultValidation.IsUsable((MakeResult)CurrentResult.GetValue(page), config.Result.ItemType,
            subTypes, manual && subTypes?.Count > 1 ? subType : (short)-1);
    }

    internal static void RefreshConfirmState(MakeSubPageMake page)
    {
        var view = MakeSelectMaterialPatch.GetParentView(page);
        if (view != null && Plugin.IsEnabledForLifeSkill(view.CurLifeSkillType)
            && ContinuousMakeExecutionController.IsBatchActive(view))
            return;
        var submitting = MakeSubmissionRequest.IsPending(page);
        if (!submitting && CanSubmit(page))
            return;

        var traverse = Traverse.Create(page);
        var button = traverse.Field("buttonConfirm").GetValue<CButton>();
        if (button != null && button.interactable)
        {
            button.interactable = false;
            var tip = traverse.Field("tipConfirm").GetValue<TooltipInvoker>();
            if (tip != null)
            {
                tip.enabled = true;
                tip.PresetParam = new[] { submitting ? "正在制作，请稍候" : "正在更新制作结果，请稍候" };
            }
        }

        // A local CheckCondition(false) can redraw after changing tools/options.
        // Only a stale context needs a new request; a current invalid result does not.
        if (!submitting && (Material.GetValue(page) as MakeTargetSlot)?.ItemData?.Amount > 0
            && (!Epochs.TryGetValue(page, out var epoch) || epoch.Request == null
                || epoch.Request._finishedStale || !epoch.Request._recipe.IsCurrent()))
            MakePageDeferredActionQueue.RequestCheckCondition(page);
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "OnClickButtonConfirm")]
internal static class MakePreviewConfirmReadyPatch
{
    [ThreadStatic] internal static MakeSubmissionRequest CurrentSubmission;

    [HarmonyPriority(Priority.First)]
    private static bool Prefix(MakeSubPageMake __instance, out MakeSubmissionRequest __state)
    {
        __state = CurrentSubmission;
        if (MakeSubmissionRequest.IsPending(__instance))
            return false;
        var view = MakeSelectMaterialPatch.GetParentView(__instance);
        if (view != null && Plugin.IsEnabledForLifeSkill(view.CurLifeSkillType)
            && ContinuousMakeExecutionController.IsBatchActive(view))
            return true;
        if (MakePreviewRequest.CanSubmit(__instance))
        {
            CurrentSubmission = new MakeSubmissionRequest(__instance);
            return true;
        }

        MakePageDeferredActionQueue.RequestCheckCondition(__instance);
        Debug.LogWarning("[BetterTaiwuScroll] Make preview is not ready for the current recipe; refreshing before submission.");
        return false;
    }

    private static Exception Finalizer(Exception __exception, MakeSubmissionRequest __state)
    {
        if (__exception != null && CurrentSubmission != __state)
            CurrentSubmission?.Reject();
        CurrentSubmission = __state;
        return __exception;
    }
}

internal sealed class MakeSubmissionRequest
{
    private static readonly ConditionalWeakTable<MakeSubPageMake, MakeSubmissionRequest> Pending = new();
    private readonly MakeSubPageMake _page;
    private readonly MakeRecipeSnapshot _recipe;
    private readonly Func<bool> _current;
    private bool _conditionHandled;

    internal MakeSubmissionRequest(MakeSubPageMake page)
    {
        _page = page;
        _recipe = new MakeRecipeSnapshot(page);
        _current = MakeExecutionLifetime.Capture(page);
    }

    internal static bool IsPending(MakeSubPageMake page) => Pending.TryGetValue(page, out var request) && request._current();

    internal static void Clear(MakeSubPageMake page) => Pending.Remove(page);

    private bool OwnsPending => Pending.TryGetValue(_page, out var request) && ReferenceEquals(request, this);

    internal void Reject()
    {
        if (!OwnsPending) return;
        Pending.Remove(_page);
        MakeConfirmPatch.WaitingPages.Remove(_page);
        if (!_current()) return;
        UIElement.FullScreenMask.Hide();
        MakeGameApi.Refresh(_page, false);
    }

    internal AsyncMethodCallbackDelegate Wrap(AsyncMethodCallbackDelegate callback)
    {
        Pending.Remove(_page);
        Pending.Add(_page, this);
        _page.StartCoroutine(WaitForConditionResponse());
        return (offset, pool) =>
        {
            if (!OwnsPending || !_current() || _conditionHandled) return;
            _conditionHandled = true;
            // Native Action() rereads slots and options after the async check.
            if (!_recipe.IsCurrent(includeSubmissionOptions: true))
            {
                Reject();
                return;
            }
            try
            {
                var canMake = false;
                Serializer.Deserialize(pool, offset, ref canMake);
                callback(offset, pool);
                if (!canMake) Reject();
                // Success stays locked until the native data refresh completes.
            }
            catch
            {
                Reject();
                throw;
            }
        };
    }

    private IEnumerator WaitForConditionResponse()
    {
        var deadline = Time.realtimeSinceStartup + 10f;
        while (_current() && OwnsPending && !_conditionHandled)
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                Debug.LogWarning("[BetterTaiwuScroll] Make condition check timed out; no make submitted.");
                Reject();
                yield break;
            }
            yield return null;
        }
    }
}

[HarmonyPatch(typeof(MakeSubPageMake), "Refresh", new[] { typeof(BuildingMakeDisplayData) })]
internal static class MakeSubmissionRefreshPatch
{
    private static void Prefix(MakeSubPageMake __instance) => MakeSubmissionRequest.Clear(__instance);
}

[HarmonyPatch(typeof(MakeSubPageMake), "OnDisable", new Type[] { })]
internal static class MakePreviewPageDisablePatch
{
    private static void Postfix(MakeSubPageMake __instance)
    {
        MakePreviewRequest.Cancel(__instance);
        MakeSubmissionRequest.Clear(__instance);
        MakeConfirmPatch.WaitingPages.Remove(__instance);
    }
}

[HarmonyPatch(typeof(BuildingDomainMethod.AsyncCall), "CheckMakeCondition",
    new[] { typeof(IAsyncMethodRequestHandler), typeof(MakeConditionArguments), typeof(AsyncMethodCallbackDelegate) })]
internal static class MakeConditionCallbackLifetimePatch
{
    private static void Prefix(ref AsyncMethodCallbackDelegate __2)
    {
        var request = MakePreviewConfirmReadyPatch.CurrentSubmission;
        if (request == null || __2 == null)
            return;
        __2 = request.Wrap(__2);
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
