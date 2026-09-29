using AllergySystem.Models;

namespace AllergySystem.Services
{
    // Manages customer requests for dietary restrictions or preferences
    // that are not currently available in the dietary restriction catalogue.
    public class DietaryRestrictionRecommendationService
    {
        private readonly List<DietaryRestrictionRecommendation> _recommendations = new();
        private readonly DietaryRestrictionCatalogService _catalogService;
        private int _nextId = 1;

        // Creates the service using the dietary restriction catalogue.
        public DietaryRestrictionRecommendationService(
            DietaryRestrictionCatalogService catalogService)
        {
            _catalogService = catalogService;
        }

        // Submits a new dietary restriction request for review.
        public DietaryRestrictionRecommendation SubmitRecommendation(
            int customerId,
            string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Dietary restriction name cannot be empty.",
                    nameof(name));
            }

            var recommendation = new DietaryRestrictionRecommendation
            {
                Id = _nextId++,
                CustomerId = customerId,
                Name = name.Trim(),
                Status = "Pending"
            };

            _recommendations.Add(recommendation);

            return recommendation;
        }

        // Returns all dietary restriction requests.
        public List<DietaryRestrictionRecommendation> GetRecommendations()
        {
            return _recommendations.ToList();
        }

        // Returns all dietary restriction requests that are waiting for administrator review.
        public List<DietaryRestrictionRecommendation> GetPendingRecommendations()
        {
            return _recommendations
                .Where(r => r.Status.Equals(
                    "Pending",
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Approves a pending request and adds it to the dietary restriction catalogue.
        public bool ApproveRecommendation(int recommendationId)
        {
            var recommendation = GetRecommendationById(recommendationId);

            if (recommendation == null)
            {
                throw new ArgumentException(
                    "Recommendation ID is invalid.",
                    nameof(recommendationId));
            }

            if (!recommendation.Status.Equals(
                "Pending",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Only a pending recommendation can be approved.");
            }

            _catalogService.AddDietaryRestriction(recommendation.Name);
            recommendation.Status = "Approved";

            return true;
        }

        // Rejects a pending request without adding it to the catalogue.
        public bool RejectRecommendation(int recommendationId)
        {
            var recommendation = GetRecommendationById(recommendationId);

            if (recommendation == null)
            {
                throw new ArgumentException(
                    "Recommendation ID is invalid.",
                    nameof(recommendationId));
            }

            if (!recommendation.Status.Equals(
                "Pending",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Only a pending recommendation can be rejected.");
            }

            recommendation.Status = "Rejected";

            return true;
        }

        // Finds a dietary restriction recommendation by its unique ID.
        public DietaryRestrictionRecommendation? GetRecommendationById(
            int recommendationId)
        {
            if (recommendationId <= 0)
            {
                throw new ArgumentException(
                    "Recommendation ID is invalid.",
                    nameof(recommendationId));
            }

            return _recommendations.FirstOrDefault(
                r => r.Id == recommendationId);
        }
    }
}