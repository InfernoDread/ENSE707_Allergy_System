using System;
using System.Collections.Generic;
using System.Linq;
using AllergySystem.Models;
using AllergySystem.Pages.Customer;
using AllergySystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AllergySystem.Tests
{
    [TestClass]
    public class CartModelIntegrationTests
    {
        private static (OrderService orderSvc, InMemoryOrderStore orderStore, CartService cartSvc, InMemoryAllergyProfileStore profileStore) CreateServices()
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

            return (orderService, orderStore, cartService, profileStore);
        }

        [TestMethod]
        public void Cart_DietaryWarningWithoutConfirmation_DoesNotCreateOrderAndLeavesCartIntact()
        {
            var (orderSvc, orderStore, cartSvc, profileStore) = CreateServices();

            var customerId = 1;

            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profile.DietaryRestrictions = new List<DietaryRestriction>
            {
                new DietaryRestriction { Id = 2, Name = "Vegan" }
            };
            profileStore.SaveProfile(profile);

            // Add an item that is not labelled Vegan (Classic Cheeseburger Id = 1)
            cartSvc.AddItem(customerId, 1);

            var model = new CartModel(cartSvc, orderSvc);

            model.OnPostPlaceOrder(dietaryWarningsConfirmed: false);

            Assert.IsNull(model.CreatedOrder);
            Assert.IsTrue(model.DietaryWarnings.Any(d => d.Id == 2));

            // Cart should remain intact
            Assert.IsTrue(model.Cart.Items.Any(i => i.MenuItem.Id == 1));

            // No order persisted for this customer
            var persisted = orderStore.GetOrdersForCustomer(customerId);
            Assert.AreEqual(0, persisted.Count);
        }

        [TestMethod]
        public void Cart_DietaryWarningConfirmed_CreatesPendingOrderAndClearsCart()
        {
            var (orderSvc, orderStore, cartSvc, profileStore) = CreateServices();

            var customerId = 1;

            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profile.DietaryRestrictions = new List<DietaryRestriction>
            {
                new DietaryRestriction { Id = 2, Name = "Vegan" }
            };
            profileStore.SaveProfile(profile);

            cartSvc.AddItem(customerId, 1);

            var model = new CartModel(cartSvc, orderSvc);

            model.OnPostPlaceOrder(dietaryWarningsConfirmed: true);

            Assert.IsNotNull(model.CreatedOrder);
            Assert.AreEqual(OrderStatus.Pending, model.CreatedOrder.Status);
            Assert.IsFalse(model.CreatedOrder.ConflictingAllergens.Any());

            // Cart cleared
            Assert.AreEqual(0, model.Cart.Items.Count);

            var persisted = orderStore.GetOrdersForCustomer(customerId);
            Assert.AreEqual(1, persisted.Count);
        }

        [TestMethod]
        public void Cart_AllergyConflictTakesPrecedence_ProducesPendingAllergyConfirmationAndClearsCart()
        {
            var (orderSvc, orderStore, cartSvc, profileStore) = CreateServices();

            var customerId = 1;

            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            // Add a dietary restriction so the item would also generate a dietary advisory
            profile.DietaryRestrictions = new List<DietaryRestriction>
            {
                new DietaryRestriction { Id = 2, Name = "Vegan" }
            };
            profileStore.SaveProfile(profile);

            // Add Creamy Pasta (Id = 4) while profile has no allergens
            cartSvc.AddItem(customerId, 4);

            // Now introduce Milk allergen (Id = 3) into the profile before checkout
            profile.Allergens = new List<Allergen>
            {
                new Allergen { Id = 3, Name = "Milk" }
            };
            profileStore.SaveProfile(profile);

            var model = new CartModel(cartSvc, orderSvc);

            model.OnPostPlaceOrder(dietaryWarningsConfirmed: false);

            Assert.IsNotNull(model.CreatedOrder);
            Assert.AreEqual(OrderStatus.PendingAllergyConfirmation, model.CreatedOrder.Status);
            Assert.IsTrue(model.CreatedOrder.ConflictingAllergens.Any(a => a.Id == 3));

            // Conflicting menu items should include the Creamy Pasta (Id = 4)
            Assert.IsTrue(model.ConflictingMenuItems.Any(m => m.Id == 4));

            // Dietary warnings should not be populated in this allergy-driven flow
            Assert.AreEqual(0, model.DietaryWarnings.Count);

            // Cart cleared after order saved
            Assert.AreEqual(0, model.Cart.Items.Count);

            var persisted = orderStore.GetOrdersForCustomer(customerId);
            Assert.AreEqual(1, persisted.Count);
            Assert.AreEqual(OrderStatus.PendingAllergyConfirmation, persisted[0].Status);
        }

        [TestMethod]
        public void Cart_SafeOrder_NoWarningsOrConflicts_CreatesPendingOrderAndClearsCart()
        {
            var (orderSvc, orderStore, cartSvc, profileStore) = CreateServices();

            var customerId = 1;

            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profile.DietaryRestrictions.Clear();
            profileStore.SaveProfile(profile);

            // Add a safe menu item to the cart.
            cartSvc.AddItem(customerId, 5);

            var model = new CartModel(cartSvc, orderSvc);

            model.OnPostPlaceOrder();

            Assert.IsNotNull(model.CreatedOrder);
            Assert.AreEqual(OrderStatus.Pending, model.CreatedOrder.Status);
            Assert.AreEqual(0, model.CreatedOrder.ConflictingAllergens.Count);
            Assert.AreEqual(0, model.DietaryWarnings.Count);

            Assert.AreEqual(0, model.Cart.Items.Count);

            var persisted = orderStore.GetOrdersForCustomer(customerId);
            Assert.AreEqual(1, persisted.Count);
        }
    }
}
