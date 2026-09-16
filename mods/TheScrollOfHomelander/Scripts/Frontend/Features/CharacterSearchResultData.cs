#nullable disable

using System.Collections.Generic;
using GameData.Serializer;

namespace BetterTaiwuScroll.Shared;

public sealed class CharacterSearchResultData : ISerializableGameData
{
    public List<int> CharacterIds = new List<int>();

    public CharacterSearchResultData()
    {
    }

    public CharacterSearchResultData(List<int> characterIds)
    {
        CharacterIds = characterIds ?? new List<int>();
    }

    public bool IsSerializedSizeFixed() => false;

    public int GetSerializedSize() => 4 + 4 * (CharacterIds?.Count ?? 0);

    public unsafe int Serialize(byte* pData)
    {
        var ids = CharacterIds ?? new List<int>();
        *(int*)pData = ids.Count;
        var values = (int*)(pData + 4);
        for (var i = 0; i < ids.Count; i++)
            values[i] = ids[i];
        return 4 + 4 * ids.Count;
    }

    public unsafe int Deserialize(byte* pData)
    {
        var count = *(int*)pData;
        if (count < 0 || count > 1000000)
            count = 0;

        CharacterIds = new List<int>(count);
        var values = (int*)(pData + 4);
        for (var i = 0; i < count; i++)
            CharacterIds.Add(values[i]);
        return 4 + 4 * count;
    }
}
