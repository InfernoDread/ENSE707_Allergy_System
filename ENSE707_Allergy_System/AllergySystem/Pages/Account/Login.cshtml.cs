using System.Security.Claims;
using AllergySystem.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AllergySystem.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly PrototypeAuthenticationService _authenticationService;
        private readonly AuditService _auditService;

        public LoginModel(
            PrototypeAuthenticationService authenticationService,
            AuditService auditService)
        {
            _authenticationService = authenticationService;
            _auditService = auditService;
        }

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public string? ErrorMessage { get; private set; }

        public bool DemoAccountsEnabled { get; private set; }

        public IActionResult OnGet()
        {
            DemoAccountsEnabled = _authenticationService.DemoAccountsEnabled;

            if (User.Identity?.IsAuthenticated == true)
            {
                return LocalRedirect(GetRedirectUrl(User.FindFirstValue(ClaimTypes.Role)));
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            DemoAccountsEnabled = _authenticationService.DemoAccountsEnabled;
            var user = _authenticationService.Authenticate(Username, Password);

            if (user == null)
            {
                _auditService.Record(
                    "Unauthenticated",
                    "LoginFailed",
                    "Authentication",
                    null,
                    $"AttemptedUsername={GetAuditUsername()}");
                ErrorMessage = DemoAccountsEnabled
                    ? "Username or password is incorrect."
                    : "Prototype sign-in is unavailable outside Development.";
                Password = string.Empty;
                return Page();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserId),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Role, user.Role)
            };
            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                    IsPersistent = false
                });

            _auditService.Record(
                user.Role,
                "LoginSucceeded",
                "Authentication",
                int.TryParse(user.UserId, out var userId) ? userId : null,
                $"Username={user.Username}");

            return LocalRedirect(Url.IsLocalUrl(ReturnUrl)
                ? ReturnUrl!
                : GetRedirectUrl(user.Role));
        }

        private string GetRedirectUrl(string? role)
        {
            if (Url.IsLocalUrl(ReturnUrl))
            {
                return ReturnUrl!;
            }

            return role switch
            {
                PrototypeRoles.Customer => "/Customer/Menu",
                PrototypeRoles.FrontOfHouse => "/Staff/FrontOfHouseOrders",
                PrototypeRoles.Kitchen => "/Staff/KitchenOrders",
                PrototypeRoles.Admin => "/Admin/MenuAllergens",
                _ => "/"
            };
        }

        private string GetAuditUsername()
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                return "(blank)";
            }

            var trimmedUsername = Username.Trim();
            return trimmedUsername[..Math.Min(trimmedUsername.Length, 100)];
        }
    }
}