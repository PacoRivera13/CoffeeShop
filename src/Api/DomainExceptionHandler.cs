using CoffeeShop.Domain;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Domain.Pricing;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeShop.Api;

internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException)
            return false;

        var (status, title) = exception switch
        {
            NoEffectivePriceException => (StatusCodes.Status404NotFound, "No effective price"),
            InvalidOrderTransitionException => (StatusCodes.Status409Conflict, "Invalid order transition"),
            _ => (StatusCodes.Status422UnprocessableEntity, "Domain rule violated"),
        };

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = exception.Message },
        });
    }
}
