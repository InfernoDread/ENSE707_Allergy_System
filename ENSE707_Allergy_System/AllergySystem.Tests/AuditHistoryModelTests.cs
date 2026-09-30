using System;
using System.Linq;
using AllergySystem.Models;
using AllergySystem.Pages.Admin;
using AllergySystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AllergySystem.Tests
{
    // Tests for the AuditHistory page model.
    [TestClass]
    public class AuditHistoryModelTests
    {
        [TestMethod]
        public void OnGet_PopulatesEntries_PreservesOrdering()
        {
            var store = new InMemoryAuditStore();

            var now = DateTime.UtcNow;

            var old = new AuditEntry { TimestampUtc = now.AddMinutes(-2), ActorRole = "A", Action = "Old", EntityType = "E" };
            var same1 = new AuditEntry { TimestampUtc = now, ActorRole = "B", Action = "Same1", EntityType = "E" };
            var same2 = new AuditEntry { TimestampUtc = now, ActorRole = "C", Action = "Same2", EntityType = "E" };

            store.Add(old);   // id 1
            store.Add(same1); // id 2
            store.Add(same2); // id 3

            var model = new AuditHistoryModel(store);
            model.OnGet();

            Assert.AreEqual(3, model.Entries.Count);

            // Expect newest-first. For equal timestamps expect higher Id first.
            Assert.AreEqual(3, model.Entries[0].Id);
            Assert.AreEqual(2, model.Entries[1].Id);
            Assert.AreEqual(1, model.Entries[2].Id);

            // Also ensure the model's ordering matches the store's ordering.
            var storeIds = store.GetAll().Select(e => e.Id).ToArray();
            var modelIds = model.Entries.Select(e => e.Id).ToArray();
            CollectionAssert.AreEqual(storeIds, modelIds);
        }

        [TestMethod]
        public void OnGet_WithEmptyStore_LeavesEntriesEmpty()
        {
            var store = new InMemoryAuditStore();

            var model = new AuditHistoryModel(store);
            model.OnGet();

            Assert.IsNotNull(model.Entries);
            Assert.AreEqual(0, model.Entries.Count);
        }
    }
}
