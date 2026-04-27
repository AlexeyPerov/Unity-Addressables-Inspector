using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AddressablesInspector
{
    public class BundleLayoutComparisonService
    {
        public BuildLayoutProvider OriginalLayout { get; private set; }
        public BuildLayoutProvider AlternativeLayout { get; private set; }
        
        public List<BundleComparisonEntry> ComparisonResult { get; private set; }

        private readonly AnalysisService _context;

        public BundleLayoutComparisonService(AnalysisService context)
        {
            _context = context;
        }

        public void SetOriginalLayout(BuildLayoutProvider originalLayout)
        {
            OriginalLayout = originalLayout;
            ComparisonResult = null;
        }

        public void SwapLayouts()
        {
            (AlternativeLayout, OriginalLayout) = (OriginalLayout, AlternativeLayout);
            ComparisonResult = null;
            PerformComparison();
        }
        
        public void LoadAlternativeBuildLayout(string path)
        {
            try
            {
                AlternativeLayout = new BuildLayoutProvider(BuildLayoutParser.Load(path));
                var threshold = _context?.Settings.RemoteDependencyStartupWarningThresholdBytes ?? 3100000L;
                BundleLayoutService.PerformGroupsAnalysis(AlternativeLayout, null, threshold);
                PerformComparison();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Error", $"BuildLayout Explorer cannot load the file '{path}'.\n\nError message:\n{e.Message}\n\nSee Console window for additional information.", "OK");
            }
        }

        private void PerformComparison()
        {
            if (OriginalLayout == null || AlternativeLayout == null)
                return;

            ComparisonResult = new List<BundleComparisonEntry>();
            
            var notChangedEntries = new List<BundleComparisonEntry>();

            foreach (var pair in OriginalLayout.Bundles)
            {
                if (AlternativeLayout.Bundles.TryGetValue(pair.Key, out var alternativeArchive))
                {
                    var entry = new BundleComparisonEntry(pair.Value, alternativeArchive);
                    if (entry.SizeDiffModule != 0)
                    {
                        ComparisonResult.Add(entry);
                    }
                    else
                    {
                        notChangedEntries.Add(entry);
                    }
                }
            }
            
            ComparisonResult = ComparisonResult.OrderByDescending(x => x.SizeDiffModule).ToList();
            
            foreach (var entry in notChangedEntries)
            {
                ComparisonResult.Add(entry);
            }
        }

        public class BundleComparisonEntry
        {
            public BundleComparisonEntry(BuildLayoutProvider.Archive originalBundle, BuildLayoutProvider.Archive alternativeBundle)
            {
                OriginalBundle = originalBundle;
                AlternativeBundle = alternativeBundle;

                SizeDiffModule = (long)Mathf.Abs(AlternativeBundle.Size - OriginalBundle.Size);
                OriginalLarger = OriginalBundle.Size > AlternativeBundle.Size;
            }

            public BuildLayoutProvider.Archive OriginalBundle { get; }
            public BuildLayoutProvider.Archive AlternativeBundle { get; }
            
            public long SizeDiffModule { get; }
            public bool OriginalLarger { get; } 
        }
    }
}
