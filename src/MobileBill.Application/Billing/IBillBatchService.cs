using MobileBill.Application.Common;

namespace MobileBill.Application.Billing;

public interface IBillBatchService
{
    Task<PagedResult<BillBatchListItemDto>> ListAsync(BillBatchListRequest request, CancellationToken cancellationToken);
    Task<BillBatchDto> CreateAsync(CreateBillBatchRequest request, CancellationToken cancellationToken);
    Task<BillBatchDto> UploadAsync(Guid id, Stream content, string fileName, string? contentType, long length, CancellationToken cancellationToken);
    Task<BillBatchDto> ParseAsync(Guid id, CancellationToken cancellationToken);
    Task<BillBatchDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResult<BillLineDto>> GetLinesAsync(Guid id, PagedRequest request, CancellationToken cancellationToken);
    Task<BillBatchDto> ValidateAsync(Guid id, CancellationToken cancellationToken);
}
