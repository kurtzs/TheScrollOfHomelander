#nullable disable
#pragma warning disable CS0105

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GameData.Common;
using GameData.Domains.Information;
using GameData.Domains.Information.Secret;
using HarmonyLib;
using SecretInformationData = GameData.Domains.Information.Secret.SecretInformation;

namespace BetterTaiwuScroll.Backend;

internal static class AdvanceMonthMetabolismHolderIndex
{
    private static readonly FieldInfo CharacterKnownSecretsField = AccessTools.Field(typeof(InformationDomain), "_characterKnownSecrets");
    private static readonly Dictionary<SecretInformationId, HashSet<int>> HoldersBySecret = new Dictionary<SecretInformationId, HashSet<int>>(16384);
    private static readonly Dictionary<int, HashSet<SecretInformationId>> SecretsByCharacter = new Dictionary<int, HashSet<SecretInformationId>>(8192);

    private static bool _active;
    private static bool _transpilerApplied;
    private static int _builds;
    private static int _hits;
    private static int _fallbacks;
    private static int _setCharacterUpdates;
    private static string _lastError = string.Empty;

    internal static void Begin(InformationDomain domain)
    {
        ResetCountersForRun();
        if (!AdvanceMonthDiagnosticsSettings.MetabolismHolderIndexEnabled)
        {
            return;
        }

        try
        {
            if (!TryGetCharacterKnownSecretsMap(domain, out Dictionary<int, CharacterKnownSecret> knownSecrets))
            {
                _lastError = "ReflectCharacterKnownSecretsFailed";
                return;
            }

            foreach (KeyValuePair<int, CharacterKnownSecret> pair in knownSecrets)
            {
                HashSet<SecretInformationId> characterSecrets = CopySecretSet(pair.Value);
                SecretsByCharacter[pair.Key] = characterSecrets;
                foreach (SecretInformationId secretId in characterSecrets)
                {
                    AddHolder(secretId, pair.Key);
                }
            }

            _active = true;
            _builds++;
        }
        catch (Exception exception)
        {
            _active = false;
            _lastError = exception.GetType().Name;
            ClearMaps();
        }
    }

    internal static void Finish()
    {
        _active = false;
        ClearMaps();
    }

    internal static void SetTranspilerApplied()
    {
        _transpilerApplied = true;
    }

    internal static void FillKnownSecretHolders(
        InformationDomain domain,
        HashSet<int> destination,
        SecretInformationId secretId)
    {
        if (destination == null)
        {
            return;
        }

        if (_active)
        {
            if (HoldersBySecret.TryGetValue(secretId, out HashSet<int> holders))
            {
                foreach (int holder in holders)
                {
                    destination.Add(holder);
                }
            }

            _hits++;
            return;
        }

        _fallbacks++;
        SlowFillKnownSecretHolders(domain, destination, secretId);
    }

    internal static void OnSetCharacterKnownSecrets(int characterId, CharacterKnownSecret value)
    {
        if (!_active)
        {
            return;
        }

        try
        {
            HashSet<SecretInformationId> oldSecrets;
            if (!SecretsByCharacter.TryGetValue(characterId, out oldSecrets))
            {
                oldSecrets = new HashSet<SecretInformationId>();
            }

            HashSet<SecretInformationId> newSecrets = CopySecretSet(value);
            foreach (SecretInformationId secretId in oldSecrets)
            {
                if (!newSecrets.Contains(secretId))
                {
                    RemoveHolder(secretId, characterId);
                }
            }

            foreach (SecretInformationId secretId in newSecrets)
            {
                if (!oldSecrets.Contains(secretId))
                {
                    AddHolder(secretId, characterId);
                }
            }

            SecretsByCharacter[characterId] = newSecrets;
            _setCharacterUpdates++;
        }
        catch (Exception exception)
        {
            Deactivate(exception.GetType().Name);
        }
    }

    internal static void OnRemoveCharacterKnownSecrets(int characterId)
    {
        if (!_active)
        {
            return;
        }

        try
        {
            if (!SecretsByCharacter.TryGetValue(characterId, out HashSet<SecretInformationId> oldSecrets))
            {
                return;
            }

            foreach (SecretInformationId secretId in oldSecrets)
            {
                RemoveHolder(secretId, characterId);
            }

            SecretsByCharacter.Remove(characterId);
            _setCharacterUpdates++;
        }
        catch (Exception exception)
        {
            Deactivate(exception.GetType().Name);
        }
    }

    internal static void OnRemoveSecretInformation(SecretInformationId secretId)
    {
        if (!_active)
        {
            return;
        }

        if (!HoldersBySecret.TryGetValue(secretId, out HashSet<int> holders))
        {
            return;
        }

        foreach (int holder in holders)
        {
            if (SecretsByCharacter.TryGetValue(holder, out HashSet<SecretInformationId> secrets))
            {
                secrets.Remove(secretId);
            }
        }

        HoldersBySecret.Remove(secretId);
    }

