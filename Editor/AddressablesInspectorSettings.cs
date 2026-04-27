using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AddressablesInspector
{
    [Serializable]
    public class AddressablesInspectorSettings
    {
        private const string SettingsFileName = "AddressablesInspectorSettings.json";

        public int MinWarningLevelToShow = 0;
        public bool ShowRelatedBundlesSection;
        public long RemoteDependencyStartupWarningThresholdBytes = 3100000L;
        public bool MonochromeWarnings;

        public long GateMaxTotalSizeBytes;
        public long GateMaxDuplicateWastedBytes;
        public long GateMaxStartupRemoteDepsBytes;

        public List<string> RemoteBundlePatterns = new() { "remote" };
        public List<string> StartupBundlePatterns = new();

        private static AddressablesInspectorSettings _cached;

        public static AddressablesInspectorSettings Load()
        {
            if (_cached != null)
                return _cached;

            var settingsPath = GetSettingsFilePath();
            if (File.Exists(settingsPath))
            {
                try
                {
                    var json = File.ReadAllText(settingsPath);
                    _cached = JsonUtility.FromJson<AddressablesInspectorSettings>(json);
                    if (_cached != null)
                        return _cached;
                }
                catch
                {
                    // ignored - fall through to default
                }
            }

            _cached = new AddressablesInspectorSettings();
            return _cached;
        }

        public void Save()
        {
            var json = JsonUtility.ToJson(this, true);
            var settingsPath = GetSettingsFilePath();
            var settingsDir = Path.GetDirectoryName(settingsPath);

            if (!Directory.Exists(settingsDir))
                Directory.CreateDirectory(settingsDir);

            File.WriteAllText(settingsPath, json);
            _cached = this;

            AssetDatabase.Refresh();
        }

        public static AddressablesInspectorSettings Reload()
        {
            _cached = null;
            return Load();
        }

        private static string GetSettingsFilePath()
        {
            return Path.Combine("ProjectSettings", SettingsFileName);
        }
    }
}
