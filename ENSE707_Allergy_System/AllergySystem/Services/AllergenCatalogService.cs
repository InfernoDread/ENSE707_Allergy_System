using AllergySystem.Models;

namespace AllergySystem.Services
{
    public class AllergenCatalogService
    {
        // Temporary allergen catalogue until database storage is implemented.
        public List<Allergen> GetAllergens()
        {
            return new List<Allergen>
            {
                new Allergen { Id = 1, Name = "Peanuts" },
                new Allergen { Id = 2, Name = "Tree Nuts" },
                new Allergen { Id = 3, Name = "Milk" },
                new Allergen { Id = 4, Name = "Eggs" },
                new Allergen { Id = 5, Name = "Wheat" },
                new Allergen { Id = 6, Name = "Soy" },
                new Allergen { Id = 7, Name = "Fish" },
                new Allergen { Id = 8, Name = "Shellfish" },
                new Allergen { Id = 9, Name = "Sesame" }
            };
        }
    }
}
