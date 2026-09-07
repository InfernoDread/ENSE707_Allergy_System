namespace AllergySystem.Models
{
    // This class represents an allergen in the allergy system
    // It stores the allergen's unique ID and display name.
    public class Allergen
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
