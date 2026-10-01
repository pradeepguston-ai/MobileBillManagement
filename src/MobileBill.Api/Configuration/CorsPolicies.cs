namespace MobileBill.Api.Configuration;

public static class CorsPolicies
{
    public const string LocalReactDevelopment = "LocalReactDevelopment";

    public static IServiceCollection AddLocalDevelopmentCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(
                LocalReactDevelopment,
                policy => policy
                    .WithOrigins("http://localhost:5173")
                    .AllowAnyHeader()
                    .AllowAnyMethod());
        });

        return services;
    }
}
