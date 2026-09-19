using AllergySystem.Models;
using AllergySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AllergySystem.Pages.Staff
{
    public class FrontOfHouseOrdersModel : PageModel
    {
        private readonly FrontOfHouseOrderService _orderService;

        public FrontOfHouseOrdersModel(FrontOfHouseOrderService orderService)
        {
            _orderService = orderService;
        }

        public List<Order> ActiveOrders { get; private set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
            LoadActiveOrders();
        }

        public IActionResult OnPostSendToKitchen(int orderId)
        {
            try
            {
                _orderService.SendToKitchen(orderId);
                SuccessMessage = $"Order #{orderId} was sent to the kitchen.";
            }
            catch (ArgumentException)
            {
                ErrorMessage = $"Order #{orderId} could not be found.";
            }
            catch (InvalidOperationException exception)
            {
                ErrorMessage = exception.Message;
            }

            return RedirectToPage();
        }

        public IActionResult OnPostCancel(int orderId)
        {
            try
            {
                _orderService.CancelOrder(orderId);
                SuccessMessage = $"Order #{orderId} was cancelled.";
            }
            catch (ArgumentException)
            {
                ErrorMessage = $"Order #{orderId} could not be found.";
            }
            catch (InvalidOperationException exception)
            {
                ErrorMessage = exception.Message;
            }

            return RedirectToPage();
        }

        private void LoadActiveOrders()
        {
            ActiveOrders = _orderService.GetActiveOrders();
        }
    }
}
