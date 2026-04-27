namespace AddressablesInspector
{
    public class RecommendationMessage
    {
        public RecommendationMessage(int warningLevel, string message)
        {
            WarningLevel = warningLevel;
            Message = message;
        }

        public int WarningLevel { get; }
        public string Message { get; }
    }
}