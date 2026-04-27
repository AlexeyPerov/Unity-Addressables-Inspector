using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AddressablesInspector
{
    public class BundleLayoutGroupsAnalysisPartDrawer : PartDrawerBase
    {
        private string _filterGroups;
        
        private Vector2 _groupsScroll;
        private readonly PaginationSettings _groupOutputSettings = new()
        {
            PageToShow = 0,
            PageSize = 20
        };
                
        private BuildLayoutProvider.Group _selectedGroup;
        private Vector2 _selectedGroupScroll;
        
        private readonly Dictionary<BuildLayoutProvider.Archive, ArchiveUIState> _archiveUIStates = new();
        private readonly Dictionary<BuildLayoutProvider.Archive, bool> _bundleRecommendationsFoldouts = new();
        private readonly Dictionary<BuildLayoutProvider.Archive, bool> _bundleDetailsFoldouts = new();
        private readonly Dictionary<BuildLayoutProvider.Archive, bool> _bundleDepsFoldouts = new();
        private readonly Dictionary<BuildLayoutProvider.Archive, bool> _bundleExpandedDepsFoldouts = new();

        public override void OnSelected()
        {
            base.OnSelected();
            Context.LayoutService.SortGroupsDescending();
        }

        public override void Draw()
        {
            if (_selectedGroup != null)
            {
                DrawGroup(_selectedGroup);
                return;
            }
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter Groups:");
            _filterGroups = GUILayout.TextField(_filterGroups, GUILayout.Width(550));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            var filterLowered = _filterGroups != null ? _filterGroups.ToLowerInvariant() : string.Empty;
            
            var filteredGroups = !string.IsNullOrEmpty(_filterGroups)
                ? Context.LayoutService.LoadedLayout.Groups.Where(x => x.Name.ToLowerInvariant().Contains(filterLowered)).ToList()
                : Context.LayoutService.LoadedLayout.Groups;
            
            GUILayout.Label("Groups found: " + Context.LayoutService.LoadedLayout.Groups.Count + ". " 
                            + (filteredGroups.Count != Context.LayoutService.LoadedLayout.Groups.Count 
                                ? $"Showing: {filteredGroups.Count}" : string.Empty));
            
            GUIPaginationUtilities.DrawPagesWidget(filteredGroups.Count, _groupOutputSettings);
            
            _groupsScroll = EditorGUILayout.BeginScrollView(_groupsScroll);
            
            EditorGUILayout.BeginVertical();

            for (var i = 0; i < filteredGroups.Count; i++)
            {
                if (!GUIPaginationUtilities.ShouldDrawItem(i, _groupOutputSettings))
                {
                    continue;
                }

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                var group = filteredGroups[i];

                GUILayout.Label($"{i + 1}.", GUILayout.Width(30));
                GUIUtilities.DrawColoredLabelByWarning(group.Name, group.TopWarning, 350);
                GUILayout.Label(EditorUtility.FormatBytes(group.Size), EditorStyles.miniLabel, GUILayout.Width(80));
                GUILayout.Label($"{group.Archives.Count} bundles", EditorStyles.miniLabel, GUILayout.Width(80));

                if (group.TopWarning > 0)
                {
                    var tag = GUIUtilities.GetSeverityTag(group.TopWarning);
                    GUIUtilities.DrawColoredLabelByWarning(tag, group.TopWarning);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Details >>>", GUILayout.Width(100)))
                {
                    _selectedGroupScroll = Vector2.zero;
                    _selectedGroup = group;
                }

                EditorGUILayout.EndHorizontal();
            }
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }

        private ArchiveUIState GetArchiveUIState(BuildLayoutProvider.Archive archive)
        {
            if (!_archiveUIStates.TryGetValue(archive, out var uiState))
            {
                uiState = new ArchiveUIState();
                _archiveUIStates[archive] = uiState;
            }
            return uiState;
        }

        private bool GetFoldout(Dictionary<BuildLayoutProvider.Archive, bool> dict, BuildLayoutProvider.Archive key)
        {
            dict.TryGetValue(key, out var val);
            return val;
        }

        private void SetFoldout(Dictionary<BuildLayoutProvider.Archive, bool> dict, BuildLayoutProvider.Archive key, bool val)
        {
            dict[key] = val;
        }

        private void DrawGroup(BuildLayoutProvider.Group group)
        {
            _selectedGroupScroll = EditorGUILayout.BeginScrollView(_selectedGroupScroll);

            DrawBreadcrumb("Back", group.Name, () => { _selectedGroup = null; });

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(
                $"Size: {EditorUtility.FormatBytes(group.Size)}  |  Bundles: {group.Archives.Count}  |  Startup: {BundleUtilities.IsBundleStartup(group.Name)}",
                EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (group.Archives.Count == 0)
            {
                GUIUtilities.DrawColoredLabel("Group is empty", Color.red);
            }
            else
            {
                for (var i = 0; i < group.Archives.Count; i++)
                {
                    var bundle = group.Archives[i];
                    var currentBuiltIn = !BundleUtilities.IsBundleRemote(bundle.Name);

                    if (group.Archives.Count == 1 && !_archiveUIStates.ContainsKey(bundle))
                        SetFoldout(_bundleDetailsFoldouts, bundle, true);

                    var detailsOpen = GetFoldout(_bundleDetailsFoldouts, bundle);
                    var newDetailsOpen = EditorGUILayout.Foldout(detailsOpen,
                        $"{bundle.Name}  [{EditorUtility.FormatBytes(bundle.Size)}]  {(currentBuiltIn ? "[built-in]" : "[remote]")}  {GUIUtilities.GetSeverityTag(bundle.TopWarning)}");
                    SetFoldout(_bundleDetailsFoldouts, bundle, newDetailsOpen);

                    if (!newDetailsOpen)
                        continue;

                    EditorGUI.indentLevel++;

                    GUILayout.Label(
                        $"Size: {EditorUtility.FormatBytes(bundle.Size)}  |  Compression: {bundle.Compression}  |  Assets: {bundle.ExplicitAssets.Count}/{bundle.AllAssets.Count}  |  Built-In by Unity: {bundle.IsBuiltin}  |  Startup: {BundleUtilities.IsBundleStartup(bundle.Name)}");

                    if (bundle.Recommendations.Count > 0)
                    {
                        var recOpen = GetFoldout(_bundleRecommendationsFoldouts, bundle);
                        var newRecOpen = EditorGUILayout.Foldout(recOpen,
                            $"Warnings ({bundle.Recommendations.Count})");
                        SetFoldout(_bundleRecommendationsFoldouts, bundle, newRecOpen);

                        if (newRecOpen)
                        {
                            EditorGUI.indentLevel++;
                            var warnings = bundle.Recommendations
                                .Where(x => x.WarningLevel >= Context.Settings.MinWarningLevelToShow).ToList();
                            for (var w = 0; w < warnings.Count; w++)
                            {
                                EditorGUILayout.BeginHorizontal();
                                GUIUtilities.DrawColoredLabelByWarning($"  {w + 1}. ", warnings[w].WarningLevel);
                                GUIUtilities.DrawColoredLabelByWarning(warnings[w].Message, warnings[w].WarningLevel);
                                GUILayout.FlexibleSpace();
                                EditorGUILayout.EndHorizontal();
                                GUILayout.Space(2);
                            }

                            EditorGUI.indentLevel--;
                        }
                    }

                    var directDeps = bundle.BundleDependenciesInfos;
                    if (directDeps.Count > 0)
                    {
                        var depsOpen = GetFoldout(_bundleDepsFoldouts, bundle);
                        var newDepsOpen = EditorGUILayout.Foldout(depsOpen,
                            $"Direct Dependencies ({directDeps.Count})");
                        SetFoldout(_bundleDepsFoldouts, bundle, newDepsOpen);

                        if (newDepsOpen)
                        {
                            EditorGUI.indentLevel++;
                            foreach (var dep in directDeps.OrderByDescending(x =>
                                             BundleUtilities.IsBundleRemote(x.DependentBundle.Name))
                                         .ThenByDescending(x => x.DependentBundle.Size))
                            {
                                DrawBundleDependencyInfo(dep, currentBuiltIn, true);
                            }

                            EditorGUI.indentLevel--;
                        }
                    }

                    var expandedDeps = bundle.ExpandedBundleDependenciesInfos;
                    if (expandedDeps.Count > 0)
                    {
                        var expOpen = GetFoldout(_bundleExpandedDepsFoldouts, bundle);
                        var newExpOpen = EditorGUILayout.Foldout(expOpen,
                            $"Expanded Dependencies ({expandedDeps.Count})");
                        SetFoldout(_bundleExpandedDepsFoldouts, bundle, newExpOpen);

                        if (newExpOpen)
                        {
                            EditorGUI.indentLevel++;
                            foreach (var dep in expandedDeps.OrderByDescending(x =>
                                             BundleUtilities.IsBundleRemote(x.BundleFromExpandedDependencies.Name))
                                         .ThenByDescending(x => x.BundleFromExpandedDependencies.Size))
                            {
                                DrawBundleExpandedDependencyInfo(dep, currentBuiltIn, true);
                            }

                            EditorGUI.indentLevel--;
                        }
                    }

                    var bundleUIState = GetArchiveUIState(bundle);
                    if (bundle.ReferencedByBundlesDirectly.Count > 0)
                    {
                        bundleUIState.ReferencedByBundlesDirectlyFoldout = EditorGUILayout.Foldout(
                            bundleUIState.ReferencedByBundlesDirectlyFoldout,
                            $"Referenced By Bundles Directly ({bundle.ReferencedByBundlesDirectly.Count})");

                        if (bundleUIState.ReferencedByBundlesDirectlyFoldout)
                        {
                            EditorGUI.indentLevel++;
                            foreach (var bundleDependency in bundle.ReferencedByBundlesDirectly
                                         .OrderByDescending(x => BundleUtilities.IsBundleRemote(x.Name))
                                         .ThenByDescending(x => x.Size))
                            {
                                DrawBundleDependency(bundleDependency, currentBuiltIn, false);
                            }

                            EditorGUI.indentLevel--;
                        }
                    }

                    if (bundle.ReferencedByBundlesExpanded.Count > 0)
                    {
                        bundleUIState.ReferencedByBundlesExpandedFoldout = EditorGUILayout.Foldout(
                            bundleUIState.ReferencedByBundlesExpandedFoldout,
                            $"Referenced By Bundles Expanded ({bundle.ReferencedByBundlesExpanded.Count})");

                        if (bundleUIState.ReferencedByBundlesExpandedFoldout)
                        {
                            EditorGUI.indentLevel++;
                            foreach (var bundleDependency in bundle.ReferencedByBundlesExpanded
                                         .OrderByDescending(x => BundleUtilities.IsBundleRemote(x.Name))
                                         .ThenByDescending(x => x.Size))
                            {
                                DrawBundleDependency(bundleDependency, currentBuiltIn, false);
                            }

                            EditorGUI.indentLevel--;
                        }
                    }

                    if (bundle.ReferencedByGroups.Count > 0)
                    {
                        GUIUtilities.DrawColoredLabel("Referenced By Groups:", Color.magenta);
                        EditorGUI.indentLevel++;
                        foreach (var groupDependency in bundle.ReferencedByGroups.OrderByDescending(x => x.Size))
                        {
                            DrawGroupDependency(groupDependency);
                        }

                        EditorGUI.indentLevel--;
                    }

                    EditorGUI.indentLevel--;

                    GUIUtilities.HorizontalLine();
                }
        }

        EditorGUILayout.EndScrollView();
        }

        private static void DrawBreadcrumb(string parent, string current, System.Action onBack)
        {
            if (GUILayout.Button($"< {parent}", EditorStyles.miniButton, GUILayout.Width(100)))
            {
                onBack();
            }
            
            GUILayout.Label(current);
        }
        
        private static void DrawBundleDependencyInfo(BuildLayoutProvider.BundleDependencyInfo bundleDependencyInfo, bool currentBuiltIn, bool increaseWarningForRemote)
        {
            var bundleDependency = bundleDependencyInfo.DependentBundle;
            GUIUtilities.DrawColoredLabelByWarning(
                DependencyFormatter.FormatDependencyLine(bundleDependency, currentBuiltIn, increaseWarningForRemote), 
                increaseWarningForRemote && currentBuiltIn && BundleUtilities.IsBundleRemote(bundleDependency.Name) ? 3 : bundleDependency.TopWarning);

            if (bundleDependencyInfo.AssetsCrossReferences.Count > 0)
            {
                bundleDependencyInfo.Foldout = EditorGUILayout.Foldout(bundleDependencyInfo.Foldout, "  reasons:");

                if (bundleDependencyInfo.Foldout)
                {
                    EditorGUI.indentLevel++;
                    foreach (var (key, value) in bundleDependencyInfo.AssetsCrossReferences)
                    {
                        if (value.Count == 0)
                            continue;

                        GUIUtilities.DrawAssetButton(key.Name);
                        GUIUtilities.DrawColoredLabel(" uses:", Color.yellow);

                        foreach (var asset in value)
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUIUtilities.DrawColoredLabel(" - ", Color.yellow);
                            GUIUtilities.DrawAssetButton(asset.Name);
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }
        }
        
        private static void DrawBundleExpandedDependencyInfo(BuildLayoutProvider.BundleExpandedDependencyInfo bundleDependencyInfo, bool currentBuiltIn, bool increaseWarningForRemote)
        {
            var bundleDependency = bundleDependencyInfo.BundleFromExpandedDependencies;
            GUIUtilities.DrawColoredLabelByWarning(
                DependencyFormatter.FormatDependencyLine(bundleDependency, currentBuiltIn, increaseWarningForRemote), 
                increaseWarningForRemote && currentBuiltIn && BundleUtilities.IsBundleRemote(bundleDependency.Name) ? 3 : bundleDependency.TopWarning);

            bundleDependencyInfo.Foldout = EditorGUILayout.Foldout(bundleDependencyInfo.Foldout, "  reasons:");

            if (bundleDependencyInfo.Foldout)
            {
                EditorGUI.indentLevel++;
                foreach (var reasonBundle in bundleDependencyInfo.BundlesFromDirectDependencies)
                {
                    GUIUtilities.DrawColoredLabel($"- referenced by: {reasonBundle.Name}", Color.yellow);
                }
                EditorGUI.indentLevel--;
            }
        }

        public static void DrawBundleDependency(BuildLayoutProvider.Archive bundleDependency, bool currentBuiltIn, bool increaseWarningForRemote)
        {
            GUIUtilities.DrawColoredLabelByWarning(
                DependencyFormatter.FormatDependencyLine(bundleDependency, currentBuiltIn, increaseWarningForRemote), 
                increaseWarningForRemote && currentBuiltIn && BundleUtilities.IsBundleRemote(bundleDependency.Name) ? 3 : bundleDependency.TopWarning);
        }

        public static void DrawGroupDependency(BuildLayoutProvider.Group groupDependency)
        {
            GUIUtilities.DrawColoredLabelByWarning(
                DependencyFormatter.FormatGroupDependencyLine(groupDependency), 
                groupDependency.TopWarning);
        }
    }
}
