namespace AllergySystem.Models
{
    // Represents a single recorded action in the system audit history.
    public class AuditEntry
    {
        public int Id { get; set; }

        public DateTime TimestampUtc { get; set; }

        public string ActorRole { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public string EntityType { get; set; } = string.Empty;

        public int? EntityId { get; set; }

        public string Details { get; set; } = string.Empty;
    }
}