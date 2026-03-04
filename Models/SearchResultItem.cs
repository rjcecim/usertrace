namespace UserTrace.Models;

public sealed class SearchResultItem
{
    public string SamAccountName { get; init; } = string.Empty;
    public string DisplayName    { get; init; } = string.Empty;

    public override string ToString() => $"{SamAccountName} - {DisplayName}";
}
