using Microsoft.AspNetCore.Mvc.RazorPages;
using AllergySystem.Services;
using AllergySystem.Models;
using Microsoft.AspNetCore.Mvc;

namespace AllergySystem.Pages.Customer
{
    // This code handles the customer allergy profile page.
    // It loads approved allergens, displays the customer's current selections, and saves any changes made to the allergy profile.
    public class AllergyProfileModel : PageModel
    {
        // Temporary customer ID used while the prototype has no authentication system.
        private const int DemoCustomerId = 1; 

        private readonly InMemoryAllergyProfileStore _profileStore;
        private readonly AllergenCatalogService _catalogService;
        private readonly AllergyProfileService _profileService;
        private readonly DietaryRestrictionCatalogService _dietaryRestrictionCatalogService;

        // Creates the page model with the services required to load and update allergy profiles.
        public AllergyProfileModel(InMemoryAllergyProfileStore profileStore, AllergenCatalogService catalogService, AllergyProfileService profileService, DietaryRestrictionCatalogService dietaryRestrictionCatalogService)
        {
            _profileStore = profileStore;
            _catalogService = catalogService;
            _profileService = profileService;
            _dietaryRestrictionCatalogService = dietaryRestrictionCatalogService;
        }

        public List<Allergen> AvailableAllergens { get; private set; } = new();

        public List<DietaryRestriction> AvailableDietaryRestrictions { get; private set; } = new();

        [BindProperty]
        public List<int> SelectedAllergenIds { get; set; } = new();

        [BindProperty]
        public List<int> SelectedDietaryRestrictionIds { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        // Loads the customer's current allergy profile when the page is opened.
        public void OnGet()
        {
            LoadPageData();
        }

        // Saves the customer's selected allergens and redirects back to the page.
        public IActionResult OnPost()
        {
            AvailableAllergens = _catalogService.GetAllergens();
            AvailableDietaryRestrictions = _dietaryRestrictionCatalogService.GetDietaryRestrictions();

            var selectedAllergens = AvailableAllergens.Where(a => SelectedAllergenIds.Contains(a.Id)).ToList();
            var selectedDietaryRestrictions = AvailableDietaryRestrictions.Where(r => SelectedDietaryRestrictionIds.Contains(r.Id)).ToList();
            var profile = _profileStore.GetProfile(DemoCustomerId);

            _profileService.UpdateProfile(profile, selectedAllergens);
            _profileService.UpdateDietaryRestrictions(profile, selectedDietaryRestrictions);
            _profileStore.SaveProfile(profile);

            SuccessMessage = "Allergy Profile saved successfully!";
            return RedirectToPage();
        }

        // Loads the approved allergen catalogue and the customer's saved selections.
        private void LoadPageData()
        {
            AvailableAllergens = _catalogService.GetAllergens();
            AvailableDietaryRestrictions = _dietaryRestrictionCatalogService.GetDietaryRestrictions();

            var profile = _profileStore.GetProfile(DemoCustomerId);

            SelectedAllergenIds = profile.Allergens.Select(a => a.Id).ToList();
            SelectedDietaryRestrictionIds = profile.DietaryRestrictions.Select(r => r.Id).ToList();
        }
    }
}