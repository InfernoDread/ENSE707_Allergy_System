using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AllergySystem.Pages.Staff
{
    public class KitchenOrdersModel : PageModel
    {
        private readonly KitchenOrderService _kitchenOrderService;

        public KitchenOrdersModel(KitchenOrderService kitchenOrderService)
        {
            _kitchenOrderService = kitchenOrderService;
        }

        public List<Order> KitchenOrders { get; private set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
            KitchenOrders = _kitchenOrderService.GetKitchenOrders();
        }

        public IActionResult OnPostAcknowledgeAllergy(int orderId)
        {
            try
            {
                _kitchenOrderService.AcknowledgeAllergy(orderId);

                SuccessMessage =
                    $"Order #{orderId} allergen warning was acknowledged.";
            }
            catch (ArgumentException)
            {
                ErrorMessage =
                    $"Order #{orderId} could not be found.";
            }
            catch (InvalidOperationException exception)
            {
                ErrorMessage = exception.Message;
            }

            return RedirectToPage();
        }
        public IActionResult OnPostRemoveIngredient(int orderId, int menuItemId, int ingredientId)
        {
            try
            {
                _kitchenOrderService.RemoveIngredient(
                    orderId,
                    menuItemId,
                    ingredientId);

                SuccessMessage =
                    $"The conflicting ingredient was removed from Order #{orderId}.";
            }
            catch (ArgumentException exception)
            {
                ErrorMessage = exception.Message;
            }
            catch (InvalidOperationException exception)
            {
                ErrorMessage = exception.Message;
            }

            return RedirectToPage();
        }
        public IActionResult OnPostRemoveOrderItem(int orderId, int menuItemId)
        {
            try
            {
                _kitchenOrderService.RemoveOrderItem(
                    orderId,
                    menuItemId);

                SuccessMessage =
                    $"The conflicting menu item was removed from Order #{orderId}.";
            }
            catch (ArgumentException exception)
            {
                ErrorMessage = exception.Message;
            }
            catch (InvalidOperationException exception)
            {
                ErrorMessage = exception.Message;
            }

            return RedirectToPage();
        }
        public IActionResult OnPostStartPreparation(int orderId)
        {
            try
            {
                _kitchenOrderService.StartPreparation(orderId);

                SuccessMessage =
                    $"Order #{orderId} has entered preparation.";
            }
            catch (ArgumentException exception)
            {
                ErrorMessage = exception.Message;
            }
            catch (InvalidOperationException exception)
            {
                ErrorMessage = exception.Message;
            }

            return RedirectToPage();
        }
        public IActionResult OnPostCompleteOrder(int orderId)
        {
            try
            {
                _kitchenOrderService.CompleteOrder(orderId);

                SuccessMessage =
                    $"Order #{orderId} has been completed.";
            }
            catch (ArgumentException exception)
            {
                ErrorMessage = exception.Message;
            }
            catch (InvalidOperationException exception)
            {
                ErrorMessage = exception.Message;
            }

            return RedirectToPage();
        }
    }
}