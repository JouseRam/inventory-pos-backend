using InventoryPOS.Domain.Common;

namespace InventoryPOS.Domain.Entities;

/// <summary>Editable business parameter (tax rate, business name, ticket footer...).</summary>
public class Setting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string Label { get; set; } = string.Empty;
    /// <summary>text | textarea | number | bool</summary>
    public string Type { get; set; } = "text";
    public string Group { get; set; } = "General";
}

public class UserModulePermission : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public bool CanEdit { get; set; }
}

/// <summary>Counter behind each folio series (V-, REC-, CJ-).</summary>
public class FolioSequence : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public int NextValue { get; set; } = 1;
}
