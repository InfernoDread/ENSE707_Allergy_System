using System;
using System.Linq;
using AllergySystem.Models;

namespace AllergySystem.Services
{
    // Coordinates kitchen order handling, allergen resolution,
    // status progression, and audit recording.
    public class KitchenOrderService
    {
        private readonly InMemoryOrderStore _orderStore;
        private readonly InMemoryAllergyProfileStore _profileStore;
        private readonly AllergyValidationService _validationService;
        private readonly AuditService _auditService;


        public KitchenOrderService(InMemoryOrderStore orderStore, InMemoryAllergyProfileStore profileStore, AllergyValidationService validationService, AuditService auditService)
        {
            _orderStore = orderStore;
            _profileStore = profileStore;
            _validationService = validationService;
            _auditService = auditService;
        }

        // Kitchen acknowledges an order's allergen warning.
        // Rules enforced:
        // - Order must exist.
        // - Order must be PendingAllergyConfirmation.
        // - Order must have unresolved ConflictingAllergens.
        // - Completed or Cancelled orders are rejected.
        // - Idempotent: repeated calls do not change ConflictingAllergens or Status and leave KitchenAllergyAcknowledged true.
        public void AcknowledgeAllergy(int orderId)
        {
            var order = _orderStore.GetOrder(orderId)
                ?? throw new ArgumentException("Order not found", nameof(orderId));

            if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
            {
                throw new InvalidOperationException("Completed or Cancelled orders cannot be acknowledged.");
            }

            if (!order.ConflictingAllergens.Any())
            {
                throw new InvalidOperationException("Only orders with unresolved allergen conflicts may be acknowledged.");
            }

            if (order.Status != OrderStatus.PendingAllergyConfirmation)
            {
                throw new InvalidOperationException("Only orders PendingAllergyConfirmation may be acknowledged.");
            }

            if (order.KitchenAllergyAcknowledged)
            {
                // Idempotent no-op: do not persist unnecessarily.
                return;
            }

            order.KitchenAllergyAcknowledged = true;

            // Do NOT modify ConflictingAllergens or Status.
            _orderStore.SaveOrder(order);

            // Record audit only when acknowledgement actually changed state.
            _auditService.Record(
                "Kitchen",
                "AcknowledgeAllergy",
                "Order",
                order.Id,
                $"Order acknowledged for allergen warning: {order.Id}; ConflictingAllergens=[{string.Join(',', order.ConflictingAllergens.Select(a => a.Name + "(" + a.Id + ")"))}]; CustomerId={order.CustomerId}");
        }

        // Returns orders visible to kitchen staff according to the kitchen visibility rules.
        // Included statuses: PendingAllergyConfirmation, ReadyForKitchen, InPreparation
        // Excluded: Pending, Completed, Cancelled
        public List<Order> GetKitchenOrders()
        {
            var active = _orderStore.GetActiveOrders();

            return active
                .Where(o => o.Status == OrderStatus.PendingAllergyConfirmation
                            || o.Status == OrderStatus.ReadyForKitchen
                            || o.Status == OrderStatus.InPreparation)
                .ToList();
        }

        // Moves a kitchen-visible order into preparation.
        // Only a safe, non-empty order that is ReadyForKitchen may begin preparation.
        public void StartPreparation(int orderId)
        {
            var order = _orderStore.GetOrder(orderId)
                ?? throw new ArgumentException(
                    "Order not found.",
                    nameof(orderId));

            if (order.Status != OrderStatus.ReadyForKitchen)
            {
                throw new InvalidOperationException(
                    "Only orders that are ReadyForKitchen may enter preparation.");
            }

            if (order.ConflictingAllergens.Any())
            {
                throw new InvalidOperationException(
                    "Orders with unresolved allergen conflicts cannot enter preparation.");
            }

            if (!order.Items.Any())
            {
                throw new InvalidOperationException(
                    "An empty order cannot enter preparation.");
            }

            order.Status = OrderStatus.InPreparation;

            _orderStore.SaveOrder(order);

            _auditService.Record(
                "Kitchen",
                "StartPreparation",
                "Order",
                order.Id,
                $"PrevStatus=ReadyForKitchen; Items={order.Items.Count}; CustomerId={order.CustomerId}");
        }

