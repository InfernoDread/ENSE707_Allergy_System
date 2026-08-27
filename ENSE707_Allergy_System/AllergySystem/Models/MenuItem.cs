namespace AllergySystem.Models
{
    // This class represents a menu item in the allergy system
    // It contains the item's ID, name, description, and a list of ingredients associated with the item.
    public class MenuItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<Ingredient> Ingredients { get; set; } = new();
    }
}
