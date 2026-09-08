using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AllergySystem.Tests
{
    // These tests verify cart-based order creation, persistence, allergy validation, cart clearing, and status transitions.
    [TestClass]
    public class OrderServiceTests
    {
        private static OrderService CreateService(
            out InMemoryOrderStore orderStore,
            out CartService cartService,
            out InMemoryAllergyProfileStore profileStore)
        {
            orderStore = new InMemoryOrderStore();
            var cartStore = new InMemoryCartStore();
            profileStore = new InMemoryAllergyProfileStore();

            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var validationService = new AllergyValidationService();

            cartService = new CartService(
                cartStore,
                menuCatalog,
                profileStore,
                validationService);

            return new OrderService(
                orderStore,
                cartService,
                profileStore,
                validationService);
        }

        [TestMethod]
        public void CreateOrderFromCart_SafeItems_CreatesPendingOrderAndClearsCart()
        {
            // Arrange
            var service = CreateService(
                out var orderStore,
                out var cartService,
                out var profileStore);

            var customerId = 1;

            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            cartService.AddItem(customerId, 5);
            cartService.AddItem(customerId, 5);

            // Act
            var created = service.CreateOrderFromCart(customerId);

            // Assert
            Assert.AreEqual(OrderStatus.Pending, created.Status);
            Assert.HasCount(0, created.ConflictingAllergens);

            Assert.HasCount(1, created.Items);
            Assert.AreEqual(5, created.Items[0].MenuItem.Id);
            Assert.AreEqual(2, created.Items[0].Quantity);

            var persisted = orderStore.GetOrder(created.Id);

            Assert.IsNotNull(persisted);
            Assert.AreEqual(created.Id, persisted.Id);
            Assert.AreEqual(customerId, persisted.CustomerId);

            var cart = cartService.GetCart(customerId);
            Assert.HasCount(0, cart.Items);
        }

        [TestMethod]
        public void CreateOrderFromCart_MultipleSafeItems_CreatesSingleMultiItemOrder()
        {
            // Arrange
            var service = CreateService(
                out _,
                out var cartService,
                out var profileStore);

            var customerId = 2;

            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            cartService.AddItem(customerId, 1);
            cartService.AddItem(customerId, 5);

            // Act
            var created = service.CreateOrderFromCart(customerId);

            // Assert
            Assert.HasCount(2, created.Items);

            CollectionAssert.AreEquivalent(
                new List<int> { 1, 5 },
                created.Items
                    .Select(i => i.MenuItem.Id)
                    .ToList());
        }

        [TestMethod]
        public void CreateOrderFromCart_ProfileChangedAfterCartAddition_DetectsConflict()
        {
            // Arrange
            var service = CreateService(
                out _,
                out var cartService,
                out var profileStore);

            var customerId = 3;

            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            // Creamy Pasta is safe when the profile has no Milk allergy.
            cartService.AddItem(customerId, 4);

            // Customer updates their allergy profile after the item is already in the cart.
            profile.Allergens = new List<Allergen>
            {
                new Allergen
                {
                    Id = 3,
                    Name = "Milk"
                }
            };

            profileStore.SaveProfile(profile);

            // Act
            var created = service.CreateOrderFromCart(customerId);

            // Assert
            Assert.AreEqual(
                OrderStatus.PendingAllergyConfirmation,
                created.Status);

            Assert.Contains(
                3,
                created.ConflictingAllergens
                    .Select(a => a.Id)
                    .ToList());
        }

        [TestMethod]
        public void CreateOrderFromCart_EmptyCart_ThrowsInvalidOperationException()
        {
            // Arrange
            var service = CreateService(
                out _,
                out _,
                out _);

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(
                () => service.CreateOrderFromCart(4));
        }

        [TestMethod]
        public void UpdateOrderStatus_NoConflict_AllowsTransitionToInPreparation()
        {
            // Arrange
            var service = CreateService(
                out var orderStore,
                out var cartService,
                out var profileStore);

            var customerId = 5;

            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            cartService.AddItem(customerId, 5);

            var created = service.CreateOrderFromCart(customerId);

            // Act
            service.UpdateOrderStatus(
                created.Id,
                OrderStatus.InPreparation);

            // Assert
            var persisted = orderStore.GetOrder(created.Id);

            Assert.IsNotNull(persisted);
            Assert.AreEqual(
                OrderStatus.InPreparation,
                persisted.Status);
        }

        [TestMethod]
        public void UpdateOrderStatus_WithConflict_CannotMoveToInPreparation()
        {
            // Arrange
            var service = CreateService(
                out var orderStore,
                out var cartService,
                out var profileStore);

            var customerId = 6;

            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            cartService.AddItem(customerId, 4);

            profile.Allergens = new List<Allergen>
            {
                new Allergen
                {
                    Id = 3,
                    Name = "Milk"
                }
            };

            profileStore.SaveProfile(profile);

            var created = service.CreateOrderFromCart(customerId);

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(
                () => service.UpdateOrderStatus(
                    created.Id,
                    OrderStatus.InPreparation));

            var persisted = orderStore.GetOrder(created.Id);

            Assert.IsNotNull(persisted);
            Assert.AreEqual(
                OrderStatus.PendingAllergyConfirmation,
                persisted.Status);
        }

        [TestMethod]
        public void UpdateOrderStatus_WithConflict_AllowsCancellation()
        {
            // Arrange
            var service = CreateService(
                out var orderStore,
                out var cartService,
                out var profileStore);

            var customerId = 7;

            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            cartService.AddItem(customerId, 4);

            profile.Allergens = new List<Allergen>
            {
                new Allergen
                {
                    Id = 3,
                    Name = "Milk"
                }
            };

            profileStore.SaveProfile(profile);

            var created = service.CreateOrderFromCart(customerId);

            // Act
            service.UpdateOrderStatus(
                created.Id,
                OrderStatus.Cancelled);

            // Assert
            var persisted = orderStore.GetOrder(created.Id);

            Assert.IsNotNull(persisted);
            Assert.AreEqual(
                OrderStatus.Cancelled,
                persisted.Status);
        }
    }
}