#nullable disable
using System;
using System.Collections.Generic;
using System.Reflection;
using GameData.Domains.Building;
using GameData.Domains.Character;
using GameData.Domains.Item.Display;
using HarmonyLib;

namespace BetterTaiwuScroll.Frontend;

// A preview/condition response is usable only for the state that requested it.
internal sealed class MakeRecipeSnapshot
{
    private static readonly FieldInfo Material = AccessTools.Field(typeof(MakeSubPageMake), "materialSlot");
    private static readonly FieldInfo Target = AccessTools.Field(typeof(MakeSubPageMake), "targetSlot");
    private static readonly FieldInfo Tool = AccessTools.Field(typeof(MakeSubPageMake), "toolSlot");
    private static readonly FieldInfo Type = AccessTools.Field(typeof(MakeSubPageMake), "_makeItemTypeId");
    private static readonly FieldInfo SubType = AccessTools.Field(typeof(MakeSubPageMake), "_makeItemSubTypeId");
    private static readonly FieldInfo SubTypes = AccessTools.Field(typeof(MakeSubPageMake), "_makeItemSubtypeIdList");
    private static readonly FieldInfo Manual = AccessTools.Field(typeof(MakeSubPageMake), "_isManual");
    private static readonly FieldInfo Display = AccessTools.Field(typeof(MakeSubPageMake), "DisplayData");
    private static readonly FieldInfo Count = AccessTools.Field(typeof(MakeSubPageMake), "_makeCount");
    private static readonly FieldInfo Resources = AccessTools.Field(typeof(MakeSubPageMake), "_curMakeResourceCountInts");

    private readonly MakeSubPageMake _page;
    private readonly Func<bool> _current;
    private readonly ItemDisplayData _material;
    private readonly ItemDisplayData _target;
    private readonly ItemDisplayData _tool;
    private readonly int _amount;
    private readonly short _durability;
    private readonly short _type;
    private readonly short _subType;
    private readonly List<short> _subTypes;
    private readonly bool _manual;
    private readonly bool _perfect;
    private readonly object _display;
    private readonly BuildingBlockKey _building;
    private readonly int _count;
    private readonly ResourceInts _resources;
    private readonly int _perfectSelection;

    internal MakeRecipeSnapshot(MakeSubPageMake page)
    {
        _page = page;
        _current = MakeExecutionLifetime.Capture(page);
        _material = Read(Material);
        _target = Read(Target);
        _tool = Read(Tool);
        _amount = _material?.Amount ?? 0;
        _durability = _tool?.Durability ?? 0;
        _type = (short)Type.GetValue(page);
        _subType = (short)SubType.GetValue(page);
        var list = SubTypes.GetValue(page) as List<short>;
        _subTypes = list == null ? null : new List<short>(list);
        _manual = (bool)Manual.GetValue(page);
        _perfect = MakeGameApi.GetIsPerfect(page);
        _display = Display.GetValue(page);
        _building = MakeSelectMaterialPatch.GetParentView(page).BuildingBlockKey;
        _count = Convert.ToInt32(Count.GetValue(page));
        _resources = (ResourceInts)Resources.GetValue(page);
        _perfectSelection = (MakeGameApi.PerfectDropdown.GetValue(page) as CDropdown)?.value ?? -1;
    }

    private ItemDisplayData Read(FieldInfo field) => (field.GetValue(_page) as MakeTargetSlot)?.ItemData;

    internal bool IsCurrent(bool includeSubmissionOptions = false)
    {
        if (!_current() || !ReferenceEquals(_material, Read(Material))
            || !ReferenceEquals(_target, Read(Target)) || !ReferenceEquals(_tool, Read(Tool))
            || (_material?.Amount ?? 0) != _amount || (_tool?.Durability ?? 0) != _durability
            || (short)Type.GetValue(_page) != _type
            || ((_manual || includeSubmissionOptions) && (short)SubType.GetValue(_page) != _subType)
            || (bool)Manual.GetValue(_page) != _manual || MakeGameApi.GetIsPerfect(_page) != _perfect
            || !ReferenceEquals(Display.GetValue(_page), _display))
            return false;

        var view = MakeSelectMaterialPatch.GetParentView(_page);
        if (view == null || !view.BuildingBlockKey.Equals(_building))
            return false;
        var list = SubTypes.GetValue(_page) as List<short>;
        if ((_subTypes == null) != (list == null) || (_subTypes?.Count ?? 0) != (list?.Count ?? 0))
            return false;
        if (list != null)
            for (var i = 0; i < list.Count; i++)
                if (list[i] != _subTypes[i]) return false;

        if (!includeSubmissionOptions)
            return true;
        if (Convert.ToInt32(Count.GetValue(_page)) != _count
            || ((MakeGameApi.PerfectDropdown.GetValue(_page) as CDropdown)?.value ?? -1) != _perfectSelection)
            return false;
        var resources = (ResourceInts)Resources.GetValue(_page);
        for (var i = 0; i < 6; i++)
            if (resources.Get(i) != _resources.Get(i)) return false;
        return true;
    }
}
