namespace UserTrace.Models;

public sealed class GlobalSearchHit
{
    public string Kind { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string MenuTag { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;

    public static GlobalSearchHit None { get; } = new()
    {
        Kind = "—",
        Title = "Nenhum resultado",
        Subtitle = "Tente login, nome, grupo ou setor.",
        MenuTag = string.Empty,
        Value = string.Empty
    };
}
