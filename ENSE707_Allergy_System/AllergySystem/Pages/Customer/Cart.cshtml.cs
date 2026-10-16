using System.Linq;
using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AllergySystem.Pages.Customer
{
    // This code handles the customer shopping cart page.
    // It displays the current cart contents, allows items to be removed, and creates an order from the complete cart at checkout.
    public class CartModel : PageModel
    {
        private readonly CurrentUserContext _currentUser;
        private readonly CartService _cartService;
        private readonly OrderService _orderService;
        private readonly AuditService _auditService;
        private readonly DietaryCompatibilityService _dietaryCompatibilityService;
        private readonly InMemoryAllergyProfileStore _profileStore;

        // Creates the page model with the service needed to manage the customer's cart.
        public CartModel(
            CurrentUserContext currentUser,
            CartService cartService,
            OrderService orderService,
            AuditService auditService,
            DietaryCompatibilityService dietaryCompatibilityService,
            InMemoryAllergyProfileStore profileStore)
        {
            _currentUser = currentUser;
            _cartService = cartService;
            _orderService = orderService;
            _auditService = auditService;
            _dietaryCompatibilityService = dietaryCompatibilityService;
            _profileStore = profileStore;
        }

        public ShoppingCart Cart { get; private set; } = new();

        public int TotalItemCount { get; private set; }

        public Order? CreatedOrder { get; private set; }

        public List<MenuItem> ConflictingMenuItems { get; private set; } = new();

        public List<DietaryRestriction> DietaryWarnings { get; private set; } = new();

        // Loads the customer's current cart.
        public void OnGet()
        {
            LoadCart();
        }

        // Removes the selected menu item from the customer's cart.
        public IActionResult OnPostRemove(int menuItemId)
        {
            _cartService.RemoveItem(
                _currentUser.CustomerId,
                menuItemId);

            return RedirectToPage();
        }

        // Loads the cart and calculates the total quantity of items.
        private void LoadCart()
        {
            Cart = _cartService.GetCart(_currentUser.CustomerId);

            TotalItemCount = Cart.Items
                .Sum(i => i.Quantity);
        }

        // Creates one order containing all items currently in the customer's cart.
        // Dietary warnings require explicit customer confirmation, while allergen conflicts continue through the existing safety workflow.
        public void OnPostPlaceOrder(bool dietaryWarningsConfirmed = false)
        {
            // Compute dietary incompatibilities per cart item BEFORE we call CreateOrderFromCart.
            var cartSnapshot = _cartService.GetCart(_currentUser.CustomerId);
            var profile = _profileStore.GetProfile(_currentUser.CustomerId);

            var dietaryWarningsByItem = cartSnapshot.Items.ToDictionary(
                item => item.MenuItem.Id,
                item => _dietaryCompatibilityService.FindDietaryWarnings(item.MenuItem, profile.DietaryRestrictions));

            var anyDietaryWarningsExist = dietaryWarningsByItem.Any(kvp => kvp.Value != null && kvp.Value.Count > 0);

            try
            {
                CreatedOrder = _orderService.CreateOrderFromCart(
                    _currentUser.CustomerId,
                    dietaryWarningsConfirmed);

                if (CreatedOrder.ConflictingAllergens != null && CreatedOrder.ConflictingAllergens.Count > 0)
                {
                    var conflictingAllergenPairs = string.Join(',', CreatedOrder.ConflictingAllergens.Select(a => $"{a.Id}:{a.Name}"));

                    var conflictingAllergenIds = CreatedOrder.ConflictingAllergens.Select(a => a.Id).ToHashSet();

                    var affectedItemIds = CreatedOrder.Items
                        .Where(item =>
                            item.MenuItem.Ingredients
                                .SelectMany(i => i.Allergens)
                                .Any(a => conflictingAllergenIds.Contains(a.Id)))
                        .Select(item => item.MenuItem.Id)
                        .Distinct()
                        .ToList();

                    var affectedNames = CreatedOrder.Items
                        .Where(i => affectedItemIds.Contains(i.MenuItem.Id))
                        .Select(i => i.MenuItem.Name)
                        .Distinct()
                        .ToList();

                    var details =
                        $"CustomerId={_currentUser.CustomerId};OrderId={CreatedOrder.Id};AffectedItemIds={string.Join(',', affectedItemIds)};AffectedItemNames={string.Join(',', affectedNames)};ConflictingAllergens={conflictingAllergenPairs};Source=Checkout";

                    _auditService.Record(
                        PrototypeRoles.Customer,
                        "AllergenWarning",
                        "Order",
                        CreatedOrder.Id,
                        details);

                    var conflictingAllergenIdSet = conflictingAllergenIds;
                    ConflictingMenuItems = CreatedOrder.Items
                        .Where(item =>
                            item.MenuItem.Ingredients
                                .SelectMany(i => i.Allergens)
                                .Any(a => conflictingAllergenIdSet.Contains(a.Id)))
                        .Select(item => item.MenuItem)
                        .DistinctBy(item => item.Id)
                        .ToList();
                }
                else
                {
                    if (anyDietaryWarningsExist && dietaryWarningsConfirmed)
                    {
                        var affectedItems = dietaryWarningsByItem
                            .Where(kvp => kvp.Value != null && kvp.Value.Count > 0)
                            .Select(kvp => kvp.Key)
                            .Distinct()
                            .ToList();

                        var restrictions = dietaryWarningsByItem
                            .SelectMany(kvp => kvp.Value ?? new List<DietaryRestriction>())
                            .GroupBy(r => r.Id)
                            .Select(g => g.First())
                            .ToList();

                        var restrictionPairs = string.Join(',', restrictions.Select(r => $"{r.Id}:{r.Name}"));

                        var details =
                            $"CustomerId={_currentUser.CustomerId};OrderId={CreatedOrder.Id};AffectedItemIds={string.Join(',', affectedItems)};Restrictions={restrictionPairs};Source=Checkout";
                        _auditService.Record(
                            PrototypeRoles.Customer,
                            "DietaryOverride",
                            "Order",
                            CreatedOrder.Id,
                            details);
                    }
                }
            }
            catch (DietaryConfirmationRequiredException exception)
            {
                DietaryWarnings = exception.Warnings.ToList();

                var warningIds = DietaryWarnings.Select(r => r.Id).ToHashSet();

                var affectedItemIds = dietaryWarningsByItem
                    .Where(kvp => kvp.Value.Any(r => warningIds.Contains(r.Id)))
                    .Select(kvp => kvp.Key)
                    .Distinct()
                    .ToList();

                var restrictionPairs = string.Join(',', DietaryWarnings.Select(r => $"{r.Id}:{r.Name}"));

                var details =
                    $"CustomerId={_currentUser.CustomerId};AffectedItemIds={string.Join(',', affectedItemIds)};Restrictions={restrictionPairs};Source=CheckoutPrompt";

                _auditService.Record(
                    PrototypeRoles.Customer,
                    "DietaryWarning",
                    "Cart",
                    _currentUser.CustomerId,
                    details);
            }

            LoadCart();
        }
    }
}
