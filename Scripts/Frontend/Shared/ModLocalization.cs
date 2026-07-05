#nullable disable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

/// <summary>
/// Runtime language detection and lookup for the mod's own UI text.
///
/// The Scroll of Taiwu persists the active language as a Unity <see cref="PlayerPrefs"/>
/// string keyed "Language", whose value matches the game's StreamingAssets language
/// folders: "CN" (Simplified), "CNH" (Traditional), "EN" (English), "KO" (Korean).
/// We read that once at startup (and again when settings change) so the mod shows the
/// player's own language without any per-language build.
///
/// English is the source language: code calls <c>ModLocalization.T("Some English text")</c>
/// and the catalog (<see cref="ModLocalizationCatalog"/>) supplies the other languages.
/// A missing translation falls back to the English key, so nothing ever disappears.
/// To fix or add wording — including new languages — edit the catalog only.
/// </summary>
internal static class ModLocalization
{
    internal enum Lang
    {
        Cn,
        Cnh,
        En,
        Ko,
    }

    /// <summary>Per-language translations of one English source string.</summary>
    internal readonly struct LangSet
    {
        internal readonly string Cn;
        internal readonly string Cnh;
        internal readonly string Ko;

        internal LangSet(string cn = null, string cnh = null, string ko = null)
        {
            Cn = cn;
            Cnh = cnh;
            Ko = ko;
        }

        internal string For(Lang lang)
        {
            switch (lang)
            {
                case Lang.Cn:
                    return Cn;
                case Lang.Cnh:
                    // Traditional Chinese falls back to Simplified when not provided.
                    return string.IsNullOrEmpty(Cnh) ? Cn : Cnh;
                case Lang.Ko:
                    return Ko;
                default:
                    return null;
            }
        }
    }

    private const string LanguagePlayerPrefsKey = "Language";

    internal static Lang Current { get; private set; } = Lang.Cn;

    internal static bool IsEnglish => Current == Lang.En;

    internal static void Refresh()
    {
        string raw = null;
        try
        {
            raw = PlayerPrefs.GetString(LanguagePlayerPrefsKey, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to read game language, defaulting to Chinese: " + ex);
        }

        Current = Normalize(raw);
    }

    private static Lang Normalize(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return Lang.Cn;

        switch (raw.Trim().ToUpperInvariant())
        {
            case "EN":
                return Lang.En;
            case "KO":
            case "KR":
                return Lang.Ko;
            case "CNH":
            case "TC":
                return Lang.Cnh;
            default:
                // "CN"/"SC" and any unknown value are treated as Simplified Chinese.
                return Lang.Cn;
        }
    }

    /// <summary>
    /// Translates a mod-authored English string to the player's language. On an English
    /// client returns the English source unchanged; otherwise returns the catalog entry
    /// for the current language, falling back to the English source when absent.
    /// </summary>
    internal static string T(string en)
    {
        if (Current == Lang.En || string.IsNullOrEmpty(en))
            return en;

        if (ModLocalizationCatalog.ModText.TryGetValue(en, out var set))
        {
            var translated = set.For(Current);
            if (!string.IsNullOrEmpty(translated))
                return translated;
        }

        return en;
    }

    /// <summary>
    /// The English text the game itself renders for a Chinese UI label the mod matches
    /// against (sort columns, filter names, archive headers). Returns null if unknown.
    /// Used to make label-matching work regardless of the client's language.
    /// </summary>
    internal static string GameEnglish(string chinese)
    {
        return ModLocalizationCatalog.GameLabels.TryGetValue(chinese, out var en) ? en : null;
    }

    /// <summary>
    /// Builds a set containing each Chinese label plus the English text the game renders
    /// for it, so recognition works on both Chinese and English clients.
    /// </summary>
    internal static HashSet<string> BuildBilingualLabelSet(IEnumerable<string> chineseLabels)
    {
        var set = new HashSet<string>();
        foreach (var cn in chineseLabels)
        {
            if (string.IsNullOrEmpty(cn))
                continue;

            set.Add(cn);
            var en = GameEnglish(cn);
            if (!string.IsNullOrEmpty(en))
                set.Add(en);
        }

        return set;
    }
}
