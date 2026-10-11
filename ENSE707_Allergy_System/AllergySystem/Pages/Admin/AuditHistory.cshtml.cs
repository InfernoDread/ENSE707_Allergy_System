using System.Collections.Generic;
using System.Linq;
using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AllergySystem.Pages.Admin
{
    // PageModel for the read-only audit history administration page.
    public class AuditHistoryModel : PageModel
    {
        private readonly InMemoryAuditStore _auditStore;

        public AuditHistoryModel(InMemoryAuditStore auditStore)
        {
            _auditStore = auditStore;
        }

        // Audit entries exposed to the Razor view. Preserves ordering returned by the store.
        public List<AuditEntry> Entries { get; private set; } = new();
        public List<string> AvailableActorRoles { get; private set; } = new();
        public List<string> AvailableActions { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? ActorRoleFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ActionFilter { get; set; }

        public void OnGet()
        {
            var allEntries = _auditStore.GetAll().ToList();
            AvailableActorRoles = allEntries
                .Select(entry => entry.ActorRole)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
                .ToList();
            AvailableActions = allEntries
                .Select(entry => entry.Action)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(action => action, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Entries = allEntries
                .Where(entry => string.IsNullOrWhiteSpace(ActorRoleFilter) ||
                    entry.ActorRole.Equals(ActorRoleFilter, StringComparison.OrdinalIgnoreCase))
                .Where(entry => string.IsNullOrWhiteSpace(ActionFilter) ||
                    entry.Action.Equals(ActionFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
