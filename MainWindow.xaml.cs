using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using UserTrace.Helpers;
using UserTrace.Models;
using UserTrace.Services;
using UserTrace.Views;
using Windows.UI;
using Windows.UI.Core;
using WinUIEx;

namespace UserTrace;

public sealed partial class MainWindow : WindowEx
{
    private bool _suppressNavSelectionChanged;
    private DispatcherQueueTimer? _searchDebounce;
    private CancellationTokenSource? _searchCts;

    public MainWindow()
    {
        InitializeComponent();

        Title = "UserTrace";
        this.SetWindowSize(1360, 860);
        this.CenterOnScreen();
        AppWindow.SetIcon(@"Assets\icon.ico");
        VersionText.Text = VersaoProduto();

        // MICA BEST PRACTICE #4 — ExtendsContentIntoTitleBar = true
        // faz o conteúdo subir para trás da TitleBar, permitindo que o Mica
        // apareça de forma contínua da barra de título até o rodapé.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarHost);
        NavView.PaneOpening += (_, _) => TitleBarPaneColumn.Width = new GridLength(NavView.OpenPaneLength);
        NavView.PaneClosing += (_, _) => TitleBarPaneColumn.Width = new GridLength(NavView.CompactPaneLength);

        ConfigureTitleBar();
        TrySetMicaBackdrop();
        _ = ProbeDirectoryAsync();

        NavView.SelectedItem = NavItemDashboard;
        // Precisa vir antes do Navigate inicial: senão a primeira página não dispara o handler
        // e TextBlocks com estilo (ex.: PageDescriptionStyle) ficam copiáveis até a próxima navegação.
        ContentFrame.Navigated += ContentFrame_Navigated;
        ContentFrame.Navigate(typeof(DashboardPage));
    }

    public void NavigateToMenu(string menuTag, object? parameter = null)
    {
        var pageType = menuTag switch
        {
            "Dashboard"         => typeof(DashboardPage),
            "Login"             => typeof(LoginPage),
            "Nome"              => typeof(NomePage),
            "Grupo"             => typeof(GrupoPage),
            "Setor"             => typeof(SetorPage),
            "SenhasExpiradas"   => typeof(SenhasExpiradasPage),
            "ContasBloqueadas"  => typeof(ContasBloqueadasPage),
            "ContasDesativadas" => typeof(ContasDesativadasPage),
            "Sobre"             => typeof(SobrePage),
            _                   => typeof(LoginPage)
        };

        var item = menuTag switch
        {
            "Dashboard"         => NavItemDashboard,
            "Login"             => NavItemLogin,
            "Nome"              => NavItemNome,
            "Grupo"             => NavItemGrupo,
            "Setor"             => NavItemSetor,
            "SenhasExpiradas"   => NavItemSenhasExpiradas,
            "ContasBloqueadas"  => NavItemContasBloqueadas,
            "ContasDesativadas" => NavItemContasDesativadas,
            "Sobre"             => NavItemSobre,
            _                   => NavItemLogin
        };

        _suppressNavSelectionChanged = true;
        NavView.SelectedItem = item;
        _suppressNavSelectionChanged = false;

        if (ContentFrame.Content?.GetType() == pageType && parameter is null)
            return;

        ContentFrame.Navigate(pageType, parameter, new EntranceNavigationTransitionInfo());
    }

    private void ContentFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        if (e.Content is LoginPage && e.Parameter != null)
            NavView.SelectedItem = NavItemLogin;

        var page = e.Content as FrameworkElement;
        if (page != null)
        {
            // Executa assim que a página estiver carregada na árvore visual.
            void OnPageLoaded(object? s, RoutedEventArgs _)
            {
                page.Loaded -= OnPageLoaded;
                ApplyCopyPolicyToPage(page);
                // Segunda passagem: conteúdo materializado após layout (ListView virtualizado, etc.).
                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                    ApplyCopyPolicyToPage(page));
            }

