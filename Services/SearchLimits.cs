namespace UserTrace.Services;

/// <summary>
/// As listas percorrem todas as páginas do Active Directory.
/// <see cref="Safety"/> só interrompe uma consulta que passe de cem mil itens, para a tela não travar.
/// </summary>
public static class SearchLimits
{
    public const int Safety = 100_000;
}
