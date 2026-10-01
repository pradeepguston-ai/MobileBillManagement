using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;

namespace MobileBill.Api.Controllers;

[ApiController]
public sealed class BillMatchingController(IBillMatchingService matchingService, IBillExceptionReviewService exceptionReviewService) : ControllerBase
{
    [HttpPost("api/bill-batches/{id:guid}/match"), Authorize]
    public Task<BillMatchingResult> Match(Guid id, CancellationToken cancellationToken) =>
        matchingService.MatchAsync(id, cancellationToken);

    [HttpGet("api/bill-batches/{id:guid}/exceptions")]
    public Task<PagedResult<BillExceptionDto>> Exceptions(Guid id, [FromQuery] BillExceptionListRequest request, CancellationToken cancellationToken) =>
        exceptionReviewService.GetExceptionsAsync(id, request, cancellationToken);

    [HttpGet("api/bill-batches/{id:guid}/exceptions/summary")]
    public Task<BillExceptionSummaryDto> ExceptionSummary(Guid id, CancellationToken cancellationToken) =>
        exceptionReviewService.GetSummaryAsync(id, cancellationToken);

    [HttpGet("api/bill-exceptions/{id:guid}/mobile-account-candidates")]
    public Task<IReadOnlyList<MobileAccountCandidateDto>> MobileAccountCandidates(Guid id, CancellationToken cancellationToken) =>
        exceptionReviewService.GetMobileAccountCandidatesAsync(id, cancellationToken);

    [HttpPost("api/bill-exceptions/{id:guid}/resolve-mobile-account"), Authorize]
    public Task<BillExceptionResolutionResult> ResolveMobileAccount(Guid id, ResolveMobileAccountExceptionRequest request, CancellationToken cancellationToken) =>
        exceptionReviewService.ResolveMobileNotFoundAsync(id, request, cancellationToken);
}
