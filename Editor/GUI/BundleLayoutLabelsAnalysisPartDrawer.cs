using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AddressablesInspector
{
    public class BundleLayoutLabelsAnalysisPartDrawer : PartDrawerBase
    {
        private string _filterLabels;
        private Vector2 _labelsScroll;
        private int _sortType;

        private readonly PaginationSettings _paginationSettings = new()
        {
            PageToShow = 0,
            PageSize = 20
        };

        private KeyValuePair<string, BuildLayoutProvider.LabelInfo>? _selectedLabel;
        private Vector2 _selectedLabelScroll;

        private readonly PaginationSettings _assetPagination = new()
        {
            PageToShow = 0,
            PageSize = 20
        };

        private BuildLayoutProvider Layout => Context.LayoutService.LoadedLayout;

        public override void OnSelected()
        {
            base.OnSelected();
            _selectedLabel = null;
        }

        public override void Draw()
        {
            if (Layout == null)
            {
                GUIUtilities.DrawLabelAtCenterHorizontally("Please load BundleLayout in Setup tab", Color.white);
                return;
            }

            if (Layout.Labels.Count == 0)
            {
                GUIUtilities.DrawLabelAtCenterHorizontally("No labels found in this build layout", Color.yellow);
                return;
            }

            if (_selectedLabel.HasValue)
            {
                DrawLabelDetail(_selectedLabel.Value);
                return;
            }

            DrawLabelList();
        }

        private void DrawLabelList()
        {
            var sortedLabels = GetSortedLabels();

            GUIUtilities.DrawColoredLabel($"Labels: {Layout.Labels.Count}  |  Labeled assets: {Layout.Labels.Values.Sum(l => l.Assets.Count)}  |  Total labeled size: {EditorUtility.FormatBytes(Layout.Labels.Values.Sum(l => l.TotalSize))}", Color.white);

            DrawFilterSection();

            var filterLowered = _filterLabels != null ? _filterLabels.ToLowerInvariant() : string.Empty;

            var filteredLabels = !string.IsNullOrEmpty(_filterLabels)
                ? sortedLabels.Where(x => x.Key.ToLowerInvariant().Contains(filterLowered)).ToList()
                : sortedLabels;

            GUILayout.Label($"Labels: {sortedLabels.Count}. " +
                            (filteredLabels.Count != sortedLabels.Count
                                ? $"Showing: {filteredLabels.Count}"
                                : string.Empty));

            GUIPaginationUtilities.DrawPagesWidget(filteredLabels.Count, _paginationSettings);

            _labelsScroll = EditorGUILayout.BeginScrollView(_labelsScroll);
            EditorGUILayout.BeginVertical();

            for (var i = 0; i < filteredLabels.Count; i++)
            {
                if (!GUIPaginationUtilities.ShouldDrawItem(i, _paginationSettings))
                    continue;

                var kvp = filteredLabels[i];
                var label = kvp.Key;
                var info = kvp.Value;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                GUIUtilities.DrawColoredLabel(label, Color.white);
                GUILayout.Label($"{info.Assets.Count} assets", EditorStyles.miniLabel, GUILayout.Width(80));
                GUILayout.Label($"{info.Bundles.Count} bundles", EditorStyles.miniLabel, GUILayout.Width(80));
                GUILayout.Label(EditorUtility.FormatBytes(info.TotalSize), EditorStyles.miniLabel, GUILayout.Width(80));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Details >>>", GUILayout.Width(100)))
                {
                    _selectedLabelScroll = Vector2.zero;
                    _selectedLabel = kvp;
                }

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawLabelDetail(KeyValuePair<string, BuildLayoutProvider.LabelInfo> kvp)
        {
            var labelName = kvp.Key;
            var info = kvp.Value;

            _selectedLabelScroll = EditorGUILayout.BeginScrollView(_selectedLabelScroll);

            if (GUILayout.Button("< Back", EditorStyles.miniButton, GUILayout.Width(100)))
            {
                _selectedLabel = null;
            }

            GUILayout.Label($"Label: {labelName}", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUIUtilities.DrawColoredLabel($"Assets: {info.Assets.Count}  |  Bundles: {info.Bundles.Count}  |  Total Size: {EditorUtility.FormatBytes(info.TotalSize)}", Color.white);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUIUtilities.HorizontalLine();

            GUILayout.Label("Bundles containing assets with this label:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            var sortedBundles = info.Bundles.OrderByDescending(b => b.Size).ToList();
            for (var i = 0; i < sortedBundles.Count; i++)
            {
                var bundle = sortedBundles[i];
                var isRemote = BundleUtilities.IsBundleRemote(bundle.Name);
                var typeLabel = isRemote ? "[remote]" : bundle.IsBuiltin ? "[builtin]" : "[built-in]";
                var assetsInBundle = info.Assets.Count(a => a.IncludedInBundle == bundle);

                EditorGUILayout.BeginHorizontal();
                GUIUtilities.DrawColoredLabel($"  {i + 1}. {bundle.Name}  {typeLabel}  [{EditorUtility.FormatBytes(bundle.Size)}]  Assets with label: {assetsInBundle}", Color.white);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;

            GUIUtilities.HorizontalLine();

            GUILayout.Label($"Assets with this label ({info.Assets.Count}):", EditorStyles.boldLabel);

            GUIPaginationUtilities.DrawPagesWidget(info.Assets.Count, _assetPagination);

            for (var i = 0; i < info.Assets.Count; i++)
            {
                if (!GUIPaginationUtilities.ShouldDrawItem(i, _assetPagination))
                    continue;

                var asset = info.Assets[i];
                var bundleName = asset.IncludedInBundle != null ? asset.IncludedInBundle.Name : "Unknown";
                var isRemote = asset.IncludedInBundle != null && BundleUtilities.IsBundleRemote(asset.IncludedInBundle.Name);

                EditorGUILayout.BeginHorizontal();
                GUIUtilities.DrawColoredLabel($"  {i + 1}. {Path.GetFileName(asset.Name)}  [{EditorUtility.FormatBytes(asset.Size)}] in: {bundleName}  {(isRemote ? "[remote]" : "[built-in]")}", Color.white);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndScrollView();
        }

        private List<KeyValuePair<string, BuildLayoutProvider.LabelInfo>> GetSortedLabels()
        {
            var labels = Layout.Labels.ToList();

            return _sortType switch
            {
                1 => labels.OrderBy(x => x.Value.Assets.Count).ToList(),
                2 => labels.OrderByDescending(x => x.Value.Assets.Count).ToList(),
                3 => labels.OrderBy(x => x.Value.TotalSize).ToList(),
                4 => labels.OrderByDescending(x => x.Value.TotalSize).ToList(),
                5 => labels.OrderBy(x => x.Value.Bundles.Count).ToList(),
                6 => labels.OrderByDescending(x => x.Value.Bundles.Count).ToList(),
                _ => labels.OrderBy(x => x.Key).ToList()
            };
        }

        private void DrawFilterSection()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter Labels:");
            _filterLabels = GUILayout.TextField(_filterLabels, GUILayout.Width(550));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            var sortLabel = _sortType switch
            {
                0 => "Name: Asc",
                1 => "Assets: Asc",
                2 => "Assets: Desc",
                3 => "Size: Asc",
                4 => "Size: Desc",
                5 => "Bundles: Asc",
                6 => "Bundles: Desc",
                _ => "Unsorted"
            };

            if (GUILayout.Button($"Sort: {sortLabel}"))
            {
                _sortType = _sortType >= 6 ? 0 : _sortType + 1;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }
}
