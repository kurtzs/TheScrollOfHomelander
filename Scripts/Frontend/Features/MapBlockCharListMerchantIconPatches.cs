#nullable disable

using System.Collections.Generic;
using System.Reflection;
using Game.Views.MapBlockCharList;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(MapBlockCharStat), "SetStatus")]
internal static class MapBlockCharStatSetStatusMerchantIconPatch
{
    private static void Postfix(MapBlockCharStat __instance)
    {
        MapBlockCharListMerchantIconSupport.ApplyOrRestoreMerchantIcon(__instance);
    }
}

[HarmonyPatch(typeof(MapBlockCharStatBase), "SetStatus")]
internal static class MapBlockCharStatBaseSetStatusMerchantIconPatch
{
    private static void Postfix(MapBlockCharStatBase __instance)
    {
        MapBlockCharListMerchantIconSupport.ApplyOrRestoreMerchantIcon(__instance);
    }
}

[HarmonyPatch(typeof(MapBlockCharStat), "SetMerchant")]
internal static class MapBlockCharStatSetMerchantBaseIconPatch
{
    private static void Postfix(MapBlockCharStat __instance)
    {
        MapBlockCharListMerchantIconSupport.ApplyOrRestoreBaseMerchantIcon(__instance);
    }
}

[HarmonyPatch(typeof(MapBlockCharStatBase), "SetMerchant")]
internal static class MapBlockCharStatBaseSetMerchantBaseIconPatch
{
    private static void Postfix(MapBlockCharStatBase __instance)
    {
        MapBlockCharListMerchantIconSupport.ApplyOrRestoreBaseMerchantIcon(__instance);
    }
}

[HarmonyPatch(typeof(MapBlockCharStat), "SetNormal")]
internal static class MapBlockCharStatSetNormalBaseIconPatch
{
    private static void Postfix(MapBlockCharStat __instance)
    {
        MapBlockCharListMerchantIconSupport.RestoreBaseIcon(__instance);
    }
}

[HarmonyPatch(typeof(MapBlockCharStatBase), "SetNormal")]
internal static class MapBlockCharStatBaseSetNormalBaseIconPatch
{
    private static void Postfix(MapBlockCharStatBase __instance)
    {
        MapBlockCharListMerchantIconSupport.RestoreBaseIcon(__instance);
    }
}

[HarmonyPatch(typeof(MapBlockCharStat), "SetApproveTaiwu")]
internal static class MapBlockCharStatSetApproveTaiwuBaseIconPatch
{
    private static void Postfix(MapBlockCharStat __instance)
    {
        MapBlockCharListMerchantIconSupport.RestoreBaseIcon(__instance);
    }
}

[HarmonyPatch(typeof(MapBlockCharStatBase), "SetApproveTaiwu")]
internal static class MapBlockCharStatBaseSetApproveTaiwuBaseIconPatch
{
    private static void Postfix(MapBlockCharStatBase __instance)
    {
        MapBlockCharListMerchantIconSupport.RestoreBaseIcon(__instance);
    }
}

[HarmonyPatch(typeof(MapBlockCharStat), "SetGrave")]
internal static class MapBlockCharStatSetGraveBaseIconPatch
{
    private static void Postfix(MapBlockCharStat __instance)
    {
        MapBlockCharListMerchantIconSupport.RestoreBaseIcon(__instance);
    }
}

[HarmonyPatch(typeof(MapBlockCharStatBase), "SetGrave")]
internal static class MapBlockCharStatBaseSetGraveBaseIconPatch
{
    private static void Postfix(MapBlockCharStatBase __instance)
    {
        MapBlockCharListMerchantIconSupport.RestoreBaseIcon(__instance);
    }
}

[HarmonyPatch(typeof(MapBlockCharStat), "SetEnemy")]
internal static class MapBlockCharStatSetEnemyBaseIconPatch
{
    private static void Postfix(MapBlockCharStat __instance)
    {
        MapBlockCharListMerchantIconSupport.RestoreBaseIcon(__instance);
    }
}

[HarmonyPatch(typeof(MapBlockCharStatBase), "SetEnemy")]
internal static class MapBlockCharStatBaseSetEnemyBaseIconPatch
{
    private static void Postfix(MapBlockCharStatBase __instance)
    {
        MapBlockCharListMerchantIconSupport.RestoreBaseIcon(__instance);
    }
}

