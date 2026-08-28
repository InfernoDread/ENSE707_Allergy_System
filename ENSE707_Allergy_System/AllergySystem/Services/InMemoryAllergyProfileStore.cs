using AllergySystem.Models;

namespace AllergySystem.Services
{
    // This class implements an in-memory store for allergy profiles.
    // It provides methods to add, retrieve, and manage allergy profiles.
    public class InMemoryAllergyProfileStore
    {
        private readonly Dictionary<int, AllergyProfile> _profiles = new();

        public AllergyProfile GetProfile(int customerId)
        {
            if(!_profiles.TryGetValue(customerId, out var profile))
            {
                profile = new AllergyProfile { CustomerId = customerId };
                _profiles[customerId] = profile;
            }

            return profile;
        }

        public void SaveProfile(AllergyProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            _profiles[profile.CustomerId] = profile;
        }
    }
}