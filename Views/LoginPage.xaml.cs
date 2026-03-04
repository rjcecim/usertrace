using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using UserTrace.Services;

namespace UserTrace.Views;

public sealed partial class LoginPage : Page
{
    private CancellationTokenSource? _cts;

    public LoginPage()
    {
        InitializeComponent();
        InfoPanel.ShowEmpty();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string login && !string.IsNullOrWhiteSpace(login))
        {
            LoginTextBox.Text = login.Trim();
            _ = ExecutarBuscaAsync();
        }
    }

    private async void BuscarButton_Click(object sender, RoutedEventArgs e) =>
        await ExecutarBuscaAsync();

    private async void LoginTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            await ExecutarBuscaAsync();
    }

    private void LimparButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        LoginTextBox.Text = string.Empty;
        InfoPanel.ShowEmpty();
        SetLoading(false);
    }

    private async Task ExecutarBuscaAsync()
    {
        var sam = LoginTextBox.Text.Trim();
        if (string.IsNullOrEmpty(sam)) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        SetLoading(true);

        try
        {
            var result = await NetUserService.GetUserDetailsAsync(sam, ct);

            if (result.Success && result.UserInfo is not null)
                InfoPanel.ShowUser(result.UserInfo);
            else
                InfoPanel.ShowEmpty(result.Error ?? "Erro na consulta.");
        }
        catch (OperationCanceledException)
        {
            InfoPanel.ShowEmpty("Busca cancelada.");
        }
        catch (Exception ex)
        {
            InfoPanel.ShowEmpty($"Erro: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool loading)
    {
        LoadingRing.IsActive    = loading;
        LoadingRing.Visibility  = loading ? Visibility.Visible : Visibility.Collapsed;
        InfoPanel.Visibility    = loading ? Visibility.Collapsed : Visibility.Visible;
        BuscarButton.IsEnabled  = !loading;
    }
}
