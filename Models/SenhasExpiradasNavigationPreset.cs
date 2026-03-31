namespace UserTrace.Models;

public enum SenhasExpiradasTipoBuscaPreset
{
    DataEspecifica,
    Hoje,
    Intervalo,
    ProximoLogon
}

public sealed record SenhasExpiradasNavigationPreset(
    SenhasExpiradasTipoBuscaPreset Tipo,
    DateTimeOffset? DataInicio = null,
    DateTimeOffset? DataFim = null);

