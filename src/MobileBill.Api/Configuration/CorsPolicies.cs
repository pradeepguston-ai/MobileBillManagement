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
                    .AllowAnyMethod()
                    // The web app runs on another origin, so the browser hides Content-Disposition (the download's file name)
                    // unless it is exposed; without it every report downloads under a generic fallback name.
                    .WithExposedHeaders("Content-Disposition"));
        });

        return services;
    }
}
