using System.Reflection;
using System.Security.Claims;
using AllergySystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AllergySystem.Tests
{
    [TestClass]
    public class PrototypePageAuthorizationTests
    {
        private sealed record ProtectedAction(string Page, string Action, string RequiredRole);

        private static readonly ProtectedAction[] ProtectedActions =
        {
            new("/Customer/AllergyProfile", "GET", PrototypeRoles.Customer),
            new("/Customer/AllergyProfile", "POST update profile", PrototypeRoles.Customer),
            new("/Customer/Menu", "GET", PrototypeRoles.Customer),
            new("/Customer/Menu", "POST add to cart", PrototypeRoles.Customer),
            new("/Customer/Cart", "GET", PrototypeRoles.Customer),
            new("/Customer/Cart", "POST remove item", PrototypeRoles.Customer),
            new("/Customer/Cart", "POST place order", PrototypeRoles.Customer),
            new("/Customer/RecommendAllergen", "POST recommendation", PrototypeRoles.Customer),
            new("/Customer/RecommendDietaryRestriction", "POST recommendation", PrototypeRoles.Customer),
            new("/Staff/FrontOfHouseOrders", "GET", PrototypeRoles.FrontOfHouse),
            new("/Staff/FrontOfHouseOrders", "POST send to kitchen", PrototypeRoles.FrontOfHouse),
            new("/Staff/FrontOfHouseOrders", "POST cancel", PrototypeRoles.FrontOfHouse),
            new("/Staff/KitchenOrders", "GET", PrototypeRoles.Kitchen),
            new("/Staff/KitchenOrders", "POST acknowledge allergy", PrototypeRoles.Kitchen),
            new("/Staff/KitchenOrders", "POST remove ingredient", PrototypeRoles.Kitchen),
            new("/Staff/KitchenOrders", "POST remove order item", PrototypeRoles.Kitchen),
            new("/Staff/KitchenOrders", "POST start preparation", PrototypeRoles.Kitchen),
            new("/Staff/KitchenOrders", "POST complete order", PrototypeRoles.Kitchen),
            new("/Admin/QualityDashboard", "GET", PrototypeRoles.Admin),
            new("/Admin/MenuAllergens", "GET", PrototypeRoles.Admin),
            new("/Admin/MenuAllergens", "POST add", PrototypeRoles.Admin),
            new("/Admin/MenuAllergens", "POST update", PrototypeRoles.Admin),
            new("/Admin/MenuAllergens", "POST remove", PrototypeRoles.Admin),
            new("/Admin/AllergenRecommendations", "GET", PrototypeRoles.Admin),
            new("/Admin/AllergenRecommendations", "POST approve allergen", PrototypeRoles.Admin),
            new("/Admin/AllergenRecommendations", "POST reject allergen", PrototypeRoles.Admin),
            new("/Admin/AllergenRecommendations", "POST approve dietary restriction", PrototypeRoles.Admin),
            new("/Admin/AllergenRecommendations", "POST reject dietary restriction", PrototypeRoles.Admin),
            new("/Admin/AuditHistory", "GET", PrototypeRoles.Admin)
        };

        [TestMethod]
        public async Task ProtectedPagesAndActions_OnlyAllowTheirConfiguredRole()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddRazorPages(PrototypePageAuthorization.Configure);
            services.AddAuthorization(PrototypeRoles.ConfigurePolicies);
            using var provider = services.BuildServiceProvider();
            var authorization = provider.GetRequiredService<IAuthorizationService>();
            var options = provider.GetRequiredService<IOptions<RazorPagesOptions>>().Value;

            foreach (var action in ProtectedActions)
            {
                var policyName = GetConfiguredPolicy(options, action.Page);

                foreach (var role in PrototypeRoles.All)
                {
                    var principal = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.Role, role) },
                        "Test"));
                    var result = await authorization.AuthorizeAsync(principal, policyName);

                    Assert.AreEqual(
                        role == action.RequiredRole,
                        result.Succeeded,
                        $"{role} access to {action.Page} ({action.Action}) must be " +
                        $"{(role == action.RequiredRole ? "allowed" : "denied")}.");
                }

                var anonymousPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
                var anonymousResult = await authorization.AuthorizeAsync(
                    anonymousPrincipal,
                    policyName);

                Assert.IsFalse(
                    anonymousResult.Succeeded,
                    $"Anonymous access to {action.Page} ({action.Action}) must be denied.");
            }
        }

        private static string GetConfiguredPolicy(
            RazorPagesOptions options,
            string pagePath)
        {
            var pageActionDescriptor = new PageActionDescriptor
            {
                RelativePath = $"/Pages{pagePath}.cshtml",
                ViewEnginePath = pagePath
            };
            var pageModel = new PageApplicationModel(
                pageActionDescriptor,
                typeof(PageModel).GetTypeInfo(),
                Array.Empty<object>());

            foreach (var convention in options.Conventions.OfType<IPageApplicationModelConvention>())
            {
                convention.Apply(pageModel);
            }

            var policyName = pageModel.Filters
                .OfType<AuthorizeFilter>()
                .SelectMany(filter => filter.AuthorizeData)
                .Select(data => data.Policy)
                .SingleOrDefault(policy => !string.IsNullOrWhiteSpace(policy));

            Assert.IsNotNull(policyName, $"No authorization policy configured for {pagePath}.");
            return policyName;
        }
    }
}
