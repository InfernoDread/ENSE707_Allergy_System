using System.Collections.Generic;
using AllergySystem.Models;

namespace AllergySystem.Services
{
    public class FrontOfHouseOrderService
    {
        private readonly OrderService _orderService;

        public FrontOfHouseOrderService(OrderService orderService)
        {
            _orderService = orderService;
        }

        public List<Order> GetActiveOrders()
        {
            return _orderService.GetActiveOrders();
        }

        public void SendToKitchen(int orderId)
        {
            _orderService.SendToKitchen(orderId);
        }

        public void CancelOrder(int orderId)
        {
            _orderService.CancelOrder(orderId);
        }
    }
}