    internal static void GetDiagnostics(
        out bool enabled,
        out bool active,
        out bool transpilerApplied,
        out int builds,
        out int hits,
        out int fallbacks,
        out int setCharacterUpdates,
        out string lastError)
    {
        enabled = AdvanceMonthDiagnosticsSettings.MetabolismHolderIndexEnabled;
        active = _active;
        transpilerApplied = _transpilerApplied;
        builds = _builds;
        hits = _hits;
        fallbacks = _fallbacks;
        setCharacterUpdates = _setCharacterUpdates;
        lastError = _lastError ?? string.Empty;
    }

    internal static void Reset()
    {
        _active = false;
        _builds = 0;
        _hits = 0;
        _fallbacks = 0;
        _setCharacterUpdates = 0;
        _lastError = string.Empty;
        ClearMaps();
    }

    private static void ResetCountersForRun()
    {
        _active = false;
        _builds = 0;
        _hits = 0;
        _fallbacks = 0;
        _setCharacterUpdates = 0;
        _lastError = string.Empty;
        ClearMaps();
    }

    private static HashSet<SecretInformationId> CopySecretSet(CharacterKnownSecret knownSecret)
    {
        HashSet<SecretInformationId> result = new HashSet<SecretInformationId>();
        if (knownSecret?.KnownSecrets == null)
        {
            return result;
        }

        foreach (SecretInformationId secretId in knownSecret.KnownSecrets)
        {
            result.Add(secretId);
        }

        return result;
    }

    private static void AddHolder(SecretInformationId secretId, int characterId)
    {
        if (!HoldersBySecret.TryGetValue(secretId, out HashSet<int> holders))
        {
            holders = new HashSet<int>();
            HoldersBySecret.Add(secretId, holders);
        }

        holders.Add(characterId);
    }

    private static void RemoveHolder(SecretInformationId secretId, int characterId)
    {
        if (!HoldersBySecret.TryGetValue(secretId, out HashSet<int> holders))
        {
            return;
        }

        holders.Remove(characterId);
        if (holders.Count == 0)
        {
            HoldersBySecret.Remove(secretId);
        }
    }

    private static void SlowFillKnownSecretHolders(
        InformationDomain domain,
        HashSet<int> destination,
        SecretInformationId secretId)
    {
        if (domain == null ||
            !TryGetCharacterKnownSecretsMap(domain, out Dictionary<int, CharacterKnownSecret> knownSecrets))
        {
            return;
        }

        foreach (KeyValuePair<int, CharacterKnownSecret> pair in knownSecrets)
        {
            if (pair.Value?.KnownSecrets != null && pair.Value.KnownSecrets.Contains(secretId))
            {
                destination.Add(pair.Key);
            }
        }
    }

    private static bool TryGetCharacterKnownSecretsMap(
        InformationDomain domain,
        out Dictionary<int, CharacterKnownSecret> knownSecrets)
    {
        knownSecrets = domain == null || CharacterKnownSecretsField == null
            ? null
            : CharacterKnownSecretsField.GetValue(domain) as Dictionary<int, CharacterKnownSecret>;
        return knownSecrets != null;
    }

    private static void Deactivate(string error)
    {
        _active = false;
        _lastError = error ?? string.Empty;
        ClearMaps();
    }

    private static void ClearMaps()
    {
        HoldersBySecret.Clear();
        SecretsByCharacter.Clear();
    }
}

