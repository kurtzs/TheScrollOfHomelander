#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Config;
using Game.Components.Item;
using Game.Views.Cricket.Combat;
using Game.Views.Migrate;
using Game.Views.MouseTips;
using Game.Views.MouseTips.Item.Common;
using GameData.Domains.Item;
using GameData.Domains.Item.Display;
using HarmonyLib;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

[HarmonyPatch(typeof(Colors), "Awake")]
internal static class ColorsAwakeGradeColorOptimizationPatch
{
    private static void Postfix()
    {
        GradeColorOptimizationSupport.ApplyOrRestore();
    }
}

internal static class GradeColorOptimizationSupport
{
    private static readonly MethodInfo InitPresetColorsMethod =
        AccessTools.Method(typeof(Colors), "InitPresetColors");

    private static readonly Color[] TargetGradeColors =
    {
        FromRgb(0x55, 0x55, 0x55), // 九品
        FromRgb(0xFF, 0xFF, 0xFF), // 八品
        FromRgb(0x80, 0xCC, 0x33), // 七品
        FromRgb(0x39, 0x8F, 0xE6), // 六品
        FromRgb(0x00, 0xFF, 0xFF), // 五品
        FromRgb(0xA6, 0x4C, 0xFF), // 四品
        FromRgb(0xF2, 0xF2, 0x30), // 三品
        FromRgb(0xF2, 0x7F, 0x0C), // 二品
        FromRgb(0xE6, 0x2E, 0x2E), // 一品
    };

    private static Color[] _originalGradeColors;
    private static readonly Dictionary<int, Color> OriginalPresetColors = new();
    private static bool _hasApplied;

    internal static void ApplyOrRestore()
    {
        if (Plugin.EnableCustomGradeColors)
            Apply();
        else
            Restore();
    }

    internal static void Apply()
    {
        var colors = Colors.Instance;
        if (colors == null)
            return;

        CaptureOriginals(colors);
        ApplyGradeColors(colors);
        ApplyPresetColors(colors);
        _hasApplied = true;
    }

    internal static void Restore()
    {
        var colors = Colors.Instance;
        if (colors == null || !_hasApplied)
            return;

        if (_originalGradeColors != null && colors.GradeColors != null)
        {
            var count = Math.Min(_originalGradeColors.Length, colors.GradeColors.Length);
            for (var i = 0; i < count; i++)
                colors.GradeColors[i] = _originalGradeColors[i];
        }

        if (colors.PresetColors != null)
        {
            foreach (var pair in OriginalPresetColors)
            {
                if (pair.Key >= 0 && pair.Key < colors.PresetColors.Count)
                    colors.PresetColors[pair.Key] = pair.Value;
            }

            RebuildPresetColorDictionary(colors);
        }

        _hasApplied = false;
        _originalGradeColors = null;
        OriginalPresetColors.Clear();
    }

    private static void CaptureOriginals(Colors colors)
    {
        if (_originalGradeColors == null && colors.GradeColors != null)
        {
            _originalGradeColors = new Color[colors.GradeColors.Length];
            Array.Copy(colors.GradeColors, _originalGradeColors, colors.GradeColors.Length);
        }

        if (colors.PresetColorNames == null || colors.PresetColors == null)
            return;

        for (var grade = 0; grade < TargetGradeColors.Length; grade++)
        {
            var index = FindPresetColorIndex(colors, grade);
            if (index < 0 || OriginalPresetColors.ContainsKey(index))
                continue;

            OriginalPresetColors[index] = colors.PresetColors[index];
        }
    }

    private static void ApplyGradeColors(Colors colors)
    {
        if (colors.GradeColors == null)
            return;

        var count = Math.Min(TargetGradeColors.Length, colors.GradeColors.Length);
        for (var i = 0; i < count; i++)
            colors.GradeColors[i] = TargetGradeColors[i];
    }

    private static void ApplyPresetColors(Colors colors)
    {
        if (colors.PresetColorNames == null || colors.PresetColors == null)
            return;

        var changed = false;
        for (var grade = 0; grade < TargetGradeColors.Length; grade++)
        {
            var index = FindPresetColorIndex(colors, grade);
            if (index < 0)
                continue;

            colors.PresetColors[index] = TargetGradeColors[grade];
            changed = true;
        }

        if (changed)
            RebuildPresetColorDictionary(colors);
    }

