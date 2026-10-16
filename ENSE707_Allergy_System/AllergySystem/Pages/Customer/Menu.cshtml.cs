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
        private readonly CurrentUserContext _currentUser;
        private readonly MenuCatalogService _menuCatalogService;
        private readonly CartService _cartService;
        private readonly AuditService _auditService;

        // Creates the page model with the services needed to load menu items
        // and add selected items to the customer's cart.
        public MenuModel(
            CurrentUserContext currentUser,
            MenuCatalogService menuCatalogService,
            CartService cartService,
            AuditService auditService)
        {
            _currentUser = currentUser;
            _menuCatalogService = menuCatalogService;
            _cartService = cartService;
            _auditService = auditService;
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
                _currentUser.CustomerId,
                SelectedMenuItemId);

            ItemAddedToCart = Conflicts.Count == 0;

            // Record a customer-scoped AllergenWarning only when the item was blocked.
            if (Conflicts.Count > 0)
            {
                var conflictNames = string.Join(',', Conflicts.Select(a => $"{a.Id}:{a.Name}"));
                var details =
                    $"CustomerId={_currentUser.CustomerId};MenuItemId={SelectedMenuItemId};MenuItemName={SelectedMenuItem.Name};ConflictingAllergens={conflictNames};Source=AddToCart";
                _auditService.Record(
                    PrototypeRoles.Customer,
                    "AllergenWarning",
                    "MenuItem",
                    SelectedMenuItemId,
                    details);
            }

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
            var cart = _cartService.GetCart(_currentUser.CustomerId);

            CartItemCount = cart.Items.Sum(i => i.Quantity);
        }
    }
}