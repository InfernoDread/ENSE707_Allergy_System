using System;
using System.Collections.Generic;
using System.Linq;
using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AllergySystem.Tests
{
    [TestClass]
    public class KitchenRemoveIngredientTests
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
        public void RemoveIngredient_InvalidOrderId_ThrowsArgumentException()
        {
            var (kitchen, _, _, _, _, _) = CreateServices();

            Assert.ThrowsExactly<ArgumentException>(() => kitchen.RemoveIngredient(99999, 1, 1));
        }

        [TestMethod]
        public void RemoveIngredient_OrderNotPending_ThrowsInvalidOperationException()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 500;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            // Add a safe menu item -> order will be Pending
            cartSvc.AddItem(customerId, 5); // Garden Salad (safe)
            var created = orderSvc.CreateOrderFromCart(customerId);

            Assert.AreEqual(OrderStatus.Pending, created.Status);

            Assert.ThrowsExactly<InvalidOperationException>(() => kitchen.RemoveIngredient(created.Id, 5, 13));
        }

        [TestMethod]
        public void RemoveIngredient_NotAcknowledged_ThrowsInvalidOperationException()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 501;
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
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchen.RemoveIngredient(created.Id, 4, 12));
        }

        [TestMethod]
        public void RemoveIngredient_MenuItemNotInOrder_ThrowsArgumentException()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 502;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);
            cartSvc.AddItem(customerId, 4);
            profile.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(profile);
            var created = orderSvc.CreateOrderFromCart(customerId);

            kitchen.AcknowledgeAllergy(created.Id);

            // menuItemId 2 is not in the order
            Assert.ThrowsExactly<ArgumentException>(() => kitchen.RemoveIngredient(created.Id, 2, 7));
        }

        [TestMethod]
        public void RemoveIngredient_IngredientNotInOrder_ThrowsArgumentException()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 503;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);
            cartSvc.AddItem(customerId, 4);
            profile.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(profile);
            var created = orderSvc.CreateOrderFromCart(customerId);

            kitchen.AcknowledgeAllergy(created.Id);

            // ingredient 999 does not exist
            Assert.ThrowsExactly<ArgumentException>(() => kitchen.RemoveIngredient(created.Id, 4, 999));
        }

        [TestMethod]
        public void RemoveIngredient_IngredientDoesNotContribute_ThrowsInvalidOperationException()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 504;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);
            // Peanut Chicken Noodles (id=2) has Chicken (id=6) with no allergens
            cartSvc.AddItem(customerId, 2);
            profile.Allergens = new List<Allergen> { new Allergen { Id = 1, Name = "Peanuts" } }; // allergic to peanuts
            profileStore.SaveProfile(profile);
            var created = orderSvc.CreateOrderFromCart(customerId);

            kitchen.AcknowledgeAllergy(created.Id);

            // Attempt to remove Chicken which does not contribute to peanut conflict
            Assert.ThrowsExactly<InvalidOperationException>(() => kitchen.RemoveIngredient(created.Id, 2, 6));
        }

        [TestMethod]
        public void RemoveIngredient_ValidConflictingIngredient_RemovedFromOrderSnapshot()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 505;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);
            // Creamy Pasta (id=4) -> Cream Sauce ingredient id=12 contains Milk
            cartSvc.AddItem(customerId, 4);
            profile.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(profile);
            var created = orderSvc.CreateOrderFromCart(customerId);

            kitchen.AcknowledgeAllergy(created.Id);

            // record menu catalog ingredient presence before
            var catalogMenu = menuCatalog.GetMenuItems().First(m => m.Id == 4);
            Assert.IsTrue(catalogMenu.Ingredients.Any(i => i.Id == 12));

            kitchen.RemoveIngredient(created.Id, 4, 12);

            var persisted = orderStore.GetOrder(created.Id)!;
            Assert.IsFalse(persisted.Items[0].MenuItem.Ingredients.Any(i => i.Id == 12));

            // catalog unchanged
            var catalogAfter = menuCatalog.GetMenuItems().First(m => m.Id == 4);
            Assert.IsTrue(catalogAfter.Ingredients.Any(i => i.Id == 12));
        }

        [TestMethod]
        public void RemoveIngredient_DoesNotMutateOtherOrdersOrCatalog()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerA = 506;
            var customerB = 507;
            var profileA = profileStore.GetProfile(customerA);
            profileA.Allergens.Clear();
            profileStore.SaveProfile(profileA);
            var profileB = profileStore.GetProfile(customerB);
            profileB.Allergens.Clear();
            profileStore.SaveProfile(profileB);

            // both customers order Creamy Pasta
            cartSvc.AddItem(customerA, 4);
            cartSvc.AddItem(customerB, 4);

            // now set both profiles to include Milk and create orders
            profileA.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(profileA);
            var orderA = orderSvc.CreateOrderFromCart(customerA);

            profileB.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(profileB);
            var orderB = orderSvc.CreateOrderFromCart(customerB);

            kitchen.AcknowledgeAllergy(orderA.Id);

            // remove ingredient from orderA
            kitchen.RemoveIngredient(orderA.Id, 4, 12);

            var a = orderStore.GetOrder(orderA.Id)!;
            var b = orderStore.GetOrder(orderB.Id)!;

            Assert.IsFalse(a.Items[0].MenuItem.Ingredients.Any(i => i.Id == 12));
            Assert.IsTrue(b.Items[0].MenuItem.Ingredients.Any(i => i.Id == 12));

            // catalog still has ingredient
            var catalog = menuCatalog.GetMenuItems().First(m => m.Id == 4);
            Assert.IsTrue(catalog.Ingredients.Any(i => i.Id == 12));
        }

        [TestMethod]
        public void RemoveIngredient_RemainingConflictsKeepOrderPending()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 508;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            // add Creamy Pasta (4) and Peanut Chicken Noodles (2)
            cartSvc.AddItem(customerId, 4);
            cartSvc.AddItem(customerId, 2);

            // now set profile to include Milk and Peanuts and create the order
            profile.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" }, new Allergen { Id = 1, Name = "Peanuts" } };
            profileStore.SaveProfile(profile);
            var created = orderSvc.CreateOrderFromCart(customerId);

            kitchen.AcknowledgeAllergy(created.Id);

            // remove cream sauce resolving Milk, but Peanuts remains due to item 2
            kitchen.RemoveIngredient(created.Id, 4, 12);

            var persisted = orderStore.GetOrder(created.Id)!;
            Assert.IsTrue(persisted.ConflictingAllergens.Any());
            Assert.AreEqual(OrderStatus.PendingAllergyConfirmation, persisted.Status);
        }

        [TestMethod]
        public void RemoveIngredient_AllergenStillContributedByOtherIngredient_RemainsInConflicts()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 509;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            // order Classic Cheeseburger (1) and Creamy Pasta (4) both contain Wheat
            cartSvc.AddItem(customerId, 1);
            cartSvc.AddItem(customerId, 4);

            // now set profile to include Wheat and create order
            profile.Allergens = new List<Allergen> { new Allergen { Id = 5, Name = "Wheat" } };
            profileStore.SaveProfile(profile);
            var created = orderSvc.CreateOrderFromCart(customerId);

            kitchen.AcknowledgeAllergy(created.Id);

            // remove bun (ingredient id 3) from cheeseburger; pasta still contributes wheat
            kitchen.RemoveIngredient(created.Id, 1, 3);

            var persisted = orderStore.GetOrder(created.Id)!;
            Assert.IsTrue(persisted.ConflictingAllergens.Any(a => a.Id == 5));
            Assert.AreEqual(OrderStatus.PendingAllergyConfirmation, persisted.Status);
        }

        [TestMethod]
        public void RemoveIngredient_FinalConflictCleared_TransitionsToReadyForKitchen_AndKeepsAcknowledged()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 510;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);
            cartSvc.AddItem(customerId, 4);
            profile.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(profile);
            var created = orderSvc.CreateOrderFromCart(customerId);

            kitchen.AcknowledgeAllergy(created.Id);

            kitchen.RemoveIngredient(created.Id, 4, 12);

            var persisted = orderStore.GetOrder(created.Id)!;
            Assert.IsFalse(persisted.ConflictingAllergens.Any());
            Assert.AreEqual(OrderStatus.ReadyForKitchen, persisted.Status);
            Assert.IsTrue(persisted.KitchenAllergyAcknowledged);
        }

        [TestMethod]
        public void RemoveIngredient_IngredientWithMultipleAllergens_RecalculationKeepsOnlyRemainingConflicts()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 511;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            // Classic Cheeseburger (1) has bun (id 3) with allergens 5 and 9
            cartSvc.AddItem(customerId, 1);
            // now set allergies and create order
            profile.Allergens = new List<Allergen> { new Allergen { Id = 5, Name = "Wheat" }, new Allergen { Id = 9, Name = "Sesame" } };
            profileStore.SaveProfile(profile);
            var created = orderSvc.CreateOrderFromCart(customerId);

            kitchen.AcknowledgeAllergy(created.Id);

            // remove bun which contributes both allergens
            kitchen.RemoveIngredient(created.Id, 1, 3);

            var persisted = orderStore.GetOrder(created.Id)!;
            Assert.IsFalse(persisted.ConflictingAllergens.Any());
            Assert.AreEqual(OrderStatus.ReadyForKitchen, persisted.Status);
        }

        [TestMethod]
        public void RemoveIngredient_DuplicateMenuItemEntries_ModifiesOnlySelectedCartItemAndRecalculatesFromRemainingInstance()
        {
            var (kitchen, orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 506;
            var profile = profileStore.GetProfile(customerId);
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

            // Remove the Cream Sauce ingredient (id=12) from the first CartItem only
            kitchen.RemoveIngredient(saved.Id, 4, 12);

            var persisted = orderStore.GetOrder(saved.Id)!;

            // Both CartItem instances should still exist
            Assert.AreEqual(2, persisted.Items.Count);

            // The implementation selects the first matching CartItem instance; verify that exact behaviour
            Assert.IsFalse(
                persisted.Items[0].MenuItem.Ingredients.Any(i => i.Id == 12),
                "The ingredient should be removed from the first matching CartItem.");

            Assert.IsTrue(
                persisted.Items[1].MenuItem.Ingredients.Any(i => i.Id == 12),
                "The second duplicate CartItem should remain unchanged.");

            // Because the other duplicate still contributes the allergen, the conflict should remain
            Assert.IsTrue(persisted.ConflictingAllergens.Any(a => a.Id == 3));
            Assert.AreEqual(OrderStatus.PendingAllergyConfirmation, persisted.Status);
            Assert.IsTrue(persisted.KitchenAllergyAcknowledged);
        }
    }
}
