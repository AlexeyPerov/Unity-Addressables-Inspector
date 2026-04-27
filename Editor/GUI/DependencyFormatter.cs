using UnityEditor;

namespace AddressablesInspector
{
    public static class DependencyFormatter
    {
        public static string GetDependencyTypeTag(string bundleName)
        {
            var isRemote = BundleUtilities.IsBundleRemote(bundleName);
            var type = isRemote ? "[remote]" : "[built-in]";
            
            if (BundleUtilities.IsBundleStartup(bundleName))
                type += "[startup]";
            
            return type;
        }

        public static string GetDependencySizeString(long size)
        {
            return "[" + EditorUtility.FormatBytes(size) + "]";
        }

        public static string GetDependencyCountString(BuildLayoutProvider.Archive archive)
        {
            return $"[DirDeps:{archive.BundleDependencies.Count} ExpDeps:{archive.ExpandedBundleDependencies.Count}]";
        }

        public static string FormatDependencyLine(BuildLayoutProvider.Archive bundle, bool currentBuiltIn, bool increaseWarningForRemote)
        {
            var dependencyType = GetDependencyTypeTag(bundle.Name);
            var size = GetDependencySizeString(bundle.Size);
            var dependenciesCount = GetDependencyCountString(bundle);
            var warning = increaseWarningForRemote && currentBuiltIn && BundleUtilities.IsBundleRemote(bundle.Name) ? 3 : bundle.TopWarning;
            
            return $"- {bundle.Name} {size} {dependencyType} {dependenciesCount}";
        }

        public static string FormatGroupDependencyLine(BuildLayoutProvider.Group group)
        {
            var dependencyType = GetDependencyTypeTag(group.Name);
            var size = GetDependencySizeString(group.Size);
            var warning = group.TopWarning;
            
            return $"- {group.Name} {size} {dependencyType}";
        }
    }
}