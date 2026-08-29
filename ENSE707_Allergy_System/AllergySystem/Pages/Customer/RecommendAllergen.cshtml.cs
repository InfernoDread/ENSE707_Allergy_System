using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AllergySystem.Pages.Customer
{
    public class RecommendAllergenModel : PageModel
    {
        private const int DemoCustomerId = 1;

        private readonly AllergenCatalogService _catalogService;
        private readonly AllergenRecommendationService _recommendationService;

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

        public void OnGet()
        {
            LoadPageData();
            IsSuccessStatus = false;
        }

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

        private void LoadPageData()
        {
            Allergens = _catalogService.GetAllergens();
        }
    }
}
