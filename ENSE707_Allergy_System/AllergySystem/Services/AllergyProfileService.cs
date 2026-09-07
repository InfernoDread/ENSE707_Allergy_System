using AllergySystem.Models;

namespace AllergySystem.Services
{
    // This service updates a customer's allergy profile with their selected allergens.
    public class AllergyProfileService
    {
        // Replaces the profile's current allergens with the selected allergens and removes any duplicate allergens based on their ID.
        public void UpdateProfile(AllergyProfile profile, List<Allergen> selectedAllergens)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(selectedAllergens);
            profile.Allergens = selectedAllergens.DistinctBy(a => a.Id).ToList();
        }
    }
}