            page.Loaded += OnPageLoaded;
            // Se já estiver carregada (ex.: primeira página), executa logo.
            if (page.IsLoaded)
                OnPageLoaded(null, null!);
            else
                ApplyCopyPolicyToPage(page);
        }
    }

    /// <summary>
    /// Desativa seleção em <see cref="TextBlock"/> fora do <see cref="UserInfoPanel"/>.
    /// </summary>
    private static void ApplyCopyPolicyToPage(DependencyObject? pageRoot) =>
        TextSelectionHelper.DisableTextBlocksOutsideUserInfoPanel(pageRoot);

    private void TrySetMicaBackdrop()
    {
        if (MicaController.IsSupported())
        {
            // MICA BEST PRACTICE #5 — MicaKind.Base para janela principal.
            // MicaKind.BaseAlt (mais opaco) é recomendado apenas para
            // painéis secundários ou quando há muito conteúdo sobre o fundo.
            SystemBackdrop = new MicaBackdrop
            {
                Kind = MicaKind.Base
            };
        }
        // Fallback automático: sem Mica, o WinUI usa AcrylicBackdrop ou cor sólida do tema.
    }

    private void ConfigureTitleBar()
    {
        // MICA BEST PRACTICE #6 — TitleBar com botões transparentes.
        // Sem isso, os botões Minimizar/Maximizar/Fechar ficam com fundo branco/cinza
        // quebrando a continuidade visual do Mica.
        if (AppWindow.TitleBar.ExtendsContentIntoTitleBar)
        {
            var titleBar = AppWindow.TitleBar;

            // Botões de controle: fundo transparente, ícone adaptado ao tema
            titleBar.ButtonBackgroundColor         = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonHoverBackgroundColor    = Color.FromArgb(20, 128, 128, 128);
            titleBar.ButtonPressedBackgroundColor  = Color.FromArgb(40, 128, 128, 128);

            // Foreground dos botões acompanha o tema (claro/escuro)
            titleBar.ButtonForegroundColor         = null; // herda do sistema
            titleBar.ButtonInactiveForegroundColor = null;
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_suppressNavSelectionChanged) return;
        if (args.SelectedItem is not NavigationViewItem item) return;

        var tag = item.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(tag)) return;
        NavigateToMenu(tag, null);
    }

    public void SetDirectoryConnection(bool connected)
    {
        if (connected)
        {
            var green = Color.FromArgb(255, 22, 163, 74);
            ConnectionDot.Fill = new SolidColorBrush(green);
            ConnectionText.Text = "Conectado ao AD";
            ConnectionText.Foreground = new SolidColorBrush(green);
            return;
        }

        var red = Color.FromArgb(255, 220, 38, 38);
        ConnectionDot.Fill = new SolidColorBrush(red);
        ConnectionText.Text = "Sem conexão com o AD";
        ConnectionText.Foreground = new SolidColorBrush(red);
    }

    private async Task ProbeDirectoryAsync()
    {
        try
        {
            await Task.Run(LdapDirectory.Probe);
            SetDirectoryConnection(true);
        }
        catch
        {
            SetDirectoryConnection(false);
        }
    }

    private static string VersaoProduto()
    {
        var version = typeof(App).Assembly.GetName().Version;
        return version is null
            ? "UserTrace"
            : $"UserTrace v{version.Major}.{version.Minor}.{version.Build}";
    }

    private void EnsureSearchDebounce()
    {
        if (_searchDebounce is not null) return;
        _searchDebounce = DispatcherQueue.CreateTimer();
        _searchDebounce.Interval = TimeSpan.FromMilliseconds(300);
        _searchDebounce.IsRepeating = false;
        _searchDebounce.Tick += async (_, _) => await RunGlobalSearchAsync();
    }

    private void GlobalSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            return;

        EnsureSearchDebounce();
        _searchDebounce!.Stop();
        if (sender.Text.Trim().Length < 2)
        {
            _searchCts?.Cancel();
            sender.ItemsSource = null;
            sender.IsSuggestionListOpen = false;
            return;
        }

        _searchDebounce.Start();
    }

    private async Task RunGlobalSearchAsync()
    {
        var term = GlobalSearchBox.Text.Trim();
        if (term.Length < 2) return;

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        try
        {
            var hits = await ActiveDirectorySearchService.SearchGlobalAsync(term, ct);
            if (ct.IsCancellationRequested) return;
            if (!string.Equals(GlobalSearchBox.Text.Trim(), term, StringComparison.Ordinal)) return;

            GlobalSearchBox.ItemsSource = hits.Count == 0
                ? new[] { GlobalSearchHit.None }
                : hits.ToArray();
            GlobalSearchBox.IsSuggestionListOpen = true;
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            GlobalSearchBox.ItemsSource = new[] { GlobalSearchHit.None };
            GlobalSearchBox.IsSuggestionListOpen = true;
        }
    }

    private void GlobalSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is GlobalSearchHit hit && !string.IsNullOrEmpty(hit.MenuTag))
            AbrirBusca(hit);
    }

    private void GlobalSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is GlobalSearchHit chosen && !string.IsNullOrEmpty(chosen.MenuTag))
        {
            AbrirBusca(chosen);
            return;
        }

        if (sender.ItemsSource is IEnumerable<GlobalSearchHit> hits)
        {
            var first = hits.FirstOrDefault(hit => !string.IsNullOrEmpty(hit.MenuTag));
            if (first is not null)
            {
                AbrirBusca(first);
                return;
            }
        }

        var text = sender.Text.Trim();
        if (text.Length == 0 || text.Contains(' '))
            return;

        AbrirBusca(new GlobalSearchHit
        {
            Kind = "Usuário",
            Title = text,
            MenuTag = "Login",
            Value = text
        });
    }

    private void AbrirBusca(GlobalSearchHit hit)
    {
        _searchCts?.Cancel();
        GlobalSearchBox.Text = string.Empty;
        GlobalSearchBox.ItemsSource = null;
        GlobalSearchBox.IsSuggestionListOpen = false;
        NavigateToMenu(hit.MenuTag, hit.Value);
    }

    private void Root_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        if (ctrl.HasFlag(CoreVirtualKeyStates.Down) && e.Key == Windows.System.VirtualKey.K)
        {
            GlobalSearchBox.Focus(FocusState.Programmatic);
            e.Handled = true;
            return;
        }

        if (e.Key != Windows.System.VirtualKey.Escape) return;

        if (!string.IsNullOrEmpty(GlobalSearchBox.Text) || GlobalSearchBox.IsSuggestionListOpen)
        {
            _searchCts?.Cancel();
            GlobalSearchBox.Text = string.Empty;
            GlobalSearchBox.ItemsSource = null;
            GlobalSearchBox.IsSuggestionListOpen = false;
            e.Handled = true;
            return;
        }

        if (ContentFrame.Content is DashboardPage) return;
        if (HasOpenOverlay()) return;

        NavigateToMenu("Dashboard");
        e.Handled = true;
    }

    private bool HasOpenOverlay()
    {
        var xamlRoot = Content?.XamlRoot;
        if (xamlRoot is null) return false;

        return VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot).Count > 0;
    }
}
