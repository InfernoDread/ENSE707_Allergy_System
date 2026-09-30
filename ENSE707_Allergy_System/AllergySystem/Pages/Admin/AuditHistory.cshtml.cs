using System.Collections.Generic;
using System.Linq;
using AllergySystem.Models;
using AllergySystem.Services;
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

        public void OnGet()
        {
            Entries = _auditStore.GetAll().ToList();
        }
    }
}
