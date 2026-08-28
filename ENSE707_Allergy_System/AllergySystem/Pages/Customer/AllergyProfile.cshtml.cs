using Microsoft.AspNetCore.Mvc.RazorPages;
using AllergySystem.Services;
using AllergySystem.Models;
using Microsoft.AspNetCore.Mvc;

namespace AllergySystem.Pages.Customer
{
    public class AllergyProfileModel : PageModel
    {
        private const int DemoCustomerId = 1; 

        private readonly InMemoryAllergyProfileStore _profileStore;
        private readonly AllergenCatalogService _catalogService;
        private readonly AllergyProfileService _profileService;

        public AllergyProfileModel(InMemoryAllergyProfileStore profileStore, AllergenCatalogService catalogService, AllergyProfileService profileService)
        {
            _profileStore = profileStore;
            _catalogService = catalogService;
            _profileService = profileService;
        }

        public List<Allergen> AvailableAllergens { get; private set; } = new();

        [BindProperty]
        public List<int> SelectedAllergenIds { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        public void OnGet()
        {
            LoadPageData();
        }

        public IActionResult OnPost()
        {
            AvailableAllergens = _catalogService.GetAllergens();

            var selectedAllergens = AvailableAllergens.Where(a => SelectedAllergenIds.Contains(a.Id)).ToList();
            var profile = _profileStore.GetProfile(DemoCustomerId);

            _profileService.UpdateProfile(profile, selectedAllergens);
            _profileStore.SaveProfile(profile);

            SuccessMessage = "Allergy profile saved successfully!";
            return RedirectToPage();
        }

        private void LoadPageData()
        {
            AvailableAllergens = _catalogService.GetAllergens();
            var profile = _profileStore.GetProfile(DemoCustomerId);
            SelectedAllergenIds = profile.Allergens.Select(a => a.Id).ToList();
        }
    }
}