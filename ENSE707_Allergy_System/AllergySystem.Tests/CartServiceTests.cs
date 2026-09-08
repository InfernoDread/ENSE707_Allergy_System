using AllergySystem.Models;
using AllergySystem.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace AllergySystem.Tests
{
    // Verifies shopping cart behaviour, including safe item addition,
    // allergy conflict blocking, quantity updates, removal, and clearing.
    [TestClass]
    public class CartServiceTests
    {
        private static CartService CreateService(
            out InMemoryCartStore cartStore,
            out InMemoryAllergyProfileStore profileStore)
        {
            cartStore = new InMemoryCartStore();
            profileStore = new InMemoryAllergyProfileStore();

            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var validationService = new AllergyValidationService();

            return new CartService(
                cartStore,
                menuCatalog,
                profileStore,
                validationService);
        }

        [TestMethod]
        public void AddItem_SafeItem_AddsItemToCart()
        {
            // Arrange
            var service = CreateService(
                out var cartStore,
                out var profileStore);

            var customerId = 1;

            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            // Garden Salad has no allergens.
            var gardenSaladId = 5;

            // Act
            var conflicts = service.AddItem(
                customerId,
                gardenSaladId);

            // Assert
            Assert.HasCount(0, conflicts);

            var cart = cartStore.GetCart(customerId);

            Assert.HasCount(1, cart.Items);
            Assert.AreEqual(gardenSaladId, cart.Items[0].MenuItem.Id);
            Assert.AreEqual(1, cart.Items[0].Quantity);
        }

        [TestMethod]
        public void AddItem_SameSafeItemTwice_IncrementsQuantity()
        {
            // Arrange
            var service = CreateService(
                out var cartStore,
                out var profileStore);

            var customerId = 2;

            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            var gardenSaladId = 5;

            // Act
            service.AddItem(customerId, gardenSaladId);
            service.AddItem(customerId, gardenSaladId);

            // Assert
            var cart = cartStore.GetCart(customerId);

            Assert.HasCount(1, cart.Items);
            Assert.AreEqual(2, cart.Items[0].Quantity);
        }

        [TestMethod]
        public void AddItem_ConflictingItem_DoesNotAddItemAndReturnsConflict()
        {
            // Arrange
            var service = CreateService(
                out var cartStore,
                out var profileStore);

            var customerId = 3;

            var profile = profileStore.GetProfile(customerId);
            profile.Allergens = new List<Allergen>
            {
                new Allergen
                {
                    Id = 1,
                    Name = "Peanuts"
                }
            };

            profileStore.SaveProfile(profile);

            // Peanut Chicken Noodles contains Peanuts.
            var peanutChickenNoodlesId = 2;

            // Act
            var conflicts = service.AddItem(
                customerId,
                peanutChickenNoodlesId);

            // Assert
            Assert.HasCount(1, conflicts);
            Assert.AreEqual(1, conflicts[0].Id);
            Assert.AreEqual("Peanuts", conflicts[0].Name);

            var cart = cartStore.GetCart(customerId);

            Assert.HasCount(0, cart.Items);
        }

        [TestMethod]
        public void AddItem_InvalidMenuItemId_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateService(
                out _,
                out _);

            // Act & Assert
            Assert.ThrowsExactly<ArgumentException>(
                () => service.AddItem(1, 999));
        }

        [TestMethod]
        public void RemoveItem_ExistingItem_RemovesItemFromCart()
        {
            // Arrange
            var service = CreateService(
                out var cartStore,
                out var profileStore);

            var customerId = 4;

            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            var gardenSaladId = 5;

            service.AddItem(customerId, gardenSaladId);

            // Act
            service.RemoveItem(
                customerId,
                gardenSaladId);

            // Assert
            var cart = cartStore.GetCart(customerId);

            Assert.HasCount(0, cart.Items);
        }

        [TestMethod]
        public void ClearCart_ExistingItems_EmptiesCart()
        {
            // Arrange
            var service = CreateService(
                out var cartStore,
                out var profileStore);

            var customerId = 5;

            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            service.AddItem(customerId, 1);
            service.AddItem(customerId, 5);

            // Act
            service.ClearCart(customerId);

            // Assert
            var cart = cartStore.GetCart(customerId);

            Assert.HasCount(0, cart.Items);
        }
    }
}
