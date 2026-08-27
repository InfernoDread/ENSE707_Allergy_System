namespace AllergySystem.Models
{
    public class AllergyProfile
    {
        public int CustomerId { get; set; }
        public List<Allergen> Allergens { get; set; } = new();
    }
}
