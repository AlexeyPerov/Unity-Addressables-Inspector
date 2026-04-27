using System.Collections.Generic;
using System.Linq;

namespace AddressablesInspector
{
    public static class BundleUtilities
    {
        private static AddressablesInspectorSettings _config;
        private static List<string> _startupPatternsFormatted;
        private static int _startupPatternsHash;

        public static bool IsBundleRemote(string name)
        {
            var config = GetConfig();
            var lowered = name.ToLowerInvariant();

            foreach (var pattern in config.RemoteBundlePatterns)
            {
                if (lowered.Contains(pattern.ToLowerInvariant()))
                    return true;
            }

            return false;
        }

        public static bool IsBundleStartup(string name)
        {
            if (!IsBundleRemote(name))
                return false;

            var config = GetConfig();

            if (config.StartupBundlePatterns.Count == 0)
                return false;

            EnsureStartupPatternsFormatted(config);

            var formattedName = name.ToLowerInvariant().Replace(" ", string.Empty);
            return _startupPatternsFormatted.Any(b => formattedName.StartsWith(b));
        }

        private static AddressablesInspectorSettings GetConfig()
        {
            if (_config == null)
                _config = AddressablesInspectorSettings.Load();

            return _config;
        }

        private static void EnsureStartupPatternsFormatted(AddressablesInspectorSettings config)
        {
            var hash = config.StartupBundlePatterns.GetHashCode();
            if (_startupPatternsFormatted != null && _startupPatternsHash == hash)
                return;

            _startupPatternsFormatted = new List<string>();
            foreach (var pattern in config.StartupBundlePatterns)
            {
                _startupPatternsFormatted.Add(pattern.ToLowerInvariant());
            }

            _startupPatternsHash = hash;
        }
    }
}
