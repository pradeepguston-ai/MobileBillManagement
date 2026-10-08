using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Assistant;
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
            BillingReportFilterNotFoundException => StatusCodes.Status404NotFound,
            BillingReportConflictException => StatusCodes.Status409Conflict,
            EmailAlreadyRegisteredException => StatusCodes.Status409Conflict,
            InvalidCredentialsException => StatusCodes.Status401Unauthorized,
            AccountNotActiveException => StatusCodes.Status403Forbidden,
            UserNotFoundException => StatusCodes.Status404NotFound,
            PasswordResetRequestNotFoundException => StatusCodes.Status404NotFound,
            PasswordResetConflictException => StatusCodes.Status409Conflict,
            UserManagementConflictException => StatusCodes.Status400BadRequest,
            AssistantValidationException => StatusCodes.Status400BadRequest,
            AssistantUnavailableException => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };
        httpContext.Response.StatusCode = statusCode;
        // Unexpected errors can carry database, file-path or stack details, so only the known business
        // exceptions above return their message; everything else is logged by the exception handler middleware.
        var title = statusCode == StatusCodes.Status500InternalServerError
            ? "The server could not complete the request."
            : exception.Message;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = statusCode, Title = title }
        });
    }
}
