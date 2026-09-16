#nullable disable

using System;
using System.IO;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

[Serializable]
internal sealed class AutoChickenCareSettings
{
    public bool Enabled = true;
    public int HappinessThreshold = 90;
    public bool UseTrough = true;
    public bool UseWarehouse = true;
    public bool UseInventory = false;

    internal AutoChickenCareSettings Clone()
    {
        return (AutoChickenCareSettings)MemberwiseClone();
    }

    internal void Normalize()
    {
        HappinessThreshold = Mathf.Clamp(HappinessThreshold, 0, 100);
    }
}

internal static class AutoChickenCareSettingsStore
{
    private const string FileName = "AutoChickenCareSettings.json";
    internal static AutoChickenCareSettings Current { get; private set; } = new AutoChickenCareSettings();

    internal static void Load()
    {
        Current = LoadFromCandidates() ?? new AutoChickenCareSettings();
        Current.Normalize();
    }

    internal static void Save()
    {
        try
        {
            Current.Normalize();
            AsyncSettingsSaveQueue.Enqueue(ModUserDataPaths.GetFilePath(FileName), Current.Clone());
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save auto chicken settings: " + ex);
        }
    }

    private static AutoChickenCareSettings LoadFromCandidates()
    {
        foreach (var path in ModUserDataPaths.GetFilePathCandidates(FileName))
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                return JsonUtility.FromJson<AutoChickenCareSettings>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BetterTaiwuScroll] Failed to load auto chicken settings from " + path + ": " + ex.Message);
            }
        }

        return null;
    }
}

[Serializable]
internal sealed class AutoCricketRoomSettings
{
    public bool Enabled = true;
    public bool RetrieveAutomaticallyStored = true;
    public bool RetrievePlayerStored = false;

    internal AutoCricketRoomSettings Clone()
    {
        return (AutoCricketRoomSettings)MemberwiseClone();
    }
}

internal static class AutoCricketRoomSettingsStore
{
    private const string FileName = "AutoCricketRoomSettings.json";
    internal static AutoCricketRoomSettings Current { get; private set; } = new AutoCricketRoomSettings();

    internal static void Load()
    {
        Current = LoadFromCandidates() ?? new AutoCricketRoomSettings();
    }

    internal static void Save()
    {
        try
        {
            AsyncSettingsSaveQueue.Enqueue(ModUserDataPaths.GetFilePath(FileName), Current.Clone());
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BetterTaiwuScroll] Failed to save auto cricket room settings: " + ex);
        }
    }

    private static AutoCricketRoomSettings LoadFromCandidates()
    {
        foreach (var path in ModUserDataPaths.GetFilePathCandidates(FileName))
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                return JsonUtility.FromJson<AutoCricketRoomSettings>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BetterTaiwuScroll] Failed to load auto cricket room settings from " + path + ": " + ex.Message);
            }
        }

        return null;
    }
}
