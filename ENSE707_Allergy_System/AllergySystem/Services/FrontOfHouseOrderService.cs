using System.Collections.Generic;
using AllergySystem.Models;

namespace AllergySystem.Services
{
    public class FrontOfHouseOrderService
    {
        private readonly OrderService _orderService;
        private readonly AuditService _auditService;

        public FrontOfHouseOrderService(OrderService orderService, AuditService auditService)
        {
            _orderService = orderService;
            _auditService = auditService;
        }

        public List<Order> GetActiveOrders()
        {
            return _orderService.GetActiveOrders();
        }

        public void SendToKitchen(int orderId)
        {
            _orderService.SendToKitchen(orderId);
            _auditService.Record("FrontOfHouse", "SendToKitchen", "Order", orderId, $"Order sent to kitchen: {orderId}");
        }

        public void CancelOrder(int orderId)
        {
            _orderService.CancelOrder(orderId);
            _auditService.Record("FrontOfHouse", "CancelOrder", "Order", orderId, $"Order cancelled: {orderId}");
        }
    }
}
