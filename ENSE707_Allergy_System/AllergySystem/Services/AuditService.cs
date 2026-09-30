using AllergySystem.Models;

namespace AllergySystem.Services
{
    // Creates and records audit entries for important system actions.
    public class AuditService
    {
        private readonly InMemoryAuditStore _auditStore;

        public AuditService(InMemoryAuditStore auditStore)
        {
            _auditStore = auditStore;
        }

        // Records an action in the system audit history.
        public void Record(
            string actorRole,
            string action,
            string entityType,
            int? entityId,
            string details)
        {
            var entry = new AuditEntry
            {
                TimestampUtc = DateTime.UtcNow,
                ActorRole = actorRole,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details
            };

            _auditStore.Add(entry);
        }
    }
}