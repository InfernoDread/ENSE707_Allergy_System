using AllergySystem.Models;

namespace AllergySystem.Services
{
    public class AllergyProfileService
    {
        public void UpdateProfile(AllergyProfile profile, List<Allergen> selectedAllergens)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(selectedAllergens);
            profile.Allergens = selectedAllergens.DistinctBy(a => a.Id).ToList();
        }
    }
}
