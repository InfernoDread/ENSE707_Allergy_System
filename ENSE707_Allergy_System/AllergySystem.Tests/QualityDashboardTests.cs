using System.Diagnostics;
using AllergySystem.Models;
using AllergySystem.Pages.Admin;
using AllergySystem.Services;

namespace AllergySystem.Tests
{
    [TestClass]
    public class QualityDashboardTests
    {
        public TestContext TestContext { get; set; } = null!;

        private static QualityDashboardModel CreateDashboard(
            InMemoryOrderStore orderStore,
            InMemoryAuditStore auditStore)
        {
            var allergenCatalog = new AllergenCatalogService();
            var menuCatalog = new MenuCatalogService(
                allergenCatalog,
                new MenuAllergenMappingStore());

            return new QualityDashboardModel(orderStore, menuCatalog, auditStore);
        }

        [TestMethod]
        public void OnGet_SummarizesCurrentOrdersCatalogueAndRecentAudit()
        {
            var orderStore = new InMemoryOrderStore();
            var auditStore = new InMemoryAuditStore();
            var statuses = Enum.GetValues<OrderStatus>();

            foreach (var status in statuses)
            {
                orderStore.SaveOrder(new Order
                {
                    CustomerId = 1,
                    CreatedAt = DateTime.UtcNow,
                    Status = status
                });
            }

            var now = DateTime.UtcNow;
            for (var index = 0; index < 12; index++)
            {
                auditStore.Add(new AuditEntry
                {
                    TimestampUtc = now.AddMinutes(index),
                    ActorRole = PrototypeRoles.Admin,
                    Action = "DashboardTest",
                    EntityType = "Order",
                    EntityId = index + 1,
                    Details = $"Entry {index + 1}"
                });
            }

            var dashboard = CreateDashboard(orderStore, auditStore);

            dashboard.OnGet();

            Assert.AreEqual(6, dashboard.TotalOrderCount);
            Assert.AreEqual(4, dashboard.ActiveOrderCount);
            Assert.HasCount(6, dashboard.OrderStatuses);
            Assert.AreEqual(5, dashboard.MenuItemCount);
            Assert.AreEqual(16, dashboard.IngredientCount);
            Assert.AreEqual(7, dashboard.IngredientsWithAllergensCount);
            Assert.AreEqual(12, dashboard.AllergenAssociationCount);
            Assert.HasCount(10, dashboard.RecentAuditEntries);
            Assert.AreEqual(12, dashboard.RecentAuditEntries[0].Id);
            Assert.AreEqual(3, dashboard.RecentAuditEntries[^1].Id);
        }

        [TestMethod]
        public void OnGet_WithEmptyStores_ReturnsZeroOperationalCounts()
        {
            var dashboard = CreateDashboard(new InMemoryOrderStore(), new InMemoryAuditStore());

            dashboard.OnGet();

            Assert.AreEqual(0, dashboard.TotalOrderCount);
            Assert.AreEqual(0, dashboard.ActiveOrderCount);
            Assert.IsTrue(dashboard.OrderStatuses.All(status => status.Count == 0));
            Assert.IsEmpty(dashboard.RecentAuditEntries);
        }

        [TestMethod]
        public void OnGet_WithThreeHundredOrders_CompletesWithinFiveSeconds()
        {
            var orderStore = new InMemoryOrderStore();
            var auditStore = new InMemoryAuditStore();
            var statuses = Enum.GetValues<OrderStatus>();
            var now = DateTime.UtcNow;

            for (var index = 0; index < 300; index++)
            {
                orderStore.SaveOrder(new Order
                {
                    CustomerId = index % 2 + 1,
                    CreatedAt = now.AddSeconds(-index),
                    Status = statuses[index % statuses.Length]
                });
                auditStore.Add(new AuditEntry
                {
                    TimestampUtc = now.AddSeconds(-index),
                    ActorRole = "TestHarness",
                    Action = "SeedOrder",
                    EntityType = "Order",
                    EntityId = index + 1,
                    Details = "Isolated NFR3 fixture"
                });
            }

            var dashboard = CreateDashboard(orderStore, auditStore);
            var stopwatch = Stopwatch.StartNew();

            dashboard.OnGet();

            stopwatch.Stop();
            TestContext.WriteLine(
                $"NFR3 isolated fixture: 300 orders and 300 audit entries; PageModel load {stopwatch.Elapsed.TotalMilliseconds:F2} ms.");

            Assert.AreEqual(300, dashboard.TotalOrderCount);
            Assert.AreEqual(300, dashboard.OrderStatuses.Sum(status => status.Count));
            Assert.HasCount(10, dashboard.RecentAuditEntries);
            Assert.IsTrue(
                stopwatch.Elapsed <= TimeSpan.FromSeconds(5),
                $"Dashboard PageModel took {stopwatch.Elapsed.TotalMilliseconds:F2} ms; NFR3 limit is 5000 ms.");
        }
    }
}