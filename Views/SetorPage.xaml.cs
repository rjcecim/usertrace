using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UserTrace.Helpers;
using UserTrace.Models;
using UserTrace.Services;

namespace UserTrace.Views;

public sealed partial class SetorPage : Page
{
    private List<string> _todosSetores = [];
    private CancellationTokenSource? _ctsSetores;
    private CancellationTokenSource? _ctsUsuarios;
    private CancellationTokenSource? _ctsDetalhes;

    public SetorPage()
    {
        InitializeComponent();
        DetalhesPanel.ShowEmpty("Selecione um usuário para ver os detalhes.");
        Loaded += (_, _) => _ = CarregarSetoresAsync();
    }

    private void FiltrarButton_Click(object sender, RoutedEventArgs e) => AplicarFiltroSetores();

    private void LimparFiltroButton_Click(object sender, RoutedEventArgs e)
    {
        FiltroSetorTextBox.Text = string.Empty;
        AplicarFiltroSetores();
    }

    private void FiltroSetorTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            AplicarFiltroSetores();
    }

    private async Task CarregarSetoresAsync()
    {
        _ctsSetores?.Cancel();
        _ctsSetores = new CancellationTokenSource();
        var ct = _ctsSetores.Token;

        SetSetoresLoading(true);
        SetoresListView.ItemsSource = null;
        UsuariosListView.ItemsSource = null;
        SetoresContadorText.Text = "Buscando setores…";
        UsuariosContadorText.Text = "Selecione um setor.";
        SetorSelecionadoText.Text = string.Empty;
        SetorSelecionadoText.Visibility = Visibility.Collapsed;
        DetalhesPanel.ShowEmpty("Selecione um usuário para ver os detalhes.");

        try
        {
            var setores = await ActiveDirectorySearchService.GetAllOfficesAsync(ct);

            _todosSetores = setores;
            AplicarFiltroSetores();
        }
        catch (OperationCanceledException)
        {
            SetoresContadorText.Text = "Busca cancelada.";
        }
        catch (Exception ex)
        {
            SetoresContadorText.Text = $"Erro: {ex.Message}";
        }
        finally
        {
            SetSetoresLoading(false);
        }
    }

    private void AplicarFiltroSetores()
    {
        var filtro = FiltroSetorTextBox.Text.Trim();
        var setoresFiltrados = string.IsNullOrWhiteSpace(filtro)
            ? _todosSetores
            : _todosSetores
                .Where(x => x.Contains(filtro, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

        var setorSelecionado = SetoresListView.SelectedItem as string;
        SetoresListView.ItemsSource = setoresFiltrados;
        SetoresContadorText.Text = ContagemPt.Texto(
            setoresFiltrados.Count,
            "Nenhum setor encontrado.",
            "1 setor encontrado.",
            "{0} setores encontrados.");

        if (string.IsNullOrWhiteSpace(setorSelecionado) || !setoresFiltrados.Contains(setorSelecionado))
        {
            SetoresListView.SelectedItem = null;
            UsuariosListView.ItemsSource = null;
            UsuariosContadorText.Text = "Selecione um setor.";
            SetorSelecionadoText.Text = string.Empty;
            SetorSelecionadoText.Visibility = Visibility.Collapsed;
            DetalhesPanel.ShowEmpty("Selecione um usuário para ver os detalhes.");
        }
    }

    private async void SetoresListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SetoresListView.SelectedItem is not string setor || string.IsNullOrWhiteSpace(setor))
            return;

        await CarregarUsuariosDoSetorAsync(setor);
    }

    private async Task CarregarUsuariosDoSetorAsync(string setor)
    {
        _ctsUsuarios?.Cancel();
        _ctsUsuarios = new CancellationTokenSource();
        var ct = _ctsUsuarios.Token;

        SetUsuariosLoading(true);
        UsuariosListView.ItemsSource = null;
        UsuariosContadorText.Text = "Carregando usuários…";
        SetorSelecionadoText.Text = setor;
        SetorSelecionadoText.Visibility = Visibility.Visible;
        DetalhesPanel.ShowEmpty("Selecione um usuário para ver os detalhes.");

        try
        {
            var usuarios = await ActiveDirectorySearchService.GetUsersByOfficeAsync(setor, ct);
            UsuariosListView.ItemsSource = usuarios;

            UsuariosContadorText.Text = ContagemPt.Texto(
                usuarios.Count,
                "Nenhum usuário encontrado.",
                "1 usuário encontrado.",
                "{0} usuários encontrados.");
        }
        catch (OperationCanceledException)
        {
            UsuariosContadorText.Text = "Busca cancelada.";
        }
        catch (Exception ex)
        {
            UsuariosContadorText.Text = $"Erro: {ex.Message}";
        }
        finally
        {
            SetUsuariosLoading(false);
        }
    }

    private async void UsuariosListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (UsuariosListView.SelectedItem is not SearchResultItem usuario) return;

        _ctsDetalhes?.Cancel();
        _ctsDetalhes = new CancellationTokenSource();
        var ct = _ctsDetalhes.Token;

        SetDetalhesLoading(true);

        try
        {
            var result = await NetUserService.GetUserDetailsAsync(usuario.SamAccountName, ct);

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

    private void SetSetoresLoading(bool loading)
    {
        SetoresLoadingRing.IsActive = loading;
        SetoresLoadingRing.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        SetoresPanel.Visibility = loading ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetUsuariosLoading(bool loading)
    {
        UsuariosLoadingRing.IsActive = loading;
        UsuariosLoadingRing.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        UsuariosPanel.Visibility = loading ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetDetalhesLoading(bool loading)
    {
        DetalhesLoadingRing.IsActive = loading;
        DetalhesLoadingRing.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        DetalhesPanel.Visibility = loading ? Visibility.Collapsed : Visibility.Visible;
    }
}