internal static class MapBlockCharListMerchantIconSupport
{
    private static readonly FieldInfo StatMerchantField = AccessTools.Field(typeof(MapBlockCharStat), "merchant");
    private static readonly FieldInfo StatBaseImageField = AccessTools.Field(typeof(MapBlockCharStat), "baseImage");
    private static readonly FieldInfo StatBaseValueTransformField = AccessTools.Field(typeof(MapBlockCharStat), "baseValueTransform");
    private static readonly FieldInfo StatBaseMerchantField = AccessTools.Field(typeof(MapBlockCharStatBase), "merchant");
    private static readonly FieldInfo StatBaseBaseImageField = AccessTools.Field(typeof(MapBlockCharStatBase), "baseImage");
    private static readonly FieldInfo StatBaseBaseValueTransformField = AccessTools.Field(typeof(MapBlockCharStatBase), "baseValueTransform");

    private static readonly Color MerchantColor = new(1f, 213f / 255f, 0f, 1f);
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly Dictionary<int, IconRecord> Records = new();

    internal static void RefreshAllActive()
    {
        if (!ShouldCustomize())
        {
            RestoreAll();
            return;
        }

        foreach (var stat in Resources.FindObjectsOfTypeAll<MapBlockCharStat>())
            ApplyOrRestoreMerchantIcon(stat);

        foreach (var stat in Resources.FindObjectsOfTypeAll<MapBlockCharStatBase>())
            ApplyOrRestoreMerchantIcon(stat);

        foreach (var record in new List<IconRecord>(Records.Values))
        {
            if (record.Icon == null)
                Records.Remove(record.InstanceId);
            else
                Apply(record.Icon);
        }
    }

    internal static void RestoreAll()
    {
        foreach (var record in Records.Values)
            record.Restore();

        Records.Clear();
    }

    internal static void ApplyOrRestoreMerchantIcon(MapBlockCharStat instance)
    {
        ApplyOrRestoreIcon(GetIcon(StatMerchantField, instance), onlyWhenActive: true);
    }

    internal static void ApplyOrRestoreMerchantIcon(MapBlockCharStatBase instance)
    {
        ApplyOrRestoreIcon(GetIcon(StatBaseMerchantField, instance), onlyWhenActive: true);
    }

    internal static void ApplyOrRestoreBaseMerchantIcon(MapBlockCharStat instance)
    {
        var icon = GetIcon(StatBaseImageField, instance);
        if (!IsBaseValueActive(StatBaseValueTransformField, instance))
            Restore(icon);
        else
            ApplyOrRestoreIcon(icon, onlyWhenActive: false);
    }

    internal static void ApplyOrRestoreBaseMerchantIcon(MapBlockCharStatBase instance)
    {
        var icon = GetIcon(StatBaseBaseImageField, instance);
        if (!IsBaseValueActive(StatBaseBaseValueTransformField, instance))
            Restore(icon);
        else
            ApplyOrRestoreIcon(icon, onlyWhenActive: false);
    }

    internal static void RestoreBaseIcon(MapBlockCharStat instance)
    {
        Restore(GetIcon(StatBaseImageField, instance));
    }

    internal static void RestoreBaseIcon(MapBlockCharStatBase instance)
    {
        Restore(GetIcon(StatBaseBaseImageField, instance));
    }

    private static CImage GetIcon(FieldInfo field, object instance)
    {
        return instance == null ? null : field?.GetValue(instance) as CImage;
    }

    private static bool IsBaseValueActive(FieldInfo field, object instance)
    {
        return instance != null
            && field?.GetValue(instance) is RectTransform transform
            && transform.gameObject.activeSelf;
    }

    private static void ApplyOrRestoreIcon(CImage icon, bool onlyWhenActive)
    {
        if (icon == null)
            return;

        if (!ShouldCustomize() || onlyWhenActive && !icon.gameObject.activeSelf)
        {
            Restore(icon);
            return;
        }

        Apply(icon);
    }

    private static bool ShouldCustomize()
    {
        return Plugin.EnableMapListMerchantIconBrightColor
            || Mathf.Clamp(Plugin.MapListMerchantIconScalePercent, 50, 200) != 100;
    }

    private static void Apply(CImage icon)
    {
        if (icon == null)
            return;

        var id = icon.GetInstanceID();
        if (!Records.TryGetValue(id, out var record) || record.Icon == null)
        {
            record = new IconRecord(icon);
            Records[id] = record;
        }

        ApplyColor(icon, record);
        ApplyScale(icon, record);
    }

