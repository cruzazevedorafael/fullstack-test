using System.ComponentModel.DataAnnotations;

namespace Investments.Api.Contracts;

public record PageRequest
{
    public const int MaxPageSize = 100;

    /// <summary>Page number, starting at 1.</summary>
    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    /// <summary>Items per page (1 to 100).</summary>
    [Range(1, MaxPageSize)]
    public int PageSize { get; init; } = 10;
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
}
