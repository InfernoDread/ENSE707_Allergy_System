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
        // Temporary customer ID used while the prototype has no authentication system.
        private const int CurrentCustomerId = 1;

        private readonly CartService _cartService;
        private readonly OrderService _orderService;

        // Creates the page model with the service needed to manage the customer's cart.
        public CartModel(CartService cartService, OrderService orderService)
        {
            _cartService = cartService;
            _orderService = orderService;
        }

        public ShoppingCart Cart { get; private set; } = new();

        public int TotalItemCount { get; private set; }

        public Order? CreatedOrder { get; private set; }

        public List<MenuItem> ConflictingMenuItems { get; private set; } = new();

        // Loads the customer's current cart.
        public void OnGet()
        {
            LoadCart();
        }

        // Removes the selected menu item from the customer's cart.
        public IActionResult OnPostRemove(int menuItemId)
        {
            _cartService.RemoveItem(
                CurrentCustomerId,
                menuItemId);

            return RedirectToPage();
        }

        // Loads the cart and calculates the total quantity of items.
        private void LoadCart()
        {
            Cart = _cartService.GetCart(CurrentCustomerId);

            TotalItemCount = Cart.Items
                .Sum(i => i.Quantity);
        }

        // Creates one order containing all items currently in the customer's cart.
        // If checkout detects an allergy conflict, the affected menu items are identified so a prominent warning can be displayed to the customer.
        public void OnPostPlaceOrder()
        {
            CreatedOrder = _orderService.CreateOrderFromCart(
                CurrentCustomerId);

            if (CreatedOrder.ConflictingAllergens.Count > 0)
            {
                var conflictingAllergenIds = CreatedOrder
                    .ConflictingAllergens
                    .Select(a => a.Id)
                    .ToHashSet();

                ConflictingMenuItems = CreatedOrder.Items
                    .Where(item =>
                        item.MenuItem.Ingredients
                            .SelectMany(i => i.Allergens)
                            .Any(a => conflictingAllergenIds.Contains(a.Id)))
                    .Select(item => item.MenuItem)
                    .DistinctBy(item => item.Id)
                    .ToList();
            }

            LoadCart();
        }
    }
}