using UnityEditor;
using UnityEngine;

namespace AddressablesInspector
{
    public class SetupPartDrawer : PartDrawerBase
    {
        private const string EditorPrefsKey = "AddressablesInspector_LastBuildLayoutFolder";
        private string _lastBuildLayoutFolder;

        public override void OnSelected()
        {
            base.OnSelected();
            _lastBuildLayoutFolder = EditorPrefs.GetString(EditorPrefsKey, "Library");
        }

        public override void Draw()
        {
            if (Context.LayoutService.LoadedLayout == null)
            {
                GUIUtilities.DrawAtCenterHorizontally(() =>
                {
                    if (GUILayout.Button("Load BuildLayout.txt"))
                    {
                        OpenBuildLayoutFileDialog();
                    }
                }, Color.white);
                
                GUIUtilities.DrawLabelAtCenterHorizontally("NOTE: the tool works with .txt format only", Color.white);
                GUIUtilities.DrawLabelAtCenterHorizontally("In order to make BuildLayout.txt you need to use:", Color.white);
                GUIUtilities.DrawLabelAtCenterHorizontally(">> UnityEditor.AddressableAssets.Settings.ProjectConfigData.GenerateBuildLayout = true;", Color.gray);
                GUIUtilities.DrawLabelAtCenterHorizontally(">> AddressableAssets.Settings.ProjectConfigData.BuildLayoutReportFileFormat = ProjectConfigData.ReportFileFormat.TXT;", Color.gray);
            }
            else
            {
                GUIUtilities.DrawLabelAtCenterHorizontally("BuildLayout.txt has been successfully loaded.", Color.green);
                
                GUIUtilities.DrawAtCenterHorizontally(() =>
                {
                    if (GUILayout.Button("Reset"))
                    {
                        Context.LayoutService.ResetBuildLayout();
                    }
                }, Color.white);
            }
            
            GUIUtilities.HorizontalLine();
        }
        
        private void OpenBuildLayoutFileDialog()
        {
            var path = EditorUtility.OpenFilePanelWithFilters("Open BuildLayout.txt", _lastBuildLayoutFolder, new[] { "Text Files (*.txt)", "txt" });
            if (string.IsNullOrEmpty(path))
                return;
            
            Context.LayoutService.LoadBuildLayout(path);
            
            _lastBuildLayoutFolder = System.IO.Path.GetDirectoryName(path);
            EditorPrefs.SetString(EditorPrefsKey, _lastBuildLayoutFolder);

            Context.SwitchToFirstAnalysisTab();
        }
    }
}