using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AddressablesInspector
{
    public class Recommendation
    {
        public Recommendation(string target)
        {
            Target = target;
        }

        public string Target { get; }

        public List<RecommendationMessage> Messages { get; } = new();
        
        public int MaxWarningLevel { get; private set; }
        
        public RecommendationMessage AddMessage(int level, string message)
        {
            var messageItem = Messages.FirstOrDefault(m => m.Message == message);
            if (messageItem != null)
                return messageItem;

            messageItem = new RecommendationMessage(level, message); 
            Messages.Add(messageItem);

            MaxWarningLevel = Messages.Max(m => m.WarningLevel);
            return messageItem;
        }
    }
}