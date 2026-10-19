using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SpendWise.Application.Common.Exceptions;

namespace SpendWise.API.Infrastructure;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // Chuyển exception của Application thành HTTP status và payload lỗi thống nhất.
        var (status, code, rawTitle) = exception switch
        {
            AppException app => (app.StatusCode, app.Code, app.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "unauthorized", "Authentication is required."),
            OperationCanceledException when context.RequestAborted.IsCancellationRequested => (499, "request_cancelled", "Request was cancelled."),
            _ => (StatusCodes.Status500InternalServerError, "server_error", "An unexpected error occurred.")
        };
        var title = ApiErrorLocalizer.Localize(code, rawTitle);

        if (status == 500)
            logger.LogError(exception, "Unhandled request failure. TraceId: {TraceId}", context.TraceIdentifier);

        context.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Type = $"https://spendwise.local/errors/{code}",
                Extensions = { ["code"] = code, ["traceId"] = context.TraceIdentifier }
            },
            Exception = exception
        });
    }
}
