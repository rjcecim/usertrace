namespace UserTrace.Models;

public sealed class SenhaExpiraItem
{
    public string SamAccountName { get; init; } = string.Empty;
    public string DisplayName    { get; init; } = string.Empty;
    public DateTime Expira       { get; init; }

    public override string ToString() => $"{SamAccountName} - {Expira:dd/MM/yyyy HH:mm}";
}
