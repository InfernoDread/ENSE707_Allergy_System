using System;
using System.Collections.Generic;
using System.Linq;
using AllergySystem.Models;

namespace AllergySystem.Services
{
    // This service stores customer orders in memory while the application is running.
    // It provides methods to save, retrieve, and list orders.
    public class InMemoryOrderStore
    {
        private readonly Dictionary<int, Order> _orders = new();
        private int _nextId = 1;

        // Saves an order and assigns a new ID if the order has not been saved before.
        public Order SaveOrder(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);

            if (order.Id == 0)
            {
                order.Id = _nextId++;
            }

            // Stores the supplied order instance using its ID as the key.
            _orders[order.Id] = order;
            return order;
        }

        // Retrieves an order by its ID, or returns null if no matching order exists.
        public Order? GetOrder(int id)
        {
            _orders.TryGetValue(id, out var order);
            return order;
        }

        // Returns all orders belonging to a specific customer.
        public List<Order> GetOrdersForCustomer(int customerId)
        {
            return _orders.Values.Where(o => o.CustomerId == customerId).ToList();
        }

        // Returns all orders currently stored in memory.
        public List<Order> GetAllOrders()
        {
            return _orders.Values.ToList();
        }

        // Returns orders that still require operational handling.
        public List<Order> GetActiveOrders()
        {
            return _orders.Values
                .Where(order =>
                    order.Status != OrderStatus.Completed &&
                    order.Status != OrderStatus.Cancelled)
                .ToList();
        }
    }
}
