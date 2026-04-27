using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AddressablesInspector
{
    public class BuildLayoutProvider
    {
        private readonly BuildLayoutParser _layoutParser;

        public List<Group> Groups = new();
        public readonly Dictionary<string, Archive> Bundles = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, Asset> AssetsByGuid = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, Asset> AssetsByPath = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, LabelInfo> Labels = new(StringComparer.OrdinalIgnoreCase);

        public long TotalSize { get; }
        public long TotalBuiltInSize { get; }
        public long TotalRemoteSize { get; }

        public string Name => _layoutParser.Name;

        public class Group
        {
            public string Name = string.Empty;
            public long Size;
            public readonly List<Archive> Archives = new();
            public int TopWarning;
        }

        public class Archive
        {
            public bool IsBuiltin;
            public string Name = "";
            public long Size;
            public string Compression = "";
            public long AssetBundleObjectSize;

            public readonly List<Archive> BundleDependencies = new();
            public readonly List<BundleDependencyInfo> BundleDependenciesInfos = new();
            public readonly List<Archive> ExpandedBundleDependencies = new();
            public readonly List<BundleExpandedDependencyInfo> ExpandedBundleDependenciesInfos = new();
            public List<Archive> AllBundleDependencies = new();

            public readonly List<Asset> ExplicitAssets = new();
            public List<Asset> AllAssets = new();

            public BuildLayoutParser.Archive BaseObject;
            public readonly List<Group> ReferencedByGroups = new();
            public readonly List<Archive> ReferencedByBundlesDirectly = new();
            public readonly List<Archive> ReferencedByBundlesExpanded = new();

            public HashSet<RecommendationMessage> Recommendations { get; } = new();
            public int TopWarning { get; private set; }

            public void TrySetWarningLevel(int level)
            {
                if (level > TopWarning)
                    TopWarning = level;
            }
        }

        public class Asset
        {
            public string Guid { get; set; }
            public string Name { get; set; }
            public long Size { get; set; }
            public long SizeFromObjects { get; set; }
            public long SizeFromStreamedData { get; set; }
            public string Address { get; set; }
            public List<Asset> ExternalReferences { get; } = new();
            public List<Asset> InternalReferences { get; } = new();
            public BuildLayoutParser.ExplicitAsset BaseObject { get; set; }
            public List<Archive> IncludedByBundle { get; } = new();
            public HashSet<Archive> UsedByBundles { get; } = new();
            public List<string> Labels { get; set; } = new();

            public bool IsEmbedded { get; set; }
            public Asset IncludedByAsset { get; set; }
            public Archive IncludedInBundle { get; set; }

            public int TopWarning { get; set; }
        }

        public class LabelInfo
        {
            public string Name;
            public readonly List<Asset> Assets = new();
            public readonly HashSet<Archive> Bundles = new();
            public long TotalSize;
        }

        public class BundleDependencyInfo
        {
            public Archive DependentBundle { get; set; }
            public Dictionary<Asset, List<Asset>> AssetsCrossReferences { get; } = new();

            public bool Foldout { get; set; }
        }

        public class BundleExpandedDependencyInfo
        {
            public Archive BundleFromExpandedDependencies { get; set; }
            public HashSet<Archive> BundlesFromDirectDependencies { get; } = new();

            public bool Foldout { get; set; }
        }

        private string GetUid(string bundleName, string assetName)
        {
            return $"{bundleName}###{assetName}";
        }

        public BuildLayoutProvider(BuildLayoutParser buildLayoutParser)
        {
            _layoutParser = buildLayoutParser;

            CollectBuiltInBundles(buildLayoutParser);
            CollectAllBundles(buildLayoutParser);
            ResolveBundleDependencies();

            CollectAllAssets();

            FillGroups(buildLayoutParser);

            CollectAllBundleDependenciesUnionLists();
            CollectDependencyReasons();

            FillUsedByBundlesField();

            BuildLabelIndex();

            foreach (var pair in Bundles)
            {
                TotalSize += pair.Value.Size;

                var name = pair.Value.Name.ToLowerInvariant();

                if (BundleUtilities.IsBundleRemote(name))
                {
                    TotalRemoteSize += pair.Value.Size;
                }
                else
                {
                    TotalBuiltInSize += pair.Value.Size;
                }
            }
        }

        private void BuildLabelIndex()
        {
            foreach (var asset in AssetsByPath.Values)
            {
                if (asset.Labels == null || asset.Labels.Count == 0)
                    continue;

                foreach (var label in asset.Labels)
                {
                    if (!Labels.TryGetValue(label, out var info))
                    {
                        info = new LabelInfo { Name = label };
                        Labels[label] = info;
                    }

                    info.Assets.Add(asset);
                    info.TotalSize += asset.Size;

                    if (asset.IncludedInBundle != null)
                        info.Bundles.Add(asset.IncludedInBundle);
                }
            }
        }

        private void FillUsedByBundlesField()
        {
            foreach (var bundle in Bundles.Values)
            {
                foreach (var asset in bundle.AllAssets)
                {
                    asset.UsedByBundles.Add(bundle);

                    foreach (var assetExternalReference in asset.ExternalReferences)
                    {
                        assetExternalReference.UsedByBundles.Add(bundle);
                    }
                }
            }
        }

        private void CollectAllBundleDependenciesUnionLists()
        {
            foreach (var group in Groups)
            {
                foreach (var archive in group.Archives)
                {
                    var allDeps = archive.BundleDependencies.Union(archive.ExpandedBundleDependencies).ToList();
                    archive.AllBundleDependencies = allDeps;
                }
            }
        }

        private void CollectDependencyReasons()
        {
            foreach (var group in Groups)
            {
                foreach (var archive in group.Archives)
                {
                    foreach (var bundleDependency in archive.BundleDependencies)
                    {
                        var info = new BundleDependencyInfo
                        {
                            DependentBundle = bundleDependency
                        };

                        archive.BundleDependenciesInfos.Add(info);

                        foreach (var asset in archive.AllAssets)
                        {
                            var assetsThatReferenceDependentBundle = asset.ExternalReferences.Where(x => x.IncludedInBundle == bundleDependency).ToList();
                            info.AssetsCrossReferences[asset] = assetsThatReferenceDependentBundle;
                        }
                    }

                    foreach (var expandedBundleDependency in archive.ExpandedBundleDependencies)
                    {
                        var info = new BundleExpandedDependencyInfo
                        {
                            BundleFromExpandedDependencies = expandedBundleDependency
                        };

                        archive.ExpandedBundleDependenciesInfos.Add(info);

                        foreach (var bundleDependency in archive.BundleDependencies)
                        {
                            if (bundleDependency.ExpandedBundleDependencies.Contains(expandedBundleDependency) ||
                                bundleDependency.BundleDependencies.Contains(expandedBundleDependency))
                            {
                                info.BundlesFromDirectDependencies.Add(bundleDependency);
                            }
                        }
                    }
                }
            }
        }

        private void CollectBuiltInBundles(BuildLayoutParser buildLayout)
        {
            foreach (var baseBundle in buildLayout.builtinBundles)
            {
                var bundle = FindBundle(baseBundle.name);
                if (bundle != null)
                    continue;

                bundle = new Archive
                {
                    BaseObject = baseBundle,
                    Name = baseBundle.name,
                    Size = baseBundle.size,
                    Compression = baseBundle.compression.ToUpper(),
                    AssetBundleObjectSize = baseBundle.assetBundleObjectSize,
                    IsBuiltin = true
                };
                Bundles.Add(bundle.Name, bundle);
            }
        }

        private void CollectAllBundles(BuildLayoutParser buildLayout)
        {
            foreach (var baseGroup in buildLayout.groups)
            {
                foreach (var baseBundle in baseGroup.bundles)
                {
                    var bundle = FindBundle(baseBundle.name);
                    if (bundle != null)
                        continue;

                    bundle = new Archive
                    {
                        BaseObject = baseBundle,
                        Name = baseBundle.name,
                        Size = baseBundle.size,
                        Compression = baseBundle.compression.ToUpper(),
                        AssetBundleObjectSize = baseBundle.assetBundleObjectSize
                    };
                    Bundles.Add(bundle.Name, bundle);
                }
            }
        }

        private void ResolveBundleDependencies()
        {
            foreach (var bundle in Bundles.Values)
            {
                foreach (var dependentBundleBaseObject in bundle.BaseObject.bundleDependencies)
                {
                    var dependentBundle = FindBundle(dependentBundleBaseObject);
                    if (dependentBundle == null)
                    {
                        Debug.LogError($"Cannot resolve bundle dependency to '{dependentBundleBaseObject}' in bundle '{bundle.Name}'.");
                        continue;
                    }

                    bundle.BundleDependencies.Add(dependentBundle);

                    if (!bundle.BaseObject.expandedBundleDependencies.Contains(dependentBundleBaseObject))
                        dependentBundle.ReferencedByBundlesDirectly.Add(bundle);
                }

                foreach (var dependentBundleBaseObject in bundle.BaseObject.expandedBundleDependencies)
                {
                    var dependentBundle = FindBundle(dependentBundleBaseObject);
                    if (dependentBundle == null)
                    {
                        Debug.LogError($"Cannot resolve bundle dependency to '{dependentBundleBaseObject}' in bundle '{bundle.Name}'.");
                        continue;
                    }
                    bundle.ExpandedBundleDependencies.Add(dependentBundle);

                    if (!bundle.BaseObject.bundleDependencies.Contains(dependentBundleBaseObject))
                        dependentBundle.ReferencedByBundlesExpanded.Add(bundle);
                }
            }
        }

        private void CollectAllAssets()
        {
            foreach (var bundle in Bundles.Values)
            {
                foreach (var baseAsset in bundle.BaseObject.explicitAssets)
                {
                    var asset = FindAssetByPath(baseAsset.name);
                    if (asset == null)
                    {
                        asset = new Asset
                        {
                            BaseObject = baseAsset,
                            Guid = GetUid(bundle.Name, baseAsset.name),
                            Name = baseAsset.name,
                            Size = baseAsset.size,
                            SizeFromObjects = baseAsset.sizeFromObjects,
                            SizeFromStreamedData = baseAsset.sizeFromStreamedData,
                            Address = baseAsset.address,
                            IncludedInBundle = bundle,
                            Labels = baseAsset.labels ?? new List<string>()
                        };
                        AssetsByGuid.Add(asset.Guid, asset);
                        AssetsByPath.Add(asset.Name, asset);
                    }
                    bundle.ExplicitAssets.Add(asset);
                    bundle.AllAssets.Add(asset);

                    asset.IncludedByBundle.Add(bundle);

                    foreach (var internalRef in baseAsset.internalReferences)
                    {
                        BuildLayoutParser.ExplicitAsset internalBaseAsset = null;
                        foreach (var file in bundle.BaseObject.files)
                        {
                            foreach (var a in file.assets)
                            {
                                if (!string.Equals(a.name, internalRef, System.StringComparison.OrdinalIgnoreCase))
                                    continue;
                                internalBaseAsset = a;
                                break;
                            }
                        }

                        if (internalBaseAsset == null)
                        {
                            Debug.LogError($"Could not find '{internalRef}'");
                            continue;
                        }

                        var internalAsset = FindAssetByUid(GetUid(bundle.Name, internalRef));
                        if (internalAsset == null)
                        {
                            internalAsset = new Asset()
                            {
                                BaseObject = internalBaseAsset,
                                Guid = GetUid(bundle.Name, internalRef),
                                Name = internalRef,
                                Size = internalBaseAsset.size,
                                SizeFromObjects = internalBaseAsset.sizeFromObjects,
                                SizeFromStreamedData = internalBaseAsset.sizeFromStreamedData,
                                IsEmbedded = true,
                                IncludedByAsset = asset,
                                IncludedInBundle = bundle
                            };

                            internalAsset.IncludedByBundle.Add(bundle);

                            AssetsByGuid.Add(internalAsset.Guid, internalAsset);
                            if (!AssetsByPath.ContainsKey(internalAsset.Name))
                                AssetsByPath.Add(internalAsset.Name, internalAsset);
                            bundle.AllAssets.Add(internalAsset);
                        }
                        asset.InternalReferences.Add(internalAsset);
                    }
                }
            }

            foreach (var asset in AssetsByGuid.Values)
            {
                foreach (var baseReference in asset.BaseObject.externalReferences)
                {
                    var reference = FindAssetByPath(baseReference);
                    if (reference == null)
                        continue;

                    asset.ExternalReferences.Add(reference);
                }
            }
        }

        private void FillGroups(BuildLayoutParser buildLayout)
        {
            foreach (var baseGroup in buildLayout.groups)
            {
                var group = new Group
                {
                    Name = baseGroup.name,
                    Size = baseGroup.size
                };
                Groups.Add(group);

                foreach (var baseBundle in baseGroup.bundles)
                {
                    var bundle = FindBundle(baseBundle.name);
                    if (bundle == null)
                        continue;

                    bundle.ReferencedByGroups.Add(group);
                    group.Archives.Add(bundle);
                }
            }
        }

        private Asset FindAssetByUid(string uid)
        {
            return AssetsByGuid.GetValueOrDefault(uid);
        }

        public Asset FindAssetByPath(string path)
        {
            return AssetsByPath.GetValueOrDefault(path);
        }

        public string GetBundleNameByAssetPath(string assetPath)
        {
            if (AssetsByPath.TryGetValue(assetPath, out var asset))
            {
                return asset.IncludedInBundle != null ? asset.IncludedInBundle.Name : string.Empty;
            }

            return null;
        }

        private Archive FindBundle(string bundleName)
        {
            return Bundles.GetValueOrDefault(bundleName);
        }
    }
}
