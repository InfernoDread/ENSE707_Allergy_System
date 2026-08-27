using System;
using System.Collections.Generic;
using System.Text;
using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AllergySystem.Tests
{
    [TestClass]
    public class AllergyValidationServiceTests
    {
        [TestMethod]
        public void FindConflicts_MatchingAllergen_ReturnsConflict()
        {
            // Arrange
            var service = new AllergyValidationService();
            var menuItem = new MenuItem
            {
                Ingredients = new List<Ingredient>
                {
                    new Ingredient
                    {
                        Allergens = new List<Allergen>
                        {
                            new Allergen { Id = 1, Name = "Peanuts" }
                        }
                    }
                }
            };
            var customerAllergens = new List<Allergen>
            {
                new Allergen { Id = 1, Name = "Peanuts" }
            };

            // Act
            var conflicts = service.FindConflicts(menuItem, customerAllergens);

            // Assert
            Assert.HasCount(1, conflicts);
            Assert.AreEqual("Peanuts", conflicts[0].Name);
        }

        [TestMethod]
        public void FindConflicts_NoMatchingAllergen_ReturnsNoConflicts()
        {
            // Arrange
            var service = new AllergyValidationService();
            var menuItem = new MenuItem
            {
                Ingredients = new List<Ingredient>
                {
                    new Ingredient
                    {
                        Allergens = new List<Allergen>
                        {
                            new Allergen { Id = 1, Name = "Peanuts" }
                        }
                    }
                }
            };
            var customerAllergens = new List<Allergen>
            {
                new Allergen { Id = 2, Name = "Shellfish" }
            };
            // Act
            var conflicts = service.FindConflicts(menuItem, customerAllergens);
            // Assert
            Assert.HasCount(0, conflicts);
        }

        [TestMethod]
        public void FindConflicts_DuplicateAllergenAcrossIngredients_ReturnsSingleConflict()
        {
            // Arrange
            var service = new AllergyValidationService();
            var peanut = new Allergen { Id = 1, Name = "Peanuts" };
            var menuItem = new MenuItem
            {
                Ingredients = new List<Ingredient>
                {
                    new Ingredient { Allergens = new List<Allergen> { peanut } },
                    new Ingredient { Allergens = new List<Allergen> { new Allergen { Id = 1, Name = "Peanuts" } } }
                }
            };
            var customerAllergens = new List<Allergen> { new Allergen { Id = 1, Name = "Peanuts" } };

            // Act
            var conflicts = service.FindConflicts(menuItem, customerAllergens);

            // Assert
            Assert.HasCount(1, conflicts);
            Assert.AreEqual(1, conflicts[0].Id);
        }

        [TestMethod]
        public void FindConflicts_MultipleDistinctMatchingAllergens_ReturnsAllConflicts()
        {
            // Arrange
            var service = new AllergyValidationService();
            var menuItem = new MenuItem
            {
                Ingredients = new List<Ingredient>
                {
                    new Ingredient { Allergens = new List<Allergen> { new Allergen { Id = 1, Name = "Peanuts" } } },
                    new Ingredient { Allergens = new List<Allergen> { new Allergen { Id = 2, Name = "Shellfish" } } }
                }
            };
            var customerAllergens = new List<Allergen>
            {
                new Allergen { Id = 1, Name = "Peanuts" },
                new Allergen { Id = 2, Name = "Shellfish" }
            };

            // Act
            var conflicts = service.FindConflicts(menuItem, customerAllergens);

            // Assert
            Assert.HasCount(2, conflicts);
            CollectionAssert.AreEquivalent(new List<int> { 1, 2 }, conflicts.ConvertAll(c => c.Id));
        }

        [TestMethod]
        public void FindConflicts_IngredientWithMultipleAllergens_ReturnsOnlyMatchingAllergen()
        {
            // Arrange
            var service = new AllergyValidationService();
            var menuItem = new MenuItem
            {
                Ingredients = new List<Ingredient>
                {
                    new Ingredient
                    {
                        Allergens = new List<Allergen>
                        {
                            new Allergen { Id = 1, Name = "Peanuts" },
                            new Allergen { Id = 2, Name = "Shellfish" }
                        }
                    }
                }
            };
            var customerAllergens = new List<Allergen> { new Allergen { Id = 2, Name = "Shellfish" } };

            // Act
            var conflicts = service.FindConflicts(menuItem, customerAllergens);

            // Assert
            Assert.HasCount(1, conflicts);
            Assert.AreEqual(2, conflicts[0].Id);
        }

        [TestMethod]
        public void FindConflicts_EmptyMenuIngredients_ReturnsNoConflicts()
        {
            // Arrange
            var service = new AllergyValidationService();
            var menuItem = new MenuItem { Ingredients = new List<Ingredient>() };
            var customerAllergens = new List<Allergen> { new Allergen { Id = 1, Name = "Peanuts" } };

            // Act
            var conflicts = service.FindConflicts(menuItem, customerAllergens);

            // Assert
            Assert.HasCount(0, conflicts);
        }

        [TestMethod]
        public void FindConflicts_EmptyCustomerAllergens_ReturnsNoConflicts()
        {
            // Arrange
            var service = new AllergyValidationService();
            var menuItem = new MenuItem
            {
                Ingredients = new List<Ingredient>
                {
                    new Ingredient { Allergens = new List<Allergen> { new Allergen { Id = 1, Name = "Peanuts" } } }
                }
            };
            var customerAllergens = new List<Allergen>();

            // Act
            var conflicts = service.FindConflicts(menuItem, customerAllergens);

            // Assert
            Assert.HasCount(0, conflicts);
        }
    }
}
