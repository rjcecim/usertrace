namespace UserTrace.Models;

public enum SenhasExpiradasTipoBuscaPreset
{
    Hoje,
    Intervalo,
    ProximoLogon
}

public sealed record SenhasExpiradasNavigationPreset(
    SenhasExpiradasTipoBuscaPreset Tipo,
    DateTimeOffset? DataInicio = null,
    DateTimeOffset? DataFim = null);

