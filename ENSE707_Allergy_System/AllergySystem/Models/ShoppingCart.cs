namespace AllergySystem.Models
{
    // This Class represents a customer's shopping cart.
    // It stores the customer ID and all menu items currently added to the cart.
    public class ShoppingCart
    {
        public int CustomerId { get; set; }

        public List<CartItem> Items { get; set; } = new();
    }

}
