using AllergySystem.Models;
using AllergySystem.Services;

namespace AllergySystem.Tests
{
    [TestClass]
    public class MenuAllergenManagementServiceTests
    {
        private sealed record TestContext(
            MenuAllergenManagementService ManagementService,
            MenuCatalogService MenuCatalogService,
            InMemoryAuditStore AuditStore,
            InMemoryOrderStore OrderStore,
            OrderService OrderService,
            CartService CartService,
            InMemoryAllergyProfileStore ProfileStore);

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

            var orderStore = new InMemoryOrderStore();
            var cartStore = new InMemoryCartStore();
            var profileStore = new InMemoryAllergyProfileStore();
            var validationService = new AllergyValidationService();
            var cartService = new CartService(cartStore, menuCatalog, profileStore, validationService);
            var orderService = new OrderService(
                orderStore,
                cartService,
                profileStore,
                validationService,
                new DietaryCompatibilityService());

            return new TestContext(
                managementService,
                menuCatalog,
                auditStore,
                orderStore,
                orderService,
                cartService,
                profileStore);
        }

        [TestMethod]
        public void AddAssociation_ValidMapping_IsVisibleAndAudited()
        {
            var context = CreateContext();

            context.ManagementService.AddAssociation(1, 4, 2);

            var lettuce = context.MenuCatalogService.GetMenuItems()
                .Single(item => item.Id == 1)
                .Ingredients.Single(ingredient => ingredient.Id == 4);
            Assert.AreEqual(2, lettuce.Allergens.Single().Id);

            var entry = context.AuditStore.GetAll().Single();
            Assert.AreEqual("Administration", entry.ActorRole);
            Assert.AreEqual("AddIngredientAllergen", entry.Action);
            Assert.AreEqual("IngredientAllergenAssociation", entry.EntityType);
            Assert.AreEqual(4, entry.EntityId);
            StringAssert.Contains(entry.Details, "MenuItemId=1");
            StringAssert.Contains(entry.Details, "AllergenId=2");
        }

        [TestMethod]
        public void UpdateAssociation_ValidMapping_ReplacesAndAudits()
        {
            var context = CreateContext();

            context.ManagementService.UpdateAssociation(1, 3, 9, 2);

            var bun = context.MenuCatalogService.GetMenuItems()
                .Single(item => item.Id == 1)
                .Ingredients.Single(ingredient => ingredient.Id == 3);
            CollectionAssert.AreEquivalent(new[] { 2, 5 }, bun.Allergens.Select(allergen => allergen.Id).ToArray());
            var entry = context.AuditStore.GetAll().Single();
            Assert.AreEqual("Administration", entry.ActorRole);
            Assert.AreEqual("UpdateIngredientAllergen", entry.Action);
            StringAssert.Contains(entry.Details, "AllergenId=9");
            StringAssert.Contains(entry.Details, "ReplacementAllergenId=2");
        }

        [TestMethod]
        public void RemoveAssociation_ValidMapping_RemovesAndAudits()
        {
            var context = CreateContext();

            context.ManagementService.RemoveAssociation(1, 3, 9);

            var bun = context.MenuCatalogService.GetMenuItems()
                .Single(item => item.Id == 1)
                .Ingredients.Single(ingredient => ingredient.Id == 3);
            CollectionAssert.AreEqual(new[] { 5 }, bun.Allergens.Select(allergen => allergen.Id).ToArray());
            var entry = context.AuditStore.GetAll().Single();
            Assert.AreEqual("Administration", entry.ActorRole);
            Assert.AreEqual("RemoveIngredientAllergen", entry.Action);
            StringAssert.Contains(entry.Details, "AllergenId=9");
        }