    private static int FindPresetColorIndex(Colors colors, int grade)
    {
        var colorName = "gradecolor_" + grade;
        var count = Math.Min(colors.PresetColorNames.Count, colors.PresetColors.Count);
        for (var i = 0; i < count; i++)
        {
            if (string.Equals(colors.PresetColorNames[i], colorName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static void RebuildPresetColorDictionary(Colors colors)
    {
        InitPresetColorsMethod?.Invoke(colors, null);
    }

    private static Color FromRgb(byte red, byte green, byte blue)
    {
        return new Color(red / 255f, green / 255f, blue / 255f, 1f);
    }

    internal static bool IsValidGrade(sbyte grade)
    {
        var colors = Colors.Instance;
        return colors != null && colors.GradeColors != null && grade >= 0 && grade < colors.GradeColors.Length;
    }

    internal static Color GetGradeColor(sbyte grade)
    {
        return Colors.Instance.GradeColors[grade];
    }
}

internal static class TooltipItemCommonAreaGradeColorSupport
{
    private static readonly FieldInfo ImageGradeBackField =
        AccessTools.Field(typeof(TooltipItemCommonArea), "imageGradeBack");

    internal static void Apply(TooltipItemCommonArea area, sbyte grade)
    {
        if (area == null)
            return;

        var imageGradeBack = ImageGradeBackField?.GetValue(area) as CImage;
        ApplyTooltipGradeBack(imageGradeBack, grade);
    }

    internal static void ApplyTooltipGradeBack(CImage gradeBack, sbyte grade)
    {
        if (gradeBack == null)
            return;

        gradeBack.OnSpriteChange = () => ApplyTooltipGradeBackNow(gradeBack, grade);
        ApplyTooltipGradeBackNow(gradeBack, grade);
    }

    internal static void ApplyTooltipGradeBackNow(CImage gradeBack, sbyte grade)
    {
        if (gradeBack == null)
            return;

        gradeBack.color = Color.white;
        gradeBack.enabled = true;
        gradeBack.canvasRenderer?.SetColor(Color.white);
        if (Plugin.EnableCustomGradeColors)
            TooltipGradeBackgroundSpriteSupport.TryApply(gradeBack, grade);

        gradeBack.SetVerticesDirty();
        gradeBack.SetMaterialDirty();
    }
}

internal static class TooltipGradeBackgroundSpriteSupport
{
    private const string FileNamePrefix = "ui9_mousetip_base_level_";

    private static readonly Dictionary<int, Texture2D> Textures = new();
    private static readonly Dictionary<int, Sprite> Sprites = new();

    internal static bool TryApply(CImage image, sbyte grade)
    {
        if (image == null || string.IsNullOrEmpty(Plugin.ModDirectory))
            return false;

        var level = Mathf.Clamp(grade, 0, 9);
        var sprite = GetOrCreateSprite(level, image.sprite);
        if (sprite == null)
            return false;

        image.sprite = sprite;
        image.enabled = true;
        return true;
    }

    internal static void Clear()
    {
        foreach (var sprite in Sprites.Values)
        {
            if (sprite != null)
                UnityEngine.Object.Destroy(sprite);
        }

        foreach (var texture in Textures.Values)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }

        Sprites.Clear();
        Textures.Clear();
    }

    private static Sprite GetOrCreateSprite(int level, Sprite referenceSprite)
    {
        if (Sprites.TryGetValue(level, out var sprite) && sprite != null)
            return sprite;

        var texture = GetOrCreateTexture(level);
        if (texture == null)
            return null;

        var pivot = new Vector2(0.5f, 0.5f);
        var pixelsPerUnit = 100f;
        var border = Vector4.zero;
        if (referenceSprite != null)
        {
            var rect = referenceSprite.rect;
            if (rect.width > 0f && rect.height > 0f)
                pivot = new Vector2(referenceSprite.pivot.x / rect.width, referenceSprite.pivot.y / rect.height);

            pixelsPerUnit = referenceSprite.pixelsPerUnit;
            border = referenceSprite.border;
        }

        sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            pivot,
            pixelsPerUnit,
            0,
            SpriteMeshType.FullRect,
            border);
        sprite.name = "BetterTaiwuScroll_" + FileNamePrefix + level;
        Sprites[level] = sprite;
        return sprite;
    }

    private static Texture2D GetOrCreateTexture(int level)
    {
        if (Textures.TryGetValue(level, out var texture) && texture != null)
            return texture;

        var path = Path.Combine(
            Plugin.ModDirectory,
            "UserData",
            "GradeBackgrounds",
            FileNamePrefix + level + ".png");
        if (!File.Exists(path))
            return null;

        texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(File.ReadAllBytes(path)))
        {
            UnityEngine.Object.Destroy(texture);
            return null;
        }

        texture.name = "BetterTaiwuScroll_" + FileNamePrefix + level;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Textures[level] = texture;
        return texture;
    }
}

[HarmonyPatch(typeof(CImage), nameof(CImage.SetSprite), new[] { typeof(string), typeof(bool), typeof(Action) })]
internal static class CImageSetSpriteMouseTipGradeColorPatch
{
    private const string MouseTipGradeBackPrefix = "ui9_mousetip_base_level_";

