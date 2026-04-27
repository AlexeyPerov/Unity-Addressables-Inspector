namespace AddressablesInspector
{
    public class AnalysisService
    {
        public BundleLayoutService LayoutService { get; }
        public BundleLayoutComparisonService LayoutComparisonService { get; }
        public RecommendationsSummary Summary { get; } = new();
        public AnalysisSettings Settings { get; } = new();

        public AnalysisService()
        {
            LayoutService = new BundleLayoutService(this);
            LayoutComparisonService = new BundleLayoutComparisonService(this);
        }
    }
}
