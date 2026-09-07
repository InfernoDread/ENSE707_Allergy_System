using System;
using System.Linq;
using AllergySystem.Models;

namespace AllergySystem.Services
{
    // This service Coordinates customer order creation and order status updates.
    // /It uses menu, allergy profile, and validation services to determine whether an order has allergen conflicts.
    public class OrderService
    {
        private readonly InMemoryOrderStore _orderStore;
        private readonly MenuCatalogService _menuCatalog;
        private readonly InMemoryAllergyProfileStore _profileStore;
        private readonly AllergyValidationService _validationService;

        // Creates the service with the stores and services needed to process customer orders.
        public OrderService(
            InMemoryOrderStore orderStore,
            MenuCatalogService menuCatalog,
            InMemoryAllergyProfileStore profileStore,
            AllergyValidationService validationService)
        {
            _orderStore = orderStore;
            _menuCatalog = menuCatalog;
            _profileStore = profileStore;
            _validationService = validationService;
        }

        // Creates a new order for a selected menu item and checks it against the customer's allergy profile before assigning the initial order status.
        public Order CreateOrder(int customerId, int menuItemId)
        {
            var menuItem = _menuCatalog.GetMenuItems().FirstOrDefault(m => m.Id == menuItemId);

            if (menuItem == null)
                throw new ArgumentException($"Menu item {menuItemId} not found", nameof(menuItemId));

            var profile = _profileStore.GetProfile(customerId);

            var conflicts = _validationService.FindConflicts(menuItem, profile.Allergens);

            var order = new Order
            {
                CustomerId = customerId,
                MenuItem = menuItem,
                CreatedAt = DateTime.UtcNow,
                ConflictingAllergens = conflicts.ToList(),
                Status = conflicts.Any() ? OrderStatus.PendingAllergyConfirmation : OrderStatus.Pending
            };

            return _orderStore.SaveOrder(order);
        }

        // Updates an order's status while preventing orders with unresolved allergen conflicts from progressing to an unsafe status.
        public void UpdateOrderStatus(int orderId, OrderStatus newStatus)
        {
            var order = _orderStore.GetOrder(orderId) ?? throw new ArgumentException("Order not found", nameof(orderId));

            // Orders with allergen conflicts can only remain awaiting confirmation or be cancelled until the conflict has been resolved.
            if (order.ConflictingAllergens != null && order.ConflictingAllergens.Any())
            {
                if (newStatus != OrderStatus.PendingAllergyConfirmation && newStatus != OrderStatus.Cancelled)
                {
                    throw new InvalidOperationException("Orders with unresolved allergen conflicts may only remain PendingAllergyConfirmation or be Cancelled.");
                }
            }

            order.Status = newStatus;
            _orderStore.SaveOrder(order);
        }
    }
}
