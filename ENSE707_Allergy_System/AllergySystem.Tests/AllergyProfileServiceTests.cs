using System;
using System.Collections.Generic;
using System.Linq;
using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AllergySystem.Tests
{
    [TestClass]
    public class AllergyProfileServiceTests
    {
        [TestMethod]
        public void UpdateProfile_WithValidAllergens_UpdatesProfile()
        {
            // Arrange
            var service = new AllergyProfileService();
            var profile = new AllergyProfile { CustomerId = 1 };
            var selected = new List<Allergen>
            {
                new Allergen { Id = 1, Name = "Peanuts" },
                new Allergen { Id = 8, Name = "Shellfish" }
            };

            // Act
            service.UpdateProfile(profile, selected);

            // Assert
            Assert.IsNotNull(profile.Allergens);
            Assert.HasCount(2, profile.Allergens);
            CollectionAssert.AreEqual(selected.Select(a => a.Id).ToList(), profile.Allergens.Select(a => a.Id).ToList());
        }

        [TestMethod]
        public void UpdateProfile_DuplicateAllergenIds_AreRemoved()
        {
            // Arrange
            var service = new AllergyProfileService();
            var profile = new AllergyProfile { CustomerId = 2 };
            var selected = new List<Allergen>
            {
                new Allergen { Id = 1, Name = "Peanuts" },
                new Allergen { Id = 1, Name = "Peanuts Duplicate" },
                new Allergen { Id = 3, Name = "Milk" }
            };

            // Act
            service.UpdateProfile(profile, selected);

            // Assert
            Assert.HasCount(2, profile.Allergens);
            CollectionAssert.AreEquivalent(new[] { 1, 3 }, profile.Allergens.Select(a => a.Id).ToList());
        }

        [TestMethod]
        public void UpdateProfile_NewSelection_ReplacesExistingProfileAllergens()
        {
            // Arrange
            var service = new AllergyProfileService();
            var profile = new AllergyProfile
            {
                CustomerId = 3,
                Allergens = new List<Allergen>
                {
                    new Allergen { Id = 5, Name = "Wheat" },
                    new Allergen { Id = 6, Name = "Soy" }
                }
            };

            var selected = new List<Allergen>
            {
                new Allergen { Id = 7, Name = "Fish" }
            };

            // Act
            service.UpdateProfile(profile, selected);

            // Assert
            Assert.HasCount(1, profile.Allergens);
            Assert.AreEqual(7, profile.Allergens.First().Id);
        }

        [TestMethod]
        public void UpdateProfile_NullProfile_ThrowsArgumentNullException()
        {
            // Arrange
            var service = new AllergyProfileService();

            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() => service.UpdateProfile(null!, new List<Allergen>()));
        }

        [TestMethod]
        public void UpdateProfile_NullSelectedAllergens_ThrowsArgumentNullException()
        {
            // Arrange
            var service = new AllergyProfileService();
            var profile = new AllergyProfile { CustomerId = 4 };

            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() => service.UpdateProfile(profile, null!));
        }
    }
}
