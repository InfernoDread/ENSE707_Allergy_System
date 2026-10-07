using System;
using System.Collections.Generic;
using System.Linq;
using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AllergySystem.Tests
{
    [TestClass]
    public class KitchenAcknowledgementTests
    {
        private static (KitchenOrderService kitchenSvc, OrderService orderSvc, InMemoryOrderStore orderStore, CartService cartService, InMemoryAllergyProfileStore profileStore) CreateServices()
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

            var orderService = new OrderService(
                orderStore,
                cartService,
                profileStore,
                validationService);

            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);

            var kitchenService = new KitchenOrderService(orderStore, profileStore, validationService, auditService);

            return (kitchenService, orderService, orderStore, cartService, profileStore);
        }

        [TestMethod]
        public void KitchenAcknowledgement_NewOrder_DefaultsToFalse()
        {
            var (kitchenSvc, orderSvc, orderStore, cartService, profileStore) = CreateServices();

            var customerId = 200;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            cartService.AddItem(customerId, 5);

            var created = orderSvc.CreateOrderFromCart(customerId);

            Assert.IsFalse(created.KitchenAllergyAcknowledged);
            var persisted = orderStore.GetOrder(created.Id);
            Assert.IsFalse(persisted!.KitchenAllergyAcknowledged);
        }

        [TestMethod]
        public void KitchenAcknowledgement_AcknowledgePendingAllergyConfirmation_SucceedsAndDoesNotChangeStatusOrConflicts()
        {
            var (kitchenSvc, orderSvc, orderStore, cartService, profileStore) = CreateServices();

            var customerId = 201;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            // Add item that will later conflict when profile is changed
            cartService.AddItem(customerId, 4);

            // Now change profile to introduce a conflict (Milk id = 3)
            profile.Allergens = new List<Allergen>
            {
                new Allergen { Id = 3, Name = "Milk" }
            };
            profileStore.SaveProfile(profile);

            var created = orderSvc.CreateOrderFromCart(customerId);

            Assert.AreEqual(OrderStatus.PendingAllergyConfirmation, created.Status);
            Assert.IsTrue(created.ConflictingAllergens.Any());

            kitchenSvc.AcknowledgeAllergy(created.Id);

            var persisted = orderStore.GetOrder(created.Id);
            Assert.IsTrue(persisted!.KitchenAllergyAcknowledged);
            Assert.AreEqual(OrderStatus.PendingAllergyConfirmation, persisted.Status);
            Assert.IsTrue(persisted.ConflictingAllergens.Any());
        }

        [TestMethod]
        public void KitchenAcknowledgement_SafeOrder_RejectsAcknowledgement()
        {
            var (kitchenSvc, orderSvc, orderStore, cartService, profileStore) = CreateServices();

            var customerId = 202;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            cartService.AddItem(customerId, 5);

            var created = orderSvc.CreateOrderFromCart(customerId);

            Assert.AreEqual(OrderStatus.Pending, created.Status);
            Assert.IsFalse(created.ConflictingAllergens.Any());

            Assert.ThrowsExactly<InvalidOperationException>(
                () => kitchenSvc.AcknowledgeAllergy(created.Id));
        }

        [TestMethod]
        public void KitchenAcknowledgement_InvalidOrderId_ThrowsArgumentException()
        {
            var (kitchenSvc, orderSvc, orderStore, cartService, profileStore) = CreateServices();

            var nonExistentId = 99999;

            Assert.ThrowsExactly<ArgumentException>(
                () => kitchenSvc.AcknowledgeAllergy(nonExistentId));
        }

        [TestMethod]
        public void KitchenAcknowledgement_CompletedOrCancelledOrders_RejectsAcknowledgement()
        {
            var (kitchenSvc, orderSvc, orderStore, cartService, profileStore) = CreateServices();

            // Create an order directly in Completed state with conflicts via the store
            var completedOrder = new Order
            {
                CustomerId = 300,
                Items = new List<CartItem>(),
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.Completed,
                ConflictingAllergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } },
                KitchenAllergyAcknowledged = false
            };
            var savedCompleted = orderStore.SaveOrder(completedOrder);

            Assert.ThrowsExactly<InvalidOperationException>(
                () => kitchenSvc.AcknowledgeAllergy(savedCompleted.Id));

            // Create an order directly in Cancelled state with conflicts via the store
            var cancelledOrder = new Order
            {
                CustomerId = 301,
                Items = new List<CartItem>(),
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.Cancelled,
                ConflictingAllergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } },
                KitchenAllergyAcknowledged = false
            };
            var savedCancelled = orderStore.SaveOrder(cancelledOrder);

            Assert.ThrowsExactly<InvalidOperationException>(
                () => kitchenSvc.AcknowledgeAllergy(savedCancelled.Id));
        }

        [TestMethod]
        public void KitchenAcknowledgement_AlreadyAcknowledged_IsIdempotent()
        {
            var (kitchenSvc, orderSvc, orderStore, cartService, profileStore) = CreateServices();

            var customerId = 203;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            cartService.AddItem(customerId, 4);

            profile.Allergens = new List<Allergen>
            {
                new Allergen { Id = 3, Name = "Milk" }
            };
            profileStore.SaveProfile(profile);

            var created = orderSvc.CreateOrderFromCart(customerId);

            kitchenSvc.AcknowledgeAllergy(created.Id);
            var first = orderStore.GetOrder(created.Id);
            Assert.IsTrue(first!.KitchenAllergyAcknowledged);
            Assert.AreEqual(OrderStatus.PendingAllergyConfirmation, first.Status);
            Assert.IsTrue(first.ConflictingAllergens.Any());

            // second call should be a no-op (idempotent)
            kitchenSvc.AcknowledgeAllergy(created.Id);
            var second = orderStore.GetOrder(created.Id);
            Assert.IsTrue(second!.KitchenAllergyAcknowledged);
            Assert.AreEqual(OrderStatus.PendingAllergyConfirmation, second.Status);
            Assert.IsTrue(second.ConflictingAllergens.Any());
        }
    }
}
