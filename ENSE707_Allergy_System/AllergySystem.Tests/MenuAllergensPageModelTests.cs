using AllergySystem.Pages.Admin;
using AllergySystem.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AllergySystem.Tests
{
    [TestClass]
    public class MenuAllergensPageModelTests
    {
        private sealed record TestContext(
            MenuAllergensModel PageModel,
            MenuCatalogService MenuCatalogService,
            InMemoryAuditStore AuditStore);

        private static TestContext CreateContext()
        {
            var allergenCatalog = new AllergenCatalogService();
            var mappingStore = new MenuAllergenMappingStore();
            var menuCatalog = new MenuCatalogService(allergenCatalog, mappingStore);
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore);
            var managementService = new MenuAllergenManagementService(
                menuCatalog,
                allergenCatalog,
                mappingStore,
                auditService);

            return new TestContext(
                new MenuAllergensModel(managementService),
                menuCatalog,
                auditStore);
        }

        [TestMethod]
        public void OnGet_LoadsMenuMappingsAndApprovedAllergens()
        {
            var context = CreateContext();

            context.PageModel.OnGet();

            Assert.HasCount(5, context.PageModel.MenuItems);
            Assert.HasCount(9, context.PageModel.AvailableAllergens);
            Assert.AreEqual("Milk", context.PageModel.MenuItems
                .Single(item => item.Id == 1)
                .Ingredients.Single(ingredient => ingredient.Id == 2)
                .Allergens.Single().Name);
        }

        [TestMethod]
        public void PostHandlers_AddUpdateAndRemoveMapping()
        {
            var context = CreateContext();

            Assert.IsInstanceOfType<PageResult>(context.PageModel.OnPostAdd(1, 4, 2));
            Assert.AreEqual("Allergen association added.", context.PageModel.StatusMessage);

            Assert.IsInstanceOfType<PageResult>(context.PageModel.OnPostUpdate(1, 4, 2, 5));
            Assert.AreEqual("Allergen association updated.", context.PageModel.StatusMessage);

            Assert.IsInstanceOfType<PageResult>(context.PageModel.OnPostRemove(1, 4, 5));
            Assert.AreEqual("Allergen association removed.", context.PageModel.StatusMessage);

            var lettuce = context.MenuCatalogService.GetMenuItems()
                .Single(item => item.Id == 1)
                .Ingredients.Single(ingredient => ingredient.Id == 4);
            Assert.IsEmpty(lettuce.Allergens);
            Assert.HasCount(3, context.AuditStore.GetAll());
        }

        [TestMethod]
        public void PostAdd_DuplicateMapping_ShowsValidationAndPreservesMapping()
        {
            var context = CreateContext();

            Assert.IsInstanceOfType<PageResult>(context.PageModel.OnPostAdd(1, 3, 5));

            Assert.IsNull(context.PageModel.StatusMessage);
            StringAssert.Contains(context.PageModel.ErrorMessage, "already associated");
            var bun = context.MenuCatalogService.GetMenuItems()
                .Single(item => item.Id == 1)
                .Ingredients.Single(ingredient => ingredient.Id == 3);
            CollectionAssert.AreEquivalent(new[] { 5, 9 }, bun.Allergens.Select(allergen => allergen.Id).ToArray());
            Assert.AreEqual("RejectIngredientAllergenChange", context.AuditStore.GetAll().Single().Action);
        }

        [TestMethod]
        public void PostAdd_MalformedTarget_ShowsValidationAndAuditsRejection()
        {
            var context = CreateContext();

            context.PageModel.OnPostAdd(1, 4, null);

            Assert.AreEqual("Select an allergen from the approved list.", context.PageModel.ErrorMessage);
            Assert.AreEqual("RejectIngredientAllergenChange", context.AuditStore.GetAll().Single().Action);
            Assert.IsEmpty(context.MenuCatalogService.GetMenuItems()
                .Single(item => item.Id == 1)
                .Ingredients.Single(ingredient => ingredient.Id == 4).Allergens);
        }

        [TestMethod]
        public void PostRemove_UnassociatedAllergen_ShowsUnsupportedChangeMessage()
        {
            var context = CreateContext();

            context.PageModel.OnPostRemove(1, 4, 2);

            StringAssert.Contains(context.PageModel.ErrorMessage, "not associated");
            Assert.AreEqual("RejectIngredientAllergenChange", context.AuditStore.GetAll().Single().Action);
            Assert.IsEmpty(context.MenuCatalogService.GetMenuItems()
                .Single(item => item.Id == 1)
                .Ingredients.Single(ingredient => ingredient.Id == 4).Allergens);
        }

        [TestMethod]
        public void OnPost_UnsupportedOperation_ShowsValidationMessage()
        {
            var context = CreateContext();

            Assert.IsInstanceOfType<PageResult>(context.PageModel.OnPost());

            Assert.AreEqual("Unsupported operation. Choose Add, Replace, or Remove.", context.PageModel.ErrorMessage);
            Assert.AreEqual("RejectUnsupportedMenuAllergenOperation", context.AuditStore.GetAll().Single().Action);
            Assert.IsEmpty(context.MenuCatalogService.GetMenuItems()
                .Single(item => item.Id == 1)
                .Ingredients.Single(ingredient => ingredient.Id == 4).Allergens);
        }
    }
}