using AllergySystem.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AllergySystem.Tests
{
    [TestClass]
    public class PrototypeAuthenticationServiceTests
    {
        private static IConfiguration CreateConfiguration(string role = PrototypeRoles.Customer)
        {
            var settings = new Dictionary<string, string?>
            {
                ["PrototypeAuthentication:DemoUsers:0:UserId"] = "1",
                ["PrototypeAuthentication:DemoUsers:0:Username"] = "customer",
                ["PrototypeAuthentication:DemoUsers:0:Password"] = "customer-demo",
                ["PrototypeAuthentication:DemoUsers:0:Role"] = role
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        [TestMethod]
        public void Authenticate_ValidDevelopmentCredentials_ReturnsIdentity()
        {
            var service = new PrototypeAuthenticationService(CreateConfiguration(), demoAccountsEnabled: true);

            var identity = service.Authenticate(" Customer ", "customer-demo");

            Assert.IsNotNull(identity);
            Assert.AreEqual("1", identity.UserId);
            Assert.AreEqual("customer", identity.Username);
            Assert.AreEqual(PrototypeRoles.Customer, identity.Role);
        }

        [TestMethod]
        public void Authenticate_InvalidPassword_ReturnsNull()
        {
            var service = new PrototypeAuthenticationService(CreateConfiguration(), demoAccountsEnabled: true);

            Assert.IsNull(service.Authenticate("customer", "wrong-password"));
        }

        [TestMethod]
        public void Authenticate_OutsideDevelopment_ReturnsNull()
        {
            var service = new PrototypeAuthenticationService(CreateConfiguration(), demoAccountsEnabled: false);

            Assert.IsNull(service.Authenticate("customer", "customer-demo"));
        }

        [TestMethod]
        public void Authenticate_UnsupportedRole_ReturnsNull()
        {
            var service = new PrototypeAuthenticationService(CreateConfiguration("Administrator"), demoAccountsEnabled: true);

            Assert.IsNull(service.Authenticate("customer", "customer-demo"));
        }

        [TestMethod]
        public async Task RolePolicies_OnlyAllowTheMatchingAuthenticatedRole()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthorization(PrototypeRoles.ConfigurePolicies);
            using var provider = services.BuildServiceProvider();
            var authorization = provider.GetRequiredService<IAuthorizationService>();

            foreach (var requiredRole in PrototypeRoles.All)
            {
                foreach (var candidateRole in PrototypeRoles.All)
                {
                    var identity = new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.Role, candidateRole) },
                        "Test");
                    var principal = new ClaimsPrincipal(identity);
                    var result = await authorization.AuthorizeAsync(principal, requiredRole);

                    Assert.AreEqual(
                        requiredRole == candidateRole,
                        result.Succeeded,
                        $"Role {candidateRole} should {(requiredRole == candidateRole ? "pass" : "fail")} policy {requiredRole}.");
                }

                var unauthenticatedPrincipal = new ClaimsPrincipal(
                    new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, requiredRole) }));
                var unauthenticatedResult = await authorization.AuthorizeAsync(
                    unauthenticatedPrincipal,
                    requiredRole);

                Assert.IsFalse(unauthenticatedResult.Succeeded);
            }
        }

        [TestMethod]
        public void CurrentUserContext_UsesAuthenticatedCustomerIdentifier()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "2"),
                    new Claim(ClaimTypes.Role, PrototypeRoles.Customer)
                },
                "Test"));
            var accessor = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            var currentUser = new CurrentUserContext(accessor);

            Assert.AreEqual(2, currentUser.CustomerId);
        }

        [TestMethod]
        public void CurrentUserContext_RejectsNonCustomerIdentity()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "2"),
                    new Claim(ClaimTypes.Role, PrototypeRoles.Admin)
                },
                "Test"));
            var accessor = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            var currentUser = new CurrentUserContext(accessor);

            Assert.ThrowsExactly<UnauthorizedAccessException>(() => _ = currentUser.CustomerId);
        }
    }
}