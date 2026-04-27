using System.Collections.Generic;
using System.Linq;

namespace AddressablesInspector
{
    public class RecommendationsSummary
    {
        private List<Recommendation> Recommendations { get; } = new();
        
        public RecommendationMessage AddRecommendation(string target, string message, int level)
        {
            var recommendation = Recommendations.FirstOrDefault(x => x.Target == target);
            
            if (recommendation == null)
            {
                recommendation = new Recommendation(target);
                Recommendations.Add(recommendation);
            }

            return recommendation.AddMessage(level, message);
        }
    }
}