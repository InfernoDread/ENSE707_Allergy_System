using System;
using System.Linq;
using System.Security.Claims;
using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AllergySystem.Tests
{
    [TestClass]
    public class AuditIdentityTests
    {
        // Reused helper to create order-related services (copied small subset pattern used elsewhere).
        private static (OrderService orderService, InMemoryOrderStore orderStore, CartService cartService, InMemoryAllergyProfileStore profileStore) CreateOrderService()
        {
            var orderStore = new InMemoryOrderStore();
            var cartStore = new InMemoryCartStore();
            var profileStore = new InMemoryAllergyProfileStore();

            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(allergenCatalog);
            var validationService = new AllergyValidationService();

            var cartService = new CartService(
                cartStore,
                menuCatalog,
                profileStore,
                validationService);

            var dietaryService = new DietaryCompatibilityService();
            var orderService = new OrderService(
                orderStore,
                cartService,
                profileStore,
                validationService,
                dietaryService);

            return (orderService, orderStore, cartService, profileStore);
        }

        [TestMethod]
        public void KitchenOrderService_RecordsAuthenticatedKitchenUserActor()
        {
            // Arrange: prepare an order in PendingAllergyConfirmation with conflict
            var (orderService, orderStore, cartService, profileStore) = CreateOrderService();
            var order = new Order
            {
                CustomerId = 200,
                Status = OrderStatus.PendingAllergyConfirmation,
                ConflictingAllergens = new System.Collections.Generic.List<Allergen> { new Allergen { Id = 1, Name = "Peanuts" } }
            };
            orderStore.SaveOrder(order);

            var ctx = new DefaultHttpContext();
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "kitchen-001"),
                new Claim(ClaimTypes.Name, "chef.anne"),
                new Claim(ClaimTypes.Role, PrototypeRoles.Kitchen)
            }, CookieAuthenticationDefaults.AuthenticationScheme));

            var accessor = new HttpContextAccessor { HttpContext = ctx };
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore, accessor);

            var kitchen = new KitchenOrderService(orderStore, profileStore, new AllergyValidationService(), auditService);

            // Act
            kitchen.AcknowledgeAllergy(order.Id);

            // Assert: find specific entry by action and entity
            var entry = auditStore.GetAll().Single(e => e.Action == "AcknowledgeAllergy" && e.EntityType == "Order" && e.EntityId == order.Id);
            Assert.AreEqual(PrototypeRoles.Kitchen, entry.ActorRole);
            Assert.AreEqual("kitchen-001", entry.ActorId);
            Assert.AreEqual("chef.anne", entry.ActorUsername);
            // Verify subject (order) still references the original customer
            var persisted = orderStore.GetOrder(order.Id);
            Assert.IsNotNull(persisted);
            Assert.AreEqual(200, persisted!.CustomerId);
        }

        [TestMethod]
        public void FrontOfHouse_SendToKitchen_RecordsAuthenticatedFOHUserActor()
        {
            // Arrange: create a safe order using existing order creation helpers
            var (orderService, orderStore, cartService, profileStore) = CreateOrderService();
            var customerId = 300;
            var profile = profileStore.GetProfile(customerId);
            profile.Allergens.Clear();
            profileStore.SaveProfile(profile);
            cartService.AddItem(customerId, 5);
            var created = orderService.CreateOrderFromCart(customerId);

            var ctx = new DefaultHttpContext();
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "foh-42"),
                new Claim(ClaimTypes.Name, "foh.jim"),
                new Claim(ClaimTypes.Role, PrototypeRoles.FrontOfHouse)
            }, CookieAuthenticationDefaults.AuthenticationScheme));

            var accessor = new HttpContextAccessor { HttpContext = ctx };
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore, accessor);

            var foh = new FrontOfHouseOrderService(orderService, auditService);

            // Act
            foh.SendToKitchen(created.Id);

            // Assert: find entry for SendToKitchen
            var entry = auditStore.GetAll().Single(e => e.Action == "SendToKitchen" && e.EntityType == "Order" && e.EntityId == created.Id);
            Assert.AreEqual(PrototypeRoles.FrontOfHouse, entry.ActorRole);
            Assert.AreEqual("foh-42", entry.ActorId);
            Assert.AreEqual("foh.jim", entry.ActorUsername);
            // Ensure the order's customer remains the original subject
            var persisted = orderStore.GetOrder(created.Id);
            Assert.IsNotNull(persisted);
            Assert.AreEqual(customerId, persisted!.CustomerId);
        }

        [TestMethod]
        public void UnauthenticatedPrincipalWithNameClaims_DoesNotPopulateActorFields()
        {
            // Arrange: principal contains name/id claims but is NOT authenticated
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "maybe-123"),
                new Claim(ClaimTypes.Name, "ghost"),
                new Claim(ClaimTypes.Role, PrototypeRoles.Customer)
            };
            // Authentication type null => IsAuthenticated == false
            var identity = new ClaimsIdentity(claims, authenticationType: null);
            var principal = new ClaimsPrincipal(identity);
            var ctx = new DefaultHttpContext { User = principal };

            var accessor = new HttpContextAccessor { HttpContext = ctx };
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore, accessor);

            // Act
            auditService.Record(PrototypeRoles.Customer, "DietaryWarning", "Cart", 123, "Details");

            // Assert
            var entry = auditStore.GetAll().Single(e => e.Action == "DietaryWarning");
            Assert.AreEqual(PrototypeRoles.Customer, entry.ActorRole);
            Assert.IsNull(entry.ActorId);
            Assert.IsNull(entry.ActorUsername);
        }

        [TestMethod]
        public void NoAccessor_RecordingSucceedsWithNullActorFields()
        {
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore); // no accessor provided

            auditService.Record(PrototypeRoles.Admin, "SomeAction", "Entity", 1, "Details");

            var entry = auditStore.GetAll().Single(e => e.Action == "SomeAction");
            Assert.AreEqual(PrototypeRoles.Admin, entry.ActorRole);
            Assert.IsNull(entry.ActorId);
            Assert.IsNull(entry.ActorUsername);
        }

        [TestMethod]
        public void AccessorExistsButHttpContextIsNull_RecordingSucceedsWithNullActorFields()
        {
            var accessor = new HttpContextAccessor { HttpContext = null }; // accessor exists but no HttpContext
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore, accessor);

            auditService.Record(PrototypeRoles.Admin, "NoContextAction", "Entity", 2, "Details");

            var entry = auditStore.GetAll().Single(e => e.Action == "NoContextAction");
            Assert.AreEqual(PrototypeRoles.Admin, entry.ActorRole);
            Assert.IsNull(entry.ActorId);
            Assert.IsNull(entry.ActorUsername);
        }

        [TestMethod]
        public void SharedAuditService_MultipleContextsProduceCorrectActorPerEvent()
        {
            var accessor = new HttpContextAccessor();
            var auditStore = new InMemoryAuditStore();
            var auditService = new AuditService(auditStore, accessor);

            // Context A
            var ctxA = new DefaultHttpContext();
            ctxA.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "A-1"),
                new Claim(ClaimTypes.Name, "alice"),
                new Claim(ClaimTypes.Role, PrototypeRoles.Admin)
            }, CookieAuthenticationDefaults.AuthenticationScheme));
            accessor.HttpContext = ctxA;
            auditService.Record(PrototypeRoles.Admin, "ActionA", "T", 10, "A");

            // Context B
            var ctxB = new DefaultHttpContext();
            ctxB.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "B-2"),
                new Claim(ClaimTypes.Name, "bob"),
                new Claim(ClaimTypes.Role, PrototypeRoles.Admin)
            }, CookieAuthenticationDefaults.AuthenticationScheme));
            accessor.HttpContext = ctxB;
            auditService.Record(PrototypeRoles.Admin, "ActionB", "T", 20, "B");

            // Anonymous context
            accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
            auditService.Record(PrototypeRoles.Admin, "ActionAnon", "T", 30, "Anon");

            var a = auditStore.GetAll().Single(e => e.Action == "ActionA");
            var b = auditStore.GetAll().Single(e => e.Action == "ActionB");
            var anon = auditStore.GetAll().Single(e => e.Action == "ActionAnon");

            Assert.AreEqual("A-1", a.ActorId);
            Assert.AreEqual("alice", a.ActorUsername);
            Assert.AreEqual("B-2", b.ActorId);
            Assert.AreEqual("bob", b.ActorUsername);
            Assert.IsNull(anon.ActorId);
            Assert.IsNull(anon.ActorUsername);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task LoginAndLogout_AreAttributedToAuthenticatedAccount()
        {
            // Helpers duplicated from AuthenticationAuditTests
            static (ServiceProvider Services, DefaultHttpContext HttpContext, Microsoft.AspNetCore.Mvc.RazorPages.PageContext PageContext) CreatePageContext(ClaimsPrincipal? user = null)
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

                var pageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext(
                    new Microsoft.AspNetCore.Mvc.ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new PageActionDescriptor()));

                return (provider, httpContext, pageContext);
            }

            static PrototypeAuthenticationService CreateAuthenticationService(bool demoAccountsEnabled = true)
            {
                var settings = new System.Collections.Generic.Dictionary<string, string?>
                {
                    ["PrototypeAuthentication:DemoUsers:0:UserId"] = "12",
                    ["PrototypeAuthentication:DemoUsers:0:Username"] = "customer",
                    ["PrototypeAuthentication:DemoUsers:0:Password"] = "customer-demo",
                    ["PrototypeAuthentication:DemoUsers:0:Role"] = PrototypeRoles.Customer
                };
                var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                    .AddInMemoryCollection(settings)
                    .Build();

                return new PrototypeAuthenticationService(configuration, demoAccountsEnabled);
            }

            // Arrange login: start anonymous
            var ctx = CreatePageContext();
            using var services = ctx.Services;
            var auditStore = new InMemoryAuditStore();
            var accessor = new HttpContextAccessor { HttpContext = ctx.HttpContext };
            var loginModel = new Pages.Account.LoginModel(
                CreateAuthenticationService(),
                new AuditService(auditStore, accessor))
            {
                PageContext = ctx.PageContext,
                Url = new Microsoft.AspNetCore.Mvc.Routing.UrlHelper(ctx.PageContext),
                Username = "customer",
                Password = "customer-demo"
            };

            // Act: perform actual login handler starting from anonymous principal
            await loginModel.OnPostAsync();

            // Assert login recorded and actor identity populated
            var loginEntry = auditStore.GetAll().Single(e => e.Action == "LoginSucceeded");
            Assert.AreEqual(PrototypeRoles.Customer, loginEntry.ActorRole);
            Assert.AreEqual("12", loginEntry.ActorId);
            Assert.AreEqual("customer", loginEntry.ActorUsername);

            // Arrange logout: create authenticated principal on context and call Logout handler
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "23"),
                new Claim(ClaimTypes.Name, "staff"),
                new Claim(ClaimTypes.Role, PrototypeRoles.Admin)
            }, CookieAuthenticationDefaults.AuthenticationScheme));
            var ctx2 = CreatePageContext(user);
            using var services2 = ctx2.Services;
            var accessor2 = new HttpContextAccessor { HttpContext = ctx2.HttpContext };
            var logoutModel = new Pages.Account.LogoutModel(new AuditService(auditStore, accessor2))
            {
                PageContext = ctx2.PageContext
            };

            await logoutModel.OnPostAsync();

            var logoutEntry = auditStore.GetAll().Single(e => e.Action == "Logout");
            Assert.AreEqual(PrototypeRoles.Admin, logoutEntry.ActorRole);
            Assert.AreEqual(23, logoutEntry.EntityId);
            Assert.AreEqual("23", logoutEntry.ActorId);
            Assert.AreEqual("staff", logoutEntry.ActorUsername);
        }
    }
}
