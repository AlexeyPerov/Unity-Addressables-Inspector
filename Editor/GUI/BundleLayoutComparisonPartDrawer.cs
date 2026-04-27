using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AddressablesInspector
{
    public class BundleLayoutComparisonPartDrawer : PartDrawerBase
    {
        private const string EditorPrefsKey = "AddressablesInspector_LastComparisonFolder";

        private string _filterEntries;
        
        private Vector2 _entriesScroll;
        private readonly PaginationSettings _groupOutputSettings = new()
        {
            PageToShow = 0,
            PageSize = 20
        };
                
        private BundleLayoutComparisonService.BundleComparisonEntry _selectedEntry;
        private Vector2 _selectedEntryScroll;
        
        private bool _statsFoldout;
        private int _showType;
        private int _showDiffType;
        private string _lastComparisonFolder;

        private BundleLayoutComparisonService Service => Context.LayoutComparison;

        public override void OnSelected()
        {
            base.OnSelected();
            _lastComparisonFolder = EditorPrefs.GetString(EditorPrefsKey, "Library");
        }

        private void OpenBuildLayoutFileDialog()
        {
            var path = EditorUtility.OpenFilePanelWithFilters("Open BuildLayout.txt", _lastComparisonFolder, new[] { "Text Files (*.txt)", "txt" });
            if (string.IsNullOrEmpty(path))
                return;

            _lastComparisonFolder = System.IO.Path.GetDirectoryName(path);
            EditorPrefs.SetString(EditorPrefsKey, _lastComparisonFolder);
            Service.LoadAlternativeBuildLayout(path);
        }
        
        public override void Draw()
        {
            if (Service.OriginalLayout == null)
            {
                GUIUtilities.DrawLabelAtCenterHorizontally("Please load BundleLayout in Setup tab", Color.white);
                return;
            }
            
            if (Service.AlternativeLayout == null)
            {
                GUIUtilities.DrawLabelAtCenterHorizontally("Load an alternative BundleLayout to compare with the one loaded in Setup tab", Color.white);
                
                GUIUtilities.DrawAtCenterHorizontally(() =>
                {
                    if (GUILayout.Button("Load BuildLayout.txt"))
                    {
                        OpenBuildLayoutFileDialog();
                    }
                }, Color.white);
                
                return;
            }

            if (Service.ComparisonResult == null)
            {
                GUIUtilities.DrawLabelAtCenterHorizontally("Error performing comparison. Please re-upload BundleLayout", Color.red);
                return;
            }
            
            DrawComparisonActions();
            
            if (Service.ComparisonResult.Count == 0)
            {
                GUIUtilities.DrawLabelAtCenterHorizontally("No bundles with similar names found so nothing to compare", Color.yellow);
                return;
            }
            
            DrawComparisonSummary();

            if (_selectedEntry != null)
            {
                DrawEntry(_selectedEntry);
                return;
            }

            DrawFilterSection();
            
            var filterLowered = _filterEntries != null ? _filterEntries.ToLowerInvariant() : string.Empty;
            
            var filteredEntries = !string.IsNullOrEmpty(_filterEntries)
                ? Service.ComparisonResult.Where(x => x.OriginalBundle.Name.ToLowerInvariant().Contains(filterLowered)).ToList()
                : Service.ComparisonResult;

            if (_showType == 1)
                filteredEntries = filteredEntries.Where(x => !BundleUtilities.IsBundleRemote(x.OriginalBundle.Name)).ToList();
            else if (_showType == 2)
                filteredEntries = filteredEntries.Where(x => BundleUtilities.IsBundleRemote(x.OriginalBundle.Name)).ToList();

            if (_showDiffType == 1)
                filteredEntries = filteredEntries.Where(x => x.SizeDiffModule != 0 && x.OriginalLarger).ToList();
            else if (_showDiffType == 2)
                filteredEntries = filteredEntries.Where(x => x.SizeDiffModule != 0 && !x.OriginalLarger).ToList();
            
            GUILayout.Label("Bundles matched: " + Service.ComparisonResult.Count + ". " 
                            + (filteredEntries.Count != Service.ComparisonResult.Count
                                ? $"Showing: {filteredEntries.Count}" : string.Empty));

            GUIPaginationUtilities.DrawPagesWidget(filteredEntries.Count, _groupOutputSettings);
            
            _entriesScroll = EditorGUILayout.BeginScrollView(_entriesScroll);
            
            EditorGUILayout.BeginVertical();

            for (var i = 0; i < filteredEntries.Count; i++)
            {
                if (!GUIPaginationUtilities.ShouldDrawItem(i, _groupOutputSettings))
                {
                    continue;
                }

                var entry = filteredEntries[i];

                var sign = string.Empty;
                var color = Color.white;

                if (entry.SizeDiffModule != 0)
                {
                    sign = entry.OriginalLarger ? "-" : "+";
                    color = entry.OriginalLarger ? Color.cyan : Color.yellow;
                }

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                GUIUtilities.DrawColoredLabel(entry.OriginalBundle.Name, color);
                GUILayout.Label($"{sign}{EditorUtility.FormatBytes(entry.SizeDiffModule)}", EditorStyles.miniLabel, GUILayout.Width(90));
                GUILayout.Label($"Orig: {EditorUtility.FormatBytes(entry.OriginalBundle.Size)}", EditorStyles.miniLabel, GUILayout.Width(110));
                GUILayout.Label($"Alt: {EditorUtility.FormatBytes(entry.AlternativeBundle.Size)}", EditorStyles.miniLabel, GUILayout.Width(110));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Details >>>", GUILayout.Width(100)))
                {
                    _selectedEntryScroll = Vector2.zero;
                    _selectedEntry = entry;
                }

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }

        private void DrawComparisonActions()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label($"Comparing: {Service.OriginalLayout.Name} vs {Service.AlternativeLayout.Name}", EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Swap A/B", EditorStyles.miniButton, GUILayout.Width(70)))
            {
                Service.SwapLayouts();
            }

            if (GUILayout.Button("Recompare", EditorStyles.miniButton, GUILayout.Width(80)))
            {
                OpenBuildLayoutFileDialog();
            }

            if (GUILayout.Button("Change Alternative", EditorStyles.miniButton, GUILayout.Width(210)))
            {
                OpenBuildLayoutFileDialog();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawComparisonSummary()
        {
            _statsFoldout = EditorGUILayout.Foldout(_statsFoldout, "Summary");
            if (!_statsFoldout)
                return;

            EditorGUI.indentLevel++;

            GUILayout.Label($"Orig Total: {EditorUtility.FormatBytes(Service.OriginalLayout.TotalSize)}  |  Alt Total: {EditorUtility.FormatBytes(Service.AlternativeLayout.TotalSize)}  |  Diff: {(Service.AlternativeLayout.TotalSize >= Service.OriginalLayout.TotalSize ? "+" : "-")}{EditorUtility.FormatBytes(System.Math.Abs(Service.AlternativeLayout.TotalSize - Service.OriginalLayout.TotalSize))}");

            var origBundles = Service.OriginalLayout.Bundles.Keys.ToHashSet();
            var altBundles = Service.AlternativeLayout.Bundles.Keys.ToHashSet();
            var added = altBundles.Except(origBundles).ToList();
            var removed = origBundles.Except(altBundles).ToList();

            if (added.Count > 0)
                GUIUtilities.DrawColoredLabel($"Added bundles ({added.Count}): {string.Join(", ", added.Take(10))}{(added.Count > 10 ? "..." : "")}", Color.yellow);
            if (removed.Count > 0)
                GUIUtilities.DrawColoredLabel($"Removed bundles ({removed.Count}): {string.Join(", ", removed.Take(10))}{(removed.Count > 10 ? "..." : "")}", Color.cyan);

            var topGrowth = Service.ComparisonResult
                .Where(x => !x.OriginalLarger && x.SizeDiffModule > 0)
                .OrderByDescending(x => x.SizeDiffModule)
                .Take(5)
                .ToList();
            if (topGrowth.Count > 0)
            {
                GUILayout.Label("Top growth contributors:");
                EditorGUI.indentLevel++;
                foreach (var entry in topGrowth)
                {
                    GUIUtilities.DrawColoredLabel($"+{EditorUtility.FormatBytes(entry.SizeDiffModule)}  {entry.OriginalBundle.Name}", Color.yellow);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
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

            var diffPostfix = _showDiffType switch
            {
                1 => "Orig Larger Only",
                2 => "Alt Larger Only",
                _ => "All"
            };
            
            if (GUILayout.Button($"Diff: {diffPostfix}"))
            {
                _showDiffType = _showDiffType switch
                {
                    0 => 1,
                    1 => 2,
                    _ => 0
                };
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        
        private void DrawEntry(BundleLayoutComparisonService.BundleComparisonEntry entry)
        {
            _selectedEntryScroll = EditorGUILayout.BeginScrollView(_selectedEntryScroll);

            DrawBreadcrumb("Back", entry.OriginalBundle.Name, () => { _selectedEntry = null; });
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Orig: {EditorUtility.FormatBytes(entry.OriginalBundle.Size)}  |  Alt: {EditorUtility.FormatBytes(entry.AlternativeBundle.Size)}  |  Diff: {(entry.OriginalLarger ? "-" : "+")}{EditorUtility.FormatBytes(entry.SizeDiffModule)}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Sort by Size Desc", GUILayout.Width(120)))
            {
                entry.OriginalBundle.AllAssets = entry.OriginalBundle.AllAssets.OrderByDescending(x => x.Size).ToList();
            }
            
            EditorGUILayout.EndHorizontal();

            var hasChanges = false;

            for (var i = 0; i < entry.OriginalBundle.AllAssets.Count; i++)
            {
                var originalAsset = entry.OriginalBundle.AllAssets[i];
                var alternativeAsset =
                    entry.AlternativeBundle.AllAssets.FirstOrDefault(x => x.Name == originalAsset.Name);

                if (alternativeAsset != null)
                {
                    if (originalAsset.Size != alternativeAsset.Size)
                    {
                        hasChanges = true;
                        var sizeDiff = (long)Mathf.Abs(alternativeAsset.Size - originalAsset.Size);
                        var sign = originalAsset.Size > alternativeAsset.Size ? "-" : "+";
                        var color = originalAsset.Size > alternativeAsset.Size ? Color.cyan : Color.yellow;

                        EditorGUILayout.BeginHorizontal();
                        GUIUtilities.DrawColoredLabel($"{originalAsset.Name}  {sign}{EditorUtility.FormatBytes(sizeDiff)} ({sign}{Mathf.Round((float)sizeDiff / originalAsset.Size * 100f)}%)  Orig:{EditorUtility.FormatBytes(originalAsset.Size)}  Alt:{EditorUtility.FormatBytes(alternativeAsset.Size)}", color);
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    hasChanges = true;
                    GUIUtilities.DrawColoredLabel($"? {originalAsset.Name} [{EditorUtility.FormatBytes(originalAsset.Size)}] not in alternative", Color.cyan);
                }
            }

            for (var i = 0; i < entry.AlternativeBundle.AllAssets.Count; i++)
            {
                var alternativeAsset = entry.AlternativeBundle.AllAssets[i];
                var originalAsset = 
                    entry.OriginalBundle.AllAssets.FirstOrDefault(x => x.Name == alternativeAsset.Name);

                if (originalAsset == null)
                {
                    hasChanges = true;
                    GUIUtilities.DrawColoredLabel($"+ {alternativeAsset.Name} [{EditorUtility.FormatBytes(alternativeAsset.Size)}] new in alternative", Color.yellow);
                }
            }

            if (!hasChanges)
            {
                GUILayout.Space(10);
                GUIUtilities.DrawLabelAtCenterHorizontally("No asset differences between Original and Alternative bundles", Color.green);
                GUILayout.Label($"Both bundles contain {entry.OriginalBundle.AllAssets.Count} identical assets.", EditorStyles.miniLabel);
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
    }
}
