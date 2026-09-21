using AllergySystem.Models;

namespace AllergySystem.Services
{
    // This service updates a customer's allergy profile with their selected allergens and dietary restrictions.
    public class AllergyProfileService
    {
        // Replaces the profile's current allergens with the selected allergens and removes any duplicate allergens based on their ID.
        public void UpdateProfile(AllergyProfile profile, List<Allergen> selectedAllergens)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(selectedAllergens);
            profile.Allergens = selectedAllergens.DistinctBy(a => a.Id).ToList();
        }

        // Replaces the profile's current dietary restrictions with the selected restrictions and removes any duplicates based on their ID.
        public void UpdateDietaryRestrictions(AllergyProfile profile, List<DietaryRestriction> selectedDietaryRestrictions)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(selectedDietaryRestrictions);

            profile.DietaryRestrictions = selectedDietaryRestrictions
                .DistinctBy(d => d.Id)
                .ToList();
        }
    }
}
