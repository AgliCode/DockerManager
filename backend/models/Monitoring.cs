
namespace backend.Models
{
    public class Monitoring
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";

        public string ContainerId { get; set; } = "";

        public string ContainerName { get; set; } = "";

        public string KeywordsJson { get; set; } = "[]";

        public string NotificationsJson { get; set; } = "[]";

        public int AlertInterval { get; set; } = 12;

        public bool IsEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; }
    }
}
