using UnityEditor;
using UnityEngine;

namespace AddressablesInspector
{
    public class DescriptionPartDrawer : PartDrawerBase
    {
        public override void Draw()
        {
            var scroll = EditorGUILayout.BeginScrollView(Vector2.zero);

            EditorGUILayout.BeginVertical();

            EditorGUILayout.Space(10);

            GUILayout.Label("Addressables Inspector", EditorStyles.boldLabel);
            GUILayout.Label("Analyzes BuildLayout.txt files to detect bundle dependency issues,\nduplicate assets, and build regression.", EditorStyles.miniLabel);

            EditorGUILayout.Space(8);
            GUIUtilities.HorizontalLine();

            GUILayout.Label("How It Works", EditorStyles.boldLabel);
            GUILayout.Label("Unity generates BuildLayout.txt when you build Addressables (requires enabling GenerateBuildLayout)." +
                            "\nThis tool parses that file and cross-references bundles, groups, and assets\nto find problems that are hard to spot manually.", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(8);
            GUIUtilities.HorizontalLine();

            GUILayout.Label("Why Some Issues Are Critical", EditorStyles.boldLabel);
            GUILayout.Label("Warning levels reflect how directly a problem impacts runtime behavior:", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(4);
            EditorGUI.indentLevel++;
            GUIUtilities.DrawColoredLabel("Level 5 (Critical) — Built-in bundle directly depends on a remote bundle.", Color.red);
            GUILayout.Label("Forces an unexpected download at startup. Users see a loading screen they shouldn't.", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(4);
            GUIUtilities.DrawColoredLabel("Level 4 (Critical) — Circular dependencies or transitive remote refs from built-in.", new Color(1f, 0.2f, 0.2f));
            GUILayout.Label("Circular deps can cause load order failures. Transitive remote refs cause the same startup download problem.", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(4);
            GUIUtilities.DrawColoredLabel("Level 3 (High) — Startup bundle has large remote non-startup dependencies.", new Color(1f, 0.4f, 0.4f));
            GUILayout.Label("Increases initial download size significantly. Directly delays when the app becomes interactive.", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(4);
            GUIUtilities.DrawColoredLabel("Level 1-2 (Low-Medium) — Potential duplicates, builtin asset conflicts.", Color.yellow);
            GUILayout.Label("Wastes storage and bandwidth. May cause unexpected behavior but usually doesn't break loading.", EditorStyles.wordWrappedLabel);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(8);
            GUIUtilities.HorizontalLine();

            GUILayout.Label("Example: The Startup Download Problem", EditorStyles.boldLabel);
            GUILayout.Label("Say you have groups A (built-in) and B (remote, downloaded from CDN).", EditorStyles.wordWrappedLabel);
            GUILayout.Label("A contains prefabs A1, B contains B1. A1 depends on asset D, B1 also depends on D.", EditorStyles.wordWrappedLabel);
            GUILayout.Label("Unity places D into one of the groups. If D ends up in B, then every time", EditorStyles.wordWrappedLabel);
            GUILayout.Label("something in A references D, Unity must download bundle B at runtime.", EditorStyles.wordWrappedLabel);
            GUILayout.Label("If A is loaded at startup, users face an unexpected download.", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(8);
            GUIUtilities.HorizontalLine();

            GUILayout.Label("Example: Circular Dependencies", EditorStyles.boldLabel);
            GUILayout.Label("Bundle [ui_common] depends on [ui_data], and [ui_data] depends on [ui_common].", EditorStyles.wordWrappedLabel);
            GUILayout.Label("Unity cannot resolve which to load first. This can manifest as missing", EditorStyles.wordWrappedLabel);
            GUILayout.Label("assets, null references, or infinite load attempts at runtime.", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(8);
            GUIUtilities.HorizontalLine();

            GUILayout.Label("Quality Gates", EditorStyles.boldLabel);
            GUILayout.Label("Define thresholds in Settings (e.g. max total size, max duplicate waste, max startup remote deps).", EditorStyles.wordWrappedLabel);
            GUILayout.Label("Enabled gates show PASS/FAIL badges in the build header, turning analysis into CI-ready checks.", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(10);

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }
    }
}
