namespace AllergySystem.Models
{
    // This class represents a customer's saved allergy profile.
    // It stores the customer ID and the allergens associated with that customer.
    public class AllergyProfile
    {
        public int CustomerId { get; set; }
        public List<Allergen> Allergens { get; set; } = new();
    }
}
