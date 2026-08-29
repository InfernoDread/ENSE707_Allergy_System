using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AllergySystem.Pages.Customer
{
    public class MenuModel : PageModel
    {
        private const int CurrentCustomerId = 1;

        private readonly MenuCatalogService _menuCatalogService;
        private readonly InMemoryAllergyProfileStore _profileStore;
        private readonly AllergyValidationService _validationService;

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

        public void OnGet()
        {
            MenuItems = _menuCatalogService.GetMenuItems();
        }

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