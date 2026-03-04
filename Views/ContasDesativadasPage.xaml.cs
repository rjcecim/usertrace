using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using UserTrace.Models;
using UserTrace.Services;

namespace UserTrace.Views;

public sealed partial class ContasDesativadasPage : Page
{
    private CancellationTokenSource? _cts;

    public ContasDesativadasPage()
    {
        InitializeComponent();
        Loaded += (_, _) => _ = ExecutarBuscaAsync();
    }

    private async void AtualizarButton_Click(object sender, RoutedEventArgs e) =>
        await ExecutarBuscaAsync();

    private void ResultadoListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ResultadoListView.SelectedItem is SearchResultItem item && !string.IsNullOrEmpty(item.SamAccountName))
        {
            Frame.Navigate(typeof(LoginPage), item.SamAccountName, new EntranceNavigationTransitionInfo());
        }
    }

    private async Task ExecutarBuscaAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        SetLoading(true);
        ResultadoListView.ItemsSource = null;
        ContadorTextBlock.Text = "Buscando…";

        try
        {
            var list = await ActiveDirectorySearchService.GetDisabledAccountsAsync(ct);
            ResultadoListView.ItemsSource = list;
            ContadorTextBlock.Text = list.Count == 0
                ? "Nenhuma conta desativada."
                : list.Count == 1 ? "1 conta desativada." : $"{list.Count} contas desativadas.";
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
        AtualizarButton.IsEnabled = !loading;
    }
}
