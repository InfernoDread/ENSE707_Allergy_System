using System;
using System.Collections.Generic;
using System.Linq;
using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AllergySystem.Tests
{
    [TestClass]
    public class KitchenAuditTests
    {
        [TestMethod]
        public void AcknowledgeAllergy_RecordsAudit_And_IsIdempotent()
        {
            var orderStore = new InMemoryOrderStore();
            var cartStore = new InMemoryCartStore();
            var profileStore = new InMemoryAllergyProfileStore();

            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var validationService = new AllergyValidationService();

            var cartService = new CartService(cartStore, menuCatalog, profileStore, validationService);
            var orderService = new OrderService(orderStore, cartService, profileStore, validationService);

            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);

            var kitchen = new KitchenOrderService(orderStore, profileStore, validationService, auditService);

            var customerId = 3000;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            // Add item that will later conflict when profile is changed
            cartService.AddItem(customerId, 4);

            // Now change profile to introduce a conflict (Milk id = 3)
            profile.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(profile);

            var created = orderService.CreateOrderFromCart(customerId);

            kitchen.AcknowledgeAllergy(created.Id);

            var entries = auditStore.GetAll();
            Assert.AreEqual(1, entries.Count);

            var entry = entries[0];
            Assert.AreEqual("Kitchen", entry.ActorRole);
            Assert.AreEqual("AcknowledgeAllergy", entry.Action);
            Assert.AreEqual("Order", entry.EntityType);
            Assert.AreEqual(created.Id, entry.EntityId);
            StringAssert.Contains(entry.Details, created.Id.ToString());

            // Idempotent: repeated call should not create another audit entry
            kitchen.AcknowledgeAllergy(created.Id);
            var again = auditStore.GetAll();
            Assert.AreEqual(1, again.Count);
        }

        [TestMethod]
        public void RemoveIngredient_RecordsAudit_WhenSuccessful()
        {
            var orderStore = new InMemoryOrderStore();
            var cartStore = new InMemoryCartStore();
            var profileStore = new InMemoryAllergyProfileStore();

            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var validationService = new AllergyValidationService();

            var cartService = new CartService(cartStore, menuCatalog, profileStore, validationService);
            var orderService = new OrderService(orderStore, cartService, profileStore, validationService);

            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);

            var kitchen = new KitchenOrderService(orderStore, profileStore, validationService, auditService);

            var customerId = 3001;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            // Creamy Pasta (id=4) -> Cream Sauce ingredient id=12 contains Milk
            cartService.AddItem(customerId, 4);

            profile.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(profile);

            var created = orderService.CreateOrderFromCart(customerId);

            kitchen.AcknowledgeAllergy(created.Id);

            var before = auditStore.GetAll().Count;

            kitchen.RemoveIngredient(created.Id, 4, 12);

            var all = auditStore.GetAll();
            Assert.AreEqual(before + 1, all.Count);

            var entry = all.First();
            Assert.AreEqual("Kitchen", entry.ActorRole);
            Assert.AreEqual("RemoveIngredient", entry.Action);
            Assert.AreEqual("Order", entry.EntityType);
            Assert.AreEqual(created.Id, entry.EntityId);
            StringAssert.Contains(entry.Details, "MenuItemId=4");
            StringAssert.Contains(entry.Details, "IngredientId=12");
        }

        [TestMethod]
        public void RemoveOrderItem_RecordsAudit_WhenSuccessful()
        {
            var orderStore = new InMemoryOrderStore();
            var cartStore = new InMemoryCartStore();
            var profileStore = new InMemoryAllergyProfileStore();

            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var validationService = new AllergyValidationService();

            var cartService = new CartService(cartStore, menuCatalog, profileStore, validationService);
            var orderService = new OrderService(orderStore, cartService, profileStore, validationService);

            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);

            var kitchen = new KitchenOrderService(orderStore, profileStore, validationService, auditService);

            var customerId = 3002;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            // Two allergens across two items
            cartService.AddItem(customerId, 2); // contributes Peanuts (id=1)
            cartService.AddItem(customerId, 4); // contributes Milk (id=3)

            profile.Allergens = new List<Allergen> { new Allergen { Id = 1, Name = "Peanuts" }, new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(profile);

            var created = orderService.CreateOrderFromCart(customerId);
            kitchen.AcknowledgeAllergy(created.Id);

            // Remove item 4
            var before = auditStore.GetAll().Count;
            kitchen.RemoveOrderItem(created.Id, 4);

            var all = auditStore.GetAll();
            Assert.AreEqual(before + 1, all.Count);

            var entry = all.First();
            Assert.AreEqual("Kitchen", entry.ActorRole);
            Assert.AreEqual("RemoveOrderItem", entry.Action);
            Assert.AreEqual("Order", entry.EntityType);
            Assert.AreEqual(created.Id, entry.EntityId);
            StringAssert.Contains(entry.Details, "MenuItemId=4");
        }

        [TestMethod]
        public void StartPreparation_RecordsAudit_WhenStatusTransitions()
        {
            var orderStore = new InMemoryOrderStore();
            var cartStore = new InMemoryCartStore();
            var profileStore = new InMemoryAllergyProfileStore();

            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var validationService = new AllergyValidationService();

            var cartService = new CartService(cartStore, menuCatalog, profileStore, validationService);
            var orderService = new OrderService(orderStore, cartService, profileStore, validationService);

            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);

            var kitchen = new KitchenOrderService(orderStore, profileStore, validationService, auditService);

            var customerId = 3003;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            cartService.AddItem(customerId, 5);
            var created = orderService.CreateOrderFromCart(customerId);

            orderService.SendToKitchen(created.Id);

            kitchen.StartPreparation(created.Id);

            var entries = auditStore.GetAll();
            Assert.AreEqual(1, entries.Count);

            var entry = entries[0];
            Assert.AreEqual("Kitchen", entry.ActorRole);
            Assert.AreEqual("StartPreparation", entry.Action);
            Assert.AreEqual("Order", entry.EntityType);
            Assert.AreEqual(created.Id, entry.EntityId);
            StringAssert.Contains(entry.Details, "PrevStatus=ReadyForKitchen");
        }

        [TestMethod]
        public void CompleteOrder_RecordsAudit_WhenStatusTransitions()
        {
            var orderStore = new InMemoryOrderStore();
            var cartStore = new InMemoryCartStore();
            var profileStore = new InMemoryAllergyProfileStore();

            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var validationService = new AllergyValidationService();

            var cartService = new CartService(cartStore, menuCatalog, profileStore, validationService);
            var orderService = new OrderService(orderStore, cartService, profileStore, validationService);

            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);

            var kitchen = new KitchenOrderService(orderStore, profileStore, validationService, auditService);

            var customerId = 3004;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            cartService.AddItem(customerId, 5);
            var created = orderService.CreateOrderFromCart(customerId);

            orderService.SendToKitchen(created.Id);
            kitchen.StartPreparation(created.Id);

            var before = auditStore.GetAll().Count;
            kitchen.CompleteOrder(created.Id);

            var all = auditStore.GetAll();
            Assert.AreEqual(before + 1, all.Count);

            var entry = all.First();
            Assert.AreEqual("Kitchen", entry.ActorRole);
            Assert.AreEqual("CompleteOrder", entry.Action);
            Assert.AreEqual("Order", entry.EntityType);
            Assert.AreEqual(created.Id, entry.EntityId);
            StringAssert.Contains(entry.Details, "PrevStatus=InPreparation");
        }

        [TestMethod]
        public void FailedOperations_DoNotCreateAuditEntries()
        {
            var orderStore = new InMemoryOrderStore();
            var cartStore = new InMemoryCartStore();
            var profileStore = new InMemoryAllergyProfileStore();

            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var validationService = new AllergyValidationService();

            var cartService = new CartService(cartStore, menuCatalog, profileStore, validationService);
            var orderService = new OrderService(orderStore, cartService, profileStore, validationService);

            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);

            var kitchen = new KitchenOrderService(orderStore, profileStore, validationService, auditService);

            // 1) Acknowledge on safe order -> fails
            var cA = 4000;
            var pA = profileStore.GetProfile(cA);
            pA.Allergens.Clear();
            profileStore.SaveProfile(pA);
            cartService.AddItem(cA, 5);
            var safe = orderService.CreateOrderFromCart(cA);
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchen.AcknowledgeAllergy(safe.Id));

            // 2) RemoveIngredient without acknowledgement -> fails
            var cB = 4001;
            var pB = profileStore.GetProfile(cB);
            pB.Allergens.Clear();
            profileStore.SaveProfile(pB);
            cartService.AddItem(cB, 4);
            pB.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(pB);
            var conflictOrder = orderService.CreateOrderFromCart(cB);
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchen.RemoveIngredient(conflictOrder.Id, 4, 12));

            // 3) RemoveOrderItem without acknowledgement -> fails
            var cC = 4002;
            var pC = profileStore.GetProfile(cC);
            pC.Allergens.Clear();
            profileStore.SaveProfile(pC);
            cartService.AddItem(cC, 4);
            pC.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(pC);
            var conflictOrder2 = orderService.CreateOrderFromCart(cC);
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchen.RemoveOrderItem(conflictOrder2.Id, 4));

            // 4) StartPreparation on non-Ready order -> fails
            var cD = 4003;
            var pD = profileStore.GetProfile(cD);
            pD.Allergens.Clear();
            profileStore.SaveProfile(pD);
            cartService.AddItem(cD, 5);
            var pending = orderService.CreateOrderFromCart(cD);
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchen.StartPreparation(pending.Id));

            // 5) CompleteOrder on non-InPreparation -> fails
            var cE = 4004;
            var pE = profileStore.GetProfile(cE);
            pE.Allergens.Clear();
            profileStore.SaveProfile(pE);
            cartService.AddItem(cE, 5);
            var notInPrep = orderService.CreateOrderFromCart(cE);
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchen.CompleteOrder(notInPrep.Id));

            // Assert no audit entries created for failed operations
            var all = auditStore.GetAll();
            Assert.AreEqual(0, all.Count);
        }
    }
}
