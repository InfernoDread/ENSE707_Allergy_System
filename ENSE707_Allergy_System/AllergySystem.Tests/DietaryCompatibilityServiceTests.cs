using System;
using System.Collections.Generic;
using System.Linq;
using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AllergySystem.Tests
{
    [TestClass]
    public class DietaryCompatibilityServiceTests
    {
        [TestMethod]
        public void FindDietaryWarnings_AllSelectedRestrictionsHaveMatchingLabels_NoWarnings()
        {
            // Arrange
            var service = new DietaryCompatibilityService();
            var menuItem = new MenuItem
            {
                DietaryLabels = new List<DietaryRestriction>
                {
                    new DietaryRestriction { Id = 1, Name = "Vegetarian" },
                    new DietaryRestriction { Id = 2, Name = "Vegan" }
                }
            };

            var customerRestrictions = new List<DietaryRestriction>
            {
                new DietaryRestriction { Id = 1, Name = "Vegetarian" },
                new DietaryRestriction { Id = 2, Name = "Vegan" }
            };

            // Act
            var warnings = service.FindDietaryWarnings(menuItem, customerRestrictions);

            // Assert
            Assert.IsNotNull(warnings);
            Assert.IsFalse(warnings.Any());
        }

        [TestMethod]
        public void FindDietaryWarnings_NoSelectedRestrictionsHaveMatchingLabels_AllRestrictionsReturned()
        {
            // Arrange
            var service = new DietaryCompatibilityService();
            var menuItem = new MenuItem(); // no labels

            var customerRestrictions = new List<DietaryRestriction>
            {
                new DietaryRestriction { Id = 1, Name = "Vegetarian" },
                new DietaryRestriction { Id = 3, Name = "Gluten-Free" }
            };

            // Act
            var warnings = service.FindDietaryWarnings(menuItem, customerRestrictions);

            // Assert
            Assert.IsNotNull(warnings);
            Assert.HasCount(2, warnings);
            CollectionAssert.AreEquivalent(customerRestrictions.Select(r => r.Id).ToList(), warnings.Select(w => w.Id).ToList());
        }

        [TestMethod]
        public void FindDietaryWarnings_SomeRestrictionsMatch_OnlyUnmatchedReturned()
        {
            // Arrange
            var service = new DietaryCompatibilityService();
            var menuItem = new MenuItem
            {
                DietaryLabels = new List<DietaryRestriction>
                {
                    new DietaryRestriction { Id = 1, Name = "Vegetarian" }
                }
            };

            var customerRestrictions = new List<DietaryRestriction>
            {
                new DietaryRestriction { Id = 1, Name = "Vegetarian" },
                new DietaryRestriction { Id = 2, Name = "Vegan" }
            };

            // Act
            var warnings = service.FindDietaryWarnings(menuItem, customerRestrictions);

            // Assert
            Assert.IsNotNull(warnings);
            Assert.HasCount(1, warnings);
            Assert.AreEqual(2, warnings.First().Id);
        }

        [TestMethod]
        public void FindDietaryWarnings_DuplicateCustomerRestrictions_WarningsDeduplicatedById()
        {
            // Arrange
            var service = new DietaryCompatibilityService();
            var menuItem = new MenuItem(); // no labels

            var customerRestrictions = new List<DietaryRestriction>
            {
                new DietaryRestriction { Id = 1, Name = "Vegetarian" },
                new DietaryRestriction { Id = 1, Name = "Vegetarian Duplicate" },
                new DietaryRestriction { Id = 2, Name = "Vegan" }
            };

            // Act
            var warnings = service.FindDietaryWarnings(menuItem, customerRestrictions);

            // Assert
            Assert.IsNotNull(warnings);
            Assert.HasCount(2, warnings);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, warnings.Select(w => w.Id).ToList());
        }

        [TestMethod]
        public void FindDietaryWarnings_DifferentInstancesWithMatchingIds_RecognisedAsCompatible()
        {
            // Arrange
            var service = new DietaryCompatibilityService();
            var menuItem = new MenuItem
            {
                DietaryLabels = new List<DietaryRestriction>
                {
                    new DietaryRestriction { Id = 1, Name = "Vegetarian (catalog)" }
                }
            };

            // Customer restriction is a different object instance with the same Id
            var customerRestrictions = new List<DietaryRestriction>
            {
                new DietaryRestriction { Id = 1, Name = "Vegetarian (profile)" }
            };

            // Act
            var warnings = service.FindDietaryWarnings(menuItem, customerRestrictions);

            // Assert
            Assert.IsNotNull(warnings);
            Assert.IsFalse(warnings.Any(), "Restrictions with matching Ids should be recognised as compatible regardless of object instance.");
        }

        [TestMethod]
        public void FindDietaryWarnings_NullArguments_ThrowsArgumentNullException()
        {
            // Arrange
            var service = new DietaryCompatibilityService();
            var menuItem = new MenuItem();
            var customerRestrictions = new List<DietaryRestriction>();

            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() => service.FindDietaryWarnings(null!, customerRestrictions));
            Assert.ThrowsExactly<ArgumentNullException>(() => service.FindDietaryWarnings(menuItem, null!));
        }

        [TestMethod]
        public void FindDietaryWarnings_ValidInputs_DoNotModifyOriginalCollectionsOrObjects()
        {
            // Arrange
            var service = new DietaryCompatibilityService();
            var label = new DietaryRestriction { Id = 1, Name = "Vegetarian" };
            var menuItem = new MenuItem
            {
                DietaryLabels = new List<DietaryRestriction> { label }
            };

            var restrictionA = new DietaryRestriction { Id = 1, Name = "Vegetarian" };
            var restrictionB = new DietaryRestriction { Id = 2, Name = "Vegan" };
            var customerRestrictions = new List<DietaryRestriction> { restrictionA, restrictionB };

            // Take snapshots
            var menuLabelSnapshot = menuItem.DietaryLabels.Select(l => (l.Id, l.Name)).ToList();
            var customerSnapshot = customerRestrictions.Select(r => (r.Id, r.Name)).ToList();

            // Act
            var warnings = service.FindDietaryWarnings(menuItem, customerRestrictions);

            // Assert - original collections have not changed
            Assert.HasCount(1, menuItem.DietaryLabels);
            Assert.HasCount(2, customerRestrictions);

            CollectionAssert.AreEqual(
                menuLabelSnapshot,
                menuItem.DietaryLabels.Select(l => (l.Id, l.Name)).ToList());

            CollectionAssert.AreEqual(
                customerSnapshot,
                customerRestrictions.Select(r => (r.Id, r.Name)).ToList());

            // Original object instances must still be present
            Assert.AreSame(label, menuItem.DietaryLabels[0]);
            Assert.AreSame(restrictionA, customerRestrictions[0]);
            Assert.AreSame(restrictionB, customerRestrictions[1]);
        }
    }
}
