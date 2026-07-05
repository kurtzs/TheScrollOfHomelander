#nullable disable

using System.Collections.Generic;
using LangSet = BetterTaiwuScroll.Frontend.ModLocalization.LangSet;

namespace BetterTaiwuScroll.Frontend;

/// <summary>
/// The single source of truth for all of the mod's translations.
///
/// * <see cref="ModText"/>  — text the MOD authors and displays (panel titles, buttons,
///   toggle labels, dropdown options, tooltips). English is the key; each entry lists the
///   other languages. To add a language, pass it to the <c>LangSet</c> (e.g. <c>ko: "..."</c>).
///   Looked up via <c>ModLocalization.T("English text")</c>. Missing translations fall
///   back to the English key.
///
/// * <see cref="GameLabels"/> — text the GAME renders that the mod matches against (sort
///   columns, filter names, archive headers). Keyed by what the game shows in Chinese, with
///   the value being what the same element shows in English (verbatim from the game's own
///   Language_EN files). Used only to recognize game UI in any client language, never shown.
///
/// Nothing else in the codebase should contain a hard-coded, user-visible English string.
/// </summary>
internal static class ModLocalizationCatalog
{
    internal static readonly Dictionary<string, LangSet> ModText = new()
    {
        // ── Continuous-crafting: panel + controls ────────────────────────────────
        ["Continuous Crafting"] = new(cn: "连续制作"),
        ["Continuous Crafting Settings"] = new(cn: "连续制作设置"),
        ["Batch Crafting"] = new(cn: "批量制作"),
        ["Stop Crafting"] = new(cn: "停止制作"),
        ["Settings"] = new(cn: "设置"),
        ["Include Travel Bag"] = new(cn: "是否包括行囊"),
        ["Include Private Storage"] = new(cn: "是否包括私库"),
        ["Include Public Storage"] = new(cn: "是否包括公库"),
        ["Highest Reagent Tier Allowed"] = new(cn: "允许使用的最高引子品级"),
        ["Lowest Reagent Tier Allowed"] = new(cn: "允许使用的最低引子品级"),
        ["Preferred Tool Tier"] = new(cn: "优先使用工具的品级"),
        ["Batch Crafting Mode"] = new(cn: "批量制作模式"),
        ["Allow Bare-Hand Crafting"] = new(cn: "是否允许徒手制作"),
        ["Enable Durability Protection"] = new(cn: "是否开启耐久保护"),
        ["Batch Crafting Speed"] = new(cn: "批量制作速度"),
        ["High Tier"] = new(cn: "高品"),
        ["Low Tier"] = new(cn: "低品"),
        ["Checkbox Mode"] = new(cn: "勾选模式"),
        ["Button Mode"] = new(cn: "按钮模式"),

        // Continuous-crafting tooltips (title + description shown on hover)
        ["When checked, crafting continues to the next batch per your settings once complete."] =
            new(cn: "勾选后，制作完成时将按照设置继续进行下一次制作。"),
        ["Open the continuous-crafting settings panel."] = new(cn: "打开连续制作的设置界面。"),
        ["Batch-craft from the current recipe using your continuous-crafting settings."] =
            new(cn: "按照连续制作设置从当前制作开始批量制作。"),

        // ── Bulk purchase: panel + controls ──────────────────────────────────────
        ["Bulk Purchase"] = new(cn: "批量采购"),
        ["Bulk Purchase Settings"] = new(cn: "批量采购设置"),
        ["Lowest Purchase Tier"] = new(cn: "采购的最低品级"),
        ["Highest Purchase Tier"] = new(cn: "采购的最高品级"),
        ["Skip Price-Increased Items"] = new(cn: "不采购涨价的物品"),
        ["Skip Original-Price Items"] = new(cn: "不采购原价的物品"),
        ["Buy from Locked Pure-Essence Shops"] = new(cn: "未解锁精纯商店也采购"),
        ["Bulk-Buy Medicine Reagents"] = new(cn: "批量采购购买药材引子"),
        ["Bulk-Buy Poison Reagents"] = new(cn: "批量采购购买毒物引子"),
        ["Open the bulk purchase settings."] = new(cn: "打开批量采购设置。"),
        ["Add matching goods to the buy list per your settings."] = new(cn: "按照设置把符合条件的商品加入买入列表。"),

        // ── Exchange filter sync ─────────────────────────────────────────────────
        ["Sync"] = new(cn: "同步"),
        ["Sync Both Sides"] = new(cn: "左右同步"),
        ["When enabled, item-category filters stay in sync on both sides."] =
            new(cn: "开启此功能时，物品大分类的筛选会同步到两边"),

        // ── Search boxes ─────────────────────────────────────────────────────────
        ["Enter keyword"] = new(cn: "输入关键字"),

        // ── Make: extra "Food" target category the mod adds ──────────────────────
        ["Food"] = new(cn: "食物"),

        // ── Grade names (crafting/purchase dropdown options), Tier 1 (best) .. 9 ──
        // Two Chinese spellings exist: dotted (continuous-crafting panel) and plain
        // (bulk-purchase panel). Both map to the same English tier here.
        ["Tier 1"] = new(cn: "神·一品"),
        ["Tier 2"] = new(cn: "绝·二品"),
        ["Tier 3"] = new(cn: "超·三品"),
        ["Tier 4"] = new(cn: "极·四品"),
        ["Tier 5"] = new(cn: "秘·五品"),
        ["Tier 6"] = new(cn: "奇·六品"),
        ["Tier 7"] = new(cn: "上·七品"),
        ["Tier 8"] = new(cn: "中·八品"),
        ["Tier 9"] = new(cn: "下·九品"),
    };

