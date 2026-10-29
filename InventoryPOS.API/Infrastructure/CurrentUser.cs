using InventoryPOS.Application.Common;

namespace InventoryPOS.API.Infrastructure;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string? UserId => Principal?.Identity?.IsAuthenticated == true ? Principal.FindFirst(PosClaims.UserId)?.Value : null;
    public string? UserName => Principal?.FindFirst(PosClaims.UserName)?.Value;
    public bool IsSuperAdmin => Principal?.FindFirst(PosClaims.SuperAdmin)?.Value == "true";
}
