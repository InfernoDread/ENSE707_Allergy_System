namespace AllergySystem.Models
{
    // Represents a customer request for a dietary restriction or preference that is not currently available in the system.
    public class DietaryRestrictionRecommendation
    {
        public int Id { get; set; }

        public int CustomerId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Status { get; set; } = "Pending";
    }
}