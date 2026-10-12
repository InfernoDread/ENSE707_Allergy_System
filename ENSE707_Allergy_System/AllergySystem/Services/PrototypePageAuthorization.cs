using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace AllergySystem.Services
{
    public static class PrototypePageAuthorization
    {
        public static void Configure(RazorPagesOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            options.Conventions.AllowAnonymousToPage("/Index");
            options.Conventions.AllowAnonymousToPage("/Privacy");
            options.Conventions.AllowAnonymousToPage("/Error");
            options.Conventions.AllowAnonymousToPage("/Account/Login");
            options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
            options.Conventions.AuthorizePage("/Account/Logout");
            options.Conventions.AddFolderApplicationModelConvention(
                "/Customer",
                model => model.Filters.Add(new AuthorizeFilter(PrototypeRoles.Customer)));
            options.Conventions.AddPageApplicationModelConvention(
                "/Staff/FrontOfHouseOrders",
                model => model.Filters.Add(new AuthorizeFilter(PrototypeRoles.FrontOfHouse)));
            options.Conventions.AddPageApplicationModelConvention(
                "/Staff/KitchenOrders",
                model => model.Filters.Add(new AuthorizeFilter(PrototypeRoles.Kitchen)));
            options.Conventions.AddFolderApplicationModelConvention(
                "/Admin",
                model => model.Filters.Add(new AuthorizeFilter(PrototypeRoles.Admin)));
        }
    }
}
