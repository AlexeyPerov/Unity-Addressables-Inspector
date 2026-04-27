using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AddressablesInspector
{
    public class BundleLayoutBundlesSizeAndContentAnalysisDrawer : PartDrawerBase
    {
        private string _filterEntries;
        
        private Vector2 _entriesScroll;
        
        private readonly PaginationSettings _bundlesListOutputSettings = new()
        {
            PageToShow = 0,
            PageSize = 20
        };
        
        private readonly PaginationSettings _assetListOutputSettings = new()
        {
            PageToShow = 0,
            PageSize = 20
        };
                
        private BuildLayoutProvider.Archive _selectedEntry;
        private Vector2 _selectedEntryScroll;
        
        private readonly Dictionary<BuildLayoutProvider.Archive, ArchiveUIState> _archiveUIStates = new();
        private readonly Dictionary<BuildLayoutProvider.Asset, AssetUIState> _assetUIStates = new();
        
        private BundleLayoutService Service => Context.LayoutService;
        private BuildLayoutProvider Layout => Context.LayoutService.LoadedLayout;
        
        private bool _statsFoldout;
        private int _showType;
        private int _sortSizeType;

        private List<BuildLayoutProvider.Archive> _bundles;

        public override void OnSelected()
        {
            base.OnSelected();

            _bundles = Service.LayoutLoaded ? Service.LoadedLayout.Bundles.Values.ToList() : new List<BuildLayoutProvider.Archive>();

            if (_sortSizeType == 1)
            {
                _bundles = _bundles.OrderBy(x => x.Size).ToList();
            }
            else
            {
                _bundles = _bundles.OrderByDescending(x => x.Size).ToList();
                _sortSizeType = 0;
            }
        }

        public override void Draw()
        {
            if (!Service.LayoutLoaded)
            {
                GUIUtilities.DrawLabelAtCenterHorizontally("Please load BundleLayout in Setup tab", Color.white);
                return;
            }
            
            if (Service.LoadedLayout.Bundles == null)
            {
                GUIUtilities.DrawLabelAtCenterHorizontally("Error performing comparison. Please re-upload BundleLayout", Color.red);
                return;
            }
            
            if (Service.LoadedLayout.Bundles.Count == 0)
            {
                GUIUtilities.DrawLabelAtCenterHorizontally("No bundles found", Color.yellow);
                return;
            }

            if (_bundles == null)
                OnSelected();

            _statsFoldout = EditorGUILayout.Foldout(_statsFoldout, "Total Stats");
            if (_statsFoldout)
            {
                EditorGUI.indentLevel++;
                GUILayout.Label($"Total Size: {EditorUtility.FormatBytes(Layout.Bundles.Values.Sum(x => x.Size))}  |  Built-In: {EditorUtility.FormatBytes(Layout.TotalBuiltInSize)}  |  Remote: {EditorUtility.FormatBytes(Layout.TotalRemoteSize)}");
                EditorGUI.indentLevel--;
            }

            if (_selectedEntry != null)
            {
                DrawEntry(_selectedEntry);
                return;
            }

            DrawFilterSection();

            var filterLowered = _filterEntries != null ? _filterEntries.ToLowerInvariant() : string.Empty;
            
            var filteredEntries = !string.IsNullOrEmpty(_filterEntries)
                ? _bundles.Where(x => x.Name.ToLowerInvariant().Contains(filterLowered)).ToList()
                : _bundles;

            if (_showType == 1)
                filteredEntries = filteredEntries.Where(x => !BundleUtilities.IsBundleRemote(x.Name)).ToList();
            else if (_showType == 2)
                filteredEntries = filteredEntries.Where(x => BundleUtilities.IsBundleRemote(x.Name)).ToList();

            GUILayout.Label("Bundles found: " + _bundles.Count + ". " 
                            + (filteredEntries.Count != _bundles.Count
                                ? $"Showing: {filteredEntries.Count}" : string.Empty));

            GUIPaginationUtilities.DrawPagesWidget(filteredEntries.Count, _bundlesListOutputSettings);
            
            _entriesScroll = EditorGUILayout.BeginScrollView(_entriesScroll);
            
            EditorGUILayout.BeginVertical();

            for (var i = 0; i < filteredEntries.Count; i++)
            {
                if (!GUIPaginationUtilities.ShouldDrawItem(i, _bundlesListOutputSettings))
                {
                    continue;
                }

                var entry = filteredEntries[i];
                var isRemote = BundleUtilities.IsBundleRemote(entry.Name);

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                GUIUtilities.DrawColoredLabel(entry.Name, Color.white);
                GUILayout.Label(EditorUtility.FormatBytes(entry.Size), EditorStyles.miniLabel, GUILayout.Width(80));
                GUIUtilities.DrawColoredLabel(isRemote ? "[remote]" : "[built-in]", isRemote ? Color.cyan : Color.gray);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Details >>>", GUILayout.Width(100)))
                {
                    Select(entry);
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

        private AssetUIState GetAssetUIState(BuildLayoutProvider.Asset asset)
        {
            if (!_assetUIStates.TryGetValue(asset, out var uiState))
            {
                uiState = new AssetUIState();
                _assetUIStates[asset] = uiState;
            }
            return uiState;
        }

        private void Select(BuildLayoutProvider.Archive entry)
        {
            _selectedEntryScroll = Vector2.zero;
            _selectedEntry = entry;
            _assetListOutputSettings.PageToShow = 0;
            _assetListOutputSettings.SortingOption = 0;
        }

        private void DrawFilterSection()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter Bundles:");
            _filterEntries = GUILayout.TextField(_filterEntries, GUILayout.Width(550));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            
            var typePostfix = _showType switch
            {
                1 => "Built-in Only",
                2 => "Remote Only",
                _ => "All"
            };
            
            if (GUILayout.Button($"Type: {typePostfix}"))
            {
                _showType = _showType switch
                {
                    0 => 1,
                    1 => 2,
                    _ => 0
                };
            }

            var sortPostfix = _sortSizeType == 1 ? "Asc" : "Desc";
            
            if (GUILayout.Button($"Sorted by Size: {sortPostfix}"))
            {
                if (_sortSizeType == 1)
                {
                    _sortSizeType = 0;
                    _bundles = _bundles.OrderByDescending(x => x.Size).ToList();
                }
                else
                {
                    _sortSizeType = 1;
                    _bundles = _bundles.OrderBy(x => x.Size).ToList();
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        
        private void DrawEntry(BuildLayoutProvider.Archive entry)
        {
            _selectedEntryScroll = EditorGUILayout.BeginScrollView(_selectedEntryScroll);

            DrawBreadcrumb("Back", entry.Name, () => { _selectedEntry = null; });
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Size: {EditorUtility.FormatBytes(entry.Size)}  |  Assets: {entry.ExplicitAssets.Count}/{entry.AllAssets.Count}", EditorStyles.boldLabel);
            
            var sortedTitle = _assetListOutputSettings.SortingOption switch
            {
                1 => "Size Desc",
                2 => "Size Asc",
                3 => "Refs Desc",
                4 => "Refs Asc",
                _ => "Unsorted"
            };

            if (GUILayout.Button($"Sort: {sortedTitle}", GUILayout.Width(100)))
            {
                if (_assetListOutputSettings.SortingOption == 1)
                {
                    _assetListOutputSettings.SortingOption = 2;
                    entry.AllAssets = entry.AllAssets.OrderBy(x => x.Size).ToList();
                }
                else if (_assetListOutputSettings.SortingOption == 2)
                {
                    _assetListOutputSettings.SortingOption = 3;
                    entry.AllAssets = entry.AllAssets.OrderByDescending(x => x.ExternalReferences.Count).ToList();
                }
                else if (_assetListOutputSettings.SortingOption == 3)
                {
                    _assetListOutputSettings.SortingOption = 4;
                    entry.AllAssets = entry.AllAssets.OrderBy(x => x.ExternalReferences.Count).ToList();
                }
                else
                {
                    _assetListOutputSettings.SortingOption = 1;
                    entry.AllAssets = entry.AllAssets.OrderByDescending(x => x.Size).ToList();
                }
            }

            GUILayout.Label("Search:");
            var bundleUIState = GetArchiveUIState(entry);
            bundleUIState.SearchFilter = GUILayout.TextField(bundleUIState.SearchFilter, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            var searchLowered = !string.IsNullOrEmpty(bundleUIState.SearchFilter)
                ? bundleUIState.SearchFilter.ToLowerInvariant()
                : string.Empty;
            
            GUILayout.Label($"Assets ({entry.AllAssets.Count}):");
            
            GUIPaginationUtilities.DrawPagesWidget(entry.AllAssets.Count, _assetListOutputSettings);

            for (var i = 0; i < entry.AllAssets.Count; i++)
            {
                if (!GUIPaginationUtilities.ShouldDrawItem(i, _assetListOutputSettings))
                {
                    continue;
                }

                var asset = entry.AllAssets[i];
                
                if (!string.IsNullOrEmpty(searchLowered))
                {
                    var assetFit = asset.Name.ToLowerInvariant().Contains(searchLowered);
                    var refsFit = asset.ExternalReferences.Any(x => x.Name.ToLowerInvariant().Contains(searchLowered) 
                                                                    || (x.IncludedInBundle != null && x.IncludedInBundle.Name.ToLowerInvariant().Contains(searchLowered)));

                    if (!assetFit && !refsFit)
                    {
                        continue;
                    }
                }
                
                GUIUtilities.HorizontalLine();

                EditorGUILayout.BeginHorizontal();
                GUIUtilities.DrawColoredLabel($"{asset.Name}  [{EditorUtility.FormatBytes(asset.Size)}]  Explicit:{entry.ExplicitAssets.Contains(asset)}", Color.white);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                if (asset.IncludedByAsset != null)
                {
                    GUIUtilities.DrawColoredLabel($"  Included By: {asset.IncludedByAsset.Name}", Color.gray);
                }
                
                DrawReferencedByBundlesSection(asset);
                DrawExternalReferencesSection(asset);
            }

            GUILayout.FlexibleSpace();
            
            GUIUtilities.HorizontalLine();
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawExternalReferencesSection(BuildLayoutProvider.Asset asset)
        {
            if (asset.ExternalReferences.Count <= 0) 
                return;
            
            var assetUIState = GetAssetUIState(asset);

            assetUIState.ExternalRefsFoldout = EditorGUILayout.Foldout(assetUIState.ExternalRefsFoldout,
                $"External References ({asset.ExternalReferences.Count})" + (assetUIState.ShowExternalReferencesToRemoteOnly ? " [remote only]" : ""));

            if (!assetUIState.ExternalRefsFoldout) 
                return;

            EditorGUI.indentLevel++;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(assetUIState.ShowExternalReferencesToRemoteOnly
                    ? "Showing: Remote Only"
                    : "Showing: All", GUILayout.Width(150)))
            {
                assetUIState.ShowExternalReferencesToRemoteOnly = !assetUIState.ShowExternalReferencesToRemoteOnly;
            }

            GUILayout.Label("Filter:");
            assetUIState.SearchFilter = GUILayout.TextField(assetUIState.SearchFilter, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            var filteredRefs = new List<(BuildLayoutProvider.Asset reference, bool isIncludedInBundle, string includedInBundleName, bool isRemote, int warning)>();

            foreach (var reference in asset.ExternalReferences)
            {
                var isIncludedInBundle = reference.IncludedInBundle != null &&
                                         reference.IncludedInBundle != asset.IncludedInBundle;
                var includedInBundleName =
                    isIncludedInBundle ? reference.IncludedInBundle.Name : string.Empty;
                var isRemote = reference.IncludedInBundle != null &&
                               BundleUtilities.IsBundleRemote(reference.IncludedInBundle.Name);

                if (assetUIState.ShowExternalReferencesToRemoteOnly && !isRemote)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(assetUIState.SearchFilter))
                {
                    var loweredFilter = assetUIState.SearchFilter.ToLowerInvariant();

                    if (!reference.Name.ToLowerInvariant().Contains(loweredFilter)
                        && !includedInBundleName.ToLowerInvariant().Contains(loweredFilter))
                    {
                        continue;
                    }
                }

                var bundleWarning = reference.IncludedInBundle?.TopWarning ?? 0;
                var warning = Mathf.Max(bundleWarning, reference.TopWarning);

                filteredRefs.Add((reference, isIncludedInBundle, includedInBundleName, isRemote, warning));
            }

            if (filteredRefs.Count > 0)
            {
                var refsScrollHeight = Mathf.Min(filteredRefs.Count * 44 + 20, 300);
                assetUIState.ExternalRefsScroll = EditorGUILayout.BeginScrollView(assetUIState.ExternalRefsScroll, GUILayout.Height(refsScrollHeight));
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                for (var r = 0; r < filteredRefs.Count; r++)
                {
                    var (reference, isIncludedInBundle, includedInBundleName, isRemote, warning) = filteredRefs[r];

                    EditorGUILayout.BeginHorizontal();
                    GUIUtilities.DrawColoredLabelByWarning($"  {r + 1}. ", warning);
                    GUIUtilities.DrawColoredLabelByWarning(
                        $" {reference.Name}  [{(isRemote ? "remote" : "built-in")}]", warning);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();

                    if (reference.IncludedInBundle != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUIUtilities.DrawColoredLabelByWarning(
                            $"     in: {reference.IncludedInBundle.Name}  Dir:{reference.IncludedInBundle.BundleDependencies.Count} Exp:{reference.IncludedInBundle.ExpandedBundleDependencies.Count}",
                            warning);
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                    }

                    GUILayout.Space(2);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndScrollView();
            }

            EditorGUI.indentLevel--;
        }

        private void DrawReferencedByBundlesSection(BuildLayoutProvider.Asset asset)
        {
            var totalBundleReferences = asset.IncludedByBundle.Count(x => x != asset.IncludedInBundle);

            if (totalBundleReferences <= 0)
                return;

            var assetUIState = GetAssetUIState(asset);
            var bundlesToShow = asset.IncludedByBundle.Where(x => x != asset.IncludedInBundle).ToList();
            var topWarning = bundlesToShow.Max(x => x.TopWarning);
                
            assetUIState.ReferencedByBundlesFoldout = EditorGUILayout.Foldout(assetUIState.ReferencedByBundlesFoldout,
                $"Referenced by Bundles ({totalBundleReferences})");

            if (assetUIState.ReferencedByBundlesFoldout)
            {
                EditorGUI.indentLevel++;
                foreach (var archive in bundlesToShow)
                {
                    GUIUtilities.DrawColoredLabelByWarning($">>> {archive.Name}  [{EditorUtility.FormatBytes(archive.Size)}]", archive.TopWarning);
                }
                EditorGUI.indentLevel--;
            }
        }

        private static void DrawBreadcrumb(string parent, string current, Action onBack)
        {
            if (GUILayout.Button($"< {parent}", EditorStyles.miniButton, GUILayout.Width(100)))
            {
                onBack();
            }

            GUILayout.Label(current);
        }
    }
}
