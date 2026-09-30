using System;
using AllergySystem.Models;

namespace AllergySystem.Services
{
    // Stores audit entries in memory for the lifetime of the application.
    public class InMemoryAuditStore
    {
        private readonly List<AuditEntry> _entries = new();
        private int _nextId = 1;

        // Adds a new audit entry to the store.
        public void Add(AuditEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            entry.Id = _nextId++;
            _entries.Add(entry);
        }

        // Returns all audit entries, newest first.
        public IReadOnlyList<AuditEntry> GetAll()
        {
            return _entries
                .OrderByDescending(entry => entry.TimestampUtc)
                .ThenByDescending(entry => entry.Id)
                .ToList();
        }
    }
}