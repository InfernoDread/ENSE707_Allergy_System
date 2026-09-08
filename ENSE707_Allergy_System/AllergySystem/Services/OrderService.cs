using System;
using System.Linq;
using AllergySystem.Models;

namespace AllergySystem.Services
{
    // This service Coordinates customer order creation and order status updates.
    // /It creates complete orders from shopping cart contents and performs a final allergy validation before an order is saved.
    public class OrderService
    {
        private readonly InMemoryOrderStore _orderStore;
        private readonly CartService _cartService;
        private readonly InMemoryAllergyProfileStore _profileStore;
        private readonly AllergyValidationService _validationService;

        // Creates the service with the stores and services needed to process customer orders.
        public OrderService( InMemoryOrderStore orderStore, CartService cartService, InMemoryAllergyProfileStore profileStore, AllergyValidationService validationService)
        {
            _orderStore = orderStore;
            _cartService = cartService;
            _profileStore = profileStore;
            _validationService = validationService;
        }

        // Creates a new order containing all items currently in the customer's cart.
        // Every cart item is revalidated against the customer's current allergy profile before the order is saved.
        public Order CreateOrderFromCart(int customerId)
        {
            var cart = _cartService.GetCart(customerId);

            if (cart.Items.Count == 0)
            {
                throw new InvalidOperationException(
                    "Cannot create an order from an empty cart.");
            }

            var profile = _profileStore.GetProfile(customerId);

            var conflicts = cart.Items
                .SelectMany(item =>
                    _validationService.FindConflicts(
                        item.MenuItem,
                        profile.Allergens))
                .GroupBy(allergen => allergen.Id)
                .Select(group => group.First())
                .ToList();

            var order = new Order
            {
                CustomerId = customerId,

                // Copy the cart contents into the order so the order
                // remains independent of the shopping cart.
                Items = cart.Items
                    .Select(item => new CartItem
                    {
                        MenuItem = item.MenuItem,
                        Quantity = item.Quantity
                    })
                    .ToList(),

                CreatedAt = DateTime.UtcNow,
                ConflictingAllergens = conflicts,

                Status = conflicts.Any()
                    ? OrderStatus.PendingAllergyConfirmation
                    : OrderStatus.Pending
            };

            var savedOrder = _orderStore.SaveOrder(order);

            // The cart is cleared only after the order has been saved.
            _cartService.ClearCart(customerId);

            return savedOrder;
        }

        // Updates an order's status while preventing orders with unresolved allergen conflicts from progressing to an unsafe status.
        public void UpdateOrderStatus(
            int orderId,
            OrderStatus newStatus)
        {
            var order = _orderStore.GetOrder(orderId)
                ?? throw new ArgumentException(
                    "Order not found",
                    nameof(orderId));

            // Orders with allergen conflicts can only remain awaiting confirmation or be cancelled until the conflict has been resolved.
            if (order.ConflictingAllergens.Any())
            {
                if (newStatus != OrderStatus.PendingAllergyConfirmation &&
                    newStatus != OrderStatus.Cancelled)
                {
                    throw new InvalidOperationException(
                        "Orders with unresolved allergen conflicts may only remain " +
                        "PendingAllergyConfirmation or be Cancelled.");
                }
            }

            order.Status = newStatus;
            _orderStore.SaveOrder(order);
        }
    }
}
