using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AllergySystem.Pages.Admin
{
    public class MenuAllergensModel : PageModel
    {
        private readonly MenuAllergenManagementService _managementService;

        public MenuAllergensModel(MenuAllergenManagementService managementService)
        {
            _managementService = managementService;
        }

        public List<MenuItem> MenuItems { get; private set; } = new();
        public List<Allergen> AvailableAllergens { get; private set; } = new();
        public string? StatusMessage { get; private set; }
        public string? ErrorMessage { get; private set; }

        public void OnGet()
        {
            LoadPageData();
        }

        public IActionResult OnPost()
        {
            _managementService.RecordUnsupportedOperation();
            ErrorMessage = "Unsupported operation. Choose Add, Replace, or Remove.";
            LoadPageData();
            return Page();
        }

        public IActionResult OnPostAdd(int? menuItemId, int? ingredientId, int? allergenId)
        {
            return RunChange(
                () => _managementService.AddAssociation(
                    menuItemId ?? 0,
                    ingredientId ?? 0,
                    allergenId ?? 0),
                "Allergen association added.");
        }

        public IActionResult OnPostUpdate(
            int? menuItemId,
            int? ingredientId,
            int? currentAllergenId,
            int? newAllergenId)
        {
            return RunChange(
                () => _managementService.UpdateAssociation(
                    menuItemId ?? 0,
                    ingredientId ?? 0,
                    currentAllergenId ?? 0,
                    newAllergenId ?? 0),
                "Allergen association updated.");
        }

        public IActionResult OnPostRemove(int? menuItemId, int? ingredientId, int? allergenId)
        {
            return RunChange(
                () => _managementService.RemoveAssociation(
                    menuItemId ?? 0,
                    ingredientId ?? 0,
                    allergenId ?? 0),
                "Allergen association removed.");
        }

        private IActionResult RunChange(Action change, string successMessage)
        {
            try
            {
                change();
                StatusMessage = successMessage;
            }
            catch (ArgumentException exception)
            {
                ErrorMessage = exception.Message;
            }
            catch (InvalidOperationException exception)
            {
                ErrorMessage = exception.Message;
            }

            LoadPageData();
            return Page();
        }

        private void LoadPageData()
        {
            MenuItems = _managementService.GetMenuItems();
            AvailableAllergens = _managementService.GetAvailableAllergens();
        }
    }
}