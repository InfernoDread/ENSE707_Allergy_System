using AllergySystem.Models;

namespace AllergySystem.Services
{
    // This service manages customer shopping carts and checks menu items against the customer's allergy profile before adding them.
    public class CartService
    {
        private readonly InMemoryCartStore _cartStore;
        private readonly MenuCatalogService _menuCatalogService;
        private readonly InMemoryAllergyProfileStore _profileStore;
        private readonly AllergyValidationService _validationService;

        // Creates the cart service with the stores and services required to manage cart contents and perform allergy checks.
        public CartService(
            InMemoryCartStore cartStore,
            MenuCatalogService menuCatalogService,
            InMemoryAllergyProfileStore profileStore,
            AllergyValidationService validationService)
        {
            _cartStore = cartStore;
            _menuCatalogService = menuCatalogService;
            _profileStore = profileStore;
            _validationService = validationService;
        }

        // Returns the customer's current shopping cart.
        public ShoppingCart GetCart(int customerId)
        {
            return _cartStore.GetCart(customerId);
        }

        // Attempts to add a menu item to the customer's cart.
        // If allergy conflicts are found, the item is not added and the conflicting allergens are returned.
        public List<Allergen> AddItem(
            int customerId,
            int menuItemId)
        {
            var menuItem = _menuCatalogService
                .GetMenuItems()
                .FirstOrDefault(m => m.Id == menuItemId);

            if (menuItem == null)
            {
                throw new ArgumentException(
                    $"Menu item {menuItemId} not found.",
                    nameof(menuItemId));
            }

            var profile = _profileStore.GetProfile(customerId);

            var conflicts = _validationService.FindConflicts(
                menuItem,
                profile.Allergens);

            if (conflicts.Count > 0)
            {
                return conflicts;
            }

            var cart = _cartStore.GetCart(customerId);

            var existingItem = cart.Items
                .FirstOrDefault(i => i.MenuItem.Id == menuItemId);

            if (existingItem != null)
            {
                existingItem.Quantity++;
            }
            else
            {
                cart.Items.Add(
                    new CartItem
                    {
                        MenuItem = menuItem,
                        Quantity = 1
                    });
            }

            _cartStore.SaveCart(cart);

            return new List<Allergen>();
        }

        // Removes a menu item completely from the customer's cart.
        public void RemoveItem(
            int customerId,
            int menuItemId)
        {
            var cart = _cartStore.GetCart(customerId);

            var item = cart.Items
                .FirstOrDefault(i => i.MenuItem.Id == menuItemId);

            if (item != null)
            {
                cart.Items.Remove(item);
                _cartStore.SaveCart(cart);
            }
        }

        // Clears all items from the customer's cart.
        public void ClearCart(int customerId)
        {
            _cartStore.ClearCart(customerId);
        }
    }
}