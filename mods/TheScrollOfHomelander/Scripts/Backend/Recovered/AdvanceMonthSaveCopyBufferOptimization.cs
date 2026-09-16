using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace BetterTaiwuScroll.Backend;

internal static class AdvanceMonthSaveCopyBufferOptimization
{
	public static long GetCopyBufferBytes()
	{
		return AdvanceMonthDiagnosticsSettings.CopyBufferBytes;
	}

	internal static IEnumerable<CodeInstruction> ReplaceOriginalCopyBuffer(IEnumerable<CodeInstruction> instructions)
	{
		MethodInfo getter = AccessTools.Method(typeof(AdvanceMonthSaveCopyBufferOptimization), "GetCopyBufferBytes");
		foreach (CodeInstruction instruction in instructions)
		{
			if (IsOriginalCopyBufferConstant(instruction))
			{
				yield return new CodeInstruction(OpCodes.Call, getter);
			}
			else
			{
				yield return instruction;
			}
		}
	}

	private static bool IsOriginalCopyBufferConstant(CodeInstruction instruction)
	{
		if (instruction.opcode == OpCodes.Ldc_I8 && instruction.operand is long num && num == 4096)
		{
			return true;
		}
		if (instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int num2 && num2 == 4096)
		{
			return true;
		}
		if (instruction.opcode == OpCodes.Ldc_I4 && instruction.operand == null)
		{
			return instruction.LoadsConstant(4096L);
		}
		return false;
	}
}

