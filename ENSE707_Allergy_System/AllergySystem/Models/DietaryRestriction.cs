namespace AllergySystem.Models
{
    // This class represents a dietary restriction or preference that can be selected by customers and assigned as a dietary label to menu items.
    public class DietaryRestriction
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}