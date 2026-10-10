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
        private readonly DietaryCompatibilityService _dietaryCompatibilityService;

        // Creates the service with the stores and services needed to process customer orders.
        public OrderService( InMemoryOrderStore orderStore, CartService cartService, InMemoryAllergyProfileStore profileStore, 
            AllergyValidationService validationService, DietaryCompatibilityService dietaryCompatibilityService)
        {
            _orderStore = orderStore;
            _cartService = cartService;
            _profileStore = profileStore;
            _validationService = validationService;
            _dietaryCompatibilityService = dietaryCompatibilityService;
        }

        // Creates a new order containing all items currently in the customer's cart.
        // Every cart item is revalidated against the customer's current allergy profile before the order is saved.
        public Order CreateOrderFromCart(int customerId, bool dietaryWarningsConfirmed = false)
        {
            var cart = _cartService.GetCart(customerId);

            if (cart.Items.Count == 0)
            {
                throw new InvalidOperationException("Cannot create an order from an empty cart.");
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
            
            var dietaryWarnings = cart.Items
                .SelectMany(item =>
                    _dietaryCompatibilityService.FindDietaryWarnings(
                        item.MenuItem,
                        profile.DietaryRestrictions))
                .GroupBy(restriction => restriction.Id)
                .Select(group => group.First())
                .ToList();

            // Dietary restrictions are advisory rather than hard safety blocks.
            // They require explicit confirmation only when there is no declared allergen conflict. Allergen safety always takes precedence.
            if (!conflicts.Any() &&
                dietaryWarnings.Any() &&
                !dietaryWarningsConfirmed)
            {
                throw new DietaryConfirmationRequiredException(dietaryWarnings);
            }

            var order = new Order
            {
                CustomerId = customerId,

                // Copy the cart contents into the order so the order
                // remains independent of the shopping cart.
                Items = cart.Items.Select(item => new CartItem
                {
                    MenuItem = new MenuItem
                    {
                        Id = item.MenuItem.Id,
                        Name = item.MenuItem.Name,
                        Description = item.MenuItem.Description,

                        DietaryLabels = item.MenuItem.DietaryLabels.Select(label => new DietaryRestriction
                        {
                            Id = label.Id,
                            Name = label.Name
                        }).ToList(),
                        Ingredients = item.MenuItem.Ingredients.Select(ingredient => new Ingredient
                        {
                            Id = ingredient.Id,
                            Name = ingredient.Name,

                            Allergens = ingredient.Allergens.Select(allergen => new Allergen
                            {
                                Id = allergen.Id,
                                Name = allergen.Name
                            }).ToList()
                        }).ToList()
                    },  
                    Quantity = item.Quantity
                }).ToList(),

                CreatedAt = DateTime.UtcNow,
                ConflictingAllergens = conflicts
                    .Select(allergen => new Allergen
                    {
                        Id = allergen.Id,
                        Name = allergen.Name
                    })
                    .ToList(),

                Status = conflicts.Any()
                    ? OrderStatus.PendingAllergyConfirmation
                    : OrderStatus.Pending
            };

            var savedOrder = _orderStore.SaveOrder(order);

            // The cart is cleared only after the order has been saved.
            _cartService.ClearCart(customerId);

            return savedOrder;
        }

        public List<Order> GetActiveOrders()
        {
            return _orderStore.GetActiveOrders();
        }

        public void SendToKitchen(int orderId)
        {
            UpdateOrderStatus(orderId, OrderStatus.ReadyForKitchen);
        }

        public void CancelOrder(int orderId)
        {
            UpdateOrderStatus(orderId, OrderStatus.Cancelled);
        }

        // Updates an order's status while preventing orders with unresolved allergen conflicts from progressing to an unsafe status.
        public void UpdateOrderStatus(int orderId, OrderStatus newStatus)
        {
            var order = _orderStore.GetOrder(orderId)
                ?? throw new ArgumentException(
                    "Order not found",
                    nameof(orderId));
            if (newStatus == OrderStatus.InPreparation || newStatus == OrderStatus.Completed)
            {
                throw new InvalidOperationException(
                    "InPreparation and Completed transitions must be handled by the kitchen workflow.");
            }

            if (newStatus == OrderStatus.ReadyForKitchen && (order.Status != OrderStatus.Pending || order.ConflictingAllergens.Any()))
            {
                throw new InvalidOperationException(
                    "Only safe Pending orders may move to ReadyForKitchen.");
            }

            if (newStatus == OrderStatus.Cancelled && (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled))
            {
                throw new InvalidOperationException(
                    "Completed or Cancelled orders cannot be cancelled again.");
            }

            if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
            {
                throw new InvalidOperationException(
                    "Completed or Cancelled orders are no longer active FOH work.");
            }

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
