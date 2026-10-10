using System;
using System.Collections.Generic;
using System.Linq;
using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AllergySystem.Tests
{
    [TestClass]
    public class KitchenRemoveOrderItemTests
    {
        private static (KitchenOrderService kitchenSvc, OrderService orderSvc, InMemoryOrderStore orderStore, CartService cartSvc, InMemoryAllergyProfileStore profileStore, MenuCatalogService menuCatalog) CreateServices()
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
                validationService,
                new DietaryCompatibilityService());

            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);

            var kitchenService = new KitchenOrderService(orderStore, profileStore, validationService, auditService);

            return (kitchenService, orderService, orderStore, cartService, profileStore, menuCatalog);
        }

        [TestMethod]
        public void RemoveOrderItem_InvalidOrderId_ThrowsArgumentException()
        {
            var (kitchen, _, _, _, _, _) = CreateServices();

            Assert.ThrowsExactly<ArgumentException>(() => kitchen.RemoveOrderItem(99999, 1));
        }

        [TestMethod]
        public void RemoveOrderItem_OrderNotPendingAllergyConfirmation_ThrowsInvalidOperationException()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 700;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            // Add a safe menu item -> order will be Pending
            cartSvc.AddItem(customerId, 5); // Garden Salad (safe)
            var created = orderSvc.CreateOrderFromCart(customerId);

            Assert.AreEqual(OrderStatus.Pending, created.Status);

            Assert.ThrowsExactly<InvalidOperationException>(() => kitchen.RemoveOrderItem(created.Id, 5));
        }

        [TestMethod]
        public void RemoveOrderItem_NotAcknowledged_ThrowsInvalidOperationException()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 701;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            // Add item first, then update profile to introduce a conflict at order creation
            cartSvc.AddItem(customerId, 4);
            profile.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(profile);
            var created = orderSvc.CreateOrderFromCart(customerId);

            Assert.AreEqual(OrderStatus.PendingAllergyConfirmation, created.Status);
            Assert.IsFalse(created.KitchenAllergyAcknowledged);

            // attempt removal without acknowledgement
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchen.RemoveOrderItem(created.Id, 4));
        }

        [TestMethod]
        public void RemoveOrderItem_MenuItemNotInOrder_ThrowsArgumentException()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 702;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);
            cartSvc.AddItem(customerId, 4);
            profile.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(profile);
            var created = orderSvc.CreateOrderFromCart(customerId);

            kitchen.AcknowledgeAllergy(created.Id);

            // menuItemId 2 is not in the order
            Assert.ThrowsExactly<ArgumentException>(() => kitchen.RemoveOrderItem(created.Id, 2));
        }

        [TestMethod]
        public void RemoveOrderItem_ItemDoesNotContributeToConflict_ThrowsInvalidOperationException()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 703;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            // Add a conflicting item and a non-contributing item
            cartSvc.AddItem(customerId, 2); // Peanut Chicken Noodles (contributes peanuts)
            cartSvc.AddItem(customerId, 5); // Garden Salad (safe)

            // Now set profile to be allergic to Peanuts
            profile.Allergens = new List<Allergen> { new Allergen { Id = 1, Name = "Peanuts" } };
            profileStore.SaveProfile(profile);

            var created = orderSvc.CreateOrderFromCart(customerId);
            kitchen.AcknowledgeAllergy(created.Id);

            // Attempt to remove the safe Garden Salad (id=5) which does not contribute to peanut conflict
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchen.RemoveOrderItem(created.Id, 5));

            // Ensure order remains unchanged
            var persisted = orderStore.GetOrder(created.Id)!;
            Assert.IsTrue(persisted.Items.Any(i => i.MenuItem.Id == 2));
            Assert.IsTrue(persisted.Items.Any(i => i.MenuItem.Id == 5));
        }

        [TestMethod]
        public void RemoveOrderItem_RemainingConflict_KeepsPendingAndRecalculatesConflicts()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 704;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            // Two allergens across two items
            cartSvc.AddItem(customerId, 2); // contributes Peanuts (id=1)
            cartSvc.AddItem(customerId, 4); // contributes Milk (id=3)

            profile.Allergens = new List<Allergen> { new Allergen { Id = 1, Name = "Peanuts" }, new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(profile);

            var created = orderSvc.CreateOrderFromCart(customerId);
            kitchen.AcknowledgeAllergy(created.Id);

            // Remove the Creamy Pasta (milk)
            kitchen.RemoveOrderItem(created.Id, 4);

            var persisted = orderStore.GetOrder(created.Id)!;

            // The peanut-contributing item should remain and peanut should still be a conflict
            Assert.IsTrue(persisted.Items.Any(i => i.MenuItem.Id == 2));
            Assert.IsFalse(persisted.Items.Any(i => i.MenuItem.Id == 4));
            Assert.IsTrue(persisted.ConflictingAllergens.Any(a => a.Id == 1));
            Assert.AreEqual(OrderStatus.PendingAllergyConfirmation, persisted.Status);
            Assert.IsTrue(persisted.KitchenAllergyAcknowledged);
        }

        [TestMethod]
        public void RemoveOrderItem_FinalConflictingItemRemoved_TransitionsToReadyForKitchen_AndKeepsAcknowledged()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 705;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            // Conflicting item with Quantity > 1 and a safe item
            cartSvc.AddItem(customerId, 4); // Creamy Pasta (milk)
            cartSvc.AddItem(customerId, 4); // increase quantity to 2
            cartSvc.AddItem(customerId, 5); // Garden Salad (safe)

            profile.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(profile);

            var created = orderSvc.CreateOrderFromCart(customerId);
            Assert.IsTrue(created.Items.Any(i => i.MenuItem.Id == 4 && i.Quantity > 1));

            kitchen.AcknowledgeAllergy(created.Id);

            kitchen.RemoveOrderItem(created.Id, 4);

            var persisted = orderStore.GetOrder(created.Id)!;

            // Creamy Pasta removed entirely despite quantity > 1; Garden Salad remains
            Assert.IsFalse(persisted.Items.Any(i => i.MenuItem.Id == 4));
            Assert.IsTrue(persisted.Items.Any(i => i.MenuItem.Id == 5));
            Assert.IsFalse(persisted.ConflictingAllergens.Any());
            Assert.AreEqual(OrderStatus.ReadyForKitchen, persisted.Status);
            Assert.IsTrue(persisted.KitchenAllergyAcknowledged);
        }

        [TestMethod]
        public void RemoveOrderItem_LastItemRemoved_CancelsOrder()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 706;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            cartSvc.AddItem(customerId, 4); // only item
            profile.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(profile);

            var created = orderSvc.CreateOrderFromCart(customerId);
            kitchen.AcknowledgeAllergy(created.Id);

            kitchen.RemoveOrderItem(created.Id, 4);

            var persisted = orderStore.GetOrder(created.Id)!;
            Assert.AreEqual(0, persisted.Items.Count);
            Assert.IsFalse(persisted.ConflictingAllergens.Any());
            Assert.AreEqual(OrderStatus.Cancelled, persisted.Status);
            Assert.IsTrue(persisted.KitchenAllergyAcknowledged);
        }

        [TestMethod]
        public void RemoveOrderItem_DuplicateMenuItemEntries_RemovesOnlySelectedCartItemAndRecalculatesFromRemainingInstance()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 707;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profile.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(profile);

            // Build two distinct CartItem instances with the same MenuItem.Id (4)
            var sourceMenu = menuCatalog.GetMenuItems().First(m => m.Id == 4);

            MenuItem CopyMenu(MenuItem m) => new MenuItem
            {
                Id = m.Id,
                Name = m.Name,
                Description = m.Description,
                Ingredients = m.Ingredients.Select(ing => new Ingredient
                {
                    Id = ing.Id,
                    Name = ing.Name,
                    Allergens = ing.Allergens.Select(a => new Allergen { Id = a.Id, Name = a.Name }).ToList()
                }).ToList()
            };

            var menuCopyA = CopyMenu(sourceMenu);
            var menuCopyB = CopyMenu(sourceMenu);

            var ciA = new CartItem { MenuItem = menuCopyA, Quantity = 1 };
            var ciB = new CartItem { MenuItem = menuCopyB, Quantity = 1 };

            var order = new Order
            {
                CustomerId = customerId,
                Items = new List<CartItem> { ciA, ciB },
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.PendingAllergyConfirmation,
                ConflictingAllergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } },
                KitchenAllergyAcknowledged = true
            };

            var saved = orderStore.SaveOrder(order);

            // Remove only the first CartItem; the second should remain and still contribute the allergen
            kitchen.RemoveOrderItem(saved.Id, 4);

            var persisted = orderStore.GetOrder(saved.Id)!;
            Assert.AreEqual(1, persisted.Items.Count);
            // remaining item still contains the allergenic ingredient (id=12)
            Assert.IsTrue(persisted.Items[0].MenuItem.Ingredients.Any(i => i.Id == 12));
            Assert.IsTrue(persisted.ConflictingAllergens.Any(a => a.Id == 3));
            Assert.AreEqual(OrderStatus.PendingAllergyConfirmation, persisted.Status);
            Assert.IsTrue(persisted.KitchenAllergyAcknowledged);
        }
    }
}
