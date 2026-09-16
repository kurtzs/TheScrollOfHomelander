#nullable disable

using System;
using System.Reflection;

namespace BetterTaiwuScroll.Frontend;

internal static class ReflectionHelpers
{
    internal static PropertyInfo FindProperty(Type type, string propertyName)
    {
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (property.GetIndexParameters().Length != 0)
                continue;

            if (property.Name == propertyName || property.Name.EndsWith("." + propertyName, StringComparison.Ordinal))
                return property;
        }

        return null;
    }
}
