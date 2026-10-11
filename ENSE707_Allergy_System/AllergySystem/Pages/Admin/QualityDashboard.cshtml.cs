using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AllergySystem.Pages.Admin
{
    public class QualityDashboardModel : PageModel
    {
        private const int RecentAuditLimit = 10;

        private readonly InMemoryOrderStore _orderStore;
        private readonly MenuCatalogService _menuCatalogService;
        private readonly InMemoryAuditStore _auditStore;

        public QualityDashboardModel(
            InMemoryOrderStore orderStore,
            MenuCatalogService menuCatalogService,
            InMemoryAuditStore auditStore)
        {
            _orderStore = orderStore;
            _menuCatalogService = menuCatalogService;
            _auditStore = auditStore;
        }

        public int TotalOrderCount { get; private set; }
        public int ActiveOrderCount { get; private set; }
        public int IngredientsWithAllergensCount { get; private set; }
        public int IngredientCount { get; private set; }
        public int AllergenAssociationCount { get; private set; }
        public int MenuItemCount { get; private set; }
        public List<OrderStatusCount> OrderStatuses { get; private set; } = new();
        public List<AuditEntry> RecentAuditEntries { get; private set; } = new();

        public void OnGet()
        {
            var orders = _orderStore.GetAllOrders();
            TotalOrderCount = orders.Count;
            ActiveOrderCount = orders.Count(order =>
                order.Status != OrderStatus.Completed &&
                order.Status != OrderStatus.Cancelled);
            OrderStatuses = Enum.GetValues<OrderStatus>()
                .Select(status => new OrderStatusCount(
                    status,
                    orders.Count(order => order.Status == status)))
                .ToList();

            var menuItems = _menuCatalogService.GetMenuItems();
            var ingredients = menuItems.SelectMany(item => item.Ingredients).ToList();
            MenuItemCount = menuItems.Count;
            IngredientCount = ingredients.Count;
            IngredientsWithAllergensCount = ingredients.Count(ingredient => ingredient.Allergens.Count > 0);
            AllergenAssociationCount = ingredients.Sum(ingredient => ingredient.Allergens.Count);

            RecentAuditEntries = _auditStore.GetAll()
                .Take(RecentAuditLimit)
                .ToList();
        }

        public sealed record OrderStatusCount(OrderStatus Status, int Count);
    }
}