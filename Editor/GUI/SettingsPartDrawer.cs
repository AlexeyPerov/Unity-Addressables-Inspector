using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AddressablesInspector
{
    public class SettingsPartDrawer : PartDrawerBase
    {
        private Vector2 _scroll;
        private string _newRemotePattern = "";
        private string _newStartupPattern = "";

        public override void Draw()
        {
            var settings = Context.Settings.Settings;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 220;

            settings.MinWarningLevelToShow = EditorGUILayout.IntField(
                new GUIContent(
                    "Min Warnings Level",
                    "Filter warnings by minimum severity level to display in the UI.\n\n" +
                    "Warning levels indicate the importance of optimization recommendations:\n" +
                    "• Level 0 (Low): Minor optimizations, nice-to-have improvements\n" +
                    "• Level 1 (Medium): Moderate optimizations, may affect load times\n" +
                    "• Level 2 (High): Critical optimizations, strongly recommended\n\n" +
                    "Examples:\n" +
                    "• Set to 0 → Show all optimization recommendations\n" +
                    "• Set to 1 → Only show medium and high priority issues\n" +
                    "• Set to 2 → Only show critical issues requiring attention\n\n" +
                    "Default: 0 (show all warnings)"
                ),
                settings.MinWarningLevelToShow);

            settings.ShowRelatedBundlesSection = EditorGUILayout.Toggle(
                new GUIContent(
                    "Show Related Bundles",
                    "Show or hide the 'Related Bundles' section in analysis.\n\n" +
                    "The Related Bundles section displays bundles that reference\n" +
                    "or are referenced by the selected bundle, helping you understand\n" +
                    "bundle dependencies and potential optimizations.\n\n" +
                    "Use cases:\n" +
                    "• Enabled: Analyze bundle dependencies and references\n" +
                    "• Disabled: Simplify UI, reduce clutter when focusing on individual bundles\n\n" +
                    "Default: false (section is hidden)"
                ),
                settings.ShowRelatedBundlesSection);

            var thresholdBytes = EditorGUILayout.LongField(
                new GUIContent(
                    "Startup Warn Threshold",
                    "Size threshold for remote dependencies in startup bundles.\n\n" +
                    "When a startup bundle references remote (non-startup) bundles,\n" +
                    "this threshold determines the warning level:\n" +
                    "• Below threshold: Level 1 (Low)\n" +
                    "• At or above threshold: Level 3 (High)\n\n" +
                    "This helps identify startup bundles that depend on too much\n" +
                    "remote content, which can affect initial load times.\n\n" +
                    "Examples:\n" +
                    "• 3100000 (3.1 MB) - Default threshold\n" +
                    "• 1048576 (1 MB) - More strict, warn earlier\n" +
                    "• 10485760 (10 MB) - More lenient\n\n" +
                    "Default: 3100000 (3.1 MB)"
                ),
                settings.RemoteDependencyStartupWarningThresholdBytes);

            if (thresholdBytes >= 0)
            {
                settings.RemoteDependencyStartupWarningThresholdBytes = thresholdBytes;
            }

            settings.MonochromeWarnings = EditorGUILayout.Toggle(
                new GUIContent(
                    "Monochrome Warnings",
                    "Display warning severity as text tags instead of color-only.\n\n" +
                    "When enabled, warnings show [CRITICAL], [HIGH], [MEDIUM], [LOW] tags.\n" +
                    "Useful for accessibility or color-blind users.\n\n" +
                    "Default: false"
                ),
                settings.MonochromeWarnings);

            GUIUtilities.HorizontalLine();

            GUILayout.Label("Quality Gates", EditorStyles.boldLabel);
            GUILayout.Label("Set thresholds to 0 to disable a gate. Non-zero values show PASS/FAIL in the build header.", EditorStyles.miniLabel);

            var gateTotalSize = EditorGUILayout.LongField(
                new GUIContent(
                    "Max Total Size (bytes)",
                    "Maximum allowed total bundle size in bytes.\n\n" +
                    "If the build's total size exceeds this value, the gate FAILS.\n" +
                    "Set to 0 to disable this gate.\n\n" +
                    "Examples:\n" +
                    "• 52428800 (50 MB)\n" +
                    "• 104857600 (100 MB)\n" +
                    "• 209715200 (200 MB)"
                ),
                settings.GateMaxTotalSizeBytes);

            settings.GateMaxTotalSizeBytes = gateTotalSize >= 0 ? gateTotalSize : 0;

            var gateDupWasted = EditorGUILayout.LongField(
                new GUIContent(
                    "Max Duplicate Waste (bytes)",
                    "Maximum allowed wasted bytes from duplicated assets.\n\n" +
                    "If total duplicate waste exceeds this value, the gate FAILS.\n" +
                    "Set to 0 to disable this gate.\n\n" +
                    "Examples:\n" +
                    "• 1048576 (1 MB)\n" +
                    "• 5242880 (5 MB)\n" +
                    "• 10485760 (10 MB)"
                ),
                settings.GateMaxDuplicateWastedBytes);

            settings.GateMaxDuplicateWastedBytes = gateDupWasted >= 0 ? gateDupWasted : 0;

            var gateStartupRemote = EditorGUILayout.LongField(
                new GUIContent(
                    "Max Startup Remote Deps (bytes)",
                    "Maximum allowed remote dependency size for startup bundles.\n\n" +
                    "If any startup bundle references remote non-startup bundles whose\n" +
                    "combined size exceeds this value, the gate FAILS.\n" +
                    "Set to 0 to disable this gate.\n\n" +
                    "Examples:\n" +
                    "• 3100000 (3.1 MB) - Default\n" +
                    "• 1048576 (1 MB) - Strict\n" +
                    "• 5242880 (5 MB) - Lenient"
                ),
                settings.GateMaxStartupRemoteDepsBytes);

            settings.GateMaxStartupRemoteDepsBytes = gateStartupRemote >= 0 ? gateStartupRemote : 0;

            EditorGUIUtility.labelWidth = prevLabelWidth;

            GUIUtilities.HorizontalLine();

            DrawPatternList(
                "Remote Bundle Patterns",
                "Substring patterns to identify remote bundles (case-insensitive).\n\n" +
                "These patterns are used to classify bundles as 'remote' in analysis.\n" +
                "A bundle is considered remote if its name (lowercased) contains any of these patterns.\n\n" +
                "Examples:\n" +
                "• 'remote' → matches 'remote_assets', 'assets_remote', 'remote_level1'\n" +
                "• 'cdn' → matches 'cdn_assets', 'content_cdn', 'cdn_bundles'\n" +
                "• 'download' → matches 'downloadable_content', 'assets_to_download'\n\n" +
                "Default: 'remote'",
                settings.RemoteBundlePatterns,
                ref _newRemotePattern);

            GUIUtilities.HorizontalLine();

            DrawPatternList(
                "Startup Bundle Patterns",
                "Patterns to identify startup bundles (prefix match, case-insensitive).\n\n" +
                "These patterns are used to classify remote bundles as 'startup' bundles.\n" +
                "A remote bundle is considered startup if its name (lowercased, spaces removed)\n" +
                "starts with any of these patterns.\n\n" +
                "Examples:\n" +
                "• 'core' → matches 'remote_coreassets', 'remote_core_game'\n" +
                "• 'startup' → matches 'remote_startup_content', 'remote_startup_assets'\n" +
                "• 'initial' → matches 'remote_initial_load', 'remote_initial_data'\n\n" +
                "Note: Only remote bundles are checked against these patterns.\n" +
                "Default: (empty)",
                settings.StartupBundlePatterns,
                ref _newStartupPattern);

            GUIUtilities.HorizontalLine();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reload Settings From Disk", GUILayout.Width(200)))
            {
                Context.Settings.ReloadFromDisk();
            }
            if (GUILayout.Button("Save Settings", GUILayout.Width(200)))
            {
                settings.Save();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
        }

        private static void DrawPatternList(string title, string tooltip, List<string> patterns, ref string newPattern)
        {
            GUILayout.Label(new GUIContent(title, tooltip), EditorStyles.boldLabel);

            for (var i = 0; i < patterns.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"  {patterns[i]}");
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    patterns.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("  Add:", GUILayout.Width(50));
            newPattern = GUILayout.TextField(newPattern);
            if (GUILayout.Button("+", GUILayout.Width(20)))
            {
                var trimmed = newPattern.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !patterns.Contains(trimmed))
                {
                    patterns.Add(trimmed);
                }
                newPattern = "";
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