        [TestMethod]
        public void AddAssociation_UnknownMenuItem_RejectsWithoutChangingMapping()
        {
            var context = CreateContext();

            Assert.ThrowsExactly<ArgumentException>(
                () => context.ManagementService.AddAssociation(999, 4, 2));

            Assert.IsEmpty(context.MenuCatalogService.GetMenuItems()
                .Single(item => item.Id == 1)
                .Ingredients.Single(ingredient => ingredient.Id == 4).Allergens);
            AssertRejectedAudit(context);
        }

        [TestMethod]
        public void AddAssociation_IngredientFromDifferentMenuItem_RejectsWithoutChangingMapping()
        {
            var context = CreateContext();

            Assert.ThrowsExactly<ArgumentException>(
                () => context.ManagementService.AddAssociation(1, 6, 2));

            Assert.IsEmpty(context.MenuCatalogService.GetMenuItems()
                .Single(item => item.Id == 1)
                .Ingredients.Single(ingredient => ingredient.Id == 4).Allergens);
            AssertRejectedAudit(context);
        }

        [TestMethod]
        public void AddAssociation_UnknownAllergen_RejectsWithoutChangingMapping()
        {
            var context = CreateContext();

            Assert.ThrowsExactly<ArgumentException>(
                () => context.ManagementService.AddAssociation(1, 4, 999));

            Assert.IsEmpty(context.MenuCatalogService.GetMenuItems()
                .Single(item => item.Id == 1)
                .Ingredients.Single(ingredient => ingredient.Id == 4).Allergens);
            AssertRejectedAudit(context);
        }

        [TestMethod]
        public void AddAssociation_DuplicateMapping_RejectsWithoutChangingMapping()
        {
            var context = CreateContext();

            Assert.ThrowsExactly<InvalidOperationException>(
                () => context.ManagementService.AddAssociation(1, 3, 5));

            var bun = context.MenuCatalogService.GetMenuItems()
                .Single(item => item.Id == 1)
                .Ingredients.Single(ingredient => ingredient.Id == 3);
            CollectionAssert.AreEquivalent(new[] { 5, 9 }, bun.Allergens.Select(allergen => allergen.Id).ToArray());
            AssertRejectedAudit(context);
        }

        [TestMethod]
        public void UpdateAssociation_DuplicateReplacement_RejectsWithoutChangingMapping()
        {
            var context = CreateContext();

            Assert.ThrowsExactly<InvalidOperationException>(
                () => context.ManagementService.UpdateAssociation(1, 3, 9, 5));

            var bun = context.MenuCatalogService.GetMenuItems()
                .Single(item => item.Id == 1)
                .Ingredients.Single(ingredient => ingredient.Id == 3);
            CollectionAssert.AreEquivalent(new[] { 5, 9 }, bun.Allergens.Select(allergen => allergen.Id).ToArray());
            AssertRejectedAudit(context);
        }

        [TestMethod]
        public void UpdateAssociation_UnknownReplacementAllergen_RejectsWithoutChangingMapping()
        {
            var context = CreateContext();

            Assert.ThrowsExactly<ArgumentException>(
                () => context.ManagementService.UpdateAssociation(1, 2, 3, 999));

            var cheese = context.MenuCatalogService.GetMenuItems()
                .Single(item => item.Id == 1)
                .Ingredients.Single(ingredient => ingredient.Id == 2);
            CollectionAssert.AreEqual(new[] { 3 }, cheese.Allergens.Select(allergen => allergen.Id).ToArray());
            AssertRejectedAudit(context);
        }

        [TestMethod]
        public void UpdateAssociation_UnknownMenuItem_RejectsWithoutChangingMapping()
        {
            var context = CreateContext();

            Assert.ThrowsExactly<ArgumentException>(
                () => context.ManagementService.UpdateAssociation(999, 2, 3, 2));

            var cheese = context.MenuCatalogService.GetMenuItems()
                .Single(item => item.Id == 1)
                .Ingredients.Single(ingredient => ingredient.Id == 2);
            CollectionAssert.AreEqual(new[] { 3 }, cheese.Allergens.Select(allergen => allergen.Id).ToArray());
            AssertRejectedAudit(context);
        }

        [TestMethod]
        public void RemoveAssociation_UnknownAllergen_RejectsWithoutChangingMapping()
        {
            var context = CreateContext();

            Assert.ThrowsExactly<ArgumentException>(
                () => context.ManagementService.RemoveAssociation(1, 2, 999));

            var cheese = context.MenuCatalogService.GetMenuItems()
                .Single(item => item.Id == 1)
                .Ingredients.Single(ingredient => ingredient.Id == 2);
            CollectionAssert.AreEqual(new[] { 3 }, cheese.Allergens.Select(allergen => allergen.Id).ToArray());
            AssertRejectedAudit(context);
        }

        [TestMethod]
        public void RemoveAssociation_IngredientFromDifferentMenuItem_RejectsWithoutChangingMapping()
        {
            var context = CreateContext();

            Assert.ThrowsExactly<ArgumentException>(
                () => context.ManagementService.RemoveAssociation(1, 6, 2));

            AssertRejectedAudit(context);
            Assert.IsEmpty(context.MenuCatalogService.GetMenuItems()
                .Single(item => item.Id == 2)
                .Ingredients.Single(ingredient => ingredient.Id == 6).Allergens);
        }

        [TestMethod]
        public void CatalogueEdit_DoesNotChangeExistingOrderSnapshot()
        {
            var context = CreateContext();
            const int customerId = 500;
            var profile = context.ProfileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            context.ProfileStore.SaveProfile(profile);

            context.CartService.AddItem(customerId, 1);
            profile.Allergens = new List<Allergen>
            {
                new() { Id = 3, Name = "Milk" }
            };
            context.ProfileStore.SaveProfile(profile);

            var historicalOrder = context.OrderService.CreateOrderFromCart(customerId);
            var historicalOrderSnapshot = context.OrderStore.GetOrder(historicalOrder.Id)!;
            Assert.AreEqual(OrderStatus.PendingAllergyConfirmation, historicalOrderSnapshot.Status);
            Assert.AreEqual("Milk", historicalOrderSnapshot.ConflictingAllergens.Single().Name);

            context.ManagementService.UpdateAssociation(1, 2, 3, 2);

            var historicalCheese = historicalOrderSnapshot
                .Items.Single().MenuItem.Ingredients.Single(ingredient => ingredient.Id == 2).Allergens.Single();
            Assert.AreEqual(3, historicalCheese.Id);
            Assert.AreEqual("Milk", historicalCheese.Name);
            Assert.AreEqual(OrderStatus.PendingAllergyConfirmation, historicalOrderSnapshot.Status);
            Assert.AreEqual("Milk", historicalOrderSnapshot.ConflictingAllergens.Single().Name);

            context.CartService.AddItem(customerId, 1);
            var newOrder = context.OrderService.CreateOrderFromCart(customerId);
            var currentCheese = context.OrderStore.GetOrder(newOrder.Id)!
                .Items.Single().MenuItem.Ingredients.Single(ingredient => ingredient.Id == 2).Allergens.Single();
            Assert.AreEqual(2, currentCheese.Id);
            Assert.AreEqual("Tree Nuts", currentCheese.Name);
            Assert.AreEqual(OrderStatus.Pending, newOrder.Status);
            Assert.IsEmpty(newOrder.ConflictingAllergens);
        }

        private static void AssertRejectedAudit(TestContext context)
        {
            var entry = context.AuditStore.GetAll().Single();
            Assert.AreEqual("Administration", entry.ActorRole);
            Assert.AreEqual("RejectIngredientAllergenChange", entry.Action);
            Assert.AreEqual("IngredientAllergenAssociation", entry.EntityType);
            StringAssert.Contains(entry.Details, "AttemptedAction=");
            StringAssert.Contains(entry.Details, "MenuItemId=");
            StringAssert.Contains(entry.Details, "IngredientId=");
            StringAssert.Contains(entry.Details, "Reason=");
        }
    }
}