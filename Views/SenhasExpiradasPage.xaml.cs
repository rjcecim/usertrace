using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using UserTrace.Models;
using UserTrace.Services;

namespace UserTrace.Views;

public sealed partial class SenhasExpiradasPage : Page
{
    private CancellationTokenSource? _cts;

    public SenhasExpiradasPage()
    {
        InitializeComponent();
        var hoje = DateTime.Today;
        DataInicioPicker.Date = hoje;
        DataFimPicker.Date = hoje.AddDays(7);
        DataEspecificaPicker.Date = hoje;
        TipoBuscaCombo.SelectedIndex = 0;
    }

    private void ResultadoListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ResultadoListView.SelectedItem is SenhaExpiraDisplay item && !string.IsNullOrEmpty(item.SamAccountName))
        {
            Frame.Navigate(typeof(LoginPage), item.SamAccountName, new EntranceNavigationTransitionInfo());
        }
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
            List<SenhaExpiraDisplay> display;
            switch (tag)
            {
                case "Intervalo":
                    var ini = DataInicioPicker.Date.DateTime;
                    var fim = DataFimPicker.Date.DateTime;
                    var listIntervalo = await ActiveDirectorySearchService.GetPasswordExpiringInRangeAsync(ini, fim, ct);
                    display = listIntervalo.Select(x => new SenhaExpiraDisplay
                    {
                        SamAccountName = x.SamAccountName,
                        DisplayName    = x.DisplayName,
                        DataExpira     = x.Expira.ToString("dd/MM/yyyy")
                    }).ToList();
                    break;
                case "DataEspecifica":
                    var data = DataEspecificaPicker.Date.DateTime;
                    var listData = await ActiveDirectorySearchService.GetPasswordExpiringOnDateAsync(data, ct);
                    display = listData.Select(x => new SenhaExpiraDisplay
                    {
                        SamAccountName = x.SamAccountName,
                        DisplayName    = x.DisplayName,
                        DataExpira     = x.Expira.ToString("dd/MM/yyyy")
                    }).ToList();
                    break;
                case "Hoje":
                    var listHoje = await ActiveDirectorySearchService.GetPasswordExpiringTodayAsync(ct);
                    display = listHoje.Select(x => new SenhaExpiraDisplay
                    {
                        SamAccountName = x.SamAccountName,
                        DisplayName    = x.DisplayName,
                        DataExpira     = x.Expira.ToString("dd/MM/yyyy")
                    }).ToList();
                    break;
                case "ProximoLogon":
                    var listProximo = await ActiveDirectorySearchService.GetMustChangePasswordAtNextLogonAsync(ct);
                    display = listProximo
                        .Select(x => new SenhaExpiraDisplay
                        {
                            SamAccountName = x.SamAccountName,
                            DisplayName    = x.DisplayName,
                            DataExpira     = ""
                        })
                        .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    break;
                default:
                    display = new List<SenhaExpiraDisplay>();
                    break;
            }

            ResultadoListView.ItemsSource = display;
            ContadorTextBlock.Text = display.Count == 0
                ? "Nenhum resultado."
                : display.Count == 1 ? "1 usuário encontrado." : $"{display.Count} usuários encontrados.";
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

    private void SetLoading(bool loading)
    {
        LoadingRing.IsActive   = loading;
        LoadingRing.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        ResultadosPanel.Visibility = loading ? Visibility.Collapsed : Visibility.Visible;
        BuscarButton.IsEnabled = !loading;
    }
}
