namespace InventoryPOS.Domain.Common;

/// <summary>Business rule violation; the message is safe to show to the user.</summary>
public class DomainException(string message) : Exception(message);
