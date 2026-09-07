using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AllergySystem.Pages.Customer
{
    // This code handles the customer menu page.
    // It displays available menu items and checks selected items for allergy conflicts.
    public class MenuModel : PageModel
    {
        // Temporary customer ID used while the prototype has no authentication system.
        private const int CurrentCustomerId = 1;

        private readonly MenuCatalogService _menuCatalogService;
        private readonly InMemoryAllergyProfileStore _profileStore;
        private readonly AllergyValidationService _validationService;

        // Creates the page model with the services needed to load menu items and validate them against the customer's allergy profile.
        public MenuModel(
            MenuCatalogService menuCatalogService,
            InMemoryAllergyProfileStore profileStore,
            AllergyValidationService validationService)
        {
            _menuCatalogService = menuCatalogService;
            _profileStore = profileStore;
            _validationService = validationService;
        }

        public List<MenuItem> MenuItems { get; private set; } = new();

        public MenuItem? SelectedMenuItem { get; private set; }

        public List<Allergen> Conflicts { get; private set; } = new();

        [BindProperty]
        public int SelectedMenuItemId { get; set; }

        // Loads the available menu items when the page is opened.
        public void OnGet()
        {
            MenuItems = _menuCatalogService.GetMenuItems();
        }

        // Checks the selected menu item against the customer's saved allergy profile and stores any identified allergen conflicts for display on the page.
        public void OnPost()
        {
            MenuItems = _menuCatalogService.GetMenuItems();

            SelectedMenuItem = MenuItems
                .FirstOrDefault(m => m.Id == SelectedMenuItemId);

            if (SelectedMenuItem == null)
            {
                return;
            }

            var profile = _profileStore.GetProfile(CurrentCustomerId);

            Conflicts = _validationService.FindConflicts(
                SelectedMenuItem,
                profile.Allergens);
        }
    }
}