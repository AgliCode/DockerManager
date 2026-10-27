namespace backend.Models
{
    public class MonitoringKeywordState
    {
        public int MonitoringId { get; set; }

        public string Keyword { get; set; } = "";

        public int MatchCount { get; set; }

        public DateTime? LastAlertAt { get; set; }
    }
}