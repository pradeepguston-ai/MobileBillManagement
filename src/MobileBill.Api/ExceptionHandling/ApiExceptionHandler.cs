using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Common;
using MobileBill.Application.Billing;
using MobileBill.Application.Reports;
using MobileBill.Application.Identity;

namespace MobileBill.Api.ExceptionHandling;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            MasterDataNotFoundException => StatusCodes.Status404NotFound,
            MasterDataConflictException => StatusCodes.Status409Conflict,
            MasterDataValidationException => StatusCodes.Status400BadRequest,
            BillBatchNotFoundException => StatusCodes.Status404NotFound,
            BillBatchConflictException => StatusCodes.Status409Conflict,
            BillBatchValidationException => StatusCodes.Status400BadRequest,
            BillBatchParsingException => StatusCodes.Status422UnprocessableEntity,
            BillReviewNotFoundException => StatusCodes.Status404NotFound,
            BillReviewRowNotFoundException => StatusCodes.Status404NotFound,
            BillReviewConflictException => StatusCodes.Status409Conflict,
            BillReviewValidationException => StatusCodes.Status400BadRequest,
            BillReviewForbiddenException => StatusCodes.Status403Forbidden,
            BillAssessmentNotFoundException => StatusCodes.Status404NotFound,
            BillAssessmentValidationException => StatusCodes.Status400BadRequest,
            BillWorkflowConflictException => StatusCodes.Status409Conflict,
            BillWorkflowValidationException => StatusCodes.Status400BadRequest,
            BillingReportNotFoundException => StatusCodes.Status404NotFound,
            BillingReportConflictException => StatusCodes.Status409Conflict,
            EmailAlreadyRegisteredException => StatusCodes.Status409Conflict,
            InvalidCredentialsException => StatusCodes.Status401Unauthorized,
            AccountNotActiveException => StatusCodes.Status403Forbidden,
            UserNotFoundException => StatusCodes.Status404NotFound,
            UserManagementConflictException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };
        httpContext.Response.StatusCode = statusCode;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = statusCode, Title = exception.Message }
        });
    }
}
