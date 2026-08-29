using System;
using System.Collections.Generic;
using System.Linq;
using AllergySystem.Models;

namespace AllergySystem.Services
{
    // Simple in-memory order store for prototype
    public class InMemoryOrderStore
    {
        private readonly Dictionary<int, Order> _orders = new();
        private int _nextId = 1;

        public Order SaveOrder(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);

            if (order.Id == 0)
            {
                order.Id = _nextId++;
            }

            // store the supplied Order instance directly (simple prototype semantics)
            _orders[order.Id] = order;
            return order;
        }

        public Order? GetOrder(int id)
        {
            _orders.TryGetValue(id, out var order);
            return order;
        }

        public List<Order> GetOrdersForCustomer(int customerId)
        {
            return _orders.Values.Where(o => o.CustomerId == customerId).ToList();
        }

        public List<Order> GetAllOrders()
        {
            return _orders.Values.ToList();
        }
    }
}
