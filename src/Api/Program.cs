using CoffeeShop.Api;
using CoffeeShop.Api.Diagnostics;
using CoffeeShop.Application;
using CoffeeShop.Application.Auth;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Infrastructure;
using CoffeeShop.Infrastructure.Auth;
using CoffeeShop.Infrastructure.Health;
using CoffeeShop.Infrastructure.Seeding;
using CoffeeShop.Infrastructure.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging.Console;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
DemoSeeder.EnsureAllowed(builder.Configuration, builder.Environment);

var jwtOptions = JwtOptions.Load(builder.Configuration);

builder.Logging.AddConsoleFormatter<CorrelationConsoleFormatter, ConsoleFormatterOptions>();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
var clockOffsetDays = builder.Services.AddClocks(builder.Configuration, builder.Environment);
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<PriceService>();
var storeCurrency = builder.Configuration["Pricing:Currency"] ?? "MXN";
if (!IsoCurrencies.IsValid(storeCurrency))
    throw new InvalidOperationException($"Pricing:Currency must be an uppercase ISO 4217 code, got '{storeCurrency}'.");
builder.Services.AddSingleton(new PricingSettings(storeCurrency));
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("Default"));
builder.Services.AddAuthInfrastructure(jwtOptions);
builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddStorage(builder.Configuration);
builder.Services.AddCoffeeShopHealthChecks();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = jwtOptions.SigningKey,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = AuthClaims.Role,
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.AdminOnly, policy => policy.RequireRole(AppRoles.Admin))
    .AddPolicy(AuthPolicies.CustomerOnly, policy => policy.RequireRole(AppRoles.Customer));

var app = builder.Build();

if (clockOffsetDays != 0)
    app.Logger.LogWarning(
        "*** DEV CLOCK SHIFTED BY {OffsetDays:+0;-0} DAYS: the app believes it is {AppNow:u}. Remove Dev__ClockOffsetDays to use the real time. ***",
        clockOffsetDays, app.Services.GetRequiredService<TimeProvider>().GetUtcNow());

await app.Services.SeedAdminAsync(app.Configuration);
await app.Services.EnsureStorageBucketAsync();
if (DemoSeeder.IsEnabled(app.Configuration))
    await app.Services.SeedDemoAsync(app.Configuration, app.Environment);

app.UseRequestDiagnostics();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "CoffeeShop API v1"));
}

app.UseAuthentication();
app.UseAuthorization();

app.MapCatalogEndpoints();
app.MapAuthEndpoints();
app.MapPriceEndpoints();
app.MapOrderEndpoints();
app.MapAdminInventoryEndpoints();
app.MapAdminOutboxEndpoints();
app.MapHealthEndpoints();

await app.RunAsync();
