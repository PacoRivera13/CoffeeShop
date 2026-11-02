using CoffeeShop.Api;
using CoffeeShop.Application;
using CoffeeShop.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CatalogService>();
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("Default"));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapCatalogEndpoints();

app.Run();
