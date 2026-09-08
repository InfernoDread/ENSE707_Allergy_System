using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AllergySystem.Pages.Customer
{
    // This code handles the customer menu page.
    // It displays available menu items and allows customers to add safe items to their cart.
    public class MenuModel : PageModel
    {
        // Temporary customer ID used while the prototype has no authentication system.
        private const int CurrentCustomerId = 1;

        private readonly MenuCatalogService _menuCatalogService;
        private readonly CartService _cartService;

        // Creates the page model with the services needed to load menu items
        // and add selected items to the customer's cart.
        public MenuModel(
            MenuCatalogService menuCatalogService,
            CartService cartService)
        {
            _menuCatalogService = menuCatalogService;
            _cartService = cartService;
        }

        public List<MenuItem> MenuItems { get; private set; } = new();

        public MenuItem? SelectedMenuItem { get; private set; }

        public List<Allergen> Conflicts { get; private set; } = new();

        public bool ItemAddedToCart { get; private set; }

        public int CartItemCount { get; private set; }

        [BindProperty]
        public int SelectedMenuItemId { get; set; }

        // Loads the available menu items and current cart quantity
        // when the page is opened.
        public void OnGet()
        {
            LoadPageData();
        }

        // Attempts to add the selected menu item to the customer's cart.
        // Items with allergy conflicts are blocked and the conflicts are shown to the customer.
        public void OnPost()
        {
            LoadPageData();

            SelectedMenuItem = MenuItems
                .FirstOrDefault(m => m.Id == SelectedMenuItemId);

            if (SelectedMenuItem == null)
            {
                return;
            }

            Conflicts = _cartService.AddItem(
                CurrentCustomerId,
                SelectedMenuItemId);

            ItemAddedToCart = Conflicts.Count == 0;

            LoadCartCount();
        }

        // Loads the menu catalogue and current cart quantity for display.
        private void LoadPageData()
        {
            MenuItems = _menuCatalogService.GetMenuItems();
            LoadCartCount();
        }

        // Calculates the total number of menu items currently in the cart,
        // including item quantities.
        private void LoadCartCount()
        {
            var cart = _cartService.GetCart(CurrentCustomerId);

            CartItemCount = cart.Items.Sum(i => i.Quantity);
        }
    }
}