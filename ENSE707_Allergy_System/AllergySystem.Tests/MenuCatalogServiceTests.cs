using AllergySystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace AllergySystem.Tests
{
    [TestClass]
    public class MenuCatalogServiceTests
    {
        [TestMethod]
        public void GetMenuItems_ReturnsFiveMenuItems()
        {
            // Arrange
            var allergenService = new AllergenCatalogService();
            var service = new MenuCatalogService(allergenService);

            // Act
            var menuItems = service.GetMenuItems();

            // Assert
            Assert.HasCount(5, menuItems);
        }

        [TestMethod]
        public void ClassicCheeseburger_HasExpectedAllergens()
        {
            // Arrange
            var allergenService = new AllergenCatalogService();
            var service = new MenuCatalogService(allergenService);

            // Act
            var menuItems = service.GetMenuItems();
            var burger = menuItems.FirstOrDefault(m => m.Id == 1);

            // Assert
            Assert.IsNotNull(burger, "Classic Cheeseburger should be present in the menu.");
            var cheese = burger.Ingredients.FirstOrDefault(i => i.Name == "Cheese");
            Assert.IsNotNull(cheese, "Cheese ingredient should exist on Classic Cheeseburger");
            Assert.HasCount(1, cheese.Allergens);
            Assert.AreEqual(3, cheese.Allergens[0].Id, "Cheese should contain allergen Id 3 (Milk)");

            var bun = burger.Ingredients.FirstOrDefault(i => i.Name == "Burger Bun");
            Assert.IsNotNull(bun, "Burger Bun ingredient should exist on Classic Cheeseburger");
            Assert.HasCount(2, bun.Allergens);
            var bunAllergenIds = bun.Allergens.Select(a => a.Id).ToList();
            Assert.Contains(5, bunAllergenIds, "Burger Bun should contain allergen Id 5 (Wheat)");
            Assert.Contains(9, bunAllergenIds, "Burger Bun should contain allergen Id 9 (Sesame)");
        }

        [TestMethod]
        public void PeanutChickenNoodles_HasExpectedAllergens()
        {
            // Arrange
            var allergenService = new AllergenCatalogService();
            var service = new MenuCatalogService(allergenService);

            // Act
            var menuItems = service.GetMenuItems();
            var dish = menuItems.FirstOrDefault(m => m.Id == 2);

            // Assert
            Assert.IsNotNull(dish, "Peanut Chicken Noodles should be present in the menu.");
            var noodles = dish.Ingredients.FirstOrDefault(i => i.Name == "Noodles");
            Assert.IsNotNull(noodles, "Noodles ingredient should exist on Peanut Chicken Noodles");
            Assert.HasCount(2, noodles.Allergens);
            var noodlesAllergenIds = noodles.Allergens.Select(a => a.Id).ToList();
            Assert.Contains(4, noodlesAllergenIds, "Noodles should contain allergen Id 4 (Eggs)");
            Assert.Contains(5, noodlesAllergenIds, "Noodles should contain allergen Id 5 (Wheat)");

            var peanutSauce = dish.Ingredients.FirstOrDefault(i => i.Name == "Peanut Sauce");
            Assert.IsNotNull(peanutSauce, "Peanut Sauce ingredient should exist on Peanut Chicken Noodles");
            Assert.HasCount(2, peanutSauce.Allergens);
            var sauceAllergenIds = peanutSauce.Allergens.Select(a => a.Id).ToList();
            Assert.Contains(1, sauceAllergenIds, "Peanut Sauce should contain allergen Id 1 (Peanuts)");
            Assert.Contains(6, sauceAllergenIds, "Peanut Sauce should contain allergen Id 6 (Soy)");
        }

        [TestMethod]
        public void FishAndChips_HasExpectedAllergens()
        {
            // Arrange
            var allergenService = new AllergenCatalogService();
            var service = new MenuCatalogService(allergenService);

            // Act
            var menuItems = service.GetMenuItems();
            var dish = menuItems.FirstOrDefault(m => m.Id == 3);

            // Assert
            Assert.IsNotNull(dish, "Fish and Chips should be present in the menu.");
            var batteredFish = dish.Ingredients.FirstOrDefault(i => i.Name == "Battered Fish");
            Assert.IsNotNull(batteredFish, "Battered Fish ingredient should exist on Fish and Chips");
            Assert.HasCount(2, batteredFish.Allergens);
            var fishAllergenIds = batteredFish.Allergens.Select(a => a.Id).ToList();
            Assert.Contains(7, fishAllergenIds, "Battered Fish should contain allergen Id 7 (Fish)");
            Assert.Contains(5, fishAllergenIds, "Battered Fish should contain allergen Id 5 (Wheat)");

            var chips = dish.Ingredients.FirstOrDefault(i => i.Name == "Chips");
            Assert.IsNotNull(chips, "Chips ingredient should exist on Fish and Chips");
            Assert.HasCount(0, chips.Allergens);
        }

        [TestMethod]
        public void CreamyPasta_HasExpectedAllergens()
        {
            // Arrange
            var allergenService = new AllergenCatalogService();
            var service = new MenuCatalogService(allergenService);

            // Act
            var menuItems = service.GetMenuItems();
            var dish = menuItems.FirstOrDefault(m => m.Id == 4);

            // Assert
            Assert.IsNotNull(dish, "Creamy Pasta should be present in the menu.");
            var pasta = dish.Ingredients.FirstOrDefault(i => i.Name == "Pasta");
            Assert.IsNotNull(pasta, "Pasta ingredient should exist on Creamy Pasta");
            Assert.HasCount(2, pasta.Allergens);
            var pastaAllergenIds = pasta.Allergens.Select(a => a.Id).ToList();
            Assert.Contains(4, pastaAllergenIds, "Pasta should contain allergen Id 4 (Eggs)");
            Assert.Contains(5, pastaAllergenIds, "Pasta should contain allergen Id 5 (Wheat)");

            var cream = dish.Ingredients.FirstOrDefault(i => i.Name == "Cream Sauce");
            Assert.IsNotNull(cream, "Cream Sauce ingredient should exist on Creamy Pasta");
            Assert.HasCount(1, cream.Allergens);
            Assert.AreEqual(3, cream.Allergens[0].Id, "Cream Sauce should contain allergen Id 3 (Milk)");
        }

        [TestMethod]
        public void GardenSalad_HasNoAllergens()
        {
            // Arrange
            var allergenService = new AllergenCatalogService();
            var service = new MenuCatalogService(allergenService);

            // Act
            var menuItems = service.GetMenuItems();
            var salad = menuItems.FirstOrDefault(m => m.Id == 5);

            // Assert
            Assert.IsNotNull(salad, "Garden Salad should be present in the menu.");
            foreach (var ingredient in salad.Ingredients)
            {
                Assert.HasCount(0, ingredient.Allergens, $"Ingredient '{ingredient.Name}' should have no allergens.");
            }
        }

        [TestMethod]
        public void GetMenuItems_MenuItemIdsAreUnique()
        {
            // Arrange
            var allergenService = new AllergenCatalogService();
            var service = new MenuCatalogService(allergenService);

            // Act
            var menuItems = service.GetMenuItems();
            var uniqueIds = menuItems
                .Select(m => m.Id)
                .Distinct()
                .ToList();

            // Assert
            Assert.HasCount(menuItems.Count, uniqueIds);
        }

        [TestMethod]
        public void GetMenuItems_IngredientIdsAreUnique()
        {
            // Arrange
            var allergenService = new AllergenCatalogService();
            var service = new MenuCatalogService(allergenService);

            // Act
            var ingredients = service.GetMenuItems()
                .SelectMany(m => m.Ingredients)
                .ToList();

            var uniqueIds = ingredients
                .Select(i => i.Id)
                .Distinct()
                .ToList();

            // Assert
            Assert.HasCount(ingredients.Count, uniqueIds);
        }
    }
}
