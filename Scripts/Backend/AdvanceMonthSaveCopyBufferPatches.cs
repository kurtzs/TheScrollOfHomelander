#nullable disable
#pragma warning disable CS0105

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GameData.ArchiveData;
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
        MethodInfo getter = AccessTools.Method(typeof(AdvanceMonthSaveCopyBufferOptimization), nameof(GetCopyBufferBytes));
        foreach (CodeInstruction instruction in instructions)
        {
            if (IsOriginalCopyBufferConstant(instruction))
            {
                yield return new CodeInstruction(OpCodes.Call, getter);
                continue;
            }

            yield return instruction;
        }
    }

    private static bool IsOriginalCopyBufferConstant(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Ldc_I8 && instruction.operand is long longValue && longValue == 4096L)
        {
            return true;
        }

        if (instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int intValue && intValue == 4096)
        {
            return true;
        }

        return instruction.opcode == OpCodes.Ldc_I4 && instruction.operand == null && instruction.LoadsConstant(4096L);
    }
}

[HarmonyPatch(typeof(ArchiveFileBase), "CopyFrom", new[] { typeof(System.IO.Stream), typeof(long) })]
internal static class AdvanceMonthSaveCopyFromBufferPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return AdvanceMonthSaveCopyBufferOptimization.ReplaceOriginalCopyBuffer(instructions);
    }
}

[HarmonyPatch(typeof(ArchiveFileBase), "CopyTo", new[] { typeof(System.IO.Stream), typeof(long) })]
internal static class AdvanceMonthSaveCopyToBufferPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return AdvanceMonthSaveCopyBufferOptimization.ReplaceOriginalCopyBuffer(instructions);
    }
}
