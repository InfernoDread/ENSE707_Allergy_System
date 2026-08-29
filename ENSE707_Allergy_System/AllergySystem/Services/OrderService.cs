using System;
using System.Linq;
using AllergySystem.Models;

namespace AllergySystem.Services
{
    // Minimal OrderService coordinating creation and status updates
    public class OrderService
    {
        private readonly InMemoryOrderStore _orderStore;
        private readonly MenuCatalogService _menuCatalog;
        private readonly InMemoryAllergyProfileStore _profileStore;
        private readonly AllergyValidationService _validationService;

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

        public void UpdateOrderStatus(int orderId, OrderStatus newStatus)
        {
            var order = _orderStore.GetOrder(orderId) ?? throw new ArgumentException("Order not found", nameof(orderId));

            // Enforce stricter rule: if there are confirmed conflicts, order may only remain PendingAllergyConfirmation or be Cancelled
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
