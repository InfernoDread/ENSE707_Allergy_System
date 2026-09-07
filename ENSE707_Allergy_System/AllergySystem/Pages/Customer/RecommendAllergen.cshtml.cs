using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AllergySystem.Pages.Customer
{
    // This code handles the customer allergen recommendation page.
    // It displays the approved allergen catalogue and allows customers to submit new allergen recommendations.
    public class RecommendAllergenModel : PageModel
    {
        // Temporary customer ID used while the prototype has no authentication system.
        private const int DemoCustomerId = 1;

        private readonly AllergenCatalogService _catalogService;
        private readonly AllergenRecommendationService _recommendationService;

        // Creates the page model with the services needed to display allergens and submit new customer recommendations.
        public RecommendAllergenModel(AllergenCatalogService catalogService, AllergenRecommendationService recommendationService)
        {
            _catalogService = catalogService;
            _recommendationService = recommendationService;
        }

        public List<Allergen> Allergens { get; private set; } = new();

        [BindProperty]
        public string? RequestedAllergenName { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public bool IsSuccessStatus { get; private set; }

        // Loads the approved allergen catalogue when the page is opened.
        public void OnGet()
        {
            LoadPageData();
            IsSuccessStatus = false;
        }

        // Validates and submits a new allergen recommendation, then displays an appropriate status message for successful or invalid submissions.
        public IActionResult OnPost()
        {
            LoadPageData();

            if (string.IsNullOrWhiteSpace(RequestedAllergenName))
            {
                StatusMessage = "Please enter an allergen name.";
                IsSuccessStatus = false;
                return Page();
            }

            var submittedName = RequestedAllergenName.Trim();

            if (Allergens.Any(a => a.Name.Equals(submittedName, StringComparison.OrdinalIgnoreCase)))
            {
                StatusMessage = $"\"{submittedName}\" is already in the allergen catalog.";
                IsSuccessStatus = false;
                return Page();
            }

            try
            {
                var recommendation = _recommendationService.SubmitRecommendation(DemoCustomerId, submittedName);

                StatusMessage = $"Success! Recommendation #{recommendation.Id} for \"{recommendation.SuggestedName}\" was stored as Pending.";
                IsSuccessStatus = true;
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
                IsSuccessStatus = false;
            }

            RequestedAllergenName = string.Empty;
            return Page();
        }

        // Loads the current approved allergen catalogue for display and validation.
        private void LoadPageData()
        {
            Allergens = _catalogService.GetAllergens();
        }
    }
}