        // Marks an order as completed after kitchen preparation.
        // Only a safe, non-empty order currently InPreparation may be completed.
        public void CompleteOrder(int orderId)
        {
            var order = _orderStore.GetOrder(orderId)
                ?? throw new ArgumentException(
                    "Order not found.",
                    nameof(orderId));

            if (order.Status != OrderStatus.InPreparation)
            {
                throw new InvalidOperationException(
                    "Only orders that are InPreparation may be completed.");
            }

            if (order.ConflictingAllergens.Any())
            {
                throw new InvalidOperationException(
                    "Orders with unresolved allergen conflicts cannot be completed.");
            }

            if (!order.Items.Any())
            {
                throw new InvalidOperationException(
                    "An empty order cannot be completed.");
            }

            order.Status = OrderStatus.Completed;

            _orderStore.SaveOrder(order);

            _auditService.Record(
                "Kitchen",
                "CompleteOrder",
                "Order",
                order.Id,
                $"PrevStatus=InPreparation; Items={order.Items.Count}; CustomerId={order.CustomerId}");
        }

        // Recalculates the allergen conflicts for an order using the customer's current allergy profile and the order's remaining items.
        // A specified ingredient can be excluded so conflicts are calculated before the real order is mutated.
        private List<Allergen> RecalculateConflicts(Order order, CartItem modifiedOrderItem, int? excludedIngredientId = null)
        {
            var profile = _profileStore.GetProfile(order.CustomerId);

            return order.Items
                .Select(item =>
                {
                    if (!ReferenceEquals(item, modifiedOrderItem))
                    {
                        return item.MenuItem;
                    }

                    return new MenuItem
                    {
                        Id = item.MenuItem.Id,
                        Name = item.MenuItem.Name,
                        Description = item.MenuItem.Description,
                        Ingredients = item.MenuItem.Ingredients
                            .Where(ingredient => ingredient.Id != excludedIngredientId)
                            .ToList()
                    };
                })
                .SelectMany(menuItem =>
                    _validationService.FindConflicts(
                        menuItem,
                        profile.Allergens))
                .GroupBy(allergen => allergen.Id)
                .Select(group => group.First())
                .ToList();
        }

        // Recalculates allergen conflicts as if an entire order item were removed.
        // The real order is not modified by this method.
        private List<Allergen> RecalculateConflictsExcludingOrderItem(Order order, CartItem excludedOrderItem)
        {
            var profile = _profileStore.GetProfile(order.CustomerId);

            return order.Items
                .Where(item => !ReferenceEquals(item, excludedOrderItem))
                .SelectMany(item =>
                    _validationService.FindConflicts(
                        item.MenuItem,
                        profile.Allergens))
                .GroupBy(allergen => allergen.Id)
                .Select(group => group.First())
                .ToList();
        }

        // Removes an ingredient from a menu item in an order, if it resolves an unresolved allergen conflict.
        public void RemoveIngredient(int orderId, int menuItemId, int ingredientId)
        {
            var order = _orderStore.GetOrder(orderId)
                ?? throw new ArgumentException(
                    "Order not found.",
                    nameof(orderId));

            if (order.Status != OrderStatus.PendingAllergyConfirmation)
            {
                throw new InvalidOperationException(
                    "Ingredients may only be removed while an order is awaiting allergy resolution.");
            }

            if (!order.KitchenAllergyAcknowledged)
            {
                throw new InvalidOperationException(
                    "The allergen warning must be acknowledged before resolving the order.");
            }

            if (!order.ConflictingAllergens.Any())
            {
                throw new InvalidOperationException(
                    "The order has no unresolved allergen conflicts.");
            }

            var orderItem = order.Items
                .FirstOrDefault(item => item.MenuItem.Id == menuItemId)
                ?? throw new ArgumentException(
                    "Menu item not found in the order.",
                    nameof(menuItemId));

            var ingredient = orderItem.MenuItem.Ingredients
                .FirstOrDefault(i => i.Id == ingredientId)
                ?? throw new ArgumentException(
                    "Ingredient not found in the order item.",
                    nameof(ingredientId));

            var unresolvedConflictIds = order.ConflictingAllergens
                .Select(allergen => allergen.Id)
                .ToHashSet();

            var resolvesKnownConflict = ingredient.Allergens
                .Any(allergen => unresolvedConflictIds.Contains(allergen.Id));

            if (!resolvesKnownConflict)
            {
                throw new InvalidOperationException(
                    "Only an ingredient contributing to an unresolved allergen conflict may be removed.");
            }

            // Calculate the result before mutating the real order.
            // This prevents a failed recalculation from leaving the order partially modified.
            var remainingConflicts = RecalculateConflicts(
                order,
                orderItem,
                ingredientId);

            // Recalculation succeeded, so the mutation can now be committed.
            orderItem.MenuItem.Ingredients.Remove(ingredient);

            order.ConflictingAllergens = remainingConflicts;

            if (!remainingConflicts.Any())
            {
                // Kitchen-specific resolution transition: once all allergen conflicts have been resolved, the order is safe to proceed to the kitchen.
                order.Status = OrderStatus.ReadyForKitchen;
            }

            _orderStore.SaveOrder(order);

            _auditService.Record(
                "Kitchen",
                "RemoveIngredient",
                "Order",
                order.Id,
                $"RemovedIngredient MenuItemId={menuItemId}; IngredientId={ingredientId}; RemainingConflicts={remainingConflicts.Count}");
        }
        // Removes an entire menu item from an order when that item contributes
        // to an unresolved allergen conflict.
        //
        // Rules enforced:
        // - Order must exist.
        // - Order must be awaiting allergy resolution.
        // - Kitchen must have acknowledged the allergen warning.
        // - Order must contain unresolved allergen conflicts.
        // - Menu item must exist in the order.
        // - Menu item must contribute to at least one unresolved conflict.
        // - Remaining conflicts are recalculated before the real order is modified.
        // - If conflicts remain, the order stays PendingAllergyConfirmation.
        // - If no conflicts remain and items remain, the order becomes ReadyForKitchen.
        // - If no items remain, the order is Cancelled.
        public void RemoveOrderItem(int orderId, int menuItemId)
        {
            var order = _orderStore.GetOrder(orderId)
                ?? throw new ArgumentException(
                    "Order not found.",
                    nameof(orderId));

            if (order.Status != OrderStatus.PendingAllergyConfirmation)
            {
                throw new InvalidOperationException(
                    "Order items may only be removed while an order is awaiting allergy resolution.");
            }

            if (!order.KitchenAllergyAcknowledged)
            {
                throw new InvalidOperationException(
                    "The allergen warning must be acknowledged before resolving the order.");
            }

            if (!order.ConflictingAllergens.Any())
            {
                throw new InvalidOperationException(
                    "The order has no unresolved allergen conflicts.");
            }

            var orderItem = order.Items
                .FirstOrDefault(item => item.MenuItem.Id == menuItemId)
                ?? throw new ArgumentException(
                    "Menu item not found in the order.",
                    nameof(menuItemId));

            var unresolvedConflictIds = order.ConflictingAllergens
                .Select(allergen => allergen.Id)
                .ToHashSet();

            var contributesToKnownConflict = orderItem.MenuItem.Ingredients
                .SelectMany(ingredient => ingredient.Allergens)
                .Any(allergen => unresolvedConflictIds.Contains(allergen.Id));

            if (!contributesToKnownConflict)
            {
                throw new InvalidOperationException(
                    "Only an order item contributing to an unresolved allergen conflict may be removed.");
            }

            // Calculate the result before mutating the real order.
            // This prevents a failed recalculation from leaving the order partially modified.
            var remainingConflicts = RecalculateConflictsExcludingOrderItem(order, orderItem);

            // Recalculation succeeded, so the mutation can now be committed.
            order.Items.Remove(orderItem);

            order.ConflictingAllergens = remainingConflicts;

            if (!order.Items.Any())
            {
                // Nothing remains for the kitchen to prepare.
                order.Status = OrderStatus.Cancelled;
            }
            else if (!remainingConflicts.Any())
            {
                // Remaining order is now safe to prepare.
                order.Status = OrderStatus.ReadyForKitchen;
            }

            // Otherwise unresolved conflicts remain, so status stays PendingAllergyConfirmation.
            _orderStore.SaveOrder(order);

            _auditService.Record(
                "Kitchen",
                "RemoveOrderItem",
                "Order",
                order.Id,
                $"RemovedMenuItem MenuItemId={menuItemId}; RemainingItems={order.Items.Count}; RemainingConflicts={remainingConflicts.Count}");
        }
    }
}
