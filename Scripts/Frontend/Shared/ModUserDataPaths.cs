#nullable disable

using System;
using System.IO;

namespace BetterTaiwuScroll.Frontend;

internal static class ModUserDataPaths
{
    private static string _cachedRoot;

    internal static string GetFilePath(string fileName)
    {
        return Path.Combine(GetUserDataRoot(), fileName);
    }

    internal static string GetUserDataRoot()
    {
        if (!string.IsNullOrEmpty(_cachedRoot))
            return _cachedRoot;

        var modRoot = GetModRoot();
        if (!string.IsNullOrEmpty(modRoot))
        {
            _cachedRoot = Path.Combine(modRoot, "UserData");
            return _cachedRoot;
        }

        _cachedRoot = Path.GetFullPath("UserData");
        return _cachedRoot;
    }

    private static string GetModRoot()
    {
        if (!string.IsNullOrEmpty(Plugin.ModDirectory))
            return Path.GetFullPath(Plugin.ModDirectory);

        var assemblyPath = typeof(Plugin).Assembly.Location;
        if (!string.IsNullOrEmpty(assemblyPath))
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(assemblyPath), "..", ".."));

        var appDataModRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TaiwuStudio",
            "Taiwu Studio",
            "data",
            "mods",
            "TheScrollOfHomelander");
        return Directory.Exists(appDataModRoot) ? appDataModRoot : null;
    }
}
