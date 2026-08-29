using System;
using System.Linq;
using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace AllergySystem.Tests
{
    [TestClass]
    public class OrderServiceTests
    {
        [TestMethod]
        public void CreateOrder_NoAllergyConflict_SetsStatusPendingAndPersists()
        {
            // Arrange
            var orderStore = new InMemoryOrderStore();
            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var profileStore = new InMemoryAllergyProfileStore();
            var validation = new AllergyValidationService();
            var service = new OrderService(orderStore, menuCatalog, profileStore, validation);

            var customerId = 42;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            var menuItem = menuCatalog.GetMenuItems().First(m => m.Id == 5);

            // Act
            var created = service.CreateOrder(customerId, menuItem.Id);

            // Assert
            Assert.AreEqual(OrderStatus.Pending, created.Status);
            Assert.HasCount(0, created.ConflictingAllergens);

            var persisted = orderStore.GetOrder(created.Id);
            Assert.IsNotNull(persisted);
            Assert.AreEqual(created.Id, persisted.Id);
            Assert.AreEqual(created.CustomerId, persisted.CustomerId);
            Assert.AreEqual(created.MenuItem.Id, persisted.MenuItem.Id);
            Assert.AreEqual(created.Status, persisted.Status);
        }

        [TestMethod]
        public void CreateOrder_WithAllergyConflict_SetsStatusPendingAllergyConfirmationAndStoresConflict()
        {
            // Arrange
            var orderStore = new InMemoryOrderStore();
            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var profileStore = new InMemoryAllergyProfileStore();
            var validation = new AllergyValidationService();
            var service = new OrderService(orderStore, menuCatalog, profileStore, validation);

            var customerId = 99;
            var profile = profileStore.GetProfile(customerId);
            // Mark customer allergic to Peanuts (id 1)
            profile.Allergens = new System.Collections.Generic.List<Allergen> { new Allergen { Id = 1, Name = "Peanuts" } };
            profileStore.SaveProfile(profile);

            // Peanut Chicken Noodles has id 2 in menu catalog
            var menuItem = menuCatalog.GetMenuItems().First(m => m.Id == 2);

            // Act
            var created = service.CreateOrder(customerId, menuItem.Id);

            // Assert
            Assert.AreEqual(OrderStatus.PendingAllergyConfirmation, created.Status);
            CollectionAssert.AreEqual(new System.Collections.Generic.List<int> { 1 }, created.ConflictingAllergens.Select(a => a.Id).ToList());

            var persisted = orderStore.GetOrder(created.Id);
            Assert.IsNotNull(persisted);
            Assert.AreEqual(created.Status, persisted.Status);
            CollectionAssert.AreEqual(created.ConflictingAllergens.Select(a => a.Id).ToList(), persisted.ConflictingAllergens.Select(a => a.Id).ToList());
        }

        [TestMethod]
        public void UpdateOrderStatus_WithConflict_CannotMoveToInPreparation()
        {
            // Arrange
            var orderStore = new InMemoryOrderStore();
            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var profileStore = new InMemoryAllergyProfileStore();
            var validation = new AllergyValidationService();
            var service = new OrderService(orderStore, menuCatalog, profileStore, validation);

            var customerId = 123;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens = new System.Collections.Generic.List<Allergen> { new Allergen { Id = 1, Name = "Peanuts" } };
            profileStore.SaveProfile(profile);

            var menuItem = menuCatalog.GetMenuItems().First(m => m.Id == 2);
            var created = service.CreateOrder(customerId, menuItem.Id);

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(() => service.UpdateOrderStatus(created.Id, OrderStatus.InPreparation));

            var persisted = orderStore.GetOrder(created.Id);
            Assert.IsNotNull(persisted);
            Assert.AreEqual(OrderStatus.PendingAllergyConfirmation, persisted.Status);
        }

        [TestMethod]
        public void UpdateOrderStatus_NoConflict_AllowsTransitionToInPreparation()
        {
            // Arrange
            var orderStore = new InMemoryOrderStore();
            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var profileStore = new InMemoryAllergyProfileStore();
            var validation = new AllergyValidationService();
            var service = new OrderService(orderStore, menuCatalog, profileStore, validation);

            var customerId = 7;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            var menuItem = menuCatalog.GetMenuItems().First(m => m.Id == 1);
            var created = service.CreateOrder(customerId, menuItem.Id);

            // Act
            service.UpdateOrderStatus(created.Id, OrderStatus.InPreparation);

            // Assert
            var persisted = orderStore.GetOrder(created.Id);
            Assert.IsNotNull(persisted);
            Assert.AreEqual(OrderStatus.InPreparation, persisted.Status);
        }

        [TestMethod]
        public void CreateOrder_InvalidMenuItemId_ThrowsArgumentException()
        {
            // Arrange
            var orderStore = new InMemoryOrderStore();
            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var profileStore = new InMemoryAllergyProfileStore();
            var validation = new AllergyValidationService();
            var service = new OrderService(orderStore, menuCatalog, profileStore, validation);

            var customerId = 50;
            var invalidMenuItemId = 999;

            // Act & Assert
            Assert.ThrowsExactly<ArgumentException>(
                () => service.CreateOrder(customerId, invalidMenuItemId));
        }

        [TestMethod]
        public void UpdateOrderStatus_WithConflict_CannotSkipDirectlyToCompleted()
        {
            // Arrange
            var orderStore = new InMemoryOrderStore();
            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var profileStore = new InMemoryAllergyProfileStore();
            var validation = new AllergyValidationService();
            var service = new OrderService(orderStore, menuCatalog, profileStore, validation);

            var customerId = 321;
            var profile = profileStore.GetProfile(customerId);

            profile.Allergens = new List<Allergen>
            {
                new Allergen { Id = 1, Name = "Peanuts" }
            };

            profileStore.SaveProfile(profile);

            var menuItem = menuCatalog.GetMenuItems()
                .First(m => m.Id == 2);

            var created = service.CreateOrder(customerId, menuItem.Id);

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(
                () => service.UpdateOrderStatus(
                    created.Id,
                    OrderStatus.Completed));

            var persisted = orderStore.GetOrder(created.Id);

            Assert.IsNotNull(persisted);
            Assert.AreEqual(
                OrderStatus.PendingAllergyConfirmation,
                persisted.Status);
        }
    }
}
