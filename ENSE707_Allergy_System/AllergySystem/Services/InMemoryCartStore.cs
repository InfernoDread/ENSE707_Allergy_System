using AllergySystem.Models;

namespace AllergySystem.Services
{
    // This service stores customer shopping carts in memory while the application is running.
    // Each customer has one cart identified by their customer ID.
    public class InMemoryCartStore
    {
        private readonly Dictionary<int, ShoppingCart> _carts = new();

        // Retrieves the customer's existing cart or creates an empty cart if one does not exist.
        public ShoppingCart GetCart(int customerId)
        {
            if (!_carts.TryGetValue(customerId, out var cart))
            {
                cart = new ShoppingCart
                {
                    CustomerId = customerId
                };

                _carts[customerId] = cart;
            }

            return cart;
        }

        // Saves or updates the customer's cart.
        public void SaveCart(ShoppingCart cart)
        {
            ArgumentNullException.ThrowIfNull(cart);

            _carts[cart.CustomerId] = cart;
        }

        // Removes the customer's cart after an order is successfully placed.
        public void ClearCart(int customerId)
        {
            _carts.Remove(customerId);
        }
    }
}