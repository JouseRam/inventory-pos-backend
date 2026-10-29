using InventoryPOS.Application.Common;
using InventoryPOS.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InventoryPOS.API.Infrastructure;

/// <summary>Maps application exceptions to ProblemDetails; anything unexpected is logged and returned as 500 without internals.</summary>
public class ExceptionHandler(ILogger<ExceptionHandler> logger, IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            DomainException => (StatusCodes.Status400BadRequest, "Operación no válida"),
            NotFoundException => (StatusCodes.Status404NotFound, "No encontrado"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflicto"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Sin permiso"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno"),
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status == StatusCodes.Status500InternalServerError
                    ? "Ocurrió un error inesperado; intenta de nuevo o contacta al administrador."
                    : exception.Message,
            },
        });
    }
}
