using AllergySystem.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace AllergySystem.Tests
{
    // These tests verify the behaviour of the allergen catalogue service, including the predefined allergen list and unique allergen IDs.
    [TestClass]
    public class AllergenCatalogServiceTests
    {
        [TestMethod]
        public void GetAllergens_ReturnsAllPredefinedAllergens()
        {
            // Arrange
            var service = new AllergenCatalogService();

            // Act
            var allergens = service.GetAllergens();

            // Assert
            Assert.HasCount(9, allergens);
        }

        [TestMethod]
        public void GetAllergens_AllergenIdsAreUnique()
        {
            // Arrange
            var service = new AllergenCatalogService();

            // Act
            var allergens = service.GetAllergens();
            var distinctIds = allergens.Select(a => a.Id).Distinct().Count();

            // Assert
            Assert.AreEqual(allergens.Count, distinctIds);
        }
    }
}
