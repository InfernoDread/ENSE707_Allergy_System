using System;
using System.Collections.Generic;
using System.Linq;
using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AllergySystem.Tests
{
    [TestClass]
    public class OrderSnapshotTests
    {
        private static (OrderService orderService, InMemoryOrderStore orderStore, CartService cartService, InMemoryAllergyProfileStore profileStore, MenuCatalogService menuCatalog) CreateServices()
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

            return (orderService, orderStore, cartService, profileStore, menuCatalog);
        }

        [TestMethod]
        public void OrderSnapshot_CatalogIngredientChange_DoesNotModifyExistingOrder()
        {
            var (orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 400;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            // pick a menu item that has ingredients
            var menu = menuCatalog.GetMenuItems().First();
            var menuId = menu.Id;

            cartSvc.AddItem(customerId, menuId);

            var created = orderSvc.CreateOrderFromCart(customerId);

            var persisted = orderStore.GetOrder(created.Id)!;

            var originalIngredientCount = persisted.Items[0].MenuItem.Ingredients.Count;

            // mutate the source catalogue
            var sourceMenu = menuCatalog.GetMenuItems().First(m => m.Id == menuId);
            // change ingredients
            sourceMenu.Ingredients.Clear();

            // persisted order should retain original ingredients
            var reloaded = orderStore.GetOrder(created.Id)!;
            Assert.AreEqual(originalIngredientCount, reloaded.Items[0].MenuItem.Ingredients.Count);

            // references should not be the same
            Assert.AreNotSame(reloaded.Items[0].MenuItem, sourceMenu);
            Assert.AreNotSame(reloaded.Items[0].MenuItem.Ingredients, sourceMenu.Ingredients);
        }

        [TestMethod]
        public void OrderSnapshot_CatalogAllergenChange_DoesNotModifyExistingOrder()
        {
            var (orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 401;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            // pick a specific menu item that is known to include allergens (use explicit id for determinism)
            var menu = menuCatalog.GetMenuItems().First(m => m.Id == 1);
            var menuId = menu.Id;

            // identify first ingredient and allergen on the source (use the captured menu instance)
            var sourceIngredient = menu.Ingredients.First(i => i.Allergens.Any());
            var sourceAllergen = sourceIngredient.Allergens.First();

            cartSvc.AddItem(customerId, menuId);
            var created = orderSvc.CreateOrderFromCart(customerId);
            var persisted = orderStore.GetOrder(created.Id)!;

            // mutate the source allergen (change name) via the catalogue instance so subsequent calls see the change
            var catalogAllergen = menuCatalog.GetMenuItems().First(m => m.Id == menuId)
                .Ingredients.First(i => i.Allergens.Any()).Allergens.First();
            catalogAllergen.Name = "CHANGED-NAME";

            var reloaded = orderStore.GetOrder(created.Id)!;

            // persisted order's allergen name should remain unchanged
            var orderIngredientWithAllergen = reloaded.Items[0].MenuItem.Ingredients.FirstOrDefault(i => i.Allergens.Any());
            Assert.IsNotNull(orderIngredientWithAllergen, "Persisted order has no ingredient allergens to verify.");

            var orderAllergenName = orderIngredientWithAllergen!.Allergens.First().Name;
            Assert.AreNotEqual("CHANGED-NAME", orderAllergenName);

            // ensure lists and instances are not shared (compare against the captured sourceIngredient)
            Assert.AreNotSame(orderIngredientWithAllergen!.Allergens, sourceIngredient.Allergens);
            Assert.AreNotSame(orderIngredientWithAllergen.Allergens.First(), sourceIngredient.Allergens.First());
        }

        [TestMethod]
        public void OrderSnapshot_MultipleOrders_AreIndependent()
        {
            var (orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerA = 402;
            var customerB = 403;
            var profileA = profileStore.GetProfile(customerA);
            profileA.Allergens.Clear();
            profileStore.SaveProfile(profileA);
            var profileB = profileStore.GetProfile(customerB);
            profileB.Allergens.Clear();
            profileStore.SaveProfile(profileB);

            var menu = menuCatalog.GetMenuItems().First();
            var menuId = menu.Id;

            cartSvc.AddItem(customerA, menuId);
            var orderA = orderSvc.CreateOrderFromCart(customerA);

            cartSvc.AddItem(customerB, menuId);
            var orderB = orderSvc.CreateOrderFromCart(customerB);

            var a = orderStore.GetOrder(orderA.Id)!;
            var b = orderStore.GetOrder(orderB.Id)!;

            // verify instances are separate
            Assert.AreNotSame(a.Items[0].MenuItem, b.Items[0].MenuItem);
            Assert.AreNotSame(a.Items[0].MenuItem.Ingredients, b.Items[0].MenuItem.Ingredients);

            // compare corresponding ingredient and allergen instances by position
            var count = Math.Min(a.Items[0].MenuItem.Ingredients.Count, b.Items[0].MenuItem.Ingredients.Count);
            for (int i = 0; i < count; i++)
            {
                var ingA = a.Items[0].MenuItem.Ingredients[i];
                var ingB = b.Items[0].MenuItem.Ingredients[i];
                Assert.AreNotSame(ingA, ingB);
                Assert.AreNotSame(ingA.Allergens, ingB.Allergens);
                var alCount = Math.Min(ingA.Allergens.Count, ingB.Allergens.Count);
                for (int j = 0; j < alCount; j++)
                {
                    Assert.AreNotSame(ingA.Allergens[j], ingB.Allergens[j]);
                }
            }

            // mutate order A's ingredients and ensure B is unchanged
            var originalBCount = b.Items[0].MenuItem.Ingredients.Count;
            a.Items[0].MenuItem.Ingredients.Clear();

            var reloadedB = orderStore.GetOrder(orderB.Id)!;
            Assert.AreEqual(originalBCount, reloadedB.Items[0].MenuItem.Ingredients.Count);
        }

        [TestMethod]
        public void OrderSnapshot_DeepCopy_PreservesOriginalValues()
        {
            var (orderSvc, orderStore, cartSvc, profileStore, menuCatalog) = CreateServices();

            var customerId = 404;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            // pick a menu item with ingredients and allergens for richer checks
            var sourceMenu = menuCatalog.GetMenuItems().First(m => m.Ingredients.Any());
            var menuId = sourceMenu.Id;

            // capture source values
            var sourceSnapshot = new
            {
                Id = sourceMenu.Id,
                Name = sourceMenu.Name,
                Description = sourceMenu.Description,
                Ingredients = sourceMenu.Ingredients.Select(i => new { i.Id, i.Name, Allergens = i.Allergens.Select(a => new { a.Id, a.Name }).ToList() }).ToList()
            };

            cartSvc.AddItem(customerId, menuId);
            var created = orderSvc.CreateOrderFromCart(customerId);
            var persisted = orderStore.GetOrder(created.Id)!;

            var orderMenu = persisted.Items[0].MenuItem;

            Assert.AreEqual(sourceSnapshot.Id, orderMenu.Id);
            Assert.AreEqual(sourceSnapshot.Name, orderMenu.Name);
            Assert.AreEqual(sourceSnapshot.Description, orderMenu.Description);

            var oi = orderMenu.Ingredients;
            Assert.AreEqual(sourceSnapshot.Ingredients.Count, oi.Count);

            for (int i = 0; i < oi.Count; i++)
            {
                Assert.AreEqual(sourceSnapshot.Ingredients[i].Id, oi[i].Id);
                Assert.AreEqual(sourceSnapshot.Ingredients[i].Name, oi[i].Name);
                var sourceAll = sourceSnapshot.Ingredients[i].Allergens;
                var orderAll = oi[i].Allergens;
                Assert.AreEqual(sourceAll.Count, orderAll.Count);
                for (int j = 0; j < sourceAll.Count; j++)
                {
                    Assert.AreEqual(sourceAll[j].Id, orderAll[j].Id);
                    Assert.AreEqual(sourceAll[j].Name, orderAll[j].Name);
                }
            }
        }
    }
}
