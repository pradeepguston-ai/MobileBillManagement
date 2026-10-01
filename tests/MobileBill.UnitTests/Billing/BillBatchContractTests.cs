using MobileBill.Application.Billing;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;

namespace MobileBill.UnitTests.Billing;

public sealed class BillBatchContractTests
{
    [Fact]
    public void Draft_batch_has_no_upload_metadata_or_independent_total()
    {
        var batch = new BillBatch { CorporateCode = "CORP" };

        Assert.Null(batch.FileHash);
        Assert.Null(batch.StatedGrandTotal);
        Assert.Equal(GrandTotalSource.None, batch.GrandTotalSource);
    }

    [Fact]
    public void Create_request_does_not_expose_uploaded_by_or_uploaded_at()
    {
        Assert.DoesNotContain(typeof(CreateBillBatchRequest).GetProperties(), property =>
            property.Name is "UploadedBy" or "UploadedAt");
    }
}
