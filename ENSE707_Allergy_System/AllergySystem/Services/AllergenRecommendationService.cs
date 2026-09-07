using AllergySystem.Models;

namespace AllergySystem.Services
{
    // This Service manages customer allergen recommendations and their approval workflow.
    // It handles submission, review status changes, and adding approved allergens to the catalogue.
    public class AllergenRecommendationService
    {
        private readonly Dictionary<int, AllergenRecommendation> _recommendations = new();
        private int _nextId = 1;
        private readonly AllergenCatalogService _catalogService;

        // Creates the service using the approved allergen catalogue.
        public AllergenRecommendationService(AllergenCatalogService catalogService)
        {
            _catalogService = catalogService;
        }

        // Submits a new allergen recommendation after validating the customer ID, allergen name, and checking for existing allergens or recommendations.
        public AllergenRecommendation SubmitRecommendation(int customerId, string suggestedName)
        {
            if (customerId <= 0)
                throw new ArgumentException("Customer ID must be greater than zero.", nameof(customerId));

            if (string.IsNullOrWhiteSpace(suggestedName))
                throw new ArgumentException("Allergen name is required.", nameof(suggestedName));

            var trimmedName = suggestedName.Trim();

            if (trimmedName.Length == 0)
                throw new ArgumentException("Allergen name is required.", nameof(suggestedName));
            
            if (_catalogService.ContainsAllergen(trimmedName))
                throw new InvalidOperationException($"\"{trimmedName}\" is already in the approved allergen catalogue.");

            if (HasExistingRecommendation(trimmedName))
                throw new InvalidOperationException($"A recommendation for \"{trimmedName}\" already exists or has already been processed.");

            var recommendation = new AllergenRecommendation
            {
                Id = _nextId++,
                CustomerId = customerId,
                SuggestedName = trimmedName,
                Status = "Pending",
                SubmittedAt = DateTime.UtcNow
            };

            _recommendations[recommendation.Id] = recommendation;
            return recommendation;
        }

        // Returns all recommendations that are currently waiting for administrator review.
        public List<AllergenRecommendation> GetPendingRecommendations()
        {
            return _recommendations.Values
                .Where(r => r.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.SubmittedAt)
                .ToList();
        }

        // Approves a pending recommendation and adds the allergen to the approved allergen catalogue.
        public bool ApproveRecommendation(int recommendationId)
        {
            var recommendation = GetRecommendationById(recommendationId);

            if (recommendation == null)
                throw new ArgumentException("Recommendation ID is invalid.",nameof(recommendationId));

            if (!recommendation.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only a Pending recommendation can be approved.");

            _catalogService.AddAllergen(recommendation.SuggestedName);

            recommendation.Status = "Approved";
            return true;
        }

        // Rejects a pending recommendation without adding it to the approved allergen catalogue.
        public bool RejectRecommendation(int recommendationId)
        {
            var recommendation = GetRecommendationById(recommendationId);

            if (recommendation == null)
                throw new ArgumentException("Recommendation ID is invalid.", nameof(recommendationId));

            if (!recommendation.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only a Pending recommendation can be rejected.");

            recommendation.Status = "Rejected";
            return true;
        }

        // Returns all allergen recommendations submitted by a specific customer.
        public List<AllergenRecommendation> GetCustomerRecommendations(int customerId)
        {
            if (customerId <= 0)
                throw new ArgumentException("Customer ID must be greater than zero.", nameof(customerId));

            return _recommendations.Values
                .Where(r => r.CustomerId == customerId)
                .OrderByDescending(r => r.SubmittedAt)
                .ToList();
        }

        // Finds a recommendation by its unique ID and returns null when no matching recommendation exists.
        public AllergenRecommendation? GetRecommendationById(int recommendationId)
        {
            if (recommendationId <= 0)
                throw new ArgumentException("Recommendation ID is invalid.", nameof(recommendationId));

            return _recommendations.TryGetValue(recommendationId, out var recommendation)
                ? recommendation
                : null;
        }

        // Checks whether a recommendation with the same allergen name has already been submitted.
        private bool HasExistingRecommendation(string allergenName)
        {
            return _recommendations.Values.Any(r =>
                r.SuggestedName.Equals(allergenName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
