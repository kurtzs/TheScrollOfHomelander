using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GameData.Common;
using GameData.Domains.Information;
using GameData.Domains.Information.Secret;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;
using SecretInformation = GameData.Domains.Information.Secret.SecretInformation;

[HarmonyPatch]
internal static class AdvanceMonthMetabolismHolderIndexTranspilerPatch
{
    internal static bool UsesNativeHolderQuery => AccessTools.Method(typeof(InformationDomain),
        "GetSecretInformationHolders", new[] { typeof(SecretInformationId) }) != null;

    private static bool Prepare() => !UsesNativeHolderQuery;

    private static bool _reportedMismatch;
    private static IEnumerable<CodeInstruction> KeepOriginal(List<CodeInstruction> instructions, string reason)
    {
        if (!_reportedMismatch)
        {
            _reportedMismatch = true;
            GameData.Utilities.AdaptableLog.Warning("[BetterTaiwuScroll] Secret holder optimization skipped; native logic retained: " + reason, false);
        }
        return instructions;
    }
	private static readonly FieldInfo CharacterKnownSecretsField = AccessTools.Field(typeof(InformationDomain), "_characterKnownSecrets");

	private static readonly FieldInfo SecretInformationIdField = AccessTools.Field(typeof(SecretInformation), "Id");

	private static readonly MethodInfo FillKnownSecretHoldersMethod = AccessTools.Method(typeof(AdvanceMonthMetabolismHolderIndex), "FillKnownSecretHolders");

	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(InformationDomain), "MetabolismSecretInformation", new Type[1] { typeof(DataContext) });
	}

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		List<CodeInstruction> list = new List<CodeInstruction>(instructions);
		if (CharacterKnownSecretsField == null || SecretInformationIdField == null || FillKnownSecretHoldersMethod == null)
		{
			return KeepOriginal(list, "dependencies changed");
		}
		int matches = 0;
		for (int index = 0; index < list.Count; index++)
		{
			if (!IsHashSetIntUnionWith(list[index])) continue;
			int start = FindHolderScanStart(list, index);
			if (start >= 0 && start + 3 < list.Count && FindDisplaySecretField(list, start) != null) matches++;
		}
		if (matches != 1) return KeepOriginal(list, "scan match count=" + matches);
		for (int i = 0; i < list.Count; i++)
		{
			if (!IsHashSetIntUnionWith(list[i]))
			{
				continue;
			}
			int num = FindHolderScanStart(list, i);
			if (num >= 0 && num + 3 < list.Count)
			{
				FieldInfo fieldInfo = FindDisplaySecretField(list, num);
				if (!(fieldInfo == null))
				{
					CodeInstruction codeInstruction = new CodeInstruction(OpCodes.Ldarg_0);
					codeInstruction.labels.AddRange(list[num].labels);
					codeInstruction.blocks.AddRange(list[num].blocks);
					list[num].labels.Clear();
					list[num].blocks.Clear();
					List<CodeInstruction> collection = new List<CodeInstruction>
					{
						codeInstruction,
						CloneWithoutBranches(list[num]),
						CloneWithoutBranches(list[num + 3]),
						new CodeInstruction(OpCodes.Ldfld, fieldInfo),
						new CodeInstruction(OpCodes.Ldfld, SecretInformationIdField),
						new CodeInstruction(OpCodes.Call, FillKnownSecretHoldersMethod)
					};
					list.RemoveRange(num, i - num + 1);
					list.InsertRange(num, collection);
					AdvanceMonthMetabolismHolderIndex.SetTranspilerApplied();
					return list;
				}
			}
		}
		return KeepOriginal(list, "replacement was not applied");
	}

	private static int FindHolderScanStart(List<CodeInstruction> codes, int unionWithIndex)
	{
		int num = Math.Max(0, unionWithIndex - 32);
		for (int num2 = unionWithIndex - 1; num2 >= num; num2--)
		{
			if (num2 + 3 < codes.Count && IsLoadLocal(codes[num2]) && codes[num2 + 1].opcode == OpCodes.Ldarg_0 && codes[num2 + 2].LoadsField(CharacterKnownSecretsField) && IsLoadLocal(codes[num2 + 3]))
			{
				return num2;
			}
		}
		return -1;
	}

	private static FieldInfo FindDisplaySecretField(List<CodeInstruction> codes, int holderScanStart)
	{
		int num = Math.Max(0, holderScanStart - 16);
		for (int num2 = holderScanStart - 1; num2 >= num; num2--)
		{
			if (!(codes[num2].opcode != OpCodes.Ldfld) && codes[num2].operand is FieldInfo { Name: "secret" } fieldInfo && fieldInfo.FieldType == typeof(SecretInformation))
			{
				return fieldInfo;
			}
		}
		return null;
	}

	private static bool IsHashSetIntUnionWith(CodeInstruction instruction)
	{
		if (instruction.opcode != OpCodes.Callvirt || !(instruction.operand is MethodInfo methodInfo))
		{
			return false;
		}
		if (methodInfo.Name == "UnionWith")
		{
			return methodInfo.DeclaringType == typeof(HashSet<int>);
		}
		return false;
	}

	private static bool IsLoadLocal(CodeInstruction instruction)
	{
		if (!(instruction.opcode == OpCodes.Ldloc_0) && !(instruction.opcode == OpCodes.Ldloc_1) && !(instruction.opcode == OpCodes.Ldloc_2) && !(instruction.opcode == OpCodes.Ldloc_3) && !(instruction.opcode == OpCodes.Ldloc_S))
		{
			return instruction.opcode == OpCodes.Ldloc;
		}
		return true;
	}

	private static CodeInstruction CloneWithoutBranches(CodeInstruction instruction)
	{
		return new CodeInstruction(instruction.opcode, instruction.operand);
	}
}

