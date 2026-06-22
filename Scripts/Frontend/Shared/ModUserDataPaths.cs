#nullable disable

using System.IO;
using UnityEngine;

namespace BetterTaiwuScroll.Frontend;

internal static class ModUserDataPaths
{
    internal static string GetFilePath(string fileName)
    {
        return Path.Combine(GetUserDataRoot(), fileName);
    }

    internal static string GetUserDataRoot()
    {
        var assemblyPath = typeof(Plugin).Assembly.Location;
        if (string.IsNullOrEmpty(assemblyPath))
        {
            Debug.LogWarning("[BetterTaiwuScroll] Assembly location is empty; UserData path may be relative.");
            return "UserData";
        }

        var pluginDir = Path.GetDirectoryName(assemblyPath);
        var modRoot = Path.GetFullPath(Path.Combine(pluginDir, "..", ".."));
        return Path.Combine(modRoot, "UserData");
    }
}