[HarmonyPatch]
internal static class AdvanceMonthMetabolismHolderIndexTranspilerPatch
{
    private static readonly FieldInfo CharacterKnownSecretsField = AccessTools.Field(typeof(InformationDomain), "_characterKnownSecrets");
    private static readonly FieldInfo SecretInformationIdField = AccessTools.Field(typeof(SecretInformationData), "Id");
    private static readonly MethodInfo FillKnownSecretHoldersMethod = AccessTools.Method(
        typeof(AdvanceMonthMetabolismHolderIndex),
        nameof(AdvanceMonthMetabolismHolderIndex.FillKnownSecretHolders));

    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(InformationDomain),
            "MetabolismSecretInformation",
            new[] { typeof(DataContext) });
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
        if (CharacterKnownSecretsField == null ||
            SecretInformationIdField == null ||
            FillKnownSecretHoldersMethod == null)
        {
            return codes;
        }

        for (int i = 0; i < codes.Count; i++)
        {
            if (!IsHashSetIntUnionWith(codes[i]))
            {
                continue;
            }

            int start = FindHolderScanStart(codes, i);
            if (start < 0 || start + 3 >= codes.Count)
            {
                continue;
            }

            FieldInfo displaySecretField = FindDisplaySecretField(codes, start);
            if (displaySecretField == null)
            {
                continue;
            }

            CodeInstruction first = new CodeInstruction(OpCodes.Ldarg_0);
            first.labels.AddRange(codes[start].labels);
            first.blocks.AddRange(codes[start].blocks);
            codes[start].labels.Clear();
            codes[start].blocks.Clear();

            List<CodeInstruction> replacement = new List<CodeInstruction>
            {
                first,
                CloneWithoutBranches(codes[start]),
                CloneWithoutBranches(codes[start + 3]),
                new CodeInstruction(OpCodes.Ldfld, displaySecretField),
                new CodeInstruction(OpCodes.Ldfld, SecretInformationIdField),
                new CodeInstruction(OpCodes.Call, FillKnownSecretHoldersMethod)
            };

            codes.RemoveRange(start, i - start + 1);
            codes.InsertRange(start, replacement);
            AdvanceMonthMetabolismHolderIndex.SetTranspilerApplied();
            return codes;
        }

        return codes;
    }

    private static int FindHolderScanStart(List<CodeInstruction> codes, int unionWithIndex)
    {
        int min = Math.Max(0, unionWithIndex - 32);
        for (int i = unionWithIndex - 1; i >= min; i--)
        {
            if (i + 3 >= codes.Count)
            {
                continue;
            }

            if (IsLoadLocal(codes[i]) &&
                codes[i + 1].opcode == OpCodes.Ldarg_0 &&
                CodeInstructionExtensions.LoadsField(codes[i + 2], CharacterKnownSecretsField) &&
                IsLoadLocal(codes[i + 3]))
            {
                return i;
            }
        }

        return -1;
    }

    private static FieldInfo FindDisplaySecretField(List<CodeInstruction> codes, int holderScanStart)
    {
        int min = Math.Max(0, holderScanStart - 16);
        for (int i = holderScanStart - 1; i >= min; i--)
        {
            if (codes[i].opcode != OpCodes.Ldfld || codes[i].operand is not FieldInfo field)
            {
                continue;
            }

            if (field.Name == "secret" && field.FieldType == typeof(SecretInformationData))
            {
                return field;
            }
        }

        return null;
    }

    private static bool IsHashSetIntUnionWith(CodeInstruction instruction)
    {
        if (instruction.opcode != OpCodes.Callvirt || instruction.operand is not MethodInfo method)
        {
            return false;
        }

        return method.Name == "UnionWith" &&
            method.DeclaringType == typeof(HashSet<int>);
    }

    private static bool IsLoadLocal(CodeInstruction instruction)
    {
        return instruction.opcode == OpCodes.Ldloc_0 ||
            instruction.opcode == OpCodes.Ldloc_1 ||
            instruction.opcode == OpCodes.Ldloc_2 ||
            instruction.opcode == OpCodes.Ldloc_3 ||
            instruction.opcode == OpCodes.Ldloc_S ||
            instruction.opcode == OpCodes.Ldloc;
    }

    private static CodeInstruction CloneWithoutBranches(CodeInstruction instruction)
    {
        return new CodeInstruction(instruction.opcode, instruction.operand);
    }
}

[HarmonyPatch]
internal static class AdvanceMonthMetabolismHolderIndexLifecyclePatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(InformationDomain),
            "MetabolismSecretInformation",
            new[] { typeof(DataContext) });
    }

    [HarmonyPriority(Priority.First)]
    private static void Prefix(InformationDomain __instance)
    {
        AdvanceMonthMetabolismHolderIndex.Begin(__instance);
    }

    private static Exception Finalizer(Exception __exception)
    {
        AdvanceMonthMetabolismHolderIndex.Finish();
        return __exception;
    }
}

[HarmonyPatch]
internal static class AdvanceMonthMetabolismHolderIndexSetCharacterKnownSecretsPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(InformationDomain),
            "SetElement_CharacterKnownSecrets",
            new[] { typeof(int), typeof(CharacterKnownSecret), typeof(DataContext) });
    }

    private static void Postfix(int elementId, CharacterKnownSecret value)
    {
        AdvanceMonthMetabolismHolderIndex.OnSetCharacterKnownSecrets(elementId, value);
    }
}

[HarmonyPatch]
internal static class AdvanceMonthMetabolismHolderIndexAddCharacterKnownSecretsPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(InformationDomain),
            "AddElement_CharacterKnownSecrets",
            new[] { typeof(int), typeof(CharacterKnownSecret), typeof(DataContext) });
    }

    private static void Postfix(int elementId, CharacterKnownSecret value)
    {
        AdvanceMonthMetabolismHolderIndex.OnSetCharacterKnownSecrets(elementId, value);
    }
}

[HarmonyPatch]
internal static class AdvanceMonthMetabolismHolderIndexRemoveCharacterKnownSecretsPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(InformationDomain),
            "RemoveElement_CharacterKnownSecrets",
            new[] { typeof(int), typeof(DataContext) });
    }

    private static void Prefix(int elementId)
    {
        AdvanceMonthMetabolismHolderIndex.OnRemoveCharacterKnownSecrets(elementId);
    }
}

[HarmonyPatch]
internal static class AdvanceMonthMetabolismHolderIndexRemoveSecretInformationPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(InformationDomain),
            "RemoveElement_SecretInformation",
            new[] { typeof(SecretInformationId), typeof(DataContext) });
    }

    private static void Prefix(SecretInformationId elementId)
    {
        AdvanceMonthMetabolismHolderIndex.OnRemoveSecretInformation(elementId);
    }
}
