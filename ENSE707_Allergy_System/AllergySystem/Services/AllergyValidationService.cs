using AllergySystem.Models;

namespace AllergySystem.Services
{
    public class AllergyValidationService
    {
        public List<Allergen> FindConflicts(MenuItem menuItem, List<Allergen> customerAllergens)
        {
            var conflicts = new List<Allergen>();
            foreach (var ingredient in menuItem.Ingredients)
            {
                foreach (var allergen in ingredient.Allergens)
                {
                    if (customerAllergens.Any(a => a.Id == allergen.Id) && !conflicts.Any(c => c.Id == allergen.Id))
                    {
                        conflicts.Add(allergen);
                    }
                }
            }
            return conflicts;
        }
    }
}
