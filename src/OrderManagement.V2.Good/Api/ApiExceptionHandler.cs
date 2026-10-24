using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.V2.Good.Domain;
using OrderManagement.V2.Good.Services;

namespace OrderManagement.V2.Good.Api;

/// <summary>
/// One place that turns application exceptions into correct HTTP responses:
/// user mistakes become 400, missing orders 404, illegal state transitions 409.
///
/// V1 returned 500 for everything and leaked the exception message.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;

    public ApiExceptionHandler(IProblemDetailsService problemDetails) => _problemDetails = problemDetails;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not (OrderValidationException
            or OrderNotFoundException
            or UnknownPaymentMethodException
            or DomainException))
        {
            return false;   // let the framework handle genuine 500s
        }

        var (status, title) = exception switch
        {
            OrderValidationException => (StatusCodes.Status400BadRequest, "Order validation failed"),
            UnknownPaymentMethodException => (StatusCodes.Status400BadRequest, "Unsupported payment method"),
            OrderNotFoundException => (StatusCodes.Status404NotFound, "Order not found"),
            _ => (StatusCodes.Status409Conflict, "Order state conflict")
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };

        if (exception is OrderValidationException validation)
            problem.Extensions["errors"] = validation.Errors;

        httpContext.Response.StatusCode = status;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
