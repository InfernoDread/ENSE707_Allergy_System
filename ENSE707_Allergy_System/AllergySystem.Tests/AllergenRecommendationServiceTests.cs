using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AllergySystem.Tests
{
    [TestClass]
    public class AllergenRecommendationServiceTests
    {
        private static AllergenRecommendationService CreateService()
        {
            var catalogService = new AllergenCatalogService();
            return new AllergenRecommendationService(catalogService);
        }

        [TestMethod]
        public void SubmitRecommendation_ValidInput_CreatesPendingRecommendation()
        {
            var service = CreateService();

            var recommendation = service.SubmitRecommendation(42, "Gluten");

            Assert.IsNotNull(recommendation);
            Assert.AreNotEqual(0, recommendation.Id);
            Assert.AreEqual(42, recommendation.CustomerId);
            Assert.AreEqual("Gluten", recommendation.SuggestedName);
            Assert.AreEqual("Pending", recommendation.Status);
            Assert.AreNotEqual(default, recommendation.SubmittedAt);
        }

        [TestMethod]
        public void SubmitRecommendation_BlankName_ThrowsArgumentException()
        {
            var service = CreateService();

            Assert.ThrowsExactly<ArgumentException>(() => service.SubmitRecommendation(42, "   "));
        }

        [TestMethod]
        public void GetPendingRecommendations_ReturnsOnlyPendingRecommendations()
        {
            var service = CreateService();
            service.SubmitRecommendation(1, "Coconut");
            var approved = service.SubmitRecommendation(2, "Mustard");
            service.ApproveRecommendation(approved.Id);

            var pending = service.GetPendingRecommendations();

            Assert.HasCount(1, pending);
            Assert.AreEqual("Pending", pending[0].Status);
        }

        [TestMethod]
        public void ApproveRecommendation_ValidPending_ChangesStatusToApproved()
        {
            var service = CreateService();
            var recommendation = service.SubmitRecommendation(1, "Coconut");

            var result = service.ApproveRecommendation(recommendation.Id);

            Assert.IsTrue(result);
            Assert.AreEqual("Approved", service.GetRecommendationById(recommendation.Id)!.Status);
        }

        [TestMethod]
        public void ApproveRecommendation_AlreadyApproved_ThrowsInvalidOperationException()
        {
            var service = CreateService();
            var recommendation = service.SubmitRecommendation(1, "Coconut");
            service.ApproveRecommendation(recommendation.Id);

            Assert.ThrowsExactly<InvalidOperationException>(() => service.ApproveRecommendation(recommendation.Id));
        }

        [TestMethod]
        public void RejectRecommendation_ValidPending_ChangesStatusToRejected()
        {
            var service = CreateService();
            var recommendation = service.SubmitRecommendation(1, "Coconut");

            var result = service.RejectRecommendation(recommendation.Id);

            Assert.IsTrue(result);
            Assert.AreEqual("Rejected", service.GetRecommendationById(recommendation.Id)!.Status);
        }

        [TestMethod]
        public void GetCustomerRecommendations_ReturnsOnlySpecifiedCustomer()
        {
            var service = CreateService();
            service.SubmitRecommendation(1, "Coconut");
            service.SubmitRecommendation(2, "Blue Cheese");
            service.SubmitRecommendation(2, "Molluscs");

            var customerRecommendations = service.GetCustomerRecommendations(2);

            Assert.HasCount(2, customerRecommendations);
            Assert.IsTrue(customerRecommendations.All(r => r.CustomerId == 2));
        }

        [TestMethod]
        public void SubmitRecommendation_DuplicatePendingName_ThrowsInvalidOperationException()
        {
            var service = CreateService();
            service.SubmitRecommendation(1, "Gluten");

            Assert.ThrowsExactly<InvalidOperationException>(() => service.SubmitRecommendation(2, "gluten"));
        }

        [TestMethod]
        public void SubmitRecommendation_AlreadyResolvedName_ThrowsInvalidOperationException()
        {
            var service = CreateService();
            var recommendation = service.SubmitRecommendation(1, "Celery");
            service.ApproveRecommendation(recommendation.Id);

            Assert.ThrowsExactly<InvalidOperationException>(() => service.SubmitRecommendation(2, " Celery "));
        }

        [TestMethod]
        public void ApproveRecommendation_InvalidId_ThrowsArgumentException()
        {
            var service = CreateService();

            Assert.ThrowsExactly<ArgumentException>(() => service.ApproveRecommendation(999));
        }

        [TestMethod]
        public void SubmitRecommendation_ExistingApprovedAllergen_ThrowsInvalidOperationException()
        {
            // Arrange
            var catalogService = new AllergenCatalogService();
            var service = new AllergenRecommendationService(catalogService);

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(
                () => service.SubmitRecommendation(1, "Peanuts"));
        }

        [TestMethod]
        public void ApproveRecommendation_AddsAllergenToCatalog()
        {
            // Arrange
            var catalogService = new AllergenCatalogService();
            var service = new AllergenRecommendationService(catalogService);

            var recommendation =
                service.SubmitRecommendation(1, "Mustard");

            // Act
            service.ApproveRecommendation(recommendation.Id);

            // Assert
            var allergens = catalogService.GetAllergens();

            Assert.Contains(
                "Mustard",
                allergens.Select(a => a.Name).ToList());
        }

    }
}
