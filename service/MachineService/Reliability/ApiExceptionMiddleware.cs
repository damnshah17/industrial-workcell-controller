using System.Data.Common;
using MachineService.Transport;
using Microsoft.AspNetCore.Mvc;

namespace MachineService.Reliability;

public sealed class ApiExceptionMiddleware(
    RequestDelegate next,
    ILogger<ApiExceptionMiddleware> logger
)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug(
                "API request was canceled by the client; TraceId={TraceId} Path={Path}",
                context.TraceIdentifier, context.Request.Path
            );
        }
        catch (Exception exception)
        {
            var unavailable = IsUnavailable(exception);
            var status = unavailable
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status500InternalServerError;
            logger.Log(
                unavailable ? LogLevel.Warning : LogLevel.Error,
                exception,
                "API operation failed; TraceId={TraceId} Path={Path} StatusCode={StatusCode}",
                context.TraceIdentifier, context.Request.Path, status
            );
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = unavailable ? "Required component unavailable" : "Unexpected service error",
                Detail = unavailable
                    ? "The requested component is temporarily unavailable."
                    : "The service could not complete the request.",
                Extensions = { ["traceId"] = context.TraceIdentifier }
            });
        }
    }

    private static bool IsUnavailable(Exception exception) =>
        exception is ControllerUnavailableException or TimeoutException or IOException or DbException
        || exception.InnerException is not null && IsUnavailable(exception.InnerException);
}
