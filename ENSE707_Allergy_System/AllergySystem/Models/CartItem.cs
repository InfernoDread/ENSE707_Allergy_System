namespace AllergySystem.Models
{
    // This class represents a menu item that has been added to a customer's shopping cart.
    // It stores the selected menu item and the quantity requested.
    public class CartItem
    {
        public MenuItem MenuItem { get; set; } = new();

        public int Quantity { get; set; } = 1;
    }
}
