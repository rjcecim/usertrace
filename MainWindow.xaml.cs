using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using UserTrace.Views;
using Windows.UI;
using WinUIEx;

namespace UserTrace;

public sealed partial class MainWindow : WindowEx
{
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

        NavView.SelectedItem = NavItemLogin;
        ContentFrame.Navigate(typeof(LoginPage));

        ContentFrame.Navigated += ContentFrame_Navigated;
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
                EnableTextSelectionInPage(page);
                // Segunda passagem para itens de lista virtualizados (ListView, etc.).
                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                    EnableTextSelectionInPage(page));
            }

            page.Loaded += OnPageLoaded;
            // Se já estiver carregada (ex.: primeira página), executa logo.
            if (page.IsLoaded)
                OnPageLoaded(null, null!);
            else
                EnableTextSelectionInPage(page);
        }

        // Menu lateral (Busca por Login, Nome, Grupo, Senhas Expiradas, etc.) e ícones Unicode
        // permanecem sem seleção — só o conteúdo das páginas (detalhes do usuário, listas) pode ser copiado.
    }

    /// <summary>
    /// Percorre a árvore visual e ativa IsTextSelectionEnabled em todos os TextBlocks
    /// (exceto os que estão dentro de botões), permitindo copiar com Ctrl+C ou botão direito.
    /// </summary>
    private static void EnableTextSelectionInPage(DependencyObject? root)
    {
        if (root == null) return;
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock tb && !IsInsideButton(tb))
                tb.IsTextSelectionEnabled = true;
            EnableTextSelectionInPage(child);
        }
    }

    private static bool IsInsideButton(DependencyObject? element)
    {
        for (var parent = VisualTreeHelper.GetParent(element); parent != null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is ButtonBase)
                return true;
        return false;
    }

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
        if (args.SelectedItem is not NavigationViewItem item) return;

        var tag = item.Tag?.ToString();
        var pageType = tag switch
            {
                "Login"             => typeof(LoginPage),
                "Nome"              => typeof(NomePage),
                "Grupo"             => typeof(GrupoPage),
                "SenhasExpiradas"   => typeof(SenhasExpiradasPage),
                "ContasBloqueadas"  => typeof(ContasBloqueadasPage),
                "ContasDesativadas" => typeof(ContasDesativadasPage),
                "Sobre"             => typeof(SobrePage),
                _                   => typeof(LoginPage)
            };

        if (ContentFrame.Content?.GetType() == pageType)
            return;

        // MICA BEST PRACTICE #7 — Transição de navegação suave.
        // EntranceNavigationTransitionInfo desliza o conteúdo de baixo para cima,
        // revelando o Mica progressivamente — sensação de profundidade real.
        ContentFrame.Navigate(pageType, null,
            new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
    }
}
