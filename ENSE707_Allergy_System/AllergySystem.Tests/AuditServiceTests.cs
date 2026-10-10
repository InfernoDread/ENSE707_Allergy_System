using System;
using System.Linq;
using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AllergySystem.Tests
{
    // Tests for audit recording behaviors used across the system.
    [TestClass]
    public class AuditServiceTests
    {
        private static (OrderService orderService, InMemoryOrderStore orderStore, CartService cartService, InMemoryAllergyProfileStore profileStore) CreateOrderService()
        {
            var orderStore = new InMemoryOrderStore();
            var cartStore = new InMemoryCartStore();
            var profileStore = new InMemoryAllergyProfileStore();

            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var validationService = new AllergyValidationService();

            var cartService = new CartService(
                cartStore,
                menuCatalog,
                profileStore,
                validationService);

            var dietaryService = new DietaryCompatibilityService();
            var orderService = new OrderService(
                orderStore,
                cartService,
                profileStore,
                validationService,
                dietaryService);

            return (orderService, orderStore, cartService, profileStore);
        }

        [TestMethod]
        public void DietaryRestrictionRecommendationService_ApproveRecommendation_RecordsAudit()
        {
            var catalogService = new DietaryRestrictionCatalogService();
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);

            var svc = new DietaryRestrictionRecommendationService(catalogService, auditService);

            var recommendation = svc.SubmitRecommendation(1, "Low-Sodium");

            svc.ApproveRecommendation(recommendation.Id);

            var entries = auditStore.GetAll();
            Assert.AreEqual(1, entries.Count);

            var entry = entries[0];
            Assert.AreEqual("Administration", entry.ActorRole);
            Assert.AreEqual("ApproveDietaryRestrictionRecommendation", entry.Action);
            Assert.AreEqual("DietaryRestrictionRecommendation", entry.EntityType);
            Assert.AreEqual(recommendation.Id, entry.EntityId);
            StringAssert.Contains(entry.Details, "Low-Sodium");
            Assert.IsTrue(entry.TimestampUtc != default);
            Assert.IsTrue(entry.Id >= 1);
        }

        [TestMethod]
        public void DietaryRestrictionRecommendationService_RejectRecommendation_RecordsAudit()
        {
            var catalogService = new DietaryRestrictionCatalogService();
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);

            var svc = new DietaryRestrictionRecommendationService(catalogService, auditService);

            var recommendation = svc.SubmitRecommendation(2, "Gluten-Free");

            svc.RejectRecommendation(recommendation.Id);

            var entries = auditStore.GetAll();
            Assert.AreEqual(1, entries.Count);

            var entry = entries[0];
            Assert.AreEqual("Administration", entry.ActorRole);
            Assert.AreEqual("RejectDietaryRestrictionRecommendation", entry.Action);
            Assert.AreEqual("DietaryRestrictionRecommendation", entry.EntityType);
            Assert.AreEqual(recommendation.Id, entry.EntityId);
            StringAssert.Contains(entry.Details, "Gluten-Free");
            Assert.IsTrue(entry.TimestampUtc != default);
            Assert.IsTrue(entry.Id >= 1);
        }

        [TestMethod]
        public void AuditService_Record_CreatesEntryWithCorrectFields()
        {
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);

            var before = DateTime.UtcNow;

            auditService.Record(
                "Tester",
                "TestAction",
                "MyEntity",
                123,
                "Some details");

            var entries = auditStore.GetAll();

            Assert.AreEqual(1, entries.Count);

            var entry = entries[0];

            Assert.AreEqual("Tester", entry.ActorRole);
            Assert.AreEqual("TestAction", entry.Action);
            Assert.AreEqual("MyEntity", entry.EntityType);
            Assert.AreEqual(123, entry.EntityId);
            Assert.AreEqual("Some details", entry.Details);
            Assert.IsTrue(entry.Id >= 1);
            Assert.AreNotEqual(default(DateTime), entry.TimestampUtc);
            Assert.IsTrue(entry.TimestampUtc >= before);
            Assert.IsTrue(entry.TimestampUtc <= DateTime.UtcNow.AddSeconds(1));
        }

        [TestMethod]
        public void InMemoryAuditStore_GetAll_OrdersByTimestampThenIdDesc()
        {
            var store = new InMemoryAuditStore();

            var now = DateTime.UtcNow;

            var old = new AuditEntry { TimestampUtc = now.AddMinutes(-2), ActorRole = "A" , Action = "Old", EntityType = "E" };
            var same1 = new AuditEntry { TimestampUtc = now, ActorRole = "B", Action = "Same1", EntityType = "E" };
            var same2 = new AuditEntry { TimestampUtc = now, ActorRole = "C", Action = "Same2", EntityType = "E" };

            store.Add(old);   // id 1
            store.Add(same1); // id 2
            store.Add(same2); // id 3

            var all = store.GetAll();

            // Expect newest timestamp first. For equal timestamps expect higher Id first.
            Assert.AreEqual(3, all.Count);
            Assert.AreEqual(3, all[0].Id);
            Assert.AreEqual(2, all[1].Id);
            Assert.AreEqual(1, all[2].Id);
        }

        [TestMethod]
        public void FrontOfHouseOrderService_SendToKitchen_RecordsAudit()
        {
            var (orderService, orderStore, cartService, profileStore) = CreateOrderService();

            // Prepare a safe profile and cart
            var customerId = 100;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            cartService.AddItem(customerId, 5);

            var created = orderService.CreateOrderFromCart(customerId);

            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);

            var foh = new FrontOfHouseOrderService(orderService, auditService);

            foh.SendToKitchen(created.Id);

            var entries = auditStore.GetAll();
            Assert.AreEqual(1, entries.Count);

            var entry = entries[0];
            Assert.AreEqual("FrontOfHouse", entry.ActorRole);
            Assert.AreEqual("SendToKitchen", entry.Action);
            Assert.AreEqual("Order", entry.EntityType);
            Assert.AreEqual(created.Id, entry.EntityId);
            StringAssert.Contains(entry.Details, $"Order sent to kitchen: {created.Id}");
            Assert.IsTrue(entry.TimestampUtc != default);
            Assert.IsTrue(entry.Id >= 1);
        }

        [TestMethod]
        public void FrontOfHouseOrderService_CancelOrder_RecordsAudit()
        {
            var (orderService, orderStore, cartService, profileStore) = CreateOrderService();

            var customerId = 101;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            cartService.AddItem(customerId, 5);
            var created = orderService.CreateOrderFromCart(customerId);

            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);

            var foh = new FrontOfHouseOrderService(orderService, auditService);

            foh.CancelOrder(created.Id);

            var entries = auditStore.GetAll();
            Assert.AreEqual(1, entries.Count);

            var entry = entries[0];
            Assert.AreEqual("FrontOfHouse", entry.ActorRole);
            Assert.AreEqual("CancelOrder", entry.Action);
            Assert.AreEqual("Order", entry.EntityType);
            Assert.AreEqual(created.Id, entry.EntityId);
            StringAssert.Contains(entry.Details, $"Order cancelled: {created.Id}");
            Assert.IsTrue(entry.TimestampUtc != default);
            Assert.IsTrue(entry.Id >= 1);
        }

        [TestMethod]
        public void AllergenRecommendationService_ApproveRecommendation_RecordsAudit()
        {
            var catalogService = new AllergenCatalogService();
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);

            var svc = new AllergenRecommendationService(catalogService, auditService);

            var recommendation = svc.SubmitRecommendation(1, "TestAllergen");

            svc.ApproveRecommendation(recommendation.Id);

            var entries = auditStore.GetAll();
            Assert.AreEqual(1, entries.Count);

            var entry = entries[0];
            Assert.AreEqual("Administration", entry.ActorRole);
            Assert.AreEqual("ApproveAllergenRecommendation", entry.Action);
            Assert.AreEqual("AllergenRecommendation", entry.EntityType);
            Assert.AreEqual(recommendation.Id, entry.EntityId);
            StringAssert.Contains(entry.Details, "TestAllergen");
            Assert.IsTrue(entry.TimestampUtc != default);
            Assert.IsTrue(entry.Id >= 1);
        }

        [TestMethod]
        public void AllergenRecommendationService_RejectRecommendation_RecordsAudit()
        {
            var catalogService = new AllergenCatalogService();
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);

            var svc = new AllergenRecommendationService(catalogService, auditService);

            var recommendation = svc.SubmitRecommendation(2, "RejectAllergen");

            svc.RejectRecommendation(recommendation.Id);

            var entries = auditStore.GetAll();
            Assert.AreEqual(1, entries.Count);

            var entry = entries[0];
            Assert.AreEqual("Administration", entry.ActorRole);
            Assert.AreEqual("RejectAllergenRecommendation", entry.Action);
            Assert.AreEqual("AllergenRecommendation", entry.EntityType);
            Assert.AreEqual(recommendation.Id, entry.EntityId);
            StringAssert.Contains(entry.Details, "RejectAllergen");
            Assert.IsTrue(entry.TimestampUtc != default);
            Assert.IsTrue(entry.Id >= 1);
        }
    }
}
