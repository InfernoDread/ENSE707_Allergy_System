using AllergySystem.Models;

namespace AllergySystem.Services
{
    public class AllergenCatalogService
    {
        private readonly List<Allergen> _allergens = new()
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

        public List<Allergen> GetAllergens()
        {
            return _allergens.ToList();
        }

        public Allergen AddAllergen(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Allergen name is required.", nameof(name));

            var trimmedName = name.Trim();

            if (_allergens.Any(a =>
                a.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Allergen \"{trimmedName}\" already exists.");
            }

            var allergen = new Allergen
            {
                Id = _allergens.Max(a => a.Id) + 1,
                Name = trimmedName
            };

            _allergens.Add(allergen);
            return allergen;
        }

        public bool ContainsAllergen(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            return _allergens.Any(a =>
                a.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }
}