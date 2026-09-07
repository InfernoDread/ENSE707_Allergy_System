using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AllergySystem.Pages.Admin
{
    // This code handles the administrator allergen recommendation page.
    // It displays pending recommendations and allows administrators to approve or reject them.
    public class AllergenRecommendationsModel : PageModel
    {
        private readonly AllergenRecommendationService _recommendationService;

        // Creates the page model with the service used to manage allergen recommendations.
        public AllergenRecommendationsModel(AllergenRecommendationService recommendationService)
        {
            _recommendationService = recommendationService;
        }

        public List<AllergenRecommendation> PendingRecommendations { get; private set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        // Loads all recommendations that are currently waiting for administrator review.
        public void OnGet()
        {
            LoadPendingRecommendations();
        }

        // Approves the selected pending recommendation and displays the result to the administrator.
        public IActionResult OnPostApprove(int recommendationId)
        {
            try
            {
                _recommendationService.ApproveRecommendation(recommendationId);
                StatusMessage = $"Recommendation #{recommendationId} was approved.";
            }
            catch (ArgumentException)
            {
                StatusMessage = "Invalid recommendation ID.";
            }
            catch (InvalidOperationException)
            {
                StatusMessage = "Only a pending recommendation can be approved.";
            }

            LoadPendingRecommendations();
            return Page();
        }

        // Rejects the selected pending recommendation and displays the result to the administrator.
        public IActionResult OnPostReject(int recommendationId)
        {
            try
            {
                _recommendationService.RejectRecommendation(recommendationId);
                StatusMessage = $"Recommendation #{recommendationId} was rejected.";
            }
            catch (ArgumentException)
            {
                StatusMessage = "Invalid recommendation ID.";
            }
            catch (InvalidOperationException)
            {
                StatusMessage = "Only a pending recommendation can be rejected.";
            }

            LoadPendingRecommendations();
            return Page();
        }

        // Refreshes the list of recommendations that are still awaiting review.
        private void LoadPendingRecommendations()
        {
            PendingRecommendations = _recommendationService.GetPendingRecommendations();
        }
    }
}
