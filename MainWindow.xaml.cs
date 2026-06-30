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
using UserTrace.Views;
using Windows.UI;
using WinUIEx;

namespace UserTrace;

public sealed partial class MainWindow : WindowEx
{
    private bool _suppressNavSelectionChanged;

    public MainWindow()
    {
        InitializeComponent();

        Title = "UserTrace";
        this.SetWindowSize(1160, 740);
        this.CenterOnScreen();
        AppWindow.SetIcon(@"Assets\app.ico");

        // MICA BEST PRACTICE #4 — ExtendsContentIntoTitleBar = true
        // faz o conteúdo subir para trás da TitleBar, permitindo que o Mica
        // apareça de forma contínua da barra de título até o rodapé.
        ExtendsContentIntoTitleBar = true;

        ConfigureTitleBar();
        TrySetMicaBackdrop();

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

    private void Root_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Escape) return;
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