    private static void ApplyColor(CImage icon, IconRecord record)
    {
        if (!Plugin.EnableMapListMerchantIconBrightColor)
        {
            icon.color = record.OriginalColor;
            icon.canvasRenderer.SetColor(record.OriginalColor);
            icon.material = record.OriginalMaterial;
            icon.SetMaterialDirty();
            icon.SetVerticesDirty();
            record.DestroyBrightnessMaterial();
            return;
        }

        var boost = Mathf.Clamp(Plugin.MapListMerchantIconBrightnessPercent, 100, 400) / 100f;
        icon.color = MerchantColor;

        if (boost <= 1.01f)
        {
            icon.material = record.OriginalMaterial;
            icon.canvasRenderer.SetColor(MerchantColor);
            record.DestroyBrightnessMaterial();
        }
        else
        {
            var material = record.GetOrCreateBrightnessMaterial();
            if (material != null)
            {
                if (material.HasProperty(ColorPropertyId))
                    material.SetColor(ColorPropertyId, new Color(boost, boost, boost, 1f));

                icon.material = material;
            }

            icon.canvasRenderer.SetColor(new Color(MerchantColor.r * boost, MerchantColor.g * boost, MerchantColor.b * boost, MerchantColor.a));
        }

        icon.SetMaterialDirty();
        icon.SetVerticesDirty();
    }

    private static void ApplyScale(CImage icon, IconRecord record)
    {
        var scalePercent = Mathf.Clamp(Plugin.MapListMerchantIconScalePercent, 50, 200);
        var factor = scalePercent / 100f;
        var baseScale = record.OriginalScale;
        icon.transform.localScale = new Vector3(baseScale.x * factor, baseScale.y * factor, baseScale.z);
        ApplyScalePositionOffset(icon, record, factor);
    }

    private static void ApplyScalePositionOffset(CImage icon, IconRecord record, float factor)
    {
        var offsetX = record.OriginalWidth * Mathf.Max(0f, factor - 1f) * 0.5f;
        if (icon.transform is RectTransform rectTransform)
            rectTransform.anchoredPosition = record.OriginalAnchoredPosition + new Vector2(offsetX, 0f);
        else
            icon.transform.localPosition = record.OriginalLocalPosition + new Vector3(offsetX, 0f, 0f);
    }

    private static void Restore(CImage icon)
    {
        if (icon == null)
            return;

        var id = icon.GetInstanceID();
        if (!Records.TryGetValue(id, out var record))
            return;

        record.Restore();
        Records.Remove(id);
    }

    private sealed class IconRecord
    {
        internal IconRecord(CImage icon)
        {
            Icon = icon;
            InstanceId = icon.GetInstanceID();
            OriginalColor = icon.color;
            OriginalMaterial = icon.material;
            OriginalScale = icon.transform.localScale;
            OriginalLocalPosition = icon.transform.localPosition;
            if (icon.transform is RectTransform rectTransform)
            {
                OriginalAnchoredPosition = rectTransform.anchoredPosition;
                OriginalWidth = GetRectWidth(rectTransform);
            }
            else
            {
                OriginalWidth = 0f;
            }
        }

        internal CImage Icon { get; }
        internal int InstanceId { get; }
        internal Color OriginalColor { get; }
        internal UnityEngine.Material OriginalMaterial { get; }
        internal Vector3 OriginalScale { get; }
        internal Vector3 OriginalLocalPosition { get; }
        internal Vector2 OriginalAnchoredPosition { get; }
        internal float OriginalWidth { get; }
        private UnityEngine.Material BrightnessMaterial { get; set; }

        internal UnityEngine.Material GetOrCreateBrightnessMaterial()
        {
            if (BrightnessMaterial != null)
                return BrightnessMaterial;

            var sourceMaterial = OriginalMaterial != null ? OriginalMaterial : Graphic.defaultGraphicMaterial;
            if (sourceMaterial == null)
                return null;

            BrightnessMaterial = new UnityEngine.Material(sourceMaterial)
            {
                hideFlags = HideFlags.DontSave,
                name = "BetterTaiwuScrollMapListMerchantIconBrightness"
            };
            return BrightnessMaterial;
        }

        internal void Restore()
        {
            if (Icon != null)
            {
                Icon.color = OriginalColor;
                Icon.canvasRenderer.SetColor(OriginalColor);
                Icon.material = OriginalMaterial;
                Icon.transform.localScale = OriginalScale;
                Icon.transform.localPosition = OriginalLocalPosition;
                if (Icon.transform is RectTransform rectTransform)
                    rectTransform.anchoredPosition = OriginalAnchoredPosition;
                Icon.SetMaterialDirty();
                Icon.SetVerticesDirty();
            }

            DestroyBrightnessMaterial();
        }

        internal void DestroyBrightnessMaterial()
        {
            if (BrightnessMaterial == null)
                return;

            UnityEngine.Object.Destroy(BrightnessMaterial);
            BrightnessMaterial = null;
        }

        private static float GetRectWidth(RectTransform rectTransform)
        {
            var width = rectTransform.rect.width;
            if (width > 0.01f)
                return width;

            width = rectTransform.sizeDelta.x;
            return width > 0.01f ? width : 40f;
        }
    }
}
