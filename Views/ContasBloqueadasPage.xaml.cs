using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UserTrace.Helpers;
using UserTrace.Models;
using UserTrace.Services;

namespace UserTrace.Views;

public sealed partial class ContasBloqueadasPage : Page
{
    private CancellationTokenSource? _cts;

    public ContasBloqueadasPage()
    {
        InitializeComponent();
        Loaded += (_, _) => _ = ExecutarBuscaAsync();
    }

    private async void AtualizarButton_Click(object sender, RoutedEventArgs e) =>
        await ExecutarBuscaAsync();

    private void ResultadoListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ResultadoListView.SelectedItem is SearchResultItem item)
            Frame.NavigateToLoginWithSam(item.SamAccountName);
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
            var list = await ActiveDirectorySearchService.GetLockedOutAccountsAsync(ct);
            ResultadoListView.ItemsSource = list;
            ContadorTextBlock.Text = ContagemPt.Texto(
                list.Count,
                "Nenhuma conta bloqueada.",
                "1 conta bloqueada.",
                "{0} contas bloqueadas.");
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
