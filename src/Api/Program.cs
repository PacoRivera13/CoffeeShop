using CoffeeShop.Api;
using CoffeeShop.Application;
using CoffeeShop.Application.Auth;
using CoffeeShop.Infrastructure;
using CoffeeShop.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwtOptions = JwtOptions.Load(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<PriceService>();
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("Default"));
builder.Services.AddAuthInfrastructure(jwtOptions);

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

await app.Services.SeedAdminAsync(app.Configuration);

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

await app.RunAsync();
