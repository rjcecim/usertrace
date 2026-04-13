namespace UserTrace.Models;

public sealed class SearchResultItem
{
    public string SamAccountName { get; init; } = string.Empty;
    public string DisplayName    { get; init; } = string.Empty;
    /// <summary>
    /// Data/hora do lockout (quando aplicável). Vazio para itens que não representam contas bloqueadas.
    /// </summary>
    public string LockoutTime    { get; init; } = string.Empty;

    public override string ToString() => $"{SamAccountName} - {DisplayName}";
}
