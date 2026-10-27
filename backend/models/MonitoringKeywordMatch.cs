namespace backend.Models
{
    public class MonitoringKeywordMatch
    {
        public int Id { get; set; }

        public int MonitoringId { get; set; }

        public string Keyword { get; set; } = "";

        public DateTime MatchedAt { get; set; }

        public string LogLine { get; set; } = "";
    }
}