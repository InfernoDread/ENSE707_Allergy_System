using AllergySystem.Models;

namespace AllergySystem.Services
{
    // Checks menu items against customer dietary preferences.
    // Dietary warnings are advisory and do not block orders.
    public class DietaryCompatibilityService
    {
        public List<DietaryRestriction> FindDietaryWarnings(
            MenuItem menuItem,
            List<DietaryRestriction> customerRestrictions)
        {
            ArgumentNullException.ThrowIfNull(menuItem);
            ArgumentNullException.ThrowIfNull(customerRestrictions);

            // Identify which dietary requirements the menu item has been explicitly labelled as meeting.
            var compatibleLabelIds = menuItem.DietaryLabels
                .Select(label => label.Id)
                .ToHashSet();

            // A selected restriction without a matching menu label should generate an advisory warning.
            return customerRestrictions
                .Where(restriction =>
                    !compatibleLabelIds.Contains(restriction.Id))
                .GroupBy(restriction => restriction.Id)
                .Select(group => group.First())
                .ToList();
        }
    }
}