using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UserTrace.Helpers;
using UserTrace.Models;
using UserTrace.Services;

namespace UserTrace.Views;

public sealed partial class NomePage : Page
{
    private CancellationTokenSource? _ctsBusca;
    private CancellationTokenSource? _ctsDetalhes;
    private List<SearchResultItem> _resultados = [];

    public NomePage()
    {
        InitializeComponent();
        DetalhesPanel.ShowEmpty("Selecione um usuário na lista para ver os detalhes.");
    }

    private async void BuscarButton_Click(object sender, RoutedEventArgs e) =>
        await ExecutarBuscaAsync();

    private async void NomeTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            await ExecutarBuscaAsync();
    }

    private void LimparButton_Click(object sender, RoutedEventArgs e)
    {
        _ctsBusca?.Cancel();
        _ctsDetalhes?.Cancel();
        NomeTextBox.Text              = string.Empty;
        ResultadoListView.ItemsSource = null;
        ContadorTextBlock.Text        = "Aguardando busca...";
        DetalhesPanel.ShowEmpty("Selecione um usuário na lista para ver os detalhes.");
        SetListaLoading(false);
        SetDetalhesLoading(false);
    }

    private async Task ExecutarBuscaAsync()
    {
        var termo = NomeTextBox.Text.Trim();
        if (string.IsNullOrEmpty(termo)) return;

        _ctsBusca?.Cancel();
        _ctsBusca = new CancellationTokenSource();
        var ct = _ctsBusca.Token;

        SetListaLoading(true);
        ResultadoListView.ItemsSource = null;
        ContadorTextBlock.Text        = "Buscando…";
        DetalhesPanel.ShowEmpty("Selecione um usuário na lista para ver os detalhes.");

        try
        {
            var items = await ActiveDirectorySearchService.SearchByNameAsync(termo, ct);

            _resultados = items;
            ResultadoListView.ItemsSource = items;
            ContadorTextBlock.Text = ContagemPt.Texto(
                items.Count,
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
            SetListaLoading(false);
        }
    }

    private async void ResultadoListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultadoListView.SelectedItem is not SearchResultItem item) return;

        _ctsDetalhes?.Cancel();
        _ctsDetalhes = new CancellationTokenSource();
        var ct = _ctsDetalhes.Token;

        SetDetalhesLoading(true);

        try
        {
            var result = await NetUserService.GetUserDetailsAsync(item.SamAccountName, ct);

            if (result.Success && result.UserInfo is not null)
                DetalhesPanel.ShowUser(result.UserInfo);
            else
                DetalhesPanel.ShowEmpty($"Erro: {result.Error}");
        }
        catch (OperationCanceledException)
        {
            DetalhesPanel.ShowEmpty("Busca cancelada.");
        }
        catch (Exception ex)
        {
            DetalhesPanel.ShowEmpty($"Erro: {ex.Message}");
        }
        finally
        {
            SetDetalhesLoading(false);
        }
    }

    private void SetListaLoading(bool loading)
    {
        ListaLoadingRing.IsActive   = loading;
        ListaLoadingRing.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        ListaPanel.Visibility       = loading ? Visibility.Collapsed : Visibility.Visible;
        BuscarButton.IsEnabled      = !loading;
    }

    private void SetDetalhesLoading(bool loading)
    {
        DetalhesLoadingRing.IsActive   = loading;
        DetalhesLoadingRing.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        DetalhesPanel.Visibility       = loading ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ExportItem_Click(object sender, RoutedEventArgs e) =>
        ExportHelper.IniciarExportacaoMenu(this, sender, ExportarAsync);

    private async Task ExportarAsync(string formato)
    {
        if (App.CurrentWindow is null)
            throw new InvalidOperationException("Janela principal indisponível.");

        var termo = NomeTextBox.Text.Trim();
        var nomeBase = $"busca_nome_{Sanitize(termo)}_{DateTime.Now:yyyyMMdd_HHmm}";
        await ExportHelper.ExportarListaDiretoAsync("Busca por Nome", _resultados, nomeBase, formato, XamlRoot, App.CurrentWindow);
    }

    private static string Sanitize(string s) =>
        string.Concat(s.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
