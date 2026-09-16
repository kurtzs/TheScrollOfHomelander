#nullable disable

using System;
using System.Collections.Generic;
using System.IO;

namespace BetterTaiwuScroll.Frontend;

internal static class ModUserDataPaths
{
    private const string UserFolderName = "TheScrollOfHomelander";
    private static string _cachedRoot;
    private static string _cachedLegacyRoot;

    internal static string GetFilePath(string fileName)
    {
        return Path.Combine(GetUserDataRoot(), fileName);
    }

    internal static string GetLegacyFilePath(string fileName)
    {
        var legacyRoot = GetLegacyUserDataRoot();
        return string.IsNullOrEmpty(legacyRoot) ? null : Path.Combine(legacyRoot, fileName);
    }

    internal static IEnumerable<string> GetFilePathCandidates(string fileName)
    {
        var primaryPath = GetFilePath(fileName);
        yield return primaryPath;

        var legacyPath = GetLegacyFilePath(fileName);
        if (!string.IsNullOrEmpty(legacyPath)
            && !string.Equals(primaryPath, legacyPath, StringComparison.OrdinalIgnoreCase))
        {
            yield return legacyPath;
        }
    }

    internal static string GetUserDataRoot()
    {
        if (!string.IsNullOrEmpty(_cachedRoot))
            return _cachedRoot;

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrEmpty(documents))
            documents = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

        if (!string.IsNullOrEmpty(documents))
        {
            _cachedRoot = Path.Combine(documents, UserFolderName);
            return _cachedRoot;
        }

        _cachedRoot = Path.GetFullPath(UserFolderName);
        return _cachedRoot;
    }

    private static string GetLegacyUserDataRoot()
    {
        if (!string.IsNullOrEmpty(_cachedLegacyRoot))
            return _cachedLegacyRoot;

        var modRoot = GetModRoot();
        if (!string.IsNullOrEmpty(modRoot))
        {
            _cachedLegacyRoot = Path.Combine(modRoot, "UserData");
            return _cachedLegacyRoot;
        }

        _cachedLegacyRoot = Path.GetFullPath("UserData");
        return _cachedLegacyRoot;
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
