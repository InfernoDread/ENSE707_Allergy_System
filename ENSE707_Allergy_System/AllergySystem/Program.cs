using AllergySystem.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Register application services as singletons so in-memory data remains available for the lifetime of the running application.
builder.Services.AddSingleton<InMemoryAllergyProfileStore>();
builder.Services.AddSingleton<AllergenCatalogService>();
builder.Services.AddSingleton<AllergyProfileService>();
builder.Services.AddSingleton<MenuCatalogService>();
builder.Services.AddSingleton<AllergyValidationService>();
builder.Services.AddSingleton<AllergenRecommendationService>();
builder.Services.AddSingleton<InMemoryOrderStore>();
builder.Services.AddSingleton<OrderService>();
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

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
