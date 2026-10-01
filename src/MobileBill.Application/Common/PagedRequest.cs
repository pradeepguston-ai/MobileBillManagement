namespace MobileBill.Application.Common;

public sealed record PagedRequest(int PageNumber = 1, int PageSize = 20, string? Search = null, bool? IsActive = null)
{
    public int NormalizedPageNumber => Math.Max(1, PageNumber);
    public int NormalizedPageSize => Math.Clamp(PageSize, 1, 100);
}
