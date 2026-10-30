using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InventoryPOS.API.Authorization;

/// <summary>
/// Requires access to a permission module: GET/HEAD need CanView, any other verb needs CanEdit.
/// An action-level attribute overrides the controller's.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireModuleAttribute : TypeFilterAttribute
{
    public RequireModuleAttribute(string module, bool forceEdit = false) : base(typeof(ModuleAuthorizationFilter))
    {
        Arguments = [module, forceEdit];
    }
}

/// <summary>Opts an action out of its controller's module requirement; any signed-in user may call it.</summary>
[AttributeUsage(AttributeTargets.Method)]
public class AnyUserAttribute : Attribute;

public class ModuleAuthorizationFilter(string module, bool forceEdit, ICurrentUser currentUser, IPermissionService permissions)
    : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Anonymous endpoints (sign-in, register sync with a device token) and [AnyUser] actions skip module checks.
        var metadata = context.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<AnyUserAttribute>().Any() || metadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any())
            return;

        // The closest attribute wins: skip the controller-level filter when the action declares its own.
        var closest = context.ActionDescriptor.FilterDescriptors
            .Where(f => f.Filter is RequireModuleAttribute)
            .OrderByDescending(f => f.Scope)
            .Select(f => f.Filter)
            .FirstOrDefault();
        if (closest is RequireModuleAttribute attr && (!Equals(attr.Arguments![0], module) || !Equals(attr.Arguments[1], forceEdit)))
            return;

        if (currentUser.UserId == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var method = context.HttpContext.Request.Method;
        var requireEdit = forceEdit || !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method));
        if (!await permissions.HasAccessAsync(currentUser.UserId, module, requireEdit, context.HttpContext.RequestAborted))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Sin permiso",
                Detail = requireEdit ? "No tienes permiso para modificar este módulo." : "No tienes acceso a este módulo.",
            }) { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
