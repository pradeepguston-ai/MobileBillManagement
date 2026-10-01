namespace MobileBill.Infrastructure.Billing;
public sealed class BillStorageOptions { public const string SectionName = "BillStorage"; public string RootPath { get; set; } = "App_Data/BillUploads"; public long MaximumFileSizeBytes { get; set; } = 20 * 1024 * 1024; }
