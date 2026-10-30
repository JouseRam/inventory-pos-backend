using InventoryPOS.Domain.Common;

namespace InventoryPOS.Application.Features.Sync;

public enum PosRole
{
    /// <summary>Web server: administers catalog, prices and users and consults what the registers send. It does not sell.</summary>
    Central,
    /// <summary>Desktop register: sells offline. Once paired with a central server its catalog and users come from there.</summary>
    Register,
}

/// <summary>Who this installation is and what it may change.</summary>
public interface IPosEnvironment
{
    PosRole Role { get; }
    /// <summary>True on a register linked to a central server.</summary>
    Task<bool> IsPairedAsync(CancellationToken ct = default);
}

/// <summary>Business rules that depend on the installation's role.</summary>
public class PosGuard(IPosEnvironment env)
{
    /// <summary>Catalog, suppliers, users and business settings are administered on the web once a register is paired.</summary>
    public async Task EnsureCanAdministerAsync(CancellationToken ct = default)
    {
        if (env.Role == PosRole.Register && await env.IsPairedAsync(ct))
            throw new DomainException("Esta caja está vinculada: el catálogo, los usuarios y los datos del negocio se administran desde la web.");
    }

    /// <summary>Sales, cash and stock changes happen at the register; the web only consults them.</summary>
    public void EnsureCanOperate()
    {
        if (env.Role == PosRole.Central)
            throw new DomainException("La web no registra ventas, caja ni movimientos de inventario; hazlo desde la caja.");
    }
}
