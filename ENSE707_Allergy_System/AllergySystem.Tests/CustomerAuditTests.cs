using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using AllergySystem.Models;
using AllergySystem.Pages.Customer;
using AllergySystem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AllergySystem.Tests
{
    [TestClass]
    public class CustomerAuditTests
    {
        private static (OrderService orderSvc, InMemoryOrderStore orderStore, CartService cartSvc, InMemoryAllergyProfileStore profileStore, DietaryCompatibilityService dietarySvc, AuditService auditSvc, InMemoryAuditStore auditStore) CreateServices()
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

            var dietarySvc = new DietaryCompatibilityService();

            var orderService = new OrderService(
                orderStore,
                cartService,
                profileStore,
                validationService,
                dietarySvc);

            var auditStore = new InMemoryAuditStore();
            var auditSvc = new AuditService(auditStore);

            return (orderService, orderStore, cartService, profileStore, dietarySvc, auditSvc, auditStore);
        }

        private static CurrentUserContext CreateCustomerContext(int customerId)
        {
            var identity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, customerId.ToString()),
                    new Claim(ClaimTypes.Role, PrototypeRoles.Customer)
                },
                "Test");

            var accessor = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };

            return new CurrentUserContext(accessor);
        }

        [TestMethod]
        public void Menu_AddToCartBlocked_RecordsAllergenWarning()
        {
            var (orderSvc, orderStore, cartSvc, profileStore, dietarySvc, auditSvc, auditStore) = CreateServices();

            var customerId = 1;
            var profile = profileStore.GetProfile(customerId);
            profile.DietaryRestrictions.Clear();
            profile.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(profile);

            var menuModel = new MenuModel(
                CreateCustomerContext(customerId),
                new MenuCatalogService(new AllergenCatalogService()),
                cartSvc,
                auditSvc)
            {
                SelectedMenuItemId = 1
            };

            menuModel.OnPost();

            var entries = auditStore.GetAll();
            Assert.AreEqual(1, entries.Count);

            var entry = entries.Single();

            Assert.AreEqual(PrototypeRoles.Customer, entry.ActorRole);
            Assert.AreEqual("AllergenWarning", entry.Action);
            Assert.AreEqual("MenuItem", entry.EntityType);
            Assert.AreEqual(1, entry.EntityId);
            StringAssert.Contains(entry.Details, "CustomerId=1");
            StringAssert.Contains(entry.Details, "MenuItemId=1");
            StringAssert.Contains(entry.Details, "ConflictingAllergens=");
            StringAssert.Contains(entry.Details, "3:Milk");
        }

        [TestMethod]
        public void Cart_DietaryPrompt_RecordsDietaryWarningAndKeepsCartIntact()
        {
            var (orderSvc, orderStore, cartSvc, profileStore, dietarySvc, auditSvc, auditStore) = CreateServices();

            var customerId = 1;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profile.DietaryRestrictions = new List<DietaryRestriction> { new DietaryRestriction { Id = 2, Name = "Vegan" } };
            profileStore.SaveProfile(profile);

            // Add an item that is not labelled Vegan (Classic Cheeseburger id=1)
            cartSvc.AddItem(customerId, 1);

            var model = new CartModel(
                CreateCustomerContext(customerId),
                cartSvc,
                orderSvc,
                auditSvc,
                dietarySvc,
                profileStore);

            model.OnPostPlaceOrder(dietaryWarningsConfirmed: false);

            // No order created
            Assert.IsNull(model.CreatedOrder);

            // Cart should remain intact
            Assert.IsTrue(model.Cart.Items.Any(i => i.MenuItem.Id == 1));

            // Order store remains empty
            var persisted = orderStore.GetOrdersForCustomer(customerId);
            Assert.AreEqual(0, persisted.Count);

            var entries = auditStore.GetAll();
            Assert.AreEqual(1, entries.Count);
            var entry = entries.Single();

            Assert.AreEqual(PrototypeRoles.Customer, entry.ActorRole);
            Assert.AreEqual("DietaryWarning", entry.Action);
            Assert.AreEqual("Cart", entry.EntityType);
            Assert.AreEqual(customerId, entry.EntityId);
            StringAssert.Contains(entry.Details, "CustomerId=1");
            StringAssert.Contains(entry.Details, "Restrictions=");
            StringAssert.Contains(entry.Details, "2:Vegan");
            StringAssert.Contains(entry.Details, "Source=CheckoutPrompt");

            // Check AffectedItemIds exactly (extract value)
            var start = entry.Details.IndexOf("AffectedItemIds=", StringComparison.Ordinal);
            Assert.IsTrue(start >= 0);
            var after = entry.Details.Substring(start + "AffectedItemIds=".Length);
            var end = after.IndexOf(';');
            var val = end >= 0 ? after.Substring(0, end) : after;
            Assert.AreEqual("1", val);
        }

        [TestMethod]
        public void Cart_DietaryOverrideRecordedWhenConfirmed_SafeOfAllergens()
        {
            var (orderSvc, orderStore, cartSvc, profileStore, dietarySvc, auditSvc, auditStore) = CreateServices();

            var customerId = 1;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profile.DietaryRestrictions = new List<DietaryRestriction> { new DietaryRestriction { Id = 2, Name = "Vegan" } };
            profileStore.SaveProfile(profile);

            cartSvc.AddItem(customerId, 1); // Classic Cheeseburger id=1

            var model = new CartModel(
                CreateCustomerContext(customerId),
                cartSvc,
                orderSvc,
                auditSvc,
                dietarySvc,
                profileStore);

            model.OnPostPlaceOrder(dietaryWarningsConfirmed: true);

            // Order created and persisted
            Assert.IsNotNull(model.CreatedOrder);
            var persisted = orderStore.GetOrdersForCustomer(customerId);
            Assert.AreEqual(1, persisted.Count);

            // Cart cleared
            Assert.AreEqual(0, model.Cart.Items.Count);

            var entries = auditStore.GetAll();
            Assert.AreEqual(1, entries.Count);
            var entry = entries.Single();

            Assert.AreEqual(PrototypeRoles.Customer, entry.ActorRole);
            Assert.AreEqual("DietaryOverride", entry.Action);
            Assert.AreEqual("Order", entry.EntityType);
            Assert.AreEqual(model.CreatedOrder.Id, entry.EntityId);
            StringAssert.Contains(entry.Details, "CustomerId=1");

            // AffectedItemIds exactly = "1"
            var start = entry.Details.IndexOf("AffectedItemIds=", StringComparison.Ordinal);
            Assert.IsTrue(start >= 0);
            var after = entry.Details.Substring(start + "AffectedItemIds=".Length);
            var end = after.IndexOf(';');
            var val = end >= 0 ? after.Substring(0, end) : after;
            Assert.AreEqual("1", val);

            StringAssert.Contains(entry.Details, "Restrictions=2:Vegan");
        }

        [TestMethod]
        public void Cart_AllergenOnCheckout_RecordsAllergenWarningWithOrderIdOnly()
        {
            var (orderSvc, orderStore, cartSvc, profileStore, dietarySvc, auditSvc, auditStore) = CreateServices();

            var customerId = 1;

            // Start without allergens so adding Creamy Pasta succeeds
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profile.DietaryRestrictions = new List<DietaryRestriction> { new DietaryRestriction { Id = 2, Name = "Vegan" } };
            profileStore.SaveProfile(profile);

            // Add Creamy Pasta (id=4) while no allergens in profile
            cartSvc.AddItem(customerId, 4);

            // Now introduce Milk allergen (Id = 3) into the profile before checkout
            profile = profileStore.GetProfile(customerId);
            profile.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
            profileStore.SaveProfile(profile);

            var model = new CartModel(
                CreateCustomerContext(customerId),
                cartSvc,
                orderSvc,
                auditSvc,
                dietarySvc,
                profileStore);

            model.OnPostPlaceOrder(dietaryWarningsConfirmed: true); // confirmation should not bypass allergen precedence

            Assert.IsNotNull(model.CreatedOrder);
            Assert.AreEqual(OrderStatus.PendingAllergyConfirmation, model.CreatedOrder.Status);

            var persisted = orderStore.GetOrdersForCustomer(customerId);
            Assert.AreEqual(1, persisted.Count);

            var entries = auditStore.GetAll();
            Assert.AreEqual(1, entries.Count);
            var entry = entries.Single();

            Assert.AreEqual(PrototypeRoles.Customer, entry.ActorRole);
            Assert.AreEqual("AllergenWarning", entry.Action);
            Assert.AreEqual("Order", entry.EntityType);
            Assert.AreEqual(model.CreatedOrder.Id, entry.EntityId);
            StringAssert.Contains(entry.Details, "ConflictingAllergens=");
            StringAssert.Contains(entry.Details, "3:Milk");

            // AffectedItemIds exactly = "4"
            var start = entry.Details.IndexOf("AffectedItemIds=", StringComparison.Ordinal);
            Assert.IsTrue(start >= 0);
            var after = entry.Details.Substring(start + "AffectedItemIds=".Length);
            var end = after.IndexOf(';');
            var val = end >= 0 ? after.Substring(0, end) : after;
            Assert.AreEqual("4", val);
        }

        [TestMethod]
        public void Cart_CombinedAllergyAndDietary_ConfirmFalse_And_True_AlwaysAllergyPrecedence()
        {
            // Test twice: once with dietaryWarningsConfirmed=false, once with =true
            foreach (var confirm in new[] { false, true })
            {
                var (orderSvc, orderStore, cartSvc, profileStore, dietarySvc, auditSvc, auditStore) = CreateServices();

                var customerId = 1;
                var profile = profileStore.GetProfile(customerId);
                profile.Allergens.Clear();
                profile.DietaryRestrictions = new List<DietaryRestriction> { new DietaryRestriction { Id = 2, Name = "Vegan" } };
                profileStore.SaveProfile(profile);

                // Add Creamy Pasta while profile has no allergens
                cartSvc.AddItem(customerId, 4);

                // Now introduce Milk allergen (Id = 3) before checkout
                profile = profileStore.GetProfile(customerId);
                profile.Allergens = new List<Allergen> { new Allergen { Id = 3, Name = "Milk" } };
                profileStore.SaveProfile(profile);

                var model = new CartModel(
                    CreateCustomerContext(customerId),
                    cartSvc,
                    orderSvc,
                    auditSvc,
                    dietarySvc,
                    profileStore);

                model.OnPostPlaceOrder(dietaryWarningsConfirmed: confirm);

                Assert.IsNotNull(model.CreatedOrder);
                Assert.AreEqual(OrderStatus.PendingAllergyConfirmation, model.CreatedOrder.Status);

                var persisted = orderStore.GetOrdersForCustomer(customerId);
                Assert.AreEqual(1, persisted.Count);

                var entries = auditStore.GetAll();
                // Exactly one event: AllergenWarning
                Assert.AreEqual(1, entries.Count);
                var entry = entries.Single();
                Assert.AreEqual("AllergenWarning", entry.Action);
                Assert.AreEqual("Order", entry.EntityType);
                Assert.IsTrue(entry.Details.Contains("3:Milk"));

                // Ensure no dietary events are recorded
                Assert.IsFalse(entries.Any(e => e.Action == "DietaryWarning" || e.Action == "DietaryOverride"));
            }
        }

        [TestMethod]
        public void Cart_SafeOrder_WithAndWithoutConfirm_NoWarningsOrOverrides()
        {
            foreach (var confirm in new[] { false, true })
            {
                var (orderSvc, orderStore, cartSvc, profileStore, dietarySvc, auditSvc, auditStore) = CreateServices();

                var customerId = 1;
                var profile = profileStore.GetProfile(customerId);
                profile.Allergens.Clear();
                profile.DietaryRestrictions.Clear(); // safe
                profileStore.SaveProfile(profile);

                cartSvc.AddItem(customerId, 5); // Garden Salad id=5

                var model = new CartModel(
                    CreateCustomerContext(customerId),
                    cartSvc,
                    orderSvc,
                    auditSvc,
                    dietarySvc,
                    profileStore);

                model.OnPostPlaceOrder(dietaryWarningsConfirmed: confirm);

                // Order created and persisted
                Assert.IsNotNull(model.CreatedOrder);
                var persisted = orderStore.GetOrdersForCustomer(customerId);
                Assert.AreEqual(1, persisted.Count);

                // Cart cleared
                Assert.AreEqual(0, model.Cart.Items.Count);

                // No audit events
                var entries = auditStore.GetAll();
                Assert.AreEqual(0, entries.Count);
            }
        }

        [TestMethod]
        public void Cart_MixedCart_PromptThenConfirm_TargetsOnlyIncompatibleItem()
        {
            var (orderSvc, orderStore, cartSvc, profileStore, dietarySvc, auditSvc, auditStore) = CreateServices();

            var customerId = 1;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profile.DietaryRestrictions = new List<DietaryRestriction> { new DietaryRestriction { Id = 2, Name = "Vegan" } };
            profileStore.SaveProfile(profile);

            // Add Garden Salad (id=5, compatible) and Creamy Pasta (id=4, incompatible)
            cartSvc.AddItem(customerId, 5);
            cartSvc.AddItem(customerId, 4);

            var model = new CartModel(
                CreateCustomerContext(customerId),
                cartSvc,
                orderSvc,
                auditSvc,
                dietarySvc,
                profileStore);

            // First POST: should prompt (no confirm) -> DietaryWarning recorded, cart intact, no order
            model.OnPostPlaceOrder(dietaryWarningsConfirmed: false);

            Assert.IsNull(model.CreatedOrder);
            var persistedAfterPrompt = orderStore.GetOrdersForCustomer(customerId);
            Assert.AreEqual(0, persistedAfterPrompt.Count);

            var entries = auditStore.GetAll();
            Assert.AreEqual(1, entries.Count);
            var promptEntry = entries.Single();
            Assert.AreEqual("DietaryWarning", promptEntry.Action);
            Assert.AreEqual("Cart", promptEntry.EntityType);

            // Extract AffectedItemIds exactly and assert equals "4"
            var start = promptEntry.Details.IndexOf("AffectedItemIds=", StringComparison.Ordinal);
            Assert.IsTrue(start >= 0);
            var after = promptEntry.Details.Substring(start + "AffectedItemIds=".Length);
            var end = after.IndexOf(';');
            var val = end >= 0 ? after.Substring(0, end) : after;
            Assert.AreEqual("4", val);

            // Cart still contains both items
            Assert.IsTrue(model.Cart.Items.Any(i => i.MenuItem.Id == 4));
            Assert.IsTrue(model.Cart.Items.Any(i => i.MenuItem.Id == 5));

            // Now confirm and post again using a fresh CartModel to simulate a new HTTP request
            var confirmModel = new CartModel(
                CreateCustomerContext(customerId),
                cartSvc,
                orderSvc,
                auditSvc,
                dietarySvc,
                profileStore);

            confirmModel.OnPostPlaceOrder(dietaryWarningsConfirmed: true);

            // Order saved and cart cleared
            Assert.IsNotNull(confirmModel.CreatedOrder);
            var persistedAfterConfirm = orderStore.GetOrdersForCustomer(customerId);
            Assert.AreEqual(1, persistedAfterConfirm.Count);
            Assert.AreEqual(0, confirmModel.Cart.Items.Count);

            // Now audit store should contain two entries: first DietaryWarning, second DietaryOverride
            var allEntries = auditStore.GetAll();
            Assert.AreEqual(2, allEntries.Count);

            var overrideEntry = allEntries.Single(entry => entry.Action == "DietaryOverride");
            Assert.AreEqual("Order", overrideEntry.EntityType);
            Assert.AreEqual(confirmModel.CreatedOrder.Id, overrideEntry.EntityId);

            // Extract AffectedItemIds exactly from override entry
            var s = overrideEntry.Details.IndexOf("AffectedItemIds=", StringComparison.Ordinal);
            Assert.IsTrue(s >= 0);
            var aft = overrideEntry.Details.Substring(s + "AffectedItemIds=".Length);
            var e = aft.IndexOf(';');
            var v = e >= 0 ? aft.Substring(0, e) : aft;
            Assert.AreEqual("4", v);

            // Ensure both dietary entries reference restriction id and name
            var promptContainsRestriction = promptEntry.Details.Contains("2:Vegan");
            var overrideContainsRestriction = overrideEntry.Details.Contains("2:Vegan");
            Assert.IsTrue(promptContainsRestriction);
            Assert.IsTrue(overrideContainsRestriction);
        }

        [TestMethod]
        public void MenuAndCart_GetRequests_DoNotCreateEvents()
        {
            var (orderSvc, orderStore, cartSvc, profileStore, dietarySvc, auditSvc, auditStore) = CreateServices();

            var customerId = 1;
            var menuModel = new MenuModel(
                CreateCustomerContext(customerId),
                new MenuCatalogService(new AllergenCatalogService()),
                cartSvc,
                auditSvc);

            var cartModel = new CartModel(
                CreateCustomerContext(customerId),
                cartSvc,
                orderSvc,
                auditSvc,
                dietarySvc,
                profileStore);

            menuModel.OnGet();
            cartModel.OnGet();

            var entries = auditStore.GetAll();
            Assert.AreEqual(0, entries.Count);
        }
    }
}
