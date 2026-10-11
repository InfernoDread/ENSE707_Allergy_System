using System.Security.Claims;
using AllergySystem.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AllergySystem.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly AuditService _auditService;

        public LogoutModel(AuditService auditService)
        {
            _auditService = auditService;
        }

        public IActionResult OnGet()
        {
            return RedirectToPage("/Index");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            _auditService.Record(
                User.FindFirstValue(ClaimTypes.Role) ?? "Unauthenticated",
                "Logout",
                "Authentication",
                int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null,
                $"Username={User.Identity?.Name ?? "(unknown)"}");

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Account/Login");
        }
    }
}