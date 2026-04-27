using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AddressablesInspector
{
    public class BundleLayoutService
    {
        public BuildLayoutProvider LoadedLayout { get; private set; }
        
        private readonly AnalysisService _context;
        
        public BundleLayoutService(AnalysisService context)
        {
            _context = context;
        }
        
        public void LoadBuildLayout(string path)
        {
            try
            {
                LoadedLayout = new BuildLayoutProvider(BuildLayoutParser.Load(path));
                _context.LayoutComparisonService.SetOriginalLayout(LoadedLayout);
                PerformGroupsAnalysis(LoadedLayout, _context, _context.Settings.RemoteDependencyStartupWarningThresholdBytes);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Error", $"BuildLayout Explorer cannot load the file '{path}'.\n\nError message:\n{e.Message}\n\nSee Console window for additional information.", "OK");
            }
        }

        public void ResetBuildLayout()
        {
            LoadedLayout = null;
            _context.LayoutComparisonService.SetOriginalLayout(null);
        }

        public bool LayoutLoaded => LoadedLayout != null;

        public void SortGroupsDescending()
        {
            if (LoadedLayout == null)
                return;
            
            LoadedLayout.Groups = LoadedLayout.Groups.OrderByDescending(x => x.TopWarning).ToList();
        }
        
        public static void PerformGroupsAnalysis(BuildLayoutProvider layout, AnalysisService context, long remoteDependencyStartupWarningThresholdBytes)
        {
            foreach (var group in layout.Groups)
            {
                foreach (var bundle in group.Archives)
                {
                    var allDeps = bundle.AllBundleDependencies;

                    var circularDeps = allDeps.Where(x => x.BundleDependencies.Contains(bundle)).ToList();
                    
                    if (circularDeps.Count > 0)
                    {
                        if (context != null)
                        {
                            var circularInfo = circularDeps.Aggregate(string.Empty, (current, circularDep) => current + $"[{circularDep.Name}]");

                            var recommendationMessage = context.Summary.AddRecommendation(group.Name,
                                $"Bundle {bundle.Name} of group {group.Name} has CIRCULAR dependencies with {circularInfo}",
                                4);
                            bundle.Recommendations.Add(recommendationMessage);
                            bundle.TrySetWarningLevel(4);
                        }
                    }
                }
            }

            foreach (var group in layout.Groups)
            {
                foreach (var bundle in group.Archives)
                {
                    if (bundle.AllAssets.Count == 0)
                    {
                        if (context != null)
                        {
                            var recommendationMessage = context.Summary.AddRecommendation(group.Name,
                                $"Bundle {bundle.Name} of group {group.Name} contains no assets",
                                4);
                            bundle.Recommendations.Add(recommendationMessage);
                            bundle.TrySetWarningLevel(4);
                        }
                    }
                    else
                    {
                        foreach (var asset in bundle.AllAssets)
                        {
                            if (asset.Name.Contains("builtin"))
                            {
                                if (context != null)
                                {
                                    var recommendationMessage = context.Summary.AddRecommendation(group.Name,
                                        $"Bundle {bundle.Name} of group {group.Name} contains builtin asset {asset.Name}. This might cause a duplicate with Unity builtin assets",
                                        1);
                                    bundle.Recommendations.Add(recommendationMessage);
                                }

                                bundle.TrySetWarningLevel(1);
                            }
                        }
                    }

                    var isCurrentRemote = BundleUtilities.IsBundleRemote(bundle.Name);

                    foreach (var bundleDependency in bundle.BundleDependencies)
                    {
                        var isDependencyRemote = BundleUtilities.IsBundleRemote(bundleDependency.Name);
                        if (!isCurrentRemote && isDependencyRemote)
                        {
                            if (context != null)
                            {
                                var recommendationMessage = context.Summary.AddRecommendation(group.Name,
                                    $"Built-In bundle {bundle.Name} of group {group.Name} directly (!) references remote bundle {bundleDependency.Name}",
                                    5);
                                bundle.Recommendations.Add(recommendationMessage);
                                bundle.TrySetWarningLevel(5);
                            }
                        }
                    }
                    
                    foreach (var expandedBundleDependency in bundle.ExpandedBundleDependencies)
                    {
                        var isDependencyRemote = BundleUtilities.IsBundleRemote(expandedBundleDependency.Name);
                        if (!isCurrentRemote && isDependencyRemote)
                        {
                            if (context != null)
                            {
                                var recommendationMessage = context.Summary.AddRecommendation(group.Name,
                                    $"Built-In bundle {bundle.Name} of group {group.Name} references remote bundle {expandedBundleDependency.Name}",
                                    4);
                                bundle.Recommendations.Add(recommendationMessage);
                            }

                            bundle.TrySetWarningLevel(4);
                        }
                    }

                    if (isCurrentRemote)
                    {
                        var isCurrentStartup = BundleUtilities.IsBundleStartup(bundle.Name);

                        if (isCurrentStartup)
                        {
                            var referencedRemoteSize = 0L;
                            var referencedBundlesList = string.Empty;
                        
                            foreach (var bundleDependency in bundle.AllBundleDependencies.OrderByDescending(x => x.Size))
                            {
                                var isDependencyRemote = BundleUtilities.IsBundleRemote(bundleDependency.Name);
                                var isDependencyStartup = BundleUtilities.IsBundleStartup(bundleDependency.Name);
                                
                                if (isDependencyRemote && !isDependencyStartup)
                                {
                                    referencedRemoteSize += bundleDependency.Size;
                                    referencedBundlesList += bundleDependency.Name 
                                                             + "[" + EditorUtility.FormatBytes(bundleDependency.Size) + "]; ";
                                    
                                    bundle.TrySetWarningLevel(1);
                                }
                            }

                            if (referencedRemoteSize > 0)
                            {
                                var warningLevel = referencedRemoteSize >= remoteDependencyStartupWarningThresholdBytes
                                    ? 3
                                    : 1;
                                
                                if (context != null)
                                {
                                    var recommendation = context.Summary.AddRecommendation(group.Name,
                                        $"Startup remote bundle {bundle.Name} of group {group.Name} references " +
                                        $"remote (non-startup) bundles with total size of {EditorUtility.FormatBytes(referencedRemoteSize)}. Bundles: {referencedBundlesList}", warningLevel);
                                    bundle.Recommendations.Add(recommendation);
                                }

                                bundle.TrySetWarningLevel(warningLevel);
                            }
                        }
                    }
                }

                if (group.Archives.Count > 0)
                {
                    group.TopWarning = group.Archives.Max(x => x.TopWarning);
                }
                else
                {
                    Debug.LogWarning($"Group {group.Name} contains no bundles");
                    group.TopWarning = 3;
                }
            }

            foreach (var pair in layout.AssetsByPath)
            {
                if (pair.Value.IncludedByBundle.Count(b => b != pair.Value.IncludedInBundle) > 0)
                {
                    pair.Value.TopWarning = Mathf.Max(pair.Value.IncludedByBundle
                        .Where(b => b != pair.Value.IncludedInBundle).Max(x => x.TopWarning), pair.Value.TopWarning);
                }
            }
        }
    }
}