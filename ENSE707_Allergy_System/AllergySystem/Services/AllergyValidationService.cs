using AllergySystem.Models;

namespace AllergySystem.Services
{
    // This Service checks menu items against a customer's allergy profile to identify any allergens that may cause a conflict.
    public class AllergyValidationService
    {
        // Checks menu items against a customer's allergy profile to identify any allergens that may cause a conflict.
        public List<Allergen> FindConflicts(MenuItem menuItem, List<Allergen> customerAllergens)
        {
            ArgumentNullException.ThrowIfNull(menuItem);
            ArgumentNullException.ThrowIfNull(customerAllergens);

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