    private static void Postfix(CImage __instance, string spriteName)
    {
        Apply(__instance, spriteName);
    }

    internal static void Apply(CImage image, string spriteName)
    {
        if (image == null || string.IsNullOrEmpty(spriteName))
            return;

        if (!spriteName.StartsWith(MouseTipGradeBackPrefix, StringComparison.Ordinal))
            return;

        var gradeText = spriteName.Substring(MouseTipGradeBackPrefix.Length);
        if (!int.TryParse(gradeText, out var grade) || grade < 0 || grade > sbyte.MaxValue)
            return;

        TooltipItemCommonAreaGradeColorSupport.ApplyTooltipGradeBackNow(image, (sbyte)grade);
    }
}

[HarmonyPatch(typeof(CImage), nameof(CImage.SetSpriteOnly), new[] { typeof(string), typeof(bool), typeof(Action) })]
internal static class CImageSetSpriteOnlyMouseTipGradeColorPatch
{
    private static void Postfix(CImage __instance, string spriteName)
    {
        CImageSetSpriteMouseTipGradeColorPatch.Apply(__instance, spriteName);
    }
}

internal static class TooltipItemBaseGradeColorSupport
{
    private static readonly FieldInfo CommonAreaField =
        AccessTools.Field(typeof(TooltipItemBase), "commonArea");

    private static readonly FieldInfo ItemKeyField =
        AccessTools.Field(typeof(TooltipItemBase), "_itemKey");

    private static readonly FieldInfo ItemDataField =
        AccessTools.Field(typeof(TooltipItemBase), "_itemData");

    private static readonly FieldInfo TemplateDataOnlyField =
        AccessTools.Field(typeof(TooltipItemBase), "_templateDataOnly");

    internal static void Apply(TooltipItemBase tooltip)
    {
        if (tooltip == null)
            return;

        if (!TryGetTitleGrade(tooltip, out var grade))
            return;

        var commonArea = CommonAreaField?.GetValue(tooltip) as TooltipItemCommonArea;
        TooltipItemCommonAreaGradeColorSupport.Apply(commonArea, grade);
    }

    private static bool TryGetTitleGrade(TooltipItemBase tooltip, out sbyte grade)
    {
        grade = -1;

        var templateDataOnly = TemplateDataOnlyField?.GetValue(tooltip) is bool value && value;
        if (!templateDataOnly)
        {
            var itemData = ItemDataField?.GetValue(tooltip) as ItemDisplayData;
            if (itemData != null && itemData.RealKey.IsValid())
                return TryGetTitleGrade(itemData.RealKey, out grade);
        }

        if (ItemKeyField?.GetValue(tooltip) is ItemKey itemKey && itemKey.IsValid())
            return TryGetTitleGrade(itemKey, out grade);

        return false;
    }

    private static bool TryGetTitleGrade(ItemKey itemKey, out sbyte grade)
    {
        grade = ItemTemplateHelper.GetGrade(itemKey.ItemType, itemKey.TemplateId);
        if (itemKey.ItemType == 2)
        {
            var accessoryItem = Accessory.Instance[itemKey.TemplateId];
            if (accessoryItem != null && accessoryItem.MysteryEffectId >= 0)
                grade = 9;
        }

        return grade >= 0;
    }
}

[HarmonyPatch(typeof(TooltipItemBase), nameof(TooltipItemBase.Refresh))]
internal static class TooltipItemBaseRefreshGradeColorPatch
{
    private static void Postfix(TooltipItemBase __instance)
    {
        TooltipItemBaseGradeColorSupport.Apply(__instance);
    }
}

[HarmonyPatch(typeof(TooltipItemCommonArea), "Refresh", new[] { typeof(ItemKey), typeof(bool) })]
internal static class TooltipItemCommonAreaRefreshItemKeyGradeColorPatch
{
    private static void Postfix(TooltipItemCommonArea __instance, ItemKey itemKey)
    {
        var grade = ItemTemplateHelper.GetGrade(itemKey.ItemType, itemKey.TemplateId);
        TooltipItemCommonAreaGradeColorSupport.Apply(__instance, grade);
    }
}

[HarmonyPatch(
    typeof(TooltipItemCommonArea),
    "Refresh",
    new[]
    {
        typeof(ItemDisplayData),
        typeof(string),
        typeof(string),
        typeof(string),
        typeof(sbyte),
        typeof(string),
        typeof(string),
        typeof(string)
    })]
internal static class TooltipItemCommonAreaRefreshCustomGradeColorPatch
{
    private static void Postfix(TooltipItemCommonArea __instance, sbyte grade)
    {
        TooltipItemCommonAreaGradeColorSupport.Apply(__instance, grade);
    }
}

internal static class CombatSkillTooltipGradeColorSupport
{
    private static readonly FieldInfo TooltipCombatSkillGradeBackField =
        AccessTools.Field(typeof(TooltipCombatSkill), "gradeBack");

    private static readonly FieldInfo TooltipCombatSkillConfigDataField =
        AccessTools.Field(typeof(TooltipCombatSkill), "_configData");

    private static readonly FieldInfo SelectSkillInfoGradeBackField =
        AccessTools.Field(typeof(SelectSkillInfo), "gradeBack");

    private static readonly FieldInfo SelectSkillInfoConfigDataField =
        AccessTools.Field(typeof(SelectSkillInfo), "_configData");

    internal static void Apply(TooltipCombatSkill tooltip)
    {
        if (tooltip == null)
            return;

        var configData = TooltipCombatSkillConfigDataField?.GetValue(tooltip) as CombatSkillItem;
        if (configData == null)
            return;

        var gradeBack = TooltipCombatSkillGradeBackField?.GetValue(tooltip) as CImage;
        TooltipItemCommonAreaGradeColorSupport.ApplyTooltipGradeBack(gradeBack, configData.Grade);
    }

    internal static void Apply(SelectSkillInfo info)
    {
        if (info == null)
            return;

        var configData = SelectSkillInfoConfigDataField?.GetValue(info) as CombatSkillItem;
        if (configData == null)
            return;

        var gradeBack = SelectSkillInfoGradeBackField?.GetValue(info) as CImage;
        TooltipItemCommonAreaGradeColorSupport.ApplyTooltipGradeBack(gradeBack, configData.Grade);
    }
}

[HarmonyPatch(typeof(TooltipCombatSkill), "RefreshConfigOnlyInfo")]
internal static class TooltipCombatSkillRefreshConfigOnlyInfoGradeColorPatch
{
    private static void Postfix(TooltipCombatSkill __instance)
    {
        CombatSkillTooltipGradeColorSupport.Apply(__instance);
    }
}

[HarmonyPatch(typeof(SelectSkillInfo), "RefreshConfigOnlyInfo")]
internal static class SelectSkillInfoRefreshConfigOnlyInfoGradeColorPatch
{
    private static void Postfix(SelectSkillInfo __instance)
    {
        CombatSkillTooltipGradeColorSupport.Apply(__instance);
    }
}

internal static class TooltipCricketGradeColorSupport
{
    private static readonly FieldInfo GradeBackImageField =
        AccessTools.Field(typeof(TooltipCricket), "gradeBackImage");

    private static readonly FieldInfo ItemDataField =
        AccessTools.Field(typeof(TooltipCricket), "_itemData");

    internal static void Apply(TooltipCricket tooltip)
    {
        if (tooltip == null)
            return;

        var itemData = ItemDataField?.GetValue(tooltip) as ItemDisplayData;
        if (itemData == null)
            return;

        var gradeBack = GradeBackImageField?.GetValue(tooltip) as CImage;
        var grade = (sbyte)CricketFairCombatHelper.GetCricketGrade(itemData);
        TooltipItemCommonAreaGradeColorSupport.ApplyTooltipGradeBack(gradeBack, grade);
    }
}

[HarmonyPatch(typeof(TooltipCricket), "RefreshConfigOnlyInfo")]
internal static class TooltipCricketRefreshConfigOnlyInfoGradeColorPatch
{
    private static void Postfix(TooltipCricket __instance)
    {
        TooltipCricketGradeColorSupport.Apply(__instance);
    }
}
