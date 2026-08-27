namespace AllergySystem.Models
{
    // This class represents an ingredient in a menu item
    // It contains the ingredient's ID, name, and a list of allergens associated with the ingredient.
    public class Ingredient
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<Allergen> Allergens { get; set; } = new();
    }
}
