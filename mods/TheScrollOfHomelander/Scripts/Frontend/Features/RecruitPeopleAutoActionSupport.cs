#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Config;
using FrameWork;
using Game.Views.Building;
using GameData.Domains.Building;
using GameData.Domains.Extra;
using GameData.GameDataBridge;
using GameData.Serializer;
using GameData.Utilities;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

internal static class RecruitPeopleAutoActionSupport
{
    private static bool _running;
    private static int _run;
    private static int _step;
    private static readonly Queue<RecruitOperation> Operations = new();

    internal static void Reset()
    {
        _run++;
        _step++;
        _running = false;
        Operations.Clear();
    }

    private static bool Enabled => Plugin.EnableAutoHarvestAfterAdvanceMonth
        && Plugin.EnableAutoHarvestAfterAdvanceMonthPeople && Plugin.EnableAutoRecruitPeople;

    internal static void StartBackgroundAutoProcess()
    {
        if (_running || !Enabled || !ModSession.Active) return;
        _running = true;
        var run = ++_run;
        var session = ModSession.Generation;
        ArmTimeout(run, session, ++_step);
        try
        {
            ExtraDomainMethod.AsyncCall.RequestAllRecruitCharacterData(null, BuildingBlockKey.Invalid, (offset, pool) =>
            {
                if (!IsCurrent(run, session)) return;
                try
                {
                    var data = new List<BuildingRecruitData>();
                    Serializer.Deserialize(pool, offset, ref data);
                    foreach (var operation in BuildOperations(data)) Operations.Enqueue(operation);
                    Next(run, session);
                }
                catch (Exception ex) { Fail(ex); }
            });
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static bool IsCurrent(int run, int session) =>
        _running && run == _run && ModSession.IsCurrent(session) && Enabled;

    private static void Next(int run, int session)
    {
        if (!IsCurrent(run, session)) return;
        _step++;
        if (Operations.Count == 0)
        {
            Reset();
            RefreshQuickButtons();
            return;
        }
        var operation = Operations.Dequeue();
        var data = operation.Data;
        var step = _step;
        ArmTimeout(run, session, step);
        try
        {
            if (!operation.Accept)
            {
                BuildingDomainMethod.Call.RejectBuildingBlockRecruitPeople(data.BuildingBlockKey, data.RecruitInfoIndex);
                ModSession.Delay(0, () => Next(run, session));
                return;
            }
            BuildingDomainMethod.AsyncCall.AcceptBuildingBlockRecruitPeople(null,
                data.BuildingBlockKey, data.RecruitInfoIndex, (offset, pool) =>
                {
                    if (!IsCurrent(run, session) || step != _step) return;
                    try
                    {
                        int id = -1;
                        Serializer.Deserialize(pool, offset, ref id);
                        if (id < 0) { Fail(new InvalidOperationException("Recruitment was rejected; refresh before continuing.")); return; }
                        ModSession.Delay(0, () => Next(run, session));
                    }
                    catch (Exception ex) { Fail(ex); }
                });
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void ArmTimeout(int run, int session, int step)
    {
        ModSession.Delay(30, () =>
        {
            if (IsCurrent(run, session) && step == _step)
                Fail(new TimeoutException("Recruitment response timed out; submitted operations will not be retried."));
        });
    }

    private static void Fail(Exception error)
    {
        Reset();
        Debug.LogWarning("[BetterTaiwuScroll] Auto recruitment stopped: " + error);
        RefreshQuickButtons();
    }

    private static void RefreshQuickButtons()
    {
        GEvent.OnEvent(UiEvents.OnUpdateQuickBtnState);
        GEvent.OnEvent(UiEvents.UpdateAllBlockInfo);
    }

    private static List<RecruitOperation> BuildOperations(List<BuildingRecruitData> cachedData)
    {
        var operations = new List<RecruitOperation>();
        foreach (var group in cachedData)
        {
            var list = group.CharacterDataList;
            if (list == null || list.Count == 0)
                continue;

            foreach (var data in list)
            {
                if (!TryGetQualification(group.BuildingTemplateId, data, out var qualification))
                    continue;

                if (qualification >= Plugin.AutoRecruitPeopleMinQualification)
                    operations.Add(new RecruitOperation(data, accept: true));
                else if (qualification <= Plugin.AutoRejectRecruitPeopleMaxQualification)
                    operations.Add(new RecruitOperation(data, accept: false));
            }
        }

        return operations.GroupBy(operation => operation.Data.BuildingBlockKey)
            .SelectMany(group => group.OrderByDescending(operation => operation.Data.RecruitInfoIndex))
            .Take(4096).ToList();
    }

    private static bool TryGetQualification(short buildingTemplateId, BuildingRecruitCharacterData data, out int qualification)
    {
        qualification = 0;
        try
        {
            var buildingBlock = BuildingBlock.Instance[buildingTemplateId];
            if (data.CharacterData == null)
                return false;

            if (buildingBlock.RequireCombatSkillType >= 0)
            {
                qualification = data.CharacterData.CombatSkillQualifications[buildingBlock.RequireCombatSkillType];
                return true;
            }

            if (buildingBlock.RequireLifeSkillType < 0)
                return false;

            qualification = data.CharacterData.LifeSkillQualifications[buildingBlock.RequireLifeSkillType];
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Skip auto recruit qualification check: " + ex.Message);
            qualification = 0;
            return false;
        }
    }

    private sealed class RecruitOperation
    {
        internal readonly BuildingRecruitCharacterData Data;
        internal readonly bool Accept;
        internal RecruitOperation(BuildingRecruitCharacterData data, bool accept) { Data = data; Accept = accept; }
    }
}
