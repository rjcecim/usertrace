using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UserTrace.Helpers;
using UserTrace.Models;
using UserTrace.Services;
using DomainGroupItem = UserTrace.Models.GroupItem;

namespace UserTrace.Views;

public sealed partial class GrupoPage : Page
{
    private CancellationTokenSource? _ctsGrupos;
    private CancellationTokenSource? _ctsMembros;
    private CancellationTokenSource? _ctsDetalhes;

    public GrupoPage()
    {
        InitializeComponent();
        DetalhesPanel.ShowEmpty("Selecione um membro para ver os detalhes.");
        Loaded += (_, _) => _ = ListarGruposAsync();
    }

    // ── Eventos de UI ────────────────────────────────────────────────────────

    private async void ListarButton_Click(object sender, RoutedEventArgs e) =>
        await ListarGruposAsync();

    private async void FiltroTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            await ListarGruposAsync();
    }

    private void LimparButton_Click(object sender, RoutedEventArgs e)
    {
        _ctsGrupos?.Cancel();
        _ctsMembros?.Cancel();
        _ctsDetalhes?.Cancel();

        FiltroTextBox.Text             = string.Empty;
        GruposListView.ItemsSource     = null;
        MembrosListView.ItemsSource    = null;
        GruposContadorText.Text        = "Carregando…";
        MembrosContadorText.Text       = "Selecione um grupo.";
        GrupoSelecionadoText.Text      = string.Empty;
        GrupoSelecionadoText.Visibility = Visibility.Collapsed;

        DetalhesPanel.ShowEmpty("Selecione um membro para ver os detalhes.");
        SetGruposLoading(false);
        SetMembrosLoading(false);
        SetDetalhesLoading(false);

        _ = ListarGruposAsync();
    }

    private async void GruposListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GruposListView.SelectedItem is not DomainGroupItem grupo) return;
        await CarregarMembrosAsync(grupo);
    }

    private async void MembrosListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MembrosListView.SelectedItem is not SearchResultItem membro) return;
        await CarregarDetalhesAsync(membro);
    }

    // ── Lógica principal ─────────────────────────────────────────────────────

    private async Task ListarGruposAsync()
    {
        _ctsGrupos?.Cancel();
        _ctsGrupos = new CancellationTokenSource();
        var ct = _ctsGrupos.Token;

        var filtro = FiltroTextBox.Text.Trim();

        SetGruposLoading(true);
        GruposListView.ItemsSource  = null;
        MembrosListView.ItemsSource = null;
        GruposContadorText.Text     = "Buscando grupos…";
        MembrosContadorText.Text    = "Selecione um grupo.";
        GrupoSelecionadoText.Visibility = Visibility.Collapsed;
        DetalhesPanel.ShowEmpty("Selecione um membro para ver os detalhes.");

        try
        {
            var grupos = await GroupService.GetAllGroupsAsync(filtro, ct);

            GruposListView.ItemsSource = grupos;
            GruposContadorText.Text = ContagemPt.Texto(
                grupos.Count,
                "Nenhum grupo encontrado.",
                "1 grupo encontrado.",
                "{0} grupos encontrados.");
        }
        catch (OperationCanceledException)
        {
            GruposContadorText.Text = "Busca cancelada.";
        }
        catch (Exception ex)
        {
            GruposContadorText.Text = $"Erro: {ex.Message}";
        }
        finally
        {
            SetGruposLoading(false);
        }
    }

    private async Task CarregarMembrosAsync(DomainGroupItem grupo)
    {
        _ctsMembros?.Cancel();
        _ctsMembros = new CancellationTokenSource();
        var ct = _ctsMembros.Token;

        SetMembrosLoading(true);
        MembrosListView.ItemsSource = null;
        MembrosContadorText.Text    = "Carregando membros…";
        GrupoSelecionadoText.Text   = grupo.Name;
        GrupoSelecionadoText.Visibility = Visibility.Visible;
        DetalhesPanel.ShowEmpty("Selecione um membro para ver os detalhes.");

        try
        {
            var membros = await GroupService.GetGroupMembersAsync(grupo.Name, ct);

            MembrosListView.ItemsSource = membros;
            MembrosContadorText.Text = ContagemPt.Texto(
                membros.Count,
                "Nenhum membro encontrado.",
                "1 membro encontrado.",
                "{0} membros encontrados.");
        }
        catch (OperationCanceledException)
        {
            MembrosContadorText.Text = "Busca cancelada.";
        }
        catch (Exception ex)
        {
            MembrosContadorText.Text = $"Erro: {ex.Message}";
        }
        finally
        {
            SetMembrosLoading(false);
        }
    }

    private async Task CarregarDetalhesAsync(SearchResultItem membro)
    {
        _ctsDetalhes?.Cancel();
        _ctsDetalhes = new CancellationTokenSource();
        var ct = _ctsDetalhes.Token;

        SetDetalhesLoading(true);

        try
        {
            var result = await NetUserService.GetUserDetailsAsync(membro.SamAccountName, ct);

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

    // ── Helpers de loading ───────────────────────────────────────────────────

    private void SetGruposLoading(bool loading)
    {
        GruposLoadingRing.IsActive   = loading;
        GruposLoadingRing.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        GruposPanel.Visibility       = loading ? Visibility.Collapsed : Visibility.Visible;
        ListarButton.IsEnabled       = !loading;
    }

    private void SetMembrosLoading(bool loading)
    {
        MembrosLoadingRing.IsActive   = loading;
        MembrosLoadingRing.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        MembrosPanel.Visibility       = loading ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetDetalhesLoading(bool loading)
    {
        DetalhesLoadingRing.IsActive   = loading;
        DetalhesLoadingRing.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        DetalhesPanel.Visibility       = loading ? Visibility.Collapsed : Visibility.Visible;
    }
}
