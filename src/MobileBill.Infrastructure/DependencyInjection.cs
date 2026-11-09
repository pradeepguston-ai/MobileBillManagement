using Microsoft.Extensions.Options;
using MobileBill.Application.Assistant;
using MobileBill.Infrastructure.Assistant;
using MobileBill.Application.Insights;
using MobileBill.Infrastructure.Insights;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MobileBill.Infrastructure.Persistence;
using MobileBill.Infrastructure.MasterData;
using MobileBill.Application.MasterData;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Infrastructure.Billing;
using MobileBill.Application.Pdf;
using MobileBill.Infrastructure.Pdf;
using MobileBill.Application.Reports;
using MobileBill.Infrastructure.Reports;
using MobileBill.Application.Identity;
using MobileBill.Infrastructure.Identity;
using MobileBill.Application.Notifications;
using MobileBill.Infrastructure.Notifications;
using MobileBill.Application.Activity;
using MobileBill.Infrastructure.Activity;

namespace MobileBill.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is required.");

        services.AddDbContext<MobileBillDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddScoped<IMasterDataService, EfMasterDataService>();
        services.AddScoped<MobileBill.Application.Devices.IDeviceService, MobileBill.Infrastructure.Devices.EfDeviceService>();        services.AddScoped<IMasterDataImportService, ClosedXmlMasterDataImportService>();
        services.AddScoped<IMobileAllocationReportService, ClosedXmlMobileAllocationReportService>();
        services.Configure<BillStorageOptions>(options =>
        {
            var section = configuration.GetSection(BillStorageOptions.SectionName);
            options.RootPath = section["RootPath"] ?? options.RootPath;
            if (long.TryParse(section["MaximumFileSizeBytes"], out var maximumFileSize)) options.MaximumFileSizeBytes = maximumFileSize;
        });
        services.AddSingleton<IClock, SystemClock>();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, EfAuthService>();
        services.AddScoped<IUserSessionValidator, EfUserSessionValidator>();
        services.AddScoped<IUserManagementService, EfUserManagementService>();
        services.AddSingleton<IBillFileStorage, FileSystemBillFileStorage>();
        services.AddScoped<IBillBatchService, EfBillBatchService>();
        services.AddScoped<IBillMatchingService, EfBillMatchingService>();
        services.AddScoped<IBillExceptionReviewService, EfBillExceptionReviewService>();
        services.AddScoped<IBillAssessmentService, EfBillAssessmentService>();
        services.AddScoped<IBillApprovalWorkflowService, EfBillApprovalWorkflowService>();
        services.AddScoped<IBillBatchReviewQueryService, EfBillBatchReviewQueryService>();
        services.AddScoped<IBillTrendService, EfBillTrendService>();
        services.AddScoped<IVasReportService, VasReportService>();
        services.AddScoped<IBillingExcelReportService, ClosedXmlBillingExcelReportService>();
        services.AddScoped<IBillingPdfReportService, PdfBillingReportService>();
        services.AddScoped<IBillLinesExcelExportService, ClosedXmlBillLinesExcelExportService>();
        services.AddScoped<IBillingInsightsService, EfBillingInsightsService>();
        services.AddSingleton<IPdfBillParser, PdfBillParser>();

        // AI assistant (OpenRouter). Without OpenRouter:ApiKey it reports itself as not enabled.
        services.AddMemoryCache();
        services.Configure<OpenRouterOptions>(configuration.GetSection(OpenRouterOptions.SectionName));
        services.AddHttpClient<IChatModelClient, OpenRouterChatModelClient>((provider, client) =>
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(provider.GetRequiredService<IOptions<OpenRouterOptions>>().Value.TimeoutSeconds, 10, 300)));
        services.AddScoped<AssistantToolbox>();
        services.AddScoped<IAssistantService, AssistantService>();

        // Approval emails through Microsoft Graph. Until Email:Enabled is true and the credentials are set, nothing is sent.
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.AddHttpClient<IEmailSender, GraphEmailSender>(client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<IEmailQueue, ChannelEmailQueue>();
        services.AddTransient<RetryingEmailDelivery>();
        services.AddScoped<IBillApprovalNotifier, BillApprovalEmailNotifier>();
        // Emailing the monthly bill report to each distribution rule's recipients (queued in ReportEmailDeliveries).
        services.AddSingleton<ReportEmailWakeUp>();
        services.AddScoped<EfReportDistributionService>();
        services.AddScoped<IReportDistributionService>(provider => provider.GetRequiredService<EfReportDistributionService>());
        services.AddScoped<IReportLockDistribution>(provider => provider.GetRequiredService<EfReportDistributionService>());
        services.AddScoped<IReportEmailProcessor, ReportEmailProcessor>();
        // Activity Log: what each user did, in plain words (recorded by the API's activity filter).
        services.AddScoped<IActivityLogService, EfActivityLogService>();

        return services;
    }
}
