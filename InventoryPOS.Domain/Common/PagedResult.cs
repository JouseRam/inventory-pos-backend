namespace InventoryPOS.Domain.Common;

public record PagedResult<T>(IReadOnlyList<T> Data, int Total, int Page, int PageSize)
{
    public PagedResult<TOut> Map<TOut>(Func<T, TOut> map) => new(Data.Select(map).ToList(), Total, Page, PageSize);
}
