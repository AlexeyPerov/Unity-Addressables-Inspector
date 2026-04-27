using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AddressablesInspector
{
    public class BundleLayoutAssetsAnalysisPartDrawer : PartDrawerBase
    {
        private string _filterAssetsTemp;
        private string _filterAssets;

        private List<BuildLayoutProvider.Asset> _assetsToShow; 
        
        private Vector2 _assetsScroll;
        private readonly PaginationSettings _assetsOutputSettings = new()
        {
            PageToShow = 0,
            PageSize = 20
        };
                
        private BuildLayoutProvider.Asset _selectedAsset;
        private Vector2 _selectedAssetScroll;
        private Vector2 _usedByBundlesScroll;

        public override void OnSelected()
        {
            base.OnSelected();
            ResetFilter();
        }

        private void ResetFilter()
        {
            _assetsToShow = Context.LayoutService.LoadedLayout.AssetsByPath.Values.ToList();
            _assetsToShow = _assetsToShow.OrderByDescending(a => a.UsedByBundles.Count).ThenByDescending(a => a.Size)
                .ToList();
            _filterAssets = string.Empty;
        }

        public override void Draw()
        {
            if (_selectedAsset != null)
            {
                DrawAsset(_selectedAsset);
                return;
            }

            if (Context.LayoutService.LoadedLayout == null)
            {
                GUIUtilities.DrawLabelAtCenterHorizontally("Please load BundleLayout in Setup tab", Color.white);
                return;
            }
            
            var allAssets = Context.LayoutService.LoadedLayout.AssetsByPath;

            if (_assetsToShow == null)
                ResetFilter();
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Filter Assets"))
            {
                _filterAssets = _filterAssetsTemp;

                if (string.IsNullOrEmpty(_filterAssets))
                {
                    ResetFilter();
                }
                else
                {
                    var filterLowered = _filterAssets != null ? _filterAssets.ToLowerInvariant() : string.Empty;
                    _assetsToShow = new List<BuildLayoutProvider.Asset>();

                    foreach (var assetPair in allAssets)
                    {
                        if (assetPair.Key.ToLowerInvariant().Contains(filterLowered))
                        {
                            _assetsToShow.Add(assetPair.Value);
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(_filterAssets))
            {
                if (GUILayout.Button("Reset Filter"))
                {
                    ResetFilter();
                }
            }
            
            _filterAssetsTemp = GUILayout.TextField(_filterAssetsTemp, GUILayout.Width(550));
            
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Label(
                $"Assets found: {allAssets.Count}. " +
                $"{(_assetsToShow.Count != allAssets.Count ? $"Showing: {_assetsToShow.Count}" : string.Empty)}");
            
            GUIPaginationUtilities.DrawPagesWidget(_assetsToShow.Count, _assetsOutputSettings);
            
            _assetsScroll = EditorGUILayout.BeginScrollView(_assetsScroll);
            
            EditorGUILayout.BeginVertical();

            var i = 0;
            
            foreach (var asset in _assetsToShow)
            {
                if (asset == null)
                    continue;
                
                if (!GUIPaginationUtilities.ShouldDrawItem(i, _assetsOutputSettings))
                {
                    i++;
                    continue;
                }

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                GUILayout.Label(Path.GetFileName(asset.Name));
                if (asset.IncludedInBundle != null)
                    GUIUtilities.DrawColoredLabel($"in: {asset.IncludedInBundle.Name}", Color.gray);
                GUILayout.Label(EditorUtility.FormatBytes(asset.Size), EditorStyles.miniLabel, GUILayout.Width(80));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Details >>>", GUILayout.Width(100)))
                {
                    _selectedAssetScroll = Vector2.zero;
                    _selectedAsset = asset;
                }

                EditorGUILayout.EndHorizontal();

                i++;
            }
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }
        
        private void DrawAsset(BuildLayoutProvider.Asset asset)
        {
            _selectedAssetScroll = EditorGUILayout.BeginScrollView(_selectedAssetScroll);

            DrawBreadcrumb("Back", Path.GetFileName(asset.Name), () => { _selectedAsset = null; });

            var includedInBuiltIn = asset.IncludedInBundle != null && !BundleUtilities.IsBundleRemote(asset.IncludedInBundle.Name);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Size: {EditorUtility.FormatBytes(asset.Size)}  |  Built-In: {includedInBuiltIn}  |  GUID: {asset.Guid}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Label($"Path: {asset.Name}");
            
            if (asset.IncludedByAsset != null)
                GUILayout.Label($"Included By Asset: {asset.IncludedByAsset.Name}");

            if (asset.IncludedInBundle != null)
            {
                GUILayout.Label($"Included In Bundle: {asset.IncludedInBundle.Name}");

                if (asset.IncludedInBundle.ReferencedByGroups.Count > 0)
                {
                    GUILayout.Label("Bundle Referenced By Groups:");
                    EditorGUI.indentLevel++;
                    foreach (var referencedByGroup in asset.IncludedInBundle.ReferencedByGroups)
                    {
                        BundleLayoutGroupsAnalysisPartDrawer.DrawGroupDependency(referencedByGroup);
                    }
                    EditorGUI.indentLevel--;
                }
            }
            
            GUIUtilities.HorizontalLine();

            if (asset.InternalReferences.Count > 0)
            {
                GUILayout.Label($"Internal References ({asset.InternalReferences.Count}):");
                EditorGUI.indentLevel++;
                foreach (var internalReference in asset.InternalReferences)
                {
                    GUILayout.Label(internalReference.Name);
                }
                EditorGUI.indentLevel--;
            }
            
            if (asset.ExternalReferences.Count > 0)
            {
                GUIUtilities.HorizontalLine();
                GUILayout.Label($"External References ({asset.ExternalReferences.Count}):");
                EditorGUI.indentLevel++;
                foreach (var externalReference in asset.ExternalReferences)
                {
                    GUILayout.Label(externalReference.Name);
                }
                EditorGUI.indentLevel--;
            }

            if (asset.IncludedByBundle.Count > 0)
            {
                GUIUtilities.HorizontalLine();
                GUILayout.Label($"Referenced (Included) By Bundles [{asset.IncludedByBundle.Count}]:");
                EditorGUI.indentLevel++;
                foreach (var bundleDependency in asset.IncludedByBundle.OrderByDescending(x => BundleUtilities.IsBundleRemote(x.Name))
                             .ThenByDescending(x => x.Size))
                {
                    BundleLayoutGroupsAnalysisPartDrawer.DrawBundleDependency(bundleDependency, includedInBuiltIn, false);
                }
                EditorGUI.indentLevel--;
            }
            
            if (asset.UsedByBundles.Count > 0)
            {
                GUIUtilities.HorizontalLine();
                GUILayout.Label($"Used By Bundles [{asset.UsedByBundles.Count}]:");
                var scrollHeight = Mathf.Min(asset.UsedByBundles.Count * 22 + 10, 300);
                _usedByBundlesScroll = EditorGUILayout.BeginScrollView(_usedByBundlesScroll, GUILayout.Height(scrollHeight));
                EditorGUI.indentLevel++;
                foreach (var bundleDependency in asset.UsedByBundles.OrderByDescending(x => BundleUtilities.IsBundleRemote(x.Name))
                             .ThenByDescending(x => x.Size))
                {
                    BundleLayoutGroupsAnalysisPartDrawer.DrawBundleDependency(bundleDependency, includedInBuiltIn, false);
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.EndScrollView();
            }
            
            GUIUtilities.HorizontalLine();

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
