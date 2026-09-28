using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using UserTrace.Helpers;
using UserTrace.Models;
using UserTrace.Services;

namespace UserTrace.Views;

public sealed partial class SenhasExpiradasPage : Page
{
    private CancellationTokenSource? _cts;
    private bool _autoSearchRequested;
    private List<SenhaExpiraDisplay> _resultados = [];

    public SenhasExpiradasPage()
    {
        InitializeComponent();
        var hoje = DateTime.Today;
        DataInicioPicker.Date = hoje;
        DataFimPicker.Date = hoje.AddDays(7);
        DataEspecificaPicker.Date = hoje;
        TipoBuscaCombo.SelectedIndex = 0;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not SenhasExpiradasNavigationPreset preset)
            return;

        _autoSearchRequested = true;

        switch (preset.Tipo)
        {
            case SenhasExpiradasTipoBuscaPreset.DataEspecifica:
                TipoBuscaCombo.SelectedIndex = 0;
                if (preset.DataInicio != null) DataEspecificaPicker.Date = preset.DataInicio.Value;
                break;
            case SenhasExpiradasTipoBuscaPreset.Hoje:
                TipoBuscaCombo.SelectedIndex = 2;
                break;
            case SenhasExpiradasTipoBuscaPreset.Intervalo:
                TipoBuscaCombo.SelectedIndex = 1;
                if (preset.DataInicio != null) DataInicioPicker.Date = preset.DataInicio.Value;
                if (preset.DataFim != null) DataFimPicker.Date = preset.DataFim.Value;
                break;
            case SenhasExpiradasTipoBuscaPreset.ProximoLogon:
                TipoBuscaCombo.SelectedIndex = 3;
                break;
        }

        // Aguarda a UI aplicar SelectionChanged/visibilidades e então dispara a busca.
        DispatcherQueue.TryEnqueue(async () =>
        {
            if (!_autoSearchRequested) return;
            _autoSearchRequested = false;
            await ExecutarBuscaAsync();
        });
    }

    private void ResultadoListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ResultadoListView.SelectedItem is SenhaExpiraDisplay item)
            Frame.NavigateToLoginWithSam(item.SamAccountName);
    }

    private void TipoBuscaCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var tag = (TipoBuscaCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        PainelIntervalo.Visibility       = tag == "Intervalo" ? Visibility.Visible : Visibility.Collapsed;
        PainelDataEspecifica.Visibility  = tag == "DataEspecifica" ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void BuscarButton_Click(object sender, RoutedEventArgs e) =>
        await ExecutarBuscaAsync();

    private void LimparButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        ResultadoListView.ItemsSource = null;
        ContadorTextBlock.Text = "Escolha o tipo de busca e clique em Buscar.";
        SetLoading(false);
    }

    private async Task ExecutarBuscaAsync()
    {
        var tag = (TipoBuscaCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (string.IsNullOrEmpty(tag)) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        SetLoading(true);
        ResultadoListView.ItemsSource = null;
        ContadorTextBlock.Text = "Buscando…";

        try
        {
            var tagBusca = tag;
            var (display, truncated, limit) = tagBusca switch
            {
                "Intervalo" => await ListarExpiracaoAsync(
                    ActiveDirectorySearchService.GetPasswordExpiringInRangeAsync(
                        DataInicioPicker.Date.DateTime,
                        DataFimPicker.Date.DateTime,
                        ct)),
                "DataEspecifica" => await ListarExpiracaoAsync(
                    ActiveDirectorySearchService.GetPasswordExpiringOnDateAsync(
                        DataEspecificaPicker.Date.DateTime,
                        ct)),
                "Hoje" => await ListarExpiracaoAsync(
                    ActiveDirectorySearchService.GetPasswordExpiringTodayAsync(ct)),
                "ProximoLogon" => await ListarProximoLogonAsync(ct),
                _ => (new List<SenhaExpiraDisplay>(), false, 0)
            };

            _resultados = display;
            ResultadoListView.ItemsSource = display;
            ContadorTextBlock.Text = ContagemPt.TextoLista(
                display.Count,
                truncated,
                limit,
                "Nenhum resultado.",
                "1 usuário encontrado.",
                "{0} usuários encontrados.");
        }
        catch (OperationCanceledException)
        {
            ContadorTextBlock.Text = "Busca cancelada.";
        }
        catch (Exception ex)
        {
            ContadorTextBlock.Text = $"Erro: {ex.Message}";
        }
        finally
        {
            SetLoading(false);
        }
    }

    private static async Task<(List<SenhaExpiraDisplay> Items, bool Truncated, int Limit)> ListarExpiracaoAsync(
        Task<QueryResult<SenhaExpiraItem>> busca)
    {
        var resultado = await busca;
        return (
            resultado.Items.Select(SenhaExpiraDisplay.FromPasswordExpiry).ToList(),
            resultado.Truncated,
            resultado.Limit);
    }

    private static async Task<(List<SenhaExpiraDisplay> Items, bool Truncated, int Limit)> ListarProximoLogonAsync(
        CancellationToken ct)
    {
        var resultado = await ActiveDirectorySearchService.GetMustChangePasswordAtNextLogonAsync(ct);
        var items = resultado.Items
            .Select(SenhaExpiraDisplay.FromSearchResultNextLogon)
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return (items, resultado.Truncated, resultado.Limit);
    }

    private void SetLoading(bool loading)
    {
        LoadingRing.IsActive   = loading;
        LoadingRing.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        ResultadosPanel.Visibility = loading ? Visibility.Collapsed : Visibility.Visible;
        BuscarButton.IsEnabled = !loading;
    }

    private void ExportItem_Click(object sender, RoutedEventArgs e) =>
        ExportHelper.IniciarExportacaoMenu(this, sender, ExportarAsync);

    private async Task ExportarAsync(string formato)
    {
        if (App.CurrentWindow is null)
            throw new InvalidOperationException("Janela principal indisponível.");

        await ExportHelper.ExportarSenhasExpiradasDiretoAsync(_resultados, formato, XamlRoot, App.CurrentWindow);
    }
}
