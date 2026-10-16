namespace Dentara.Application.Common.Results;

/// <summary>
/// Pagination wrapper for query results. The API layer maps this to a paged
/// response with X-Total-Count header and pagination links.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;

    public static PagedResult<T> Empty(int pageNumber, int pageSize) =>
        new(Array.Empty<T>(), 0, pageNumber, pageSize);
}
