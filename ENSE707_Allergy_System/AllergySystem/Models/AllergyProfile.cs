namespace AllergySystem.Models
{
    // This class represents a customer's saved allergy profile.
    // It stores the customer ID, the allergens, and the dietary restrictions associated with that customer.
    public class AllergyProfile
    {
        public int CustomerId { get; set; }
        public List<Allergen> Allergens { get; set; } = new();
        public List<DietaryRestriction> DietaryRestrictions { get; set; } = new();
    }
}
