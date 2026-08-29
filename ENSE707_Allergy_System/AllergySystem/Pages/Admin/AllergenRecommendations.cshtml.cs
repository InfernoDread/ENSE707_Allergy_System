using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AllergySystem.Pages.Admin
{
    public class AllergenRecommendationsModel : PageModel
    {
        private readonly AllergenRecommendationService _recommendationService;

        public AllergenRecommendationsModel(AllergenRecommendationService recommendationService)
        {
            _recommendationService = recommendationService;
        }

        public List<AllergenRecommendation> PendingRecommendations { get; private set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public void OnGet()
        {
            LoadPendingRecommendations();
        }

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

        private void LoadPendingRecommendations()
        {
            PendingRecommendations = _recommendationService.GetPendingRecommendations();
        }
    }
}
