using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UserTrace.Models;

namespace UserTrace.Views;

public sealed partial class UserInfoPanel : UserControl
{
    public UserInfoPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Só os valores dos cards Identidade, Status da conta, Senha e Logon podem ser selecionados para copiar.
    /// Rótulos (ex.: "Definida pela última vez"), títulos dos cards e listas de grupos não são selecionáveis.
    /// </summary>
    private void ApplyCopySelectionRules()
    {
        SetTextSelectionInSubtree(ContentPanel, enabled: false);
        foreach (var tb in CopyableResultTextBlocks())
            tb.IsTextSelectionEnabled = true;

        // Itens de lista (grupos) podem ser materializados após o layout; segunda passagem mantém rótulos desligados.
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            SetTextSelectionInSubtree(ContentPanel, enabled: false);
            foreach (var tb in CopyableResultTextBlocks())
                tb.IsTextSelectionEnabled = true;
        });
    }

    private static void SetTextSelectionInSubtree(DependencyObject? root, bool enabled)
    {
        if (root == null) return;
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock tb)
                tb.IsTextSelectionEnabled = enabled;
            SetTextSelectionInSubtree(child, enabled);
        }
    }

    /// <summary>TextBlocks que exibem apenas o resultado (valor), não o rótulo do campo.</summary>
    private IEnumerable<TextBlock> CopyableResultTextBlocks()
    {
        // Identidade
        yield return FullNameText;
        yield return SamText;
        yield return DomainText;

        // Status da conta
        yield return ContaAtivaText;
        yield return ContaExpiraText;
        yield return SenhaExpiradaText;
        yield return SmartcardText;

        // Senha
        yield return SenhaDefinidaText;
        yield return SenhaExpiraText;
        yield return SenhaAlteravelText;
        yield return SenhaObrigText;

        // Logon
        yield return UltimoLogonText;
        yield return UltimoLogoffText;
        yield return EstacoesText;
        yield return ScriptText;
        yield return PerfilText;
        yield return HomeDirText;
    }

    public void ShowEmpty(string message = "Informe um login e clique em Buscar.")
    {
        EmptyStateText.Text     = message;
        EmptyStateText.IsTextSelectionEnabled = false;
        EmptyState.Visibility   = Visibility.Visible;
        ContentPanel.Visibility = Visibility.Collapsed;
    }

    public void ShowUser(UserInfo u)
    {
        // Identidade
        FullNameText.Text = string.IsNullOrWhiteSpace(u.FullName) ? u.SamAccountName : u.FullName;
        SamText.Text      = u.SamAccountName;
        DomainText.Text   = u.Domain;

        // Status da conta
        SetBadge(ContaAtivaBadge, ContaAtivaIcon, ContaAtivaText,
            u.AccountActive, "Ativa", "Desabilitada",
            positiveGood: true);

        ContaExpiraText.Text = u.AccountExpires;

        SetBadge(SenhaExpiradaBadge, SenhaExpiradaIcon, SenhaExpiradaText,
            u.PasswordExpired, "Expirada", "Válida",
            positiveGood: false);

        SetBadge(SmartcardBadge, SmartcardIcon, SmartcardText,
            u.SmartcardRequired, "Obrigatório", "Não requerido",
            positiveGood: false);

        // Senha
        SenhaDefinidaText.Text = u.PasswordLastSet;
        SenhaExpiraText.Text   = u.PasswordNeverExpires ? "Nunca" : "Conforme política";

        SetBadge(SenhaAlteravelBadge, SenhaAlteravelIcon, SenhaAlteravelText,
            u.PasswordChangeable, "Permitida", "Bloqueada",
            positiveGood: true);

        SetBadge(SenhaObrigBadge, SenhaObrigIcon, SenhaObrigText,
            u.PasswordRequired, "Obrigatória", "Não obrigatória",
            positiveGood: true);

        // Logon
        UltimoLogonText.Text  = u.LastLogon;
        UltimoLogoffText.Text = u.LastLogoff;
        EstacoesText.Text     = u.Workstations;
        ScriptText.Text       = string.IsNullOrEmpty(u.LogonScript)   ? "—" : u.LogonScript;
        PerfilText.Text       = string.IsNullOrEmpty(u.ProfilePath)   ? "—" : u.ProfilePath;
        HomeDirText.Text      = string.IsNullOrEmpty(u.HomeDirectory) ? "—" : u.HomeDirectory;

        // Grupos locais
        LocalGroupsList.ItemsSource = u.LocalGroups.Count > 0
            ? u.LocalGroups
            : (IEnumerable<string>)["(nenhum)"];
        LocalGroupCountText.Text = u.LocalGroups.Count.ToString();

        // Grupos globais
        GlobalGroupsList.ItemsSource = u.GlobalGroups.Count > 0
            ? u.GlobalGroups
            : (IEnumerable<string>)["(nenhum)"];
        GlobalGroupCountText.Text = u.GlobalGroups.Count.ToString();

        EmptyState.Visibility   = Visibility.Collapsed;
        ContentPanel.Visibility = Visibility.Visible;

        ApplyCopySelectionRules();
    }

    /// <summary>
    /// Aplica estilo de badge (verde/vermelho) e ícone conforme o valor booleano.
    /// positiveGood=true → true é verde; positiveGood=false → true é vermelho.
    /// </summary>
    private void SetBadge(
        Border badge, FontIcon icon, TextBlock label,
        bool value, string trueText, string falseText,
        bool positiveGood)
    {
        bool isGood = positiveGood ? value : !value;

        label.Text  = value ? trueText : falseText;
        icon.Glyph  = isGood ? "\uE73E" : "\uE711"; // CheckMark / Cancel

        if (isGood)
        {
            badge.Background   = (Brush)Application.Current.Resources["SystemFillColorSuccessBackgroundBrush"];
            badge.BorderBrush  = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
            icon.Foreground    = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
        }
        else
        {
            badge.Background   = (Brush)Application.Current.Resources["SystemFillColorCriticalBackgroundBrush"];
            badge.BorderBrush  = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
            icon.Foreground    = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        }

        badge.BorderThickness = new Thickness(1);
        badge.CornerRadius    = new CornerRadius(4);
        badge.Padding         = new Thickness(8, 2, 8, 2);
    }
}
