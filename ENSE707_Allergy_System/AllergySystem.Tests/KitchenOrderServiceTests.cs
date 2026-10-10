using System;
using System.Collections.Generic;
using System.Linq;
using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AllergySystem.Tests
{
    [TestClass]
    public class KitchenOrderServiceTests
    {
        private static (KitchenOrderService kitchenSvc, InMemoryOrderStore orderStore) CreateServices()
        {
            var orderStore = new InMemoryOrderStore();
            var profileStore = new InMemoryAllergyProfileStore();
            var validationService = new AllergyValidationService();
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);
            var kitchenService = new KitchenOrderService(orderStore, profileStore, validationService, auditService);
            return (kitchenService, orderStore);
        }

        [TestMethod]
        public void KitchenRetrieval_IncludesPendingAllergyConfirmation()
        {
            var (kitchenSvc, orderStore) = CreateServices();

            var order = new Order
            {
                CustomerId = 1,
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.PendingAllergyConfirmation,
                ConflictingAllergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } }
            };

            var saved = orderStore.SaveOrder(order);

            var results = kitchenSvc.GetKitchenOrders();

            Assert.IsNotNull(results);
            Assert.IsTrue(results.Any(o => o.Id == saved.Id));
        }

        [TestMethod]
        public void KitchenRetrieval_IncludesReadyForKitchen()
        {
            var (kitchenSvc, orderStore) = CreateServices();

            var order = new Order
            {
                CustomerId = 2,
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.ReadyForKitchen
            };

            var saved = orderStore.SaveOrder(order);

            var results = kitchenSvc.GetKitchenOrders();

            Assert.IsTrue(results.Any(o => o.Id == saved.Id));
        }

        [TestMethod]
        public void KitchenRetrieval_IncludesInPreparation()
        {
            var (kitchenSvc, orderStore) = CreateServices();

            var order = new Order
            {
                CustomerId = 3,
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.InPreparation
            };

            var saved = orderStore.SaveOrder(order);

            var results = kitchenSvc.GetKitchenOrders();

            Assert.IsTrue(results.Any(o => o.Id == saved.Id));
        }

        [TestMethod]
        public void KitchenRetrieval_ExcludesPending()
        {
            var (kitchenSvc, orderStore) = CreateServices();

            var order = new Order
            {
                CustomerId = 4,
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.Pending
            };

            var saved = orderStore.SaveOrder(order);

            var results = kitchenSvc.GetKitchenOrders();

            Assert.IsFalse(results.Any(o => o.Id == saved.Id));
        }

        [TestMethod]
        public void KitchenRetrieval_ExcludesCompleted()
        {
            var (kitchenSvc, orderStore) = CreateServices();

            var order = new Order
            {
                CustomerId = 5,
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.Completed
            };

            var saved = orderStore.SaveOrder(order);

            var results = kitchenSvc.GetKitchenOrders();

            Assert.IsFalse(results.Any(o => o.Id == saved.Id));
        }

        [TestMethod]
        public void KitchenRetrieval_ExcludesCancelled()
        {
            var (kitchenSvc, orderStore) = CreateServices();

            var order = new Order
            {
                CustomerId = 6,
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.Cancelled
            };

            var saved = orderStore.SaveOrder(order);

            var results = kitchenSvc.GetKitchenOrders();

            Assert.IsFalse(results.Any(o => o.Id == saved.Id));
        }

        [TestMethod]
        public void KitchenRetrieval_MultipleEligibleOrdersReturned()
        {
            var (kitchenSvc, orderStore) = CreateServices();

            var eligible1 = orderStore.SaveOrder(new Order { CustomerId = 10, CreatedAt = DateTime.UtcNow, Status = OrderStatus.PendingAllergyConfirmation, ConflictingAllergens = new List<Allergen> { new Allergen { Id = 3 } } });
            var eligible2 = orderStore.SaveOrder(new Order { CustomerId = 11, CreatedAt = DateTime.UtcNow, Status = OrderStatus.ReadyForKitchen });
            var eligible3 = orderStore.SaveOrder(new Order { CustomerId = 12, CreatedAt = DateTime.UtcNow, Status = OrderStatus.InPreparation });

            // ineligible
            orderStore.SaveOrder(new Order { CustomerId = 13, CreatedAt = DateTime.UtcNow, Status = OrderStatus.Pending });
            orderStore.SaveOrder(new Order { CustomerId = 14, CreatedAt = DateTime.UtcNow, Status = OrderStatus.Completed });

            var results = kitchenSvc.GetKitchenOrders();

            var ids = results.Select(o => o.Id).ToList();
            Assert.IsTrue(ids.Contains(eligible1.Id));
            Assert.IsTrue(ids.Contains(eligible2.Id));
            Assert.IsTrue(ids.Contains(eligible3.Id));
            Assert.AreEqual(3, results.Count);
        }

        [TestMethod]
        public void KitchenRetrieval_NoEligibleOrders_ReturnsEmptyList()
        {
            var (kitchenSvc, orderStore) = CreateServices();

            orderStore.SaveOrder(new Order { CustomerId = 20, CreatedAt = DateTime.UtcNow, Status = OrderStatus.Pending });
            orderStore.SaveOrder(new Order { CustomerId = 21, CreatedAt = DateTime.UtcNow, Status = OrderStatus.Completed });
            orderStore.SaveOrder(new Order { CustomerId = 22, CreatedAt = DateTime.UtcNow, Status = OrderStatus.Cancelled });

            var results = kitchenSvc.GetKitchenOrders();

            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void StartPreparation_ValidReadyForKitchen_SetsInPreparation_WithoutAcknowledgement()
        {
            var orderStore = new InMemoryOrderStore();
            var cartStore = new InMemoryCartStore();
            var profileStore = new InMemoryAllergyProfileStore();

            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var validationService = new AllergyValidationService();

            var cartService = new CartService(cartStore, menuCatalog, profileStore, validationService);
            var orderService = new OrderService(orderStore, cartService, profileStore, validationService, new DietaryCompatibilityService());
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);
            var kitchenService = new KitchenOrderService(orderStore, profileStore, validationService, auditService);

            var customerId = 9000;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            cartService.AddItem(customerId, 5);
            var created = orderService.CreateOrderFromCart(customerId);

            orderService.SendToKitchen(created.Id);

            var before = orderStore.GetOrder(created.Id);
            Assert.IsFalse(before!.KitchenAllergyAcknowledged);

            kitchenService.StartPreparation(created.Id);

            var persisted = orderStore.GetOrder(created.Id);
            Assert.IsNotNull(persisted);
            Assert.AreEqual(OrderStatus.InPreparation, persisted!.Status);
            Assert.IsFalse(persisted.KitchenAllergyAcknowledged);
        }

        [TestMethod]
        public void CompleteOrder_ValidInPreparation_SetsCompleted()
        {
            var orderStore = new InMemoryOrderStore();
            var cartStore = new InMemoryCartStore();
            var profileStore = new InMemoryAllergyProfileStore();

            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var validationService = new AllergyValidationService();

            var cartService = new CartService(cartStore, menuCatalog, profileStore, validationService);
            var orderService = new OrderService(orderStore, cartService, profileStore, validationService, new DietaryCompatibilityService());
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);
            var kitchenService = new KitchenOrderService(orderStore, profileStore, validationService, auditService);

            var customerId = 9001;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            cartService.AddItem(customerId, 5);
            var created = orderService.CreateOrderFromCart(customerId);

            orderService.SendToKitchen(created.Id);
            kitchenService.StartPreparation(created.Id);
            kitchenService.CompleteOrder(created.Id);

            var persisted = orderStore.GetOrder(created.Id);
            Assert.IsNotNull(persisted);
            Assert.AreEqual(OrderStatus.Completed, persisted!.Status);
        }

        [TestMethod]
        public void KitchenProgression_InvalidOrderIds_ThrowArgumentException()
        {
            var orderStore = new InMemoryOrderStore();
            var profileStore = new InMemoryAllergyProfileStore();
            var validationService = new AllergyValidationService();
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);
            var kitchenService = new KitchenOrderService(orderStore, profileStore, validationService, auditService);

            Assert.ThrowsExactly<ArgumentException>(() => kitchenService.StartPreparation(999999));
            Assert.ThrowsExactly<ArgumentException>(() => kitchenService.CompleteOrder(999999));
        }

        [TestMethod]
        public void KitchenProgression_InvalidStatuses_ThrowInvalidOperationException()
        {
            var orderStore = new InMemoryOrderStore();
            var cartStore = new InMemoryCartStore();
            var profileStore = new InMemoryAllergyProfileStore();

            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var validationService = new AllergyValidationService();

            var cartService = new CartService(cartStore, menuCatalog, profileStore, validationService);
            var orderService = new OrderService(orderStore, cartService, profileStore, validationService, new DietaryCompatibilityService());
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);
            var kitchenService = new KitchenOrderService(orderStore, profileStore, validationService, auditService);

            // Pending
            var c1 = 9100;
            var p1 = profileStore.GetProfile(c1);
            p1.Allergens.Clear();
            profileStore.SaveProfile(p1);
            cartService.AddItem(c1, 5);
            var pending = orderService.CreateOrderFromCart(c1);

            // PendingAllergyConfirmation
            var c2 = 9101;
            var p2 = profileStore.GetProfile(c2);
            p2.Allergens.Clear();
            profileStore.SaveProfile(p2);
            cartService.AddItem(c2, 4);
            p2.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(p2);
            var pendingConfirm = orderService.CreateOrderFromCart(c2);

            // Ready -> InPreparation then repeated Start
            var c3 = 9102;
            var p3 = profileStore.GetProfile(c3);
            p3.Allergens.Clear();
            profileStore.SaveProfile(p3);
            cartService.AddItem(c3, 5);
            var ready = orderService.CreateOrderFromCart(c3);
            orderService.SendToKitchen(ready.Id);
            kitchenService.StartPreparation(ready.Id);

            // Completed
            var c4 = 9103;
            var p4 = profileStore.GetProfile(c4);
            p4.Allergens.Clear();
            profileStore.SaveProfile(p4);
            cartService.AddItem(c4, 5);
            var completed = orderService.CreateOrderFromCart(c4);
            orderService.SendToKitchen(completed.Id);
            kitchenService.StartPreparation(completed.Id);
            kitchenService.CompleteOrder(completed.Id);

            // Cancelled
            var c5 = 9104;
            var p5 = profileStore.GetProfile(c5);
            p5.Allergens.Clear();
            profileStore.SaveProfile(p5);
            cartService.AddItem(c5, 5);
            var cancelled = orderService.CreateOrderFromCart(c5);
            orderService.CancelOrder(cancelled.Id);

            Assert.ThrowsExactly<InvalidOperationException>(() => kitchenService.StartPreparation(pending.Id));
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchenService.StartPreparation(pendingConfirm.Id));
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchenService.StartPreparation(ready.Id));
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchenService.StartPreparation(completed.Id));
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchenService.StartPreparation(cancelled.Id));

            // CompleteOrder invalid on ReadyForKitchen
            var c6 = 9105;
            var p6 = profileStore.GetProfile(c6);
            p6.Allergens.Clear();
            profileStore.SaveProfile(p6);
            cartService.AddItem(c6, 5);
            var readyOnly = orderService.CreateOrderFromCart(c6);
            orderService.SendToKitchen(readyOnly.Id);
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchenService.CompleteOrder(readyOnly.Id));

            Assert.ThrowsExactly<InvalidOperationException>(() => kitchenService.CompleteOrder(pending.Id));
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchenService.CompleteOrder(pendingConfirm.Id));
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchenService.CompleteOrder(completed.Id));
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchenService.CompleteOrder(cancelled.Id));
        }

        [TestMethod]
        public void KitchenProgression_UnresolvedConflicts_AreRejected()
        {
            var orderStore = new InMemoryOrderStore();
            var profileStore = new InMemoryAllergyProfileStore();
            var validationService = new AllergyValidationService();
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);
            var kitchenService = new KitchenOrderService(orderStore, profileStore, validationService, auditService);

            var readyWithConflicts = new Order
            {
                CustomerId = 9200,
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.ReadyForKitchen,
                Items = new List<CartItem> { new CartItem { MenuItem = new MenuItem { Id = 5 }, Quantity = 1 } },
                ConflictingAllergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } }
            };

            var inPrepWithConflicts = new Order
            {
                CustomerId = 9201,
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.InPreparation,
                Items = new List<CartItem> { new CartItem { MenuItem = new MenuItem { Id = 5 }, Quantity = 1 } },
                ConflictingAllergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } }
            };

            var rSaved = orderStore.SaveOrder(readyWithConflicts);
            var iSaved = orderStore.SaveOrder(inPrepWithConflicts);

            Assert.ThrowsExactly<InvalidOperationException>(() => kitchenService.StartPreparation(rSaved.Id));
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchenService.CompleteOrder(iSaved.Id));
        }

        [TestMethod]
        public void KitchenProgression_EmptyOrders_AreRejected()
        {
            var orderStore = new InMemoryOrderStore();
            var profileStore = new InMemoryAllergyProfileStore();
            var validationService = new AllergyValidationService();
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);
            var kitchenService = new KitchenOrderService(orderStore, profileStore, validationService, auditService);

            var emptyReady = new Order
            {
                CustomerId = 9300,
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.ReadyForKitchen,
                Items = new List<CartItem>(),
                ConflictingAllergens = new List<Allergen>()
            };

            var emptyInPrep = new Order
            {
                CustomerId = 9301,
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.InPreparation,
                Items = new List<CartItem>(),
                ConflictingAllergens = new List<Allergen>()
            };

            var s1 = orderStore.SaveOrder(emptyReady);
            var s2 = orderStore.SaveOrder(emptyInPrep);

            Assert.ThrowsExactly<InvalidOperationException>(() => kitchenService.StartPreparation(s1.Id));
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchenService.CompleteOrder(s2.Id));
        }

        [TestMethod]
        public void UpdateOrderStatus_CannotSetKitchenOwnedStatuses()
        {
            var orderStore = new InMemoryOrderStore();
            var cartStore = new InMemoryCartStore();
            var profileStore = new InMemoryAllergyProfileStore();

            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var validationService = new AllergyValidationService();

            var cartService = new CartService(cartStore, menuCatalog, profileStore, validationService);
            var orderService = new OrderService(orderStore, cartService, profileStore, validationService, new DietaryCompatibilityService());

            var customerId = 9400;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            cartService.AddItem(customerId, 5);
            var created = orderService.CreateOrderFromCart(customerId);

            Assert.ThrowsExactly<InvalidOperationException>(() => orderService.UpdateOrderStatus(created.Id, OrderStatus.InPreparation));
            Assert.ThrowsExactly<InvalidOperationException>(() => orderService.UpdateOrderStatus(created.Id, OrderStatus.Completed));

            var persisted = orderStore.GetOrder(created.Id);
            Assert.IsNotNull(persisted);
            Assert.AreEqual(OrderStatus.Pending, persisted!.Status);
        }
    }
}
