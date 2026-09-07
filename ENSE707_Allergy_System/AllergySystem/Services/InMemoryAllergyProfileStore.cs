using AllergySystem.Models;

namespace AllergySystem.Services
{
    // This service implements an in-memory store for allergy profiles.
    // It provides methods to add, retrieve, and manage allergy profiles.
    public class InMemoryAllergyProfileStore
    {
        private readonly Dictionary<int, AllergyProfile> _profiles = new();

        // Retrieves the allergy profile for a customer.
        // If no profile exists yet, a new empty profile is created and stored.
        public AllergyProfile GetProfile(int customerId)
        {
            if (!_profiles.TryGetValue(customerId, out var profile))
            {
                profile = new AllergyProfile { CustomerId = customerId };
                _profiles[customerId] = profile;
            }

            return profile;
        }

        // Saves or updates an allergy profile using the customer ID as the key.
        public void SaveProfile(AllergyProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            _profiles[profile.CustomerId] = profile;
        }
    }
}