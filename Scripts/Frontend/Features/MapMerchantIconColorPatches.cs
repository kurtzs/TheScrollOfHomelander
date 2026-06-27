#nullable disable

using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(Game.Views.Legacy.WorldMap.MapElementMerchantTypeItem), "Init")]
internal static class MapElementMerchantTypeItemInitPatch
{
    private static void Postfix(Game.Views.Legacy.WorldMap.MapElementMerchantTypeItem __instance)
    {
        MapMerchantIconColorSupport.ApplyOrRestore(__instance);
    }
}

[HarmonyPatch(typeof(MapElementMerchant), "Refresh")]
[HarmonyPatch(new[] { typeof(List<sbyte>), typeof(bool) })]
internal static class MapElementMerchantRefreshIconColorPatch
{
    private static void Postfix(MapElementMerchant __instance)
    {
        MapMerchantIconColorSupport.ApplyOrRestore(__instance);
    }
}

internal static class MapMerchantIconColorSupport
{
    private static readonly FieldInfo ImageIconField = AccessTools.Field(typeof(Game.Views.Legacy.WorldMap.MapElementMerchantTypeItem), "imageIcon");
    private static readonly FieldInfo MerchantTypeItemArrayField = AccessTools.Field(typeof(MapElementMerchant), "merchantTypeItemArray");

    private static readonly Color MerchantColor = new(1f, 213f / 255f, 0f, 1f);
    private static readonly Dictionary<int, ColorRecord> OriginalColors = new();

    internal static void RefreshAllActive()
    {
        foreach (var item in Resources.FindObjectsOfTypeAll<Game.Views.Legacy.WorldMap.MapElementMerchantTypeItem>())
            ApplyOrRestore(item);
    }

    internal static void ApplyOrRestore(MapElementMerchant merchant)
    {
        if (merchant == null || MerchantTypeItemArrayField?.GetValue(merchant) is not Game.Views.Legacy.WorldMap.MapElementMerchantTypeItem[] items)
            return;

        foreach (var item in items)
            ApplyOrRestore(item);
    }

    internal static void RestoreAll()
    {
        foreach (var record in OriginalColors.Values)
        {
            if (record.Icon != null)
            {
                record.Icon.color = record.OriginalColor;
                record.Icon.material = record.OriginalMaterial;
                record.Icon.SetMaterialDirty();
                record.Icon.SetVerticesDirty();
            }

            record.DestroyBrightnessMaterial();
        }

        OriginalColors.Clear();
    }

    internal static void ApplyOrRestore(Game.Views.Legacy.WorldMap.MapElementMerchantTypeItem item)
    {
        if (item == null || ImageIconField?.GetValue(item) is not CImage icon)
            return;

        if (!Plugin.EnableMapTileMerchantIconBrightColor)
        {
            Restore(icon);
            return;
        }

        var id = icon.GetInstanceID();
        if (!OriginalColors.ContainsKey(id))
            OriginalColors[id] = new ColorRecord(icon, icon.color, icon.material);

        icon.color = MerchantColor;
        ApplyBrightnessMaterial(icon, OriginalColors[id]);
    }

    private static void Restore(CImage icon)
    {
        if (icon == null)
            return;

        var id = icon.GetInstanceID();
        if (!OriginalColors.TryGetValue(id, out var record))
            return;

        icon.color = record.OriginalColor;
        icon.material = record.OriginalMaterial;
        icon.SetMaterialDirty();
        icon.SetVerticesDirty();
        record.DestroyBrightnessMaterial();
        OriginalColors.Remove(id);
    }

    private static void ApplyBrightnessMaterial(CImage icon, ColorRecord record)
    {
        var boost = Mathf.Clamp(Plugin.MapTileMerchantIconBrightnessPercent, 100, 400) / 100f;
        if (boost <= 1.01f)
        {
            icon.material = record.OriginalMaterial;
            icon.SetMaterialDirty();
            icon.SetVerticesDirty();
            record.DestroyBrightnessMaterial();
            return;
        }

        var material = record.GetOrCreateBrightnessMaterial();
        if (material == null)
        {
            icon.material = record.OriginalMaterial;
            return;
        }

        if (material.HasProperty(ColorPropertyId))
            material.SetColor(ColorPropertyId, new Color(boost, boost, boost, 1f));

        icon.material = material;
        icon.canvasRenderer.SetColor(new Color(MerchantColor.r * boost, MerchantColor.g * boost, MerchantColor.b * boost, MerchantColor.a));
        icon.SetMaterialDirty();
        icon.SetVerticesDirty();
    }

    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

    private sealed class ColorRecord
    {
        internal ColorRecord(CImage icon, Color originalColor, UnityEngine.Material originalMaterial)
        {
            Icon = icon;
            OriginalColor = originalColor;
            OriginalMaterial = originalMaterial;
        }

        internal CImage Icon { get; }
        internal Color OriginalColor { get; }
        internal UnityEngine.Material OriginalMaterial { get; }
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
                name = "BetterTaiwuScrollMerchantIconBrightness"
            };
            return BrightnessMaterial;
        }

        internal void DestroyBrightnessMaterial()
        {
            if (BrightnessMaterial == null)
                return;

            UnityEngine.Object.Destroy(BrightnessMaterial);
            BrightnessMaterial = null;
        }
    }
}
