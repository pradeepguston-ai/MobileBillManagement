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
        services.AddScoped<IUserManagementService, EfUserManagementService>();
        services.AddSingleton<IBillFileStorage, FileSystemBillFileStorage>();
        services.AddScoped<IBillBatchService, EfBillBatchService>();
        services.AddScoped<IBillMatchingService, EfBillMatchingService>();
        services.AddScoped<IBillExceptionReviewService, EfBillExceptionReviewService>();
        services.AddScoped<IBillAssessmentService, EfBillAssessmentService>();
        services.AddScoped<IBillApprovalWorkflowService, EfBillApprovalWorkflowService>();
        services.AddScoped<IBillBatchReviewQueryService, EfBillBatchReviewQueryService>();
        services.AddScoped<IBillingExcelReportService, ClosedXmlBillingExcelReportService>();
        services.AddSingleton<IPdfBillParser, PdfBillParser>();

        return services;
    }
}
