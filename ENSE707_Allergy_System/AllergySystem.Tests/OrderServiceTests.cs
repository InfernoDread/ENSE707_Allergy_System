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
        public void UpdateOrderStatus_NoConflict_AllowsTransitionToReadyForKitchen()
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
                OrderStatus.ReadyForKitchen);

            // Assert
            var persisted = orderStore.GetOrder(created.Id);

            Assert.IsNotNull(persisted);
            Assert.AreEqual(
                OrderStatus.ReadyForKitchen,
                persisted.Status);
        }

        [TestMethod]
        public void GetActiveOrders_ExcludesCompletedAndCancelledOrders()
        {
            // Arrange
            var service = CreateService(
                out var orderStore,
                out var cartService,
                out var profileStore);

            var profile = profileStore.GetProfile(10);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            cartService.AddItem(10, 5);
            var pending = service.CreateOrderFromCart(10);

            cartService.AddItem(11, 5);
            var completed = service.CreateOrderFromCart(11);
            service.UpdateOrderStatus(completed.Id, OrderStatus.Completed);

            cartService.AddItem(12, 5);
            var cancelled = service.CreateOrderFromCart(12);
            service.CancelOrder(cancelled.Id);

            // Act
            var activeOrders = service.GetActiveOrders();

            // Assert
            Assert.Contains(pending.Id, activeOrders.Select(order => order.Id).ToList());
            Assert.DoesNotContain(completed.Id, activeOrders.Select(order => order.Id).ToList());
            Assert.DoesNotContain(cancelled.Id, activeOrders.Select(order => order.Id).ToList());
        }

        [TestMethod]
        public void SendToKitchen_SafePendingOrder_SetsReadyForKitchen()
        {
            // Arrange
            var service = CreateService(
                out var orderStore,
                out var cartService,
                out var profileStore);

            var profile = profileStore.GetProfile(13);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);
            cartService.AddItem(13, 5);
            var created = service.CreateOrderFromCart(13);

            // Act
            service.SendToKitchen(created.Id);

            // Assert
            Assert.AreEqual(
                OrderStatus.ReadyForKitchen,
                orderStore.GetOrder(created.Id)!.Status);
        }

        [TestMethod]
        public void FrontOfHouseOrderActions_InvalidOrderId_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateService(
                out _,
                out _,
                out _);

            // Act & Assert
            Assert.ThrowsExactly<ArgumentException>(
                () => service.SendToKitchen(999));
            Assert.ThrowsExactly<ArgumentException>(
                () => service.CancelOrder(999));
        }

        [TestMethod]
        public void SendToKitchen_OrderWithConflict_ThrowsAndRemainsPendingAllergyConfirmation()
        {
            // Arrange
            var service = CreateService(
                out var orderStore,
                out var cartService,
                out var profileStore);

            var profile = profileStore.GetProfile(14);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);
            cartService.AddItem(14, 4);

            profile.Allergens = new List<Allergen>
            {
                new Allergen
                {
                    Id = 3,
                    Name = "Milk"
                }
            };
            profileStore.SaveProfile(profile);

            var created = service.CreateOrderFromCart(14);

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(
                () => service.SendToKitchen(created.Id));
            Assert.AreEqual(
                OrderStatus.PendingAllergyConfirmation,
                orderStore.GetOrder(created.Id)!.Status);
        }

        [TestMethod]
        public void CancelOrder_ActiveOrder_SetsCancelled()
        {
            // Arrange
            var service = CreateService(
                out var orderStore,
                out var cartService,
                out var profileStore);

            var profile = profileStore.GetProfile(15);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);
            cartService.AddItem(15, 5);
            var created = service.CreateOrderFromCart(15);

            // Act
            service.CancelOrder(created.Id);

            // Assert
            Assert.AreEqual(
                OrderStatus.Cancelled,
                orderStore.GetOrder(created.Id)!.Status);
        }

        [TestMethod]
        public void CancelOrder_OrderInKitchenProcessing_SetsCancelled()
        {
            // Arrange
            var service = CreateService(
                out var orderStore,
                out var cartService,
                out var profileStore);

            var profile = profileStore.GetProfile(17);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);
            cartService.AddItem(17, 5);
            var created = service.CreateOrderFromCart(17);
            service.UpdateOrderStatus(created.Id, OrderStatus.ReadyForKitchen);
            service.UpdateOrderStatus(created.Id, OrderStatus.InPreparation);

            // Act
            service.CancelOrder(created.Id);

            // Assert
            Assert.AreEqual(
                OrderStatus.Cancelled,
                orderStore.GetOrder(created.Id)!.Status);
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
        public void UpdateOrderStatus_WithConflict_CannotMoveToReadyForKitchen()
        {
            // Arrange
            var service = CreateService(
                out var orderStore,
                out var cartService,
                out var profileStore);

            var customerId = 8;

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
                    OrderStatus.ReadyForKitchen));

            var persisted = orderStore.GetOrder(created.Id);

            Assert.IsNotNull(persisted);
            Assert.AreEqual(
                OrderStatus.PendingAllergyConfirmation,
                persisted.Status);
        }

        [TestMethod]
        public void UpdateOrderStatus_WithConflict_CannotMoveToKitchenOrCompletedStatuses()
        {
            // Arrange
            var service = CreateService(
                out var orderStore,
                out var cartService,
                out var profileStore);

            var customerId = 16;

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
            foreach (var unsafeStatus in new[]
            {
                OrderStatus.ReadyForKitchen,
                OrderStatus.InPreparation,
                OrderStatus.Completed
            })
            {
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => service.UpdateOrderStatus(
                        created.Id,
                        unsafeStatus));
            }

            Assert.AreEqual(
                OrderStatus.PendingAllergyConfirmation,
                orderStore.GetOrder(created.Id)!.Status);
        }

        [TestMethod]
        public void UpdateOrderStatus_CompletedOrder_CannotBeCancelled()
        {
            // Arrange
            var service = CreateService(
                out var orderStore,
                out var cartService,
                out var profileStore);

            var customerId = 9;

            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);

            cartService.AddItem(customerId, 5);

            var created = service.CreateOrderFromCart(customerId);
            service.UpdateOrderStatus(created.Id, OrderStatus.Completed);

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(
                () => service.UpdateOrderStatus(
                    created.Id,
                    OrderStatus.Cancelled));

            var persisted = orderStore.GetOrder(created.Id);

            Assert.IsNotNull(persisted);
            Assert.AreEqual(OrderStatus.Completed, persisted.Status);
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