using System;
using System.Collections.Generic;

namespace AllergySystem.Models
{
    // Minimal Order model for prototype: single MenuItem per order
    public enum OrderStatus
    {
        Pending,
        PendingAllergyConfirmation,
        InPreparation,
        Completed,
        Cancelled
    }

    public class Order
    {
        public int Id { get; set; }

        public int CustomerId { get; set; }

        public MenuItem MenuItem { get; set; } = new();

        public DateTime CreatedAt { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        public List<Allergen> ConflictingAllergens { get; set; } = new();
    }
}
