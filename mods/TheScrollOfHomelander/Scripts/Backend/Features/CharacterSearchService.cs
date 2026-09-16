#nullable disable

using System;
using System.Collections.Generic;
using Config;
using Config.Common;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Character;
using GameData.Domains.Item;
using GameData.Domains.Map;
using GameData.Utilities;
using BetterTaiwuScroll.Shared;
using DomainCharacter = GameData.Domains.Character.Character;

namespace BetterTaiwuScroll.Backend;

internal static class CharacterSearchService
{
    internal const string MethodName = ModProtocol.SearchCharacters;
    private const int ProtocolVersion = ModProtocol.Version;
    private const int MaxTerms = 64;

    private static string _modId = string.Empty;
    private static bool _enabled = true;
    private static bool _registered;

    internal static void Register(string modId)
    {
        _modId = modId ?? string.Empty;
        LoadSetting();
        if (_registered || string.IsNullOrEmpty(_modId))
            return;

        DomainManager.Mod.AddModMethod(_modId, MethodName, Execute);
        _registered = true;
    }

    internal static void LoadSetting()
    {
        var enabled = true;
        try
        {
            if (!string.IsNullOrEmpty(_modId))
                DomainManager.Mod.GetSetting(_modId, "search_optimization", ref enabled);
        }
        catch
        {
        }
        _enabled = enabled;
    }

    private static SerializableModData Execute(DataContext context, SerializableModData parameter)
    {
        var requestId = ReadInt(parameter, "requestId", -1);
        try
        {
            if (!_enabled)
                return CreateResponse(false, "disabled", "搜索优化已关闭。", requestId, new List<int>());
            if (context == null || parameter == null)
                return CreateResponse(false, "invalid_request", "人物搜索请求无效。", requestId, new List<int>());
            if (ReadInt(parameter, "version", 0) != ProtocolVersion)
                return CreateResponse(false, "unsupported_version", "人物搜索协议版本不匹配。", requestId, new List<int>());

            var scope = ReadInt(parameter, "scope", 0);
            if (scope < 0 || scope > 2)
                return CreateResponse(false, "invalid_scope", "人物搜索范围无效。", requestId, new List<int>());

            var areaId = ReadInt(parameter, "areaId", -1);
            var blockId = ReadInt(parameter, "blockId", -1);
            var termCount = ReadInt(parameter, "termCount", 0);
            if (termCount < 0 || termCount > MaxTerms)
                return CreateResponse(false, "invalid_terms", "人物搜索关键词数量无效。", requestId, new List<int>());

            var terms = new List<string>(termCount);
            for (var i = 0; i < termCount; i++)
            {
                if (!parameter.Get("term" + i, out string term))
                    continue;
                term = (term ?? string.Empty).Trim();
                if (term.Length > 0 && !ContainsTerm(terms, term))
                    terms.Add(term);
            }

            var result = new List<int>();
            var queryCache = new QueryCache();
            foreach (var pair in DomainManager.Character.Characters)
            {
                var character = pair.Value;
                if (character == null || !IsInScope(character, scope, areaId, blockId))
                    continue;
                if (MatchesAllTerms(pair.Key, character, terms, queryCache))
                    result.Add(pair.Key);
            }

            result.Sort();
            return CreateResponse(true, "ok", string.Empty, requestId, result);
        }
        catch (Exception ex)
        {
            AdaptableLog.Warning("[BetterTaiwuScroll] Character search failed: " + ex, false);
            return CreateResponse(false, "exception", ex.GetType().Name + ": " + ex.Message, requestId, new List<int>());
        }
    }

    private static bool IsInScope(DomainCharacter character, int scope, int areaId, int blockId)
    {
        try
        {
            var location = character.GetLocation();
            if (!location.IsValid())
                location = character.GetValidLocation();
            if (!location.IsValid())
                return false;

            if (scope == 2)
                return true;
            if (scope == 1)
                return location.AreaId == areaId;
            return location.AreaId == areaId && location.BlockId == blockId;
        }
        catch
        {
            return false;
        }
    }

    private static bool MatchesAllTerms(
        int charId,
        DomainCharacter character,
        List<string> terms,
        QueryCache cache)
    {
        if (terms.Count == 0)
            return true;

        var characterName = cache.GetCharacterName(charId);
        List<string> itemNames = null;
        List<string> featureNames = null;
        for (var i = 0; i < terms.Count; i++)
        {
            var term = terms[i];
            if (TextContains(characterName, term))
                continue;

            itemNames ??= GetItemNames(character, cache);
            if (ContainsAny(itemNames, term))
                continue;

            featureNames ??= GetFeatureNames(character, cache);
            if (ContainsAny(featureNames, term))
                continue;

            return false;
        }
        return true;
    }

