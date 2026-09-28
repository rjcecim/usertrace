namespace UserTrace.Helpers;

/// <summary>Mensagens de contagem em português (0 / 1 / N).</summary>
public static class ContagemPt
{
    public static string Texto(int total, string nenhum, string um, string variosFormatoCom0) =>
        total == 0 ? nenhum : total == 1 ? um : string.Format(variosFormatoCom0, total);

    public static string TextoLista(
        int total,
        bool truncated,
        int limit,
        string nenhum,
        string um,
        string variosFormatoCom0)
    {
        var texto = Texto(total, nenhum, um, variosFormatoCom0);
        if (!truncated)
            return texto;

        return $"{texto} A busca parou no limite de {limit}.";
    }
}
