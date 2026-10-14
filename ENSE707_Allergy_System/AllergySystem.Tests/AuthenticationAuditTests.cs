using System.Security.Claims;
using AllergySystem.Pages.Account;
using AllergySystem.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AllergySystem.Tests
{
    [TestClass]
    public class AuthenticationAuditTests
    {
        private static (ServiceProvider Services, DefaultHttpContext HttpContext, PageContext PageContext) CreatePageContext(
            ClaimsPrincipal? user = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection();
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie();
            var provider = services.BuildServiceProvider();

            var httpContext = new DefaultHttpContext
            {
                RequestServices = provider,
                User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
            };
            httpContext.Request.Scheme = "https";

            var pageContext = new PageContext(
                new ActionContext(httpContext, new RouteData(), new PageActionDescriptor()));

            return (provider, httpContext, pageContext);
        }

        private static PrototypeAuthenticationService CreateAuthenticationService(bool demoAccountsEnabled = true)
        {
            var settings = new Dictionary<string, string?>
            {
                ["PrototypeAuthentication:DemoUsers:0:UserId"] = "12",
                ["PrototypeAuthentication:DemoUsers:0:Username"] = "customer",
                ["PrototypeAuthentication:DemoUsers:0:Password"] = "customer-demo",
                ["PrototypeAuthentication:DemoUsers:0:Role"] = PrototypeRoles.Customer
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            return new PrototypeAuthenticationService(configuration, demoAccountsEnabled);
        }

        [TestMethod]
        public async Task Login_WithValidCredentials_RecordsSuccessfulLogin()
        {
            var context = CreatePageContext();
            using var services = context.Services;
            var auditStore = new InMemoryAuditStore();
            var model = new LoginModel(
                CreateAuthenticationService(),
                new AuditService(auditStore))
            {
                PageContext = context.PageContext,
                Url = new UrlHelper(context.PageContext),
                Username = "customer",
                Password = "customer-demo"
            };

            await model.OnPostAsync();

            var entry = auditStore.GetAll().Single();
            Assert.AreEqual(PrototypeRoles.Customer, entry.ActorRole);
            Assert.AreEqual("LoginSucceeded", entry.Action);
            Assert.AreEqual("Authentication", entry.EntityType);
            Assert.AreEqual(12, entry.EntityId);
            StringAssert.Contains(entry.Details, "Username=customer");
        }

        [TestMethod]
        public async Task Login_WithInvalidCredentials_RecordsFailedLogin()
        {
            var context = CreatePageContext();
            using var services = context.Services;
            var auditStore = new InMemoryAuditStore();
            var model = new LoginModel(
                CreateAuthenticationService(),
                new AuditService(auditStore))
            {
                PageContext = context.PageContext,
                Username = "unknown",
                Password = "wrong-password"
            };

            await model.OnPostAsync();

            var entry = auditStore.GetAll().Single();
            Assert.AreEqual("Unauthenticated", entry.ActorRole);
            Assert.AreEqual("LoginFailed", entry.Action);
            Assert.AreEqual("Authentication", entry.EntityType);
            Assert.IsNull(entry.EntityId);
            StringAssert.Contains(entry.Details, "AttemptedUsername=unknown");
        }

        [TestMethod]
        public async Task Logout_RecordsAuthenticatedUser()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "23"),
                    new Claim(ClaimTypes.Name, "staff"),
                    new Claim(ClaimTypes.Role, PrototypeRoles.Admin)
                },
                CookieAuthenticationDefaults.AuthenticationScheme));
            var context = CreatePageContext(user);
            using var services = context.Services;
            var auditStore = new InMemoryAuditStore();
            var model = new LogoutModel(new AuditService(auditStore))
            {
                PageContext = context.PageContext
            };

            await model.OnPostAsync();

            var entry = auditStore.GetAll().Single();
            Assert.AreEqual(PrototypeRoles.Admin, entry.ActorRole);
            Assert.AreEqual("Logout", entry.Action);
            Assert.AreEqual("Authentication", entry.EntityType);
            Assert.AreEqual(23, entry.EntityId);
            StringAssert.Contains(entry.Details, "Username=staff");
        }
    }
}
