namespace DockerManager.Models
{
    public class LogCheckHistory
    {
        public int Id { get; set; }

        public DateTime CheckedAt { get; set; }

        public string ContainersJson { get; set; } = "[]";

        public string LogsText { get; set; } = "";

        public string Filters { get; set; } = "[]";
    }
}
