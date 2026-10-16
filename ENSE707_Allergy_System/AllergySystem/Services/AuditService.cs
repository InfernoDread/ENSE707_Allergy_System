using AllergySystem.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace AllergySystem.Services
{
    // Creates and records audit entries for important system actions.
    public class AuditService
    {
        private readonly InMemoryAuditStore _auditStore;
        private readonly IHttpContextAccessor? _httpContextAccessor;

        // IHttpContextAccessor is optional to preserve existing test construction.
        public AuditService(InMemoryAuditStore auditStore, IHttpContextAccessor? httpContextAccessor = null)
        {
            _auditStore = auditStore;
            _httpContextAccessor = httpContextAccessor;
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
            // Populate actor identity only when an authenticated principal is available.
            var principal = _httpContextAccessor?.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated == true)
            {
                // Use NameIdentifier as stable account id and Name (or ClaimTypes.Name) as username.
                entry.ActorId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                entry.ActorUsername = principal.Identity?.Name ?? principal.FindFirstValue(ClaimTypes.Name);
            }

            _auditStore.Add(entry);
        }
    }
}