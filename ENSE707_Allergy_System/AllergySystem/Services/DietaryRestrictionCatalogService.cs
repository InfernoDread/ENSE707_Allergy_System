using AllergySystem.Models;

namespace AllergySystem.Services
{
    // Provides the dietary restrictions and preferences currently supported by the system.
    public class DietaryRestrictionCatalogService
    {
        private readonly List<DietaryRestriction> _dietaryRestrictions = new()
        {
            new DietaryRestriction { Id = 1, Name = "Vegetarian" },
            new DietaryRestriction { Id = 2, Name = "Vegan" },
            new DietaryRestriction { Id = 3, Name = "Gluten-Free" },
            new DietaryRestriction { Id = 4, Name = "Dairy-Free" }
        };

        // Returns all dietary restrictions and preferences available for customer selection.
        public List<DietaryRestriction> GetDietaryRestrictions()
        {
            return _dietaryRestrictions.ToList();
        }

        // Adds a newly approved dietary restriction or preference to the catalogue.
        public DietaryRestriction AddDietaryRestriction(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Dietary restriction name cannot be empty.",
                    nameof(name));
            }

            var trimmedName = name.Trim();

            if (_dietaryRestrictions.Any(r =>
                r.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"\"{trimmedName}\" is already in the dietary restriction catalogue.");
            }

            var nextId = _dietaryRestrictions.Count == 0
                ? 1
                : _dietaryRestrictions.Max(r => r.Id) + 1;

            var dietaryRestriction = new DietaryRestriction
            {
                Id = nextId,
                Name = trimmedName
            };

            _dietaryRestrictions.Add(dietaryRestriction);

            return dietaryRestriction;
        }
    }
}