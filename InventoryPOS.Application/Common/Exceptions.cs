namespace InventoryPOS.Application.Common;

public class NotFoundException(string message) : Exception(message)
{
    public static NotFoundException For(string entity, object id) => new($"{entity} {id} no encontrado.");
}

/// <summary>The request collides with current state (duplicate key, concurrent edit).</summary>
public class ConflictException(string message) : Exception(message);

public class ForbiddenException(string message) : Exception(message);
