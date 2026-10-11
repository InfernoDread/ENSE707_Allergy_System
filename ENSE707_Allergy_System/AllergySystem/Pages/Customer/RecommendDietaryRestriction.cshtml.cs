using AllergySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AllergySystem.Pages.Customer
{
    // Handles customer requests for dietary restrictions or preferences
    // that are not currently available in the system.
    public class RecommendDietaryRestrictionModel : PageModel
    {
        private readonly CurrentUserContext _currentUser;
        private readonly DietaryRestrictionRecommendationService _recommendationService;

        public RecommendDietaryRestrictionModel(
            CurrentUserContext currentUser,
            DietaryRestrictionRecommendationService recommendationService)
        {
            _currentUser = currentUser;
            _recommendationService = recommendationService;
        }

        [BindProperty]
        public string RestrictionName { get; set; } = string.Empty;

        public string? SuccessMessage { get; set; }

        public void OnGet()
        {
        }

        // Submits a new dietary restriction request for administrator review.
        public IActionResult OnPost()
        {
            if (string.IsNullOrWhiteSpace(RestrictionName))
            {
                ModelState.AddModelError(
                    nameof(RestrictionName),
                    "Please enter a dietary restriction or preference.");

                return Page();
            }

            _recommendationService.SubmitRecommendation(
                _currentUser.CustomerId,
                RestrictionName);

            SuccessMessage =
                "Your dietary restriction request has been submitted for review.";

            RestrictionName = string.Empty;

            return Page();
        }
    }
}