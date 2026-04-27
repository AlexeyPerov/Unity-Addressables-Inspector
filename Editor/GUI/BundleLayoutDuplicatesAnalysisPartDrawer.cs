using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AddressablesInspector
{
    public class BundleLayoutDuplicatesAnalysisPartDrawer : PartDrawerBase
    {
        private enum DuplicateReason
        {
            ExplicitInclude,
            DependencyPullIn,
            Mixed
        }

        private struct DuplicateEntry
        {
            public string AssetPath;
            public BuildLayoutProvider.Asset Asset;
            public List<BuildLayoutProvider.Archive> Bundles;
            public long WastedSize;
            public DuplicateReason Reason;
            public string SuggestedFix;
        }

        private List<DuplicateEntry> _duplicates;
        private string _filterEntries;
        private Vector2 _entriesScroll;
        private int _sortType;

        private readonly PaginationSettings _paginationSettings = new()
        {
            PageToShow = 0,
            PageSize = 20
        };

        private int _selectedDuplicateIndex = -1;
        private Vector2 _selectedEntryScroll;
        
        private readonly Dictionary<BuildLayoutProvider.Asset, AssetUIState> _assetUIStates = new();

        private AssetUIState GetAssetUIState(BuildLayoutProvider.Asset asset)
        {
            if (!_assetUIStates.TryGetValue(asset, out var uiState))
            {
                uiState = new AssetUIState();
                _assetUIStates[asset] = uiState;
            }
            return uiState;
        }

        private BuildLayoutProvider Layout => Context.LayoutService.LoadedLayout;

        public override void OnSelected()
        {
            base.OnSelected();
            RebuildDuplicates();
        }

        public override void Draw()
        {
            if (Context.LayoutService.LoadedLayout == null)
            {
                GUIUtilities.DrawLabelAtCenterHorizontally("Please load BundleLayout in Setup tab", Color.white);
                return;
            }

            if (_duplicates == null || _duplicates.Count == 0)
            {
                if (_duplicates == null)
                    RebuildDuplicates();

                if (_duplicates == null || _duplicates.Count == 0)
                {
                    GUIUtilities.DrawLabelAtCenterHorizontally("No duplicate assets found", Color.green);
                    return;
                }
            }

            if (_selectedDuplicateIndex >= 0 && _selectedDuplicateIndex < _duplicates.Count)
            {
                DrawDuplicateDetail(_duplicates[_selectedDuplicateIndex]);
                return;
            }

            var totalWasted = _duplicates.Sum(x => x.WastedSize);
            GUIUtilities.DrawColoredLabel($"Total wasted by duplicates: {EditorUtility.FormatBytes(totalWasted)} across {_duplicates.Count} assets", Color.yellow);

            DrawFilterSection();

            var filterLowered = _filterEntries != null ? _filterEntries.ToLowerInvariant() : string.Empty;

            var filteredDuplicates = !string.IsNullOrEmpty(_filterEntries)
                ? _duplicates.Where(x => x.AssetPath.ToLowerInvariant().Contains(filterLowered)).ToList()
                : _duplicates;

            GUILayout.Label($"Duplicates: {_duplicates.Count}. " +
                            (filteredDuplicates.Count != _duplicates.Count
                                ? $"Showing: {filteredDuplicates.Count}"
                                : string.Empty));

            GUIPaginationUtilities.DrawPagesWidget(filteredDuplicates.Count, _paginationSettings);

            _entriesScroll = EditorGUILayout.BeginScrollView(_entriesScroll);

            EditorGUILayout.BeginVertical();

            for (var i = 0; i < filteredDuplicates.Count; i++)
            {
                if (!GUIPaginationUtilities.ShouldDrawItem(i, _paginationSettings))
                {
                    continue;
                }

                var entry = filteredDuplicates[i];

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                GUIUtilities.DrawColoredLabel(System.IO.Path.GetFileName(entry.AssetPath), Color.white);
                GUILayout.Label($"{entry.Bundles.Count} bundles", EditorStyles.miniLabel, GUILayout.Width(80));
                GUILayout.Label(EditorUtility.FormatBytes(entry.WastedSize), EditorStyles.miniLabel, GUILayout.Width(80));

                var reasonTag = GetReasonTag(entry.Reason);
                var reasonColor = GetReasonColor(entry.Reason);
                if (!string.IsNullOrEmpty(reasonTag))
                    GUIUtilities.DrawColoredLabel(reasonTag, reasonColor);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Details >>>", GUILayout.Width(100)))
                {
                    SelectDuplicate(i);
                }

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }

        private static string GetReasonTag(DuplicateReason reason)
        {
            return reason switch
            {
                DuplicateReason.ExplicitInclude => "[Explicit Include]",
                DuplicateReason.DependencyPullIn => "[Dependency Pull-in]",
                DuplicateReason.Mixed => "[Mixed]",
                _ => ""
            };
        }

        private void RebuildDuplicates()
        {
            _duplicates = new List<DuplicateEntry>();

            if (Layout == null)
                return;

            var seenPaths = new HashSet<string>();

            foreach (var asset in Layout.AssetsByPath.Values)
            {
                if (asset.IncludedByBundle.Count < 2)
                    continue;

                var wastedSize = asset.Size * (asset.IncludedByBundle.Count - 1);
                var reason = DetermineReason(asset);
                var fix = BuildSuggestedFix(asset, reason);

                _duplicates.Add(new DuplicateEntry
                {
                    AssetPath = asset.Name,
                    Asset = asset,
                    Bundles = asset.IncludedByBundle.ToList(),
                    WastedSize = wastedSize,
                    Reason = reason,
                    SuggestedFix = fix
                });

                seenPaths.Add(asset.Name);
            }

            var byName = new Dictionary<string, List<BuildLayoutProvider.Asset>>();
            foreach (var asset in Layout.AssetsByGuid.Values)
            {
                if (!byName.TryGetValue(asset.Name, out var list))
                {
                    list = new List<BuildLayoutProvider.Asset>();
                    byName.Add(asset.Name, list);
                }

                list.Add(asset);
            }

            foreach (var pair in byName)
            {
                if (pair.Value.Count < 2)
                    continue;

                if (seenPaths.Contains(pair.Key))
                    continue;

                var bundles = new List<BuildLayoutProvider.Archive>();
                long wastedSize = 0;

                foreach (var asset in pair.Value)
                {
                    if (asset.IncludedInBundle != null && !bundles.Contains(asset.IncludedInBundle))
                    {
                        bundles.Add(asset.IncludedInBundle);
                        wastedSize += asset.Size;
                    }
                }

                if (bundles.Count < 2)
                    continue;

                var reason = DuplicateReason.DependencyPullIn;
                var representative = pair.Value.First();
                var fix = BuildGroupFixForGuidDuplicates(pair.Value, bundles);

                _duplicates.Add(new DuplicateEntry
                {
                    AssetPath = pair.Key,
                    Asset = representative,
                    Bundles = bundles,
                    WastedSize = wastedSize,
                    Reason = reason,
                    SuggestedFix = fix
                });
            }

            _duplicates = _duplicates.OrderByDescending(x => x.WastedSize).ToList();
            _sortType = 0;
        }

        private DuplicateReason DetermineReason(BuildLayoutProvider.Asset asset)
        {
            var explicitCount = 0;
            var pulledInCount = 0;

            foreach (var bundle in asset.IncludedByBundle)
            {
                if (bundle.ExplicitAssets.Contains(asset))
                    explicitCount++;
                else
                    pulledInCount++;
            }

            if (explicitCount > 0 && pulledInCount > 0)
                return DuplicateReason.Mixed;
            if (explicitCount > 1)
                return DuplicateReason.ExplicitInclude;
            return DuplicateReason.DependencyPullIn;
        }

        private static string BuildSuggestedFix(BuildLayoutProvider.Asset asset, DuplicateReason reason)
        {
            switch (reason)
            {
                case DuplicateReason.ExplicitInclude:
                {
                    var groups = asset.IncludedByBundle
                        .SelectMany(b => b.ReferencedByGroups)
                        .Select(g => g.Name)
                        .Distinct()
                        .ToList();
                    return $"Move asset to a shared bundle or assign it to a single group. " +
                           $"Currently referenced by groups: {string.Join(", ", groups)}. " +
                           $"Consider creating a shared group for common assets.";
                }
                case DuplicateReason.DependencyPullIn:
                {
                    var parentAssets = asset.IncludedByBundle
                        .SelectMany(b => b.ExplicitAssets)
                        .Where(a => a.InternalReferences.Contains(asset) || a.ExternalReferences.Contains(asset))
                        .Select(a => Path.GetFileName(a.Name))
                        .Distinct()
                        .ToList();
                    return $"Asset is pulled into multiple bundles as a dependency of: {string.Join(", ", parentAssets)}. " +
                           $"Consider bundling these parent assets together, or move this dependency to a shared bundle.";
                }
                case DuplicateReason.Mixed:
                {
                    var groups = asset.IncludedByBundle
                        .SelectMany(b => b.ReferencedByGroups)
                        .Select(g => g.Name)
                        .Distinct()
                        .ToList();
                    return $"Asset is both explicitly included and pulled as a dependency. " +
                           $"Remove explicit duplicate includes and consider a shared bundle. " +
                           $"Groups: {string.Join(", ", groups)}.";
                }
                default:
                    return "";
            }
        }

        private static string BuildGroupFixForGuidDuplicates(List<BuildLayoutProvider.Asset> assets, List<BuildLayoutProvider.Archive> bundles)
        {
            var groups = bundles
                .SelectMany(b => b.ReferencedByGroups)
                .Select(g => g.Name)
                .Distinct()
                .ToList();
            return $"Same-named asset appears in {bundles.Count} bundles across groups: {string.Join(", ", groups)}. " +
                   $"Check if these are truly different assets or if the same asset is assigned to multiple Addressable groups. " +
                   $"Consolidate into a single group if possible.";
        }

        private void SelectDuplicate(int index)
        {
            _selectedDuplicateIndex = index;
            _selectedEntryScroll = Vector2.zero;
        }

        private void DrawFilterSection()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter Duplicates:");
            _filterEntries = GUILayout.TextField(_filterEntries, GUILayout.Width(550));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            var sortLabel = _sortType switch
            {
                0 => "Wasted Size: Desc",
                1 => "Wasted Size: Asc",
                2 => "Count: Desc",
                3 => "Count: Asc",
                _ => "Unsorted"
            };

            if (GUILayout.Button($"Sort: {sortLabel}"))
            {
                _sortType = _sortType switch
                {
                    0 => 1,
                    1 => 2,
                    2 => 3,
                    _ => 0
                };

                _duplicates = _sortType switch
                {
                    0 => _duplicates.OrderByDescending(x => x.WastedSize).ToList(),
                    1 => _duplicates.OrderBy(x => x.WastedSize).ToList(),
                    2 => _duplicates.OrderByDescending(x => x.Bundles.Count).ToList(),
                    3 => _duplicates.OrderBy(x => x.Bundles.Count).ToList(),
                    _ => _duplicates
                };
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawDuplicateDetail(DuplicateEntry entry)
        {
            _selectedEntryScroll = EditorGUILayout.BeginScrollView(_selectedEntryScroll);

            DrawBreadcrumb("Back", Path.GetFileName(entry.AssetPath), () => { _selectedDuplicateIndex = -1; });

            GUIUtilities.DrawAssetButton(entry.AssetPath);

            EditorGUILayout.BeginHorizontal();
            GUIUtilities.DrawColoredLabel($"Size: {EditorUtility.FormatBytes(entry.Asset.Size)}  |  Duplicates: {entry.Bundles.Count}  |  Wasted: {EditorUtility.FormatBytes(entry.WastedSize)}", Color.yellow);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            var reasonTag = GetReasonTag(entry.Reason);
            GUIUtilities.DrawColoredLabel($"Reason: {reasonTag}", GetReasonColor(entry.Reason));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Label("Contained in bundles:");
            EditorGUI.indentLevel++;

            for (var i = 0; i < entry.Bundles.Count; i++)
            {
                var bundle = entry.Bundles[i];
                var isRemote = BundleUtilities.IsBundleRemote(bundle.Name);
                var isBuiltin = bundle.IsBuiltin;
                var typeLabel = isRemote ? "[REMOTE]" : isBuiltin ? "[BUILTIN]" : "[BUILT-IN]";
                var isExplicit = bundle.ExplicitAssets.Contains(entry.Asset);
                var includeTag = isExplicit ? "[Explicit]" : "[Pulled-in]";

                EditorGUILayout.BeginHorizontal();
                GUIUtilities.DrawColoredLabel($"  {i + 1}. {bundle.Name}  {typeLabel} {includeTag}  [{EditorUtility.FormatBytes(bundle.Size)}]",
                    isRemote ? Color.cyan : isBuiltin ? Color.gray : Color.white);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                var groups = bundle.ReferencedByGroups.Select(g => g.Name);
                EditorGUI.indentLevel++;
                GUIUtilities.DrawColoredLabel($"Groups: {string.Join(", ", groups)}", Color.gray);
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;

            GUIUtilities.HorizontalLine();

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUIUtilities.DrawColoredLabel("Suggested Fix:", Color.white);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUIUtilities.DrawColoredLabel($"  {entry.SuggestedFix}", new Color(0.7f, 0.85f, 1f));

            if (entry.Asset.ExternalReferences.Count > 0)
            {
                GUIUtilities.HorizontalLine();

                var assetUIState = GetAssetUIState(entry.Asset);
                assetUIState.ExternalRefsFoldout = EditorGUILayout.Foldout(assetUIState.ExternalRefsFoldout,
                    $"External References ({entry.Asset.ExternalReferences.Count})");

                if (assetUIState.ExternalRefsFoldout)
                {
                    EditorGUI.indentLevel++;
                    foreach (var extRef in entry.Asset.ExternalReferences)
                    {
                        GUIUtilities.DrawColoredLabel(
                            $"-> {extRef.Name} (in {(extRef.IncludedInBundle != null ? extRef.IncludedInBundle.Name : "Unknown")})",
                            Color.white);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static Color GetReasonColor(DuplicateReason reason)
        {
            return reason switch
            {
                DuplicateReason.ExplicitInclude => Color.yellow,
                DuplicateReason.DependencyPullIn => Color.cyan,
                DuplicateReason.Mixed => new Color(1f, 0.6f, 0.2f),
                _ => Color.white
            };
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
