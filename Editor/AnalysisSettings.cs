namespace AddressablesInspector
{
    public class AnalysisSettings
    {
        private AddressablesInspectorSettings _settings;

        public AddressablesInspectorSettings Settings => _settings ??= AddressablesInspectorSettings.Load();

        public void ReloadFromDisk()
        {
            _settings = AddressablesInspectorSettings.Reload();
        }

        public int MinWarningLevelToShow
        {
            get => Settings.MinWarningLevelToShow;
            set => Settings.MinWarningLevelToShow = value;
        }

        public bool ShowRelatedBundlesSection
        {
            get => Settings.ShowRelatedBundlesSection;
            set => Settings.ShowRelatedBundlesSection = value;
        }

        public long RemoteDependencyStartupWarningThresholdBytes
        {
            get => Settings.RemoteDependencyStartupWarningThresholdBytes;
            set => Settings.RemoteDependencyStartupWarningThresholdBytes = value;
        }

        public bool MonochromeWarnings
        {
            get => Settings.MonochromeWarnings;
            set => Settings.MonochromeWarnings = value;
        }

        public long GateMaxTotalSizeBytes
        {
            get => Settings.GateMaxTotalSizeBytes;
            set => Settings.GateMaxTotalSizeBytes = value;
        }

        public long GateMaxDuplicateWastedBytes
        {
            get => Settings.GateMaxDuplicateWastedBytes;
            set => Settings.GateMaxDuplicateWastedBytes = value;
        }

        public long GateMaxStartupRemoteDepsBytes
        {
            get => Settings.GateMaxStartupRemoteDepsBytes;
            set => Settings.GateMaxStartupRemoteDepsBytes = value;
        }
    }
}
