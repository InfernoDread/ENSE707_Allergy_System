using AllergySystem.Models;

namespace AllergySystem.Services
{
    // Indicates that an order has dietary advisory warnings
    // that require explicit customer confirmation before proceeding.
    public class DietaryConfirmationRequiredException : InvalidOperationException
    {
        public IReadOnlyList<DietaryRestriction> Warnings { get; }

        public DietaryConfirmationRequiredException(
            IEnumerable<DietaryRestriction> warnings)
            : base(
                "This order has dietary compatibility warnings that require explicit confirmation before proceeding.")
        {
            Warnings = warnings
                .Select(restriction => new DietaryRestriction
                {
                    Id = restriction.Id,
                    Name = restriction.Name
                })
                .ToList();
        }
    }
}