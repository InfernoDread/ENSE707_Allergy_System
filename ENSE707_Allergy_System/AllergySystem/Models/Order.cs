using System;
using System.Collections.Generic;

namespace AllergySystem.Models
{
    // Defines the possible status values an order can have during the ordering process.
    public enum OrderStatus
    {
        Pending,
        PendingAllergyConfirmation,
        ReadyForKitchen,
        InPreparation,
        Completed,
        Cancelled
    }

    // This class represents a customer order in the allergy system.
    // It stores the selected menu items, order status, creation time, and any conflicting allergens.
    public class Order
    {
        public int Id { get; set; }

        public int CustomerId { get; set; }

        public List<CartItem> Items { get; set; } = new();

        public DateTime CreatedAt { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        public List<Allergen> ConflictingAllergens { get; set; } = new();
    }
}
