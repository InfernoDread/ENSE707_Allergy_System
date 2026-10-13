using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace AllergySystem.Services
{
    public static class PrototypeRoles
    {
        public const string Customer = "Customer";
        public const string FrontOfHouse = "FrontOfHouse";
        public const string Kitchen = "Kitchen";
        public const string Admin = "Admin";

        public static readonly string[] All =
        {
            Customer,
            FrontOfHouse,
            Kitchen,
            Admin
        };

        public static void ConfigurePolicies(AuthorizationOptions options)
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            foreach (var role in All)
            {
                options.AddPolicy(role, policy =>
                    policy.RequireAuthenticatedUser().RequireRole(role));
            }
        }
    }

    public sealed record PrototypeAuthenticatedUser(string UserId, string Username, string Role);

    public sealed class PrototypeAuthenticationService
    {
        private readonly IConfiguration _configuration;
        private readonly bool _demoAccountsEnabled;

        public PrototypeAuthenticationService(IConfiguration configuration, bool demoAccountsEnabled)
        {
            _configuration = configuration;
            _demoAccountsEnabled = demoAccountsEnabled;
        }

        public bool DemoAccountsEnabled => _demoAccountsEnabled;

        public PrototypeAuthenticatedUser? Authenticate(string? username, string? password)
        {
            if (!_demoAccountsEnabled ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrEmpty(password))
            {
                return null;
            }

            var account = _configuration
                .GetSection("PrototypeAuthentication:DemoUsers")
                .Get<List<PrototypeDemoAccount>>()?
                .FirstOrDefault(candidate =>
                    candidate.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase));

            if (account == null ||
                string.IsNullOrWhiteSpace(account.UserId) ||
                string.IsNullOrWhiteSpace(account.Username) ||
                string.IsNullOrWhiteSpace(account.Password) ||
                !PrototypeRoles.All.Contains(account.Role, StringComparer.Ordinal))
            {
                return null;
            }

            var expectedPassword = Encoding.UTF8.GetBytes(account.Password);
            var suppliedPassword = Encoding.UTF8.GetBytes(password);

            if (!CryptographicOperations.FixedTimeEquals(expectedPassword, suppliedPassword))
            {
                return null;
            }

            return new PrototypeAuthenticatedUser(account.UserId, account.Username, account.Role);
        }

        private sealed class PrototypeDemoAccount
        {
            public string UserId { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
        }
    }
}