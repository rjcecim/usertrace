namespace UserTrace.Models;

/// <summary>Item de lista para exibição na página Senhas Expiradas (com ou sem data de expiração).</summary>
public sealed class SenhaExpiraDisplay
{
    public string SamAccountName { get; init; } = string.Empty;
    public string DisplayName    { get; init; } = string.Empty;
    /// <summary>Data de expiração formatada, ou vazio para "obrigado a trocar no próximo logon".</summary>
    public string DataExpira     { get; init; } = string.Empty;

    public static SenhaExpiraDisplay FromPasswordExpiry(SenhaExpiraItem item) => new()
    {
        SamAccountName = item.SamAccountName,
        DisplayName    = item.DisplayName,
        DataExpira     = item.Expira.ToString("dd/MM/yyyy HH:mm")
    };

    /// <summary>Lista LDAP retorna <see cref="SearchResultItem"/> (sem data de expiração).</summary>
    public static SenhaExpiraDisplay FromSearchResultNextLogon(SearchResultItem item) => new()
    {
        SamAccountName = item.SamAccountName,
        DisplayName    = item.DisplayName,
        DataExpira     = string.Empty
    };
}
