using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MobileBill.Api.Configuration;
using MobileBill.Api.ExceptionHandling;
using MobileBill.Api.Identity;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure;
using MobileBill.Infrastructure.Identity;
using MobileBill.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddLocalDevelopmentCors();
builder.Services.AddHttpContextAccessor();
builder.Services.AddInfrastructure(builder.Configuration);

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtIssuer = jwtSection["Issuer"] ?? throw new InvalidOperationException("Configuration 'Jwt:Issuer' is required.");
var jwtAudience = jwtSection["Audience"] ?? throw new InvalidOperationException("Configuration 'Jwt:Audience' is required.");
var jwtSigningKey = jwtSection["SigningKey"] ?? throw new InvalidOperationException("Configuration 'Jwt:SigningKey' is required.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();
builder.Services.AddScoped<IBillReviewAuthorizationService, RoleBasedBillReviewAuthorizationService>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicies.LocalReactDevelopment);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

await BootstrapAdministratorAsync(app.Services, builder.Configuration);

app.Run();

static async Task BootstrapAdministratorAsync(IServiceProvider services, IConfiguration configuration)
{
    // Checked first, before touching the database: leaving these unset (the default for every
    // test host and any environment that hasn't opted in) must be a pure no-op with no DB dependency.
    var adminEmail = configuration["Bootstrap:AdminEmail"];
    var adminPassword = configuration["Bootstrap:AdminPassword"];
    if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword)) return;

    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
    if (await db.Users.AnyAsync(x => x.Role == UserRole.Administrator)) return;

    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();
    var clock = scope.ServiceProvider.GetRequiredService<IClock>();
    var now = clock.UtcNow;
    var admin = new User
    {
        Email = adminEmail.Trim().ToLowerInvariant(),
        DisplayName = "System Administrator",
        Role = UserRole.Administrator,
        Status = UserAccountStatus.Active,
        PasswordHash = string.Empty,
        CreatedAtUtc = now
    };
    admin.PasswordHash = hasher.Hash(admin, adminPassword);
    db.Users.Add(admin);
    await db.SaveChangesAsync();
}

public partial class Program;
