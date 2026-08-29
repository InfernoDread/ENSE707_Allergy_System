using AllergySystem.Models;

namespace AllergySystem.Services
{
    public class AllergenRecommendationService
    {
        private readonly Dictionary<int, AllergenRecommendation> _recommendations = new();
        private int _nextId = 1;

        public AllergenRecommendation SubmitRecommendation(int customerId, string suggestedName)
        {
            if (customerId <= 0)
                throw new ArgumentException("Customer ID must be greater than zero.", nameof(customerId));

            if (string.IsNullOrWhiteSpace(suggestedName))
                throw new ArgumentException("Allergen name is required.", nameof(suggestedName));

            var trimmedName = suggestedName.Trim();

            if (trimmedName.Length == 0)
                throw new ArgumentException("Allergen name is required.", nameof(suggestedName));

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

        public List<AllergenRecommendation> GetPendingRecommendations()
        {
            return _recommendations.Values
                .Where(r => r.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.SubmittedAt)
                .ToList();
        }

        public bool ApproveRecommendation(int recommendationId)
        {
            var recommendation = GetRecommendationById(recommendationId);

            if (recommendation == null)
                throw new ArgumentException("Recommendation ID is invalid.", nameof(recommendationId));

            if (!recommendation.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only a Pending recommendation can be approved.");

            recommendation.Status = "Approved";
            return true;
        }

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

        public List<AllergenRecommendation> GetCustomerRecommendations(int customerId)
        {
            if (customerId <= 0)
                throw new ArgumentException("Customer ID must be greater than zero.", nameof(customerId));

            return _recommendations.Values
                .Where(r => r.CustomerId == customerId)
                .OrderByDescending(r => r.SubmittedAt)
                .ToList();
        }

        public AllergenRecommendation? GetRecommendationById(int recommendationId)
        {
            if (recommendationId <= 0)
                throw new ArgumentException("Recommendation ID is invalid.", nameof(recommendationId));

            return _recommendations.TryGetValue(recommendationId, out var recommendation)
                ? recommendation
                : null;
        }

        private bool HasExistingRecommendation(string allergenName)
        {
            return _recommendations.Values.Any(r =>
                r.SuggestedName.Equals(allergenName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
