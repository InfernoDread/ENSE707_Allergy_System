using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace AllergySystem.Services
{
    public sealed class CurrentUserContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int CustomerId
        {
            get
            {
                var principal = _httpContextAccessor.HttpContext?.User;
                var customerIdClaim = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

                if (principal?.Identity?.IsAuthenticated != true ||
                    !principal.IsInRole(PrototypeRoles.Customer) ||
                    !int.TryParse(customerIdClaim, out var customerId) ||
                    customerId <= 0)
                {
                    throw new UnauthorizedAccessException("An authenticated customer identity is required.");
                }

                return customerId;
            }
        }
    }
}