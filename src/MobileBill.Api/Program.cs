using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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
if (Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
    throw new InvalidOperationException("Configuration 'Jwt:SigningKey' must be at least 32 bytes long.");
if (!builder.Environment.IsDevelopment()
    && (jwtSigningKey.Contains("dev-only", StringComparison.OrdinalIgnoreCase) || jwtSigningKey.Contains("REPLACE_WITH", StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException("Configuration 'Jwt:SigningKey' is still the development or placeholder key. Set a new random secret.");

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
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        RequireExpirationTime = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var validator = context.HttpContext.RequestServices.GetRequiredService<IUserSessionValidator>();
            if (!await validator.IsValidAsync(context.Principal!, context.HttpContext.RequestAborted))
                context.Fail("The account is no longer active or its role has changed. Sign in again.");
        }
    };
});
// Every endpoint requires a signed-in user unless it is explicitly marked [AllowAnonymous].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Limits password guessing and request flooding on the sign-in, registration and password-reset endpoints.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.Authentication, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();
builder.Services.AddScoped<IBillReviewAuthorizationService, RoleBasedBillReviewAuthorizationService>();
builder.Services.AddHostedService<MobileBill.Api.BackgroundJobs.ResignationCompletionService>();

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
app.UseRateLimiter();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

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
