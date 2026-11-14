using InventoryPOS.Domain.Common;

namespace InventoryPOS.Domain.Entities;

public class Brand : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = [];
}

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentId { get; set; }
    public bool IsActive { get; set; } = true;

    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
}

public class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = [];
}

public class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    /// <summary>Free notes such as usual shade or allergies.</summary>
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    // ── Fiscal data (CFDI 4.0); empty when the customer does not invoice ──
    /// <summary>RFC, uppercase: 12 characters for a persona moral, 13 for a persona física.</summary>
    public string? TaxId { get; set; }
    /// <summary>Name or razón social exactly as registered with the SAT.</summary>
    public string? LegalName { get; set; }
    /// <summary>c_RegimenFiscal code, e.g. 612.</summary>
    public string? TaxRegime { get; set; }
    /// <summary>Código postal of the fiscal address.</summary>
    public string? TaxZipCode { get; set; }
    /// <summary>Default c_UsoCFDI code, e.g. G03.</summary>
    public string? CfdiUse { get; set; }

    public bool HasFiscalData => !string.IsNullOrEmpty(TaxId);

    public ICollection<Sale> Sales { get; set; } = [];
}