    /// <summary>
    /// Plain (dot-less) Chinese grade spelling used by the bulk-purchase panel, indexed
    /// Tier 1 (best) .. Tier 9. The continuous-crafting panel uses the dotted spelling in
    /// <see cref="ModText"/>. Both surface as "Tier N" in English.
    /// </summary>
    internal static readonly string[] PlainGradeChinese =
    {
        "神一品", "绝二品", "超三品", "极四品", "秘五品", "妙六品", "上七品", "中八品", "下九品",
    };

    internal static readonly Dictionary<string, string> GameLabels = new()
    {
        // ── Item sort-column labels (game's own wording, from SortItem_language) ──
        ["名称"] = "Name",
        ["品阶"] = "Tier",
        ["数量"] = "Quantity",
        ["类型"] = "Type",
        ["重量"] = "Weight",
        ["耐久"] = "Durability",
        ["效率"] = "Efficiency",
        ["好感"] = "Favorability",
        ["功法造诣"] = "Martial Art Attainment",
        ["价值"] = "Value",
        ["价格"] = "Price",
        ["造诣"] = "Attainment",
        ["工具效果"] = "Tool Effects",
        ["效果"] = "Effects",
        ["年龄"] = "Age",
        ["心情"] = "Mood",
        ["健康"] = "Health",
        ["状态"] = "State",
        ["属性"] = "Attribute",
        ["命中"] = "Hit",
        ["技艺"] = "Fine Arts",
        ["武学"] = "Martial Arts",
        ["赋性"] = "Disposition",
        ["持有"] = "Inventory",
        ["指令"] = "Command",
        ["培养次数"] = "Training Count",
        ["培养"] = "Training",

        // ── Team/character sort-column labels ────────────────────────────────────
        ["行为"] = "Behavior",
        ["身份"] = "Rank",
        ["伤势"] = "Injuries",
        ["内息紊乱"] = "Inner Breath Chaos",
        ["魅力"] = "Charm",
        ["立场"] = "Mindset",
        ["戒心"] = "Wariness",
        ["轮回"] = "Reincarnations",
        ["名誉"] = "Reputation",
        ["膂力"] = "Strength",
        ["体质"] = "Constitution",
        ["灵敏"] = "Agility",
        ["根骨"] = "Root Bone",
        ["悟性"] = "Comprehension",
        ["定力"] = "Willpower",
        ["音律"] = "Music",
        ["弈棋"] = "Weiqi",
        ["诗书"] = "Literature",
        ["绘画"] = "Painting",
        ["术数"] = "Astrology",
        ["品鉴"] = "Appreciation",
        ["锻造"] = "Smithing",
        ["制木"] = "Carpentry",
        ["医术"] = "Medical Art",
        ["毒术"] = "Toxicology",
        ["织锦"] = "Weaving",
        ["巧匠"] = "Jewelcrafting",
        ["道法"] = "Taoism",
        ["佛学"] = "Buddhism",
        ["厨艺"] = "Culinary Arts",
        ["杂学"] = "Unorthodox Arts",
        ["合道"] = "Harmony",
        ["成长"] = "Growth",
        ["食材"] = "Foodstuff",
        ["木材"] = "Wood",
        ["金铁"] = "Metal",
        ["玉石"] = "Jade",
        ["织物"] = "Fabric",
        ["药材"] = "Herbs",
        ["银钱"] = "Silver",
        ["威望"] = "Prestige",
        ["负重"] = "Weight",
        ["行囊"] = "Travel Bag",
        ["内功"] = "Qi Arts",
        ["身法"] = "Footwork Arts",
        ["绝技"] = "Unique Arts",
        ["拳掌"] = "Fist Arts",
        ["指法"] = "Finger Arts",
        ["腿法"] = "Kicking Arts",
        ["暗器"] = "Concealed Weapons",
        ["剑法"] = "Sword Arts",
        ["刀法"] = "Blade Arts",
        ["长兵"] = "Polearm Arts",
        ["奇门"] = "Exotic Weapons",
        ["软兵"] = "Whip Arts",
        ["御射"] = "Ranged Weapons",
        ["乐器"] = "Instrument Arts",

        // ── Item-category filter names (game rendered) ───────────────────────────
        ["功法书"] = "Martial Arts Book",
        ["技艺书"] = "Fine Arts Book",
        ["西域珍宝"] = "Western Regions Treasure",

        // ── Revert-archive header row (hidden by the cloned settings panels) ──────
        ["头像"] = "Avatar",
        ["名字"] = "Name",
        ["第几世"] = "Generation Number",
        ["第几个"] = "Generation Number",
        ["存档时间"] = "Save Time",
        ["所在地点"] = "Location",
    };
}
