using System;
using AllergySystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AllergySystem.Tests
{
    // These tests verify dietary restriction request submission,
    // validation, storage, ID generation, and default status behaviour.
    [TestClass]
    public class DietaryRestrictionRecommendationServiceTests
    {
        [TestMethod]
        public void SubmitRecommendation_ValidRequest_CreatesPendingRecommendation()
        {
            // Arrange
            var catalogService = new DietaryRestrictionCatalogService();
            var service = new DietaryRestrictionRecommendationService(catalogService);

            // Act
            var result = service.SubmitRecommendation(1, "Halal");

            // Assert
            Assert.AreEqual(1, result.Id);
            Assert.AreEqual(1, result.CustomerId);
            Assert.AreEqual("Halal", result.Name);
            Assert.AreEqual("Pending", result.Status);
        }

        [TestMethod]
        public void SubmitRecommendation_NameWithWhitespace_TrimsName()
        {
            // Arrange
            var catalogService = new DietaryRestrictionCatalogService();
            var service = new DietaryRestrictionRecommendationService(catalogService);

            // Act
            var result = service.SubmitRecommendation(2, "  Low-Sodium  ");

            // Assert
            Assert.AreEqual("Low-Sodium", result.Name);
        }

        [TestMethod]
        public void SubmitRecommendation_MultipleRequests_AssignsUniqueIds()
        {
            // Arrange
            var catalogService = new DietaryRestrictionCatalogService();
            var service = new DietaryRestrictionRecommendationService(catalogService);

            // Act
            var first = service.SubmitRecommendation(1, "Halal");
            var second = service.SubmitRecommendation(2, "Kosher");

            // Assert
            Assert.AreEqual(1, first.Id);
            Assert.AreEqual(2, second.Id);
            Assert.AreNotEqual(first.Id, second.Id);
        }

        [TestMethod]
        public void SubmitRecommendation_ValidRequest_IsStored()
        {
            // Arrange
            var catalogService = new DietaryRestrictionCatalogService();
            var service = new DietaryRestrictionRecommendationService(catalogService);

            // Act
            service.SubmitRecommendation(3, "Low-Sodium");
            var recommendations = service.GetRecommendations();

            // Assert
            Assert.HasCount(1, recommendations);
            Assert.AreEqual("Low-Sodium", recommendations[0].Name);
            Assert.AreEqual(3, recommendations[0].CustomerId);
        }

        [TestMethod]
        public void SubmitRecommendation_BlankName_ThrowsArgumentException()
        {
            // Arrange
            var catalogService = new DietaryRestrictionCatalogService();
            var service = new DietaryRestrictionRecommendationService(catalogService);

            // Act & Assert
            Assert.ThrowsExactly<ArgumentException>(
                () => service.SubmitRecommendation(1, "   "));
        }

        [TestMethod]
        public void SubmitRecommendation_NullName_ThrowsArgumentException()
        {
            // Arrange
            var catalogService = new DietaryRestrictionCatalogService();
            var service = new DietaryRestrictionRecommendationService(catalogService);

            // Act & Assert
            Assert.ThrowsExactly<ArgumentException>(
                () => service.SubmitRecommendation(1, null!));
        }

        [TestMethod]
        public void ApproveRecommendation_AddsDietaryRestrictionToCatalog()
        {
            // Arrange
            var catalogService = new DietaryRestrictionCatalogService();
            var service = new DietaryRestrictionRecommendationService(catalogService);

            var recommendation =
                service.SubmitRecommendation(1, "Low-Sodium");

            // Act
            var result = service.ApproveRecommendation(recommendation.Id);

            var dietaryRestrictions =
                catalogService.GetDietaryRestrictions();

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual("Approved", recommendation.Status);
            Assert.IsTrue(
                dietaryRestrictions.Any(r => r.Name == "Low-Sodium"));
        }

        [TestMethod]
        public void RejectRecommendation_DoesNotAddDietaryRestrictionToCatalog()
        {
            // Arrange
            var catalogService = new DietaryRestrictionCatalogService();
            var service = new DietaryRestrictionRecommendationService(catalogService);

            var recommendation =
                service.SubmitRecommendation(1, "Low-Sodium");

            // Act
            var result = service.RejectRecommendation(recommendation.Id);

            var dietaryRestrictions =
                catalogService.GetDietaryRestrictions();

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual("Rejected", recommendation.Status);
            Assert.IsFalse(
                dietaryRestrictions.Any(r => r.Name == "Low-Sodium"));
        }
    }
}