using AllergySystem.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
    options.Conventions.AuthorizePage("/Account/Logout");
    options.Conventions.AuthorizeFolder("/Customer", PrototypeRoles.Customer);
    options.Conventions.AuthorizePage("/Staff/FrontOfHouseOrders", PrototypeRoles.FrontOfHouse);
    options.Conventions.AuthorizePage("/Staff/KitchenOrders", PrototypeRoles.Kitchen);
    options.Conventions.AuthorizeFolder("/Admin", PrototypeRoles.Admin);
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "AllergySystem.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });

builder.Services.AddAuthorization(PrototypeRoles.ConfigurePolicies);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserContext>();

builder.Services.AddSingleton(serviceProvider => new PrototypeAuthenticationService(
    serviceProvider.GetRequiredService<IConfiguration>(),
    builder.Environment.IsDevelopment()));

// Register application services as singletons so in-memory data remains available for the lifetime of the running application.
builder.Services.AddSingleton<InMemoryAllergyProfileStore>();
builder.Services.AddSingleton<AllergenCatalogService>();
builder.Services.AddSingleton<DietaryRestrictionCatalogService>();
builder.Services.AddSingleton<AllergyProfileService>();
builder.Services.AddSingleton<DietaryCompatibilityService>();
builder.Services.AddSingleton<MenuAllergenMappingStore>();
builder.Services.AddSingleton<MenuCatalogService>();
builder.Services.AddSingleton<AllergyValidationService>();
builder.Services.AddSingleton<InMemoryAuditStore>();
builder.Services.AddSingleton<AuditService>();
builder.Services.AddSingleton<MenuAllergenManagementService>();

builder.Services.AddSingleton<AllergenRecommendationService>();
builder.Services.AddSingleton<DietaryRestrictionRecommendationService>();
builder.Services.AddSingleton<InMemoryOrderStore>();
builder.Services.AddSingleton<OrderService>();
builder.Services.AddSingleton<FrontOfHouseOrderService>();
builder.Services.AddSingleton<KitchenOrderService>();
builder.Services.AddSingleton<InMemoryCartStore>();
builder.Services.AddSingleton<CartService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
