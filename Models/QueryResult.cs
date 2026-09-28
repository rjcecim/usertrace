namespace UserTrace.Models;

public sealed class QueryResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public bool Truncated { get; init; }
    public int Limit { get; init; }

    public static QueryResult<T> None(int limit) => new()
    {
        Items = [],
        Truncated = false,
        Limit = limit
    };

    public QueryResult<T> WithItems(IReadOnlyList<T> items) => new()
    {
        Items = items,
        Truncated = Truncated,
        Limit = Limit
    };
}
