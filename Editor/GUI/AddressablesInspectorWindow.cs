using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AddressablesInspector
{
    public class AddressablesInspectorWindow : EditorWindow
    {
        [MenuItem("Tools/Addressables Inspector")]
        public static void LaunchAddressablesInspectorWindow()
        {
            GetWindow<AddressablesInspectorWindow>("Addressables Inspector");
        }

        public AnalysisService Services { get; } = new();

        public BundleLayoutService LayoutService => Services.LayoutService;
        public BundleLayoutComparisonService LayoutComparison => Services.LayoutComparisonService;

        private readonly Dictionary<InspectorTabs, IPartDrawer> _drawers = new()
        {
            { InspectorTabs.Setup, new SetupPartDrawer() },
            { InspectorTabs.BundleLayoutGroupsAnalysis, new BundleLayoutGroupsAnalysisPartDrawer() },
            { InspectorTabs.BundleLayoutAssetsAnalysis, new BundleLayoutAssetsAnalysisPartDrawer() },
            { InspectorTabs.BundleLayoutDuplicatesAnalysis, new BundleLayoutDuplicatesAnalysisPartDrawer() },
            { InspectorTabs.BundleLayoutLabelsAnalysis, new BundleLayoutLabelsAnalysisPartDrawer() },
            { InspectorTabs.BundleLayoutSizeAnalysis, new BundleLayoutBundlesSizeAndContentAnalysisDrawer() },
            { InspectorTabs.BundleLayoutComparison, new BundleLayoutComparisonPartDrawer() },
            { InspectorTabs.Settings, new SettingsPartDrawer() },
            { InspectorTabs.Description, new DescriptionPartDrawer() }
        };

        public AnalysisSettings Settings { get; } = new();

        private InspectorTabs CurrentTab { get; set; } = InspectorTabs.Setup;
        private InspectorTabs _previousTab;

        private readonly (string name, InspectorTabs tab)[] _toolbarTabs =
        {
            ("Setup", InspectorTabs.Setup),
            ("Groups", InspectorTabs.BundleLayoutGroupsAnalysis),
            ("Size", InspectorTabs.BundleLayoutSizeAnalysis),
            ("Assets", InspectorTabs.BundleLayoutAssetsAnalysis),
            ("Duplicates", InspectorTabs.BundleLayoutDuplicatesAnalysis),
            ("Labels", InspectorTabs.BundleLayoutLabelsAnalysis),
            ("Comparison", InspectorTabs.BundleLayoutComparison),
            ("Settings", InspectorTabs.Settings),
            ("Help", InspectorTabs.Description)
        };

        private readonly InspectorTabs[] _analysisTabs =
        {
            InspectorTabs.BundleLayoutGroupsAnalysis,
            InspectorTabs.BundleLayoutSizeAnalysis,
            InspectorTabs.BundleLayoutAssetsAnalysis,
            InspectorTabs.BundleLayoutDuplicatesAnalysis,
            InspectorTabs.BundleLayoutLabelsAnalysis,
            InspectorTabs.BundleLayoutComparison
        };

        private void OnGUI()
        {
            if (_drawers == null)
                return;

            GUIUtilities.MonochromeMode = Settings.MonochromeWarnings;

            foreach (var drawer in _drawers)
            {
                drawer.Value?.SetupContext(this);
            }

            DrawToolbar();

            DrawBuildSummaryHeader();

            if (_drawers.TryGetValue(CurrentTab, out var currentDrawer))
            {
                currentDrawer.Draw();
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var prevTab = CurrentTab;
            var canAnalyze = LayoutService.LoadedLayout != null;

            for (var i = 0; i < _toolbarTabs.Length; i++)
            {
                var (name, tab) = _toolbarTabs[i];
                var isAnalysis = Array.IndexOf(_analysisTabs, tab) >= 0;

                if (isAnalysis)
                    EditorGUI.BeginDisabledGroup(!canAnalyze);

                var isActive = CurrentTab == tab;
                var prevBg = GUI.backgroundColor;
                if (isActive) GUI.backgroundColor = new Color(0.6f, 0.8f, 0.6f, 1f);

                if (GUILayout.Button(name, EditorStyles.toolbarButton, GUILayout.MinWidth(50f)))
                {
                    CurrentTab = tab;
                }

                GUI.backgroundColor = prevBg;

                if (isAnalysis)
                    EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndHorizontal();

            if (!canAnalyze && Array.IndexOf(_analysisTabs, CurrentTab) >= 0)
            {
                CurrentTab = InspectorTabs.Setup;
            }

            if (prevTab != CurrentTab)
            {
                _previousTab = prevTab;
                _drawers[CurrentTab].OnSelected();
            }
        }

        private void DrawBuildSummaryHeader()
        {
            var layout = LayoutService.LoadedLayout;
            if (layout == null || CurrentTab == InspectorTabs.Setup || CurrentTab == InspectorTabs.Settings || CurrentTab == InspectorTabs.Description)
                return;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUILayout.Label(new GUIContent($" {layout.Name}", EditorGUIUtility.FindTexture("BuildSettings.Editor")), GUILayout.Height(20f));

            GUILayout.Label($"Total: {EditorUtility.FormatBytes(layout.TotalSize)}", EditorStyles.miniLabel, GUILayout.Height(20f));
            GUILayout.Label($"Remote: {EditorUtility.FormatBytes(layout.TotalRemoteSize)}", EditorStyles.miniLabel, GUILayout.Height(20f));
            GUILayout.Label($"Groups: {layout.Groups.Count}", EditorStyles.miniLabel, GUILayout.Height(20f));
            GUILayout.Label($"Bundles: {layout.Bundles.Count}", EditorStyles.miniLabel, GUILayout.Height(20f));

            var warningCount = layout.Groups.Sum(g => g.Archives.Sum(b => b.Recommendations.Count));
            GUILayout.Label($"Warnings: {warningCount}", EditorStyles.miniLabel, GUILayout.Height(20f));
            
            GUILayout.Space(8);

            DrawGateBadges(layout);

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawGateBadges(BuildLayoutProvider layout)
        {
            var settings = Settings;

            if (settings.GateMaxTotalSizeBytes > 0)
            {
                var pass = layout.TotalSize <= settings.GateMaxTotalSizeBytes;
                DrawGateBadge("Size", EditorUtility.FormatBytes(layout.TotalSize), pass);
            }

            if (settings.GateMaxDuplicateWastedBytes > 0)
            {
                var totalWasted = ComputeTotalDuplicateWasted(layout);
                var pass = totalWasted <= settings.GateMaxDuplicateWastedBytes;
                DrawGateBadge("Duplicates", EditorUtility.FormatBytes(totalWasted), pass);
            }

            if (settings.GateMaxStartupRemoteDepsBytes > 0)
            {
                var maxStartupRemote = ComputeMaxStartupRemoteDeps(layout);
                var pass = maxStartupRemote <= settings.GateMaxStartupRemoteDepsBytes;
                DrawGateBadge("Startup Remote", EditorUtility.FormatBytes(maxStartupRemote), pass);
            }
        }

        private static void DrawGateBadge(string label, string value, bool pass)
        {
            var tag = pass ? "PASS" : "FAIL";
            var color = pass ? Color.green : Color.red;
            GUIUtilities.DrawColoredLabel($"{label}: {value} [{tag}]", color);
        }

        private static long ComputeTotalDuplicateWasted(BuildLayoutProvider layout)
        {
            long total = 0;
            foreach (var asset in layout.AssetsByPath.Values)
            {
                if (asset.IncludedByBundle.Count >= 2)
                    total += asset.Size * (asset.IncludedByBundle.Count - 1);
            }
            return total;
        }

        private static long ComputeMaxStartupRemoteDeps(BuildLayoutProvider layout)
        {
            long maxRemoteSize = 0;
            foreach (var group in layout.Groups)
            {
                foreach (var bundle in group.Archives)
                {
                    if (!BundleUtilities.IsBundleRemote(bundle.Name) || !BundleUtilities.IsBundleStartup(bundle.Name))
                        continue;

                    long remoteSize = 0;
                    foreach (var dep in bundle.AllBundleDependencies)
                    {
                        if (BundleUtilities.IsBundleRemote(dep.Name) && !BundleUtilities.IsBundleStartup(dep.Name))
                            remoteSize += dep.Size;
                    }

                    if (remoteSize > maxRemoteSize)
                        maxRemoteSize = remoteSize;
                }
            }
            return maxRemoteSize;
        }

        public void SwitchToFirstAnalysisTab()
        {
            CurrentTab = _analysisTabs[0];
            _drawers[CurrentTab].OnSelected();
        }
    }
}