    private static List<string> GetItemNames(DomainCharacter character, QueryCache cache)
    {
        var names = new List<string>();
        try
        {
            var inventory = character.GetInventory();
            if (inventory?.Items != null)
            {
                foreach (var itemKey in inventory.Items.Keys)
                {
                    var name = cache.GetItemName(itemKey);
                    if (!string.IsNullOrEmpty(name))
                        names.Add(name);
                }
            }

            var equipment = character.GetEquipment();
            if (equipment != null)
            {
                for (var i = 0; i < equipment.Length; i++)
                {
                    var name = cache.GetItemName(equipment[i]);
                    if (!string.IsNullOrEmpty(name))
                        names.Add(name);
                }
            }
        }
        catch
        {
        }
        return names;
    }

    private static List<string> GetFeatureNames(DomainCharacter character, QueryCache cache)
    {
        var names = new List<string>();
        var featureIds = character.GetFeatureIds();
        if (featureIds == null)
            return names;

        foreach (var featureId in featureIds)
        {
            if (featureId < 0)
                continue;
            try
            {
                if (character.HideAndDisableFeature(featureId))
                    continue;
                CharacterFeatureItem feature = CharacterFeature.Instance[featureId];
                if (feature == null || feature.Hidden)
                    continue;
                var name = cache.GetFeatureName(featureId, feature);
                if (!string.IsNullOrEmpty(name))
                    names.Add(name);
            }
            catch
            {
            }
        }
        return names;
    }

    private static bool ContainsAny(List<string> values, string term)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (TextContains(values[i], term))
                return true;
        }
        return false;
    }

    private static bool TextContains(string text, string term)
    {
        return !string.IsNullOrEmpty(text)
               && text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ContainsTerm(List<string> terms, string term)
    {
        for (var i = 0; i < terms.Count; i++)
        {
            if (string.Equals(terms[i], term, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private sealed class QueryCache
    {
        private readonly Dictionary<int, string> _characterNames = new Dictionary<int, string>();
        private readonly Dictionary<ItemKey, string> _itemNames = new Dictionary<ItemKey, string>();
        private readonly Dictionary<int, string> _featureNames = new Dictionary<int, string>();

        internal string GetCharacterName(int charId)
        {
            if (_characterNames.TryGetValue(charId, out var name))
                return name;
            try
            {
                name = DomainManager.Character.GetName(charId, false) ?? string.Empty;
            }
            catch
            {
                name = string.Empty;
            }
            _characterNames[charId] = name;
            return name;
        }

        internal string GetItemName(ItemKey itemKey)
        {
            if (_itemNames.TryGetValue(itemKey, out var name))
                return name;
            name = string.Empty;
            if (itemKey.ItemType >= 0 && itemKey.TemplateId >= 0)
            {
                try
                {
                    if (itemKey.IsValid())
                        name = DomainManager.Item.TryGetBaseItem(itemKey)?.GetName();
                    if (string.IsNullOrEmpty(name))
                        name = ItemTemplateHelper.GetName(itemKey.ItemType, itemKey.TemplateId);
                }
                catch
                {
                    name = string.Empty;
                }
            }
            _itemNames[itemKey] = name ?? string.Empty;
            return name ?? string.Empty;
        }

        internal string GetFeatureName(int featureId, CharacterFeatureItem feature)
        {
            if (_featureNames.TryGetValue(featureId, out var name))
                return name;
            name = (feature?.Name ?? string.Empty) + "\n" + (feature?.SmallVillageName ?? string.Empty);
            _featureNames[featureId] = name;
            return name;
        }
    }

    private static int ReadInt(SerializableModData data, string key, int fallback)
    {
        return data != null && data.Get(key, out int value) ? value : fallback;
    }

    private static SerializableModData CreateResponse(
        bool success,
        string code,
        string message,
        int requestId,
        List<int> result)
    {
        var response = new SerializableModData();
        response.Set("success", success);
        response.Set("code", code ?? string.Empty);
        response.Set("message", message ?? string.Empty);
        response.Set("requestId", requestId);
        response.Set("result", new CharacterSearchResultData(result));
        return response;
    }
}
