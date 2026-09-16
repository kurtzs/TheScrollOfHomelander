using System.Collections.Generic;
using GameData.Common;

namespace BetterTaiwuScroll.Backend;

internal static class AdvanceMonthSecretInformationRemoveBatch
{
    private static int _batches;
    private static int _inputIds;

    internal static void Prepare(ref IEnumerable<SecretInformationId> ids)
    {
        if (!AdvanceMonthDiagnosticsSettings.SecretInformationRemoveBatchEnabled || ids == null) return;
        // Materialize before native deletion; native code owns cache invalidation and unregistering.
        var unique = new HashSet<SecretInformationId>();
        var ordered = new List<SecretInformationId>();
        var count = 0;
        foreach (var id in ids)
        {
            count++;
            if (unique.Add(id)) ordered.Add(id);
        }
        ids = ordered;
        _batches++;
        _inputIds += count;
    }

    internal static void GetDiagnostics(out bool enabled, out int batches, out int inputIds,
        out int removedIds, out int fallbacks, out string lastError)
    {
        enabled = AdvanceMonthDiagnosticsSettings.SecretInformationRemoveBatchEnabled;
        batches = _batches;
        inputIds = _inputIds;
        removedIds = 0;
        fallbacks = 0;
        lastError = string.Empty;
    }

    internal static void Reset() { _batches = 0; _inputIds = 0; }
}
