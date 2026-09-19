using System.Collections.Generic;
using GameData.Domains.Building;

namespace BetterTaiwuScroll.Frontend;

internal static class MakeResultValidation
{
    internal static bool IsUsable(MakeResult result, sbyte itemType, List<short> subTypes, short manualSubType = -1)
    {
        var stage = result.TargetResultStage;
        // default(MakeResultStage).TemplateId is 0, a real item (野果 for food).
        if (!stage.IsInit || !stage.LifeSkillIsMeet || stage.ItemType != itemType
            || subTypes == null || subTypes.Count == 0)
            return false;

        if (stage.TemplateId >= 0)
            return subTypes.Contains(stage.SubTypeId)
                && (manualSubType < 0 || stage.SubTypeId == manualSubType);

        if (stage.TemplateIdList == null || stage.TemplateIdList.Count == 0
            || stage.SubTypeIdList == null || stage.SubTypeIdList.Count != stage.TemplateIdList.Count)
            return false;
        for (var i = 0; i < stage.TemplateIdList.Count; i++)
        {
            if (stage.TemplateIdList[i] < 0 || !subTypes.Contains(stage.SubTypeIdList[i])
                || (manualSubType >= 0 && stage.SubTypeIdList[i] != manualSubType))
                return false;
        }
        return true;
    }
}
