using AllergySystem.Models;

namespace AllergySystem.Services
{
    public class MenuAllergenManagementService
    {
        private readonly MenuCatalogService _menuCatalogService;
        private readonly AllergenCatalogService _allergenCatalogService;
        private readonly MenuAllergenMappingStore _mappingStore;
        private readonly AuditService _auditService;

        public MenuAllergenManagementService(
            MenuCatalogService menuCatalogService,
            AllergenCatalogService allergenCatalogService,
            MenuAllergenMappingStore mappingStore,
            AuditService auditService)
        {
            _menuCatalogService = menuCatalogService;
            _allergenCatalogService = allergenCatalogService;
            _mappingStore = mappingStore;
            _auditService = auditService;
        }

        public List<MenuItem> GetMenuItems()
        {
            return _menuCatalogService.GetMenuItems();
        }

        public List<Allergen> GetAvailableAllergens()
        {
            return _allergenCatalogService.GetAllergens();
        }

        public void RecordUnsupportedOperation()
        {
            _auditService.Record(
                PrototypeRoles.Admin,
                "RejectUnsupportedMenuAllergenOperation",
                "IngredientAllergenAssociation",
                null,
                "Reason=Unsupported operation; SupportedActions=Add, Update, Remove");
        }

        public void AddAssociation(int menuItemId, int ingredientId, int allergenId)
        {
            ExecuteChange("AddIngredientAllergen", menuItemId, ingredientId, allergenId, null, () =>
            {
                var (_, ingredient, allergen) = ResolveTargets(menuItemId, ingredientId, allergenId);
                if (ingredient.Allergens.Any(existing => existing.Id == allergen.Id))
                {
                    throw new InvalidOperationException("This allergen is already associated with the ingredient.");
                }

                var allergenIds = ingredient.Allergens.Select(existing => existing.Id).Append(allergen.Id);
                _mappingStore.SetAllergenIds(ingredientId, allergenIds);
            });
        }

        public void UpdateAssociation(
            int menuItemId,
            int ingredientId,
            int currentAllergenId,
            int newAllergenId)
        {
            ExecuteChange("UpdateIngredientAllergen", menuItemId, ingredientId, currentAllergenId, newAllergenId, () =>
            {
                var (_, ingredient, _) = ResolveTargets(menuItemId, ingredientId, currentAllergenId);
                var newAllergen = ResolveAllergen(newAllergenId);
                var currentIndex = ingredient.Allergens.FindIndex(allergen => allergen.Id == currentAllergenId);
                if (currentIndex < 0)
                {
                    throw new InvalidOperationException("The selected allergen is not associated with the ingredient.");
                }

                if (ingredient.Allergens.Any(allergen => allergen.Id == newAllergen.Id))
                {
                    throw new InvalidOperationException("The replacement allergen is already associated with the ingredient.");
                }

                var allergenIds = ingredient.Allergens.Select(allergen => allergen.Id).ToList();
                allergenIds[currentIndex] = newAllergen.Id;
                _mappingStore.SetAllergenIds(ingredientId, allergenIds);
            });
        }

        public void RemoveAssociation(int menuItemId, int ingredientId, int allergenId)
        {
            ExecuteChange("RemoveIngredientAllergen", menuItemId, ingredientId, allergenId, null, () =>
            {
                var (_, ingredient, allergen) = ResolveTargets(menuItemId, ingredientId, allergenId);
                if (!ingredient.Allergens.Any(existing => existing.Id == allergen.Id))
                {
                    throw new InvalidOperationException("This allergen is not associated with the ingredient.");
                }

                var allergenIds = ingredient.Allergens
                    .Where(existing => existing.Id != allergen.Id)
                    .Select(existing => existing.Id);
                _mappingStore.SetAllergenIds(ingredientId, allergenIds);
            });
        }

        private (MenuItem menuItem, Ingredient ingredient, Allergen allergen) ResolveTargets(
            int menuItemId,
            int ingredientId,
            int allergenId)
        {
            if (menuItemId <= 0)
            {
                throw new ArgumentException("Select a valid menu item.");
            }

            var menuItem = _menuCatalogService.GetMenuItems()
                .FirstOrDefault(item => item.Id == menuItemId)
                ?? throw new ArgumentException($"Menu item {menuItemId} does not exist.");

            if (ingredientId <= 0)
            {
                throw new ArgumentException("Select a valid ingredient.");
            }

            var ingredient = menuItem.Ingredients
                .FirstOrDefault(item => item.Id == ingredientId)
                ?? throw new ArgumentException(
                    $"Ingredient {ingredientId} does not belong to menu item {menuItemId}.");

            return (menuItem, ingredient, ResolveAllergen(allergenId));
        }

        private Allergen ResolveAllergen(int allergenId)
        {
            if (allergenId <= 0)
            {
                throw new ArgumentException("Select an allergen from the approved list.");
            }

            return _allergenCatalogService.GetAllergens()
                .FirstOrDefault(allergen => allergen.Id == allergenId)
                ?? throw new ArgumentException($"Allergen {allergenId} does not exist.");
        }

        private void ExecuteChange(
            string action,
            int menuItemId,
            int ingredientId,
            int allergenId,
            int? replacementAllergenId,
            Action change)
        {
            try
            {
                change();
            }
            catch (ArgumentException exception)
            {
                RecordRejectedChange(action, menuItemId, ingredientId, allergenId, replacementAllergenId, exception.Message);
                throw;
            }
            catch (InvalidOperationException exception)
            {
                RecordRejectedChange(action, menuItemId, ingredientId, allergenId, replacementAllergenId, exception.Message);
                throw;
            }

            _auditService.Record(
                PrototypeRoles.Admin,
                action,
                "IngredientAllergenAssociation",
                ingredientId,
                $"MenuItemId={menuItemId}; IngredientId={ingredientId}; AllergenId={allergenId}; ReplacementAllergenId={replacementAllergenId?.ToString() ?? "-"}");
        }

        private void RecordRejectedChange(
            string action,
            int menuItemId,
            int ingredientId,
            int allergenId,
            int? replacementAllergenId,
            string reason)
        {
            _auditService.Record(
                PrototypeRoles.Admin,
                "RejectIngredientAllergenChange",
                "IngredientAllergenAssociation",
                ingredientId > 0 ? ingredientId : null,
                $"AttemptedAction={action}; MenuItemId={menuItemId}; IngredientId={ingredientId}; AllergenId={allergenId}; ReplacementAllergenId={replacementAllergenId?.ToString() ?? "-"}; Reason={reason}");
        }
    }
}