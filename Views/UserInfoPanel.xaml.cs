using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UserTrace.Helpers;
using UserTrace.Models;

namespace UserTrace.Views;

public sealed partial class UserInfoPanel : UserControl
{
    private UserInfo? _currentUser;

    public UserInfoPanel()
    {
        InitializeComponent();
    }

    private void ExportItem_Click(object sender, RoutedEventArgs e) =>
        ExportHelper.IniciarExportacaoMenu(this, sender, ExportarAsync);

    private async Task ExportarAsync(string formato)
    {
        if (_currentUser is null)
        {
            await ExportHelper.MostrarAvisoAsync(XamlRoot, "Não há usuário para exportar. Execute uma busca primeiro.");
            return;
        }

        if (App.CurrentWindow is null)
            throw new InvalidOperationException("Janela principal indisponível.");

        var sam = _currentUser.SamAccountName;
        var nomeBase = $"usuario_{Sanitize(sam)}_{DateTime.Now:yyyyMMdd_HHmm}";
        await ExportHelper.ExportarUsuarioDiretoAsync(_currentUser, formato, nomeBase, XamlRoot, App.CurrentWindow);
    }

    private static string Sanitize(string s) =>
        string.Concat(s.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    /// <summary>
    /// Só os valores dos cards Identidade, Status da conta, Senha e Logon podem ser selecionados para copiar.
    /// Rótulos (ex.: "Definida pela última vez"), títulos dos cards e listas de grupos não são selecionáveis.
    /// </summary>
    private void ApplyCopySelectionRules()
    {
        TextSelectionHelper.SetTextBlockSelectionRecursive(ContentPanel, enabled: false);
        foreach (var tb in CopyableResultTextBlocks())
            tb.IsTextSelectionEnabled = true;

        // Itens de lista (grupos) podem ser materializados após o layout; segunda passagem mantém rótulos desligados.
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            TextSelectionHelper.SetTextBlockSelectionRecursive(ContentPanel, enabled: false);
            foreach (var tb in CopyableResultTextBlocks())
                tb.IsTextSelectionEnabled = true;
        });
    }

    /// <summary>TextBlocks que exibem apenas o resultado (valor), não o rótulo do campo.</summary>
    private IEnumerable<TextBlock> CopyableResultTextBlocks()
    {
        // Identidade
        yield return FullNameText;
        yield return SamText;
        yield return DomainText;
        yield return EmailText;
        yield return PhoneText;
        yield return OfficeText;
        yield return OrganizationalUnitText;

        // Status da conta
        yield return ContaAtivaText;
        yield return ContaExpiraText;
        yield return SenhaExpiradaText;
        yield return SmartcardText;

        // Senha
        yield return SenhaDefinidaText;
        yield return SenhaExpiraText;
        yield return DiasParaExpirarText;
        yield return SenhaAlteravelText;
        yield return SenhaObrigText;
        yield return BadPasswordCountText;
        yield return BadPasswordTimeText;
        yield return LockoutTimeText;

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
        ContentScroll.Visibility = Visibility.Collapsed;
    }

    public void ShowUser(UserInfo u)
    {
        _currentUser = u;

        // Garante que o estado vazio não fique copiável (mesmo escondido).
        EmptyStateText.IsTextSelectionEnabled = false;

        // Identidade
        var nome = string.IsNullOrWhiteSpace(u.FullName) ? u.SamAccountName : u.FullName;
        FullNameText.Text = nome;
        AvatarInitialsText.Text = Iniciais(nome);
        SamText.Text      = u.SamAccountName;
        DomainText.Text   = u.Domain;
        SetValor(EmailText, u.Email);
        SetValor(PhoneText, u.PhoneNumber);
        SetValor(OfficeText, u.Office);
        SetValor(OrganizationalUnitText, u.OrganizationalUnit);

        // Status da conta
        SetBadge(ContaAtivaBadge, ContaAtivaIcon, ContaAtivaText,
            u.AccountActive, "Ativa", "Desabilitada",
            positiveGood: true);

        SetValor(ContaExpiraText, u.AccountExpires);

        SetBadge(SenhaExpiradaBadge, SenhaExpiradaIcon, SenhaExpiradaText,
            u.PasswordExpired, "Expirada", "Válida",
            positiveGood: false);

        SetBadge(SmartcardBadge, SmartcardIcon, SmartcardText,
            u.SmartcardRequired, "Obrigatório", "Não requerido",
            positiveGood: false);

        // Senha
        SetValor(SenhaDefinidaText, u.PasswordLastSet);
        SetValor(SenhaExpiraText, string.IsNullOrWhiteSpace(u.PasswordExpiresOn)
            ? (u.PasswordNeverExpires ? "Nunca" : "Conforme política")
            : u.PasswordExpiresOn);
        if (!int.TryParse(u.PasswordDaysToExpire, out var diasParaExpirar))
        {
            DiasParaExpirarLabel.Text = "Faltam (dias) para expirar";
            SetValor(DiasParaExpirarText, u.PasswordDaysToExpire);
        }
        else if (diasParaExpirar < 0)
        {
            DiasParaExpirarLabel.Text = "Vencida há (dias)";
            SetValor(DiasParaExpirarText, (-diasParaExpirar).ToString());
            DiasParaExpirarText.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        }
        else
        {
            DiasParaExpirarLabel.Text = "Faltam (dias) para expirar";
            SetValor(DiasParaExpirarText, diasParaExpirar.ToString());
            if (diasParaExpirar <= 7)
                DiasParaExpirarText.Foreground = (Brush)Application.Current.Resources["DashboardOrangeBrush"];
        }

        SetBadge(SenhaAlteravelBadge, SenhaAlteravelIcon, SenhaAlteravelText,
            u.PasswordChangeable, "Permitida", "Bloqueada",
            positiveGood: true);

        SetBadge(SenhaObrigBadge, SenhaObrigIcon, SenhaObrigText,
            u.PasswordRequired, "Obrigatória", "Não obrigatória",
            positiveGood: true);

        SetValor(BadPasswordCountText, u.BadPasswordCount);
        SetValor(BadPasswordTimeText, u.BadPasswordTime);
        SetValor(LockoutTimeText, u.LockoutTime);

        // Logon
        SetValor(UltimoLogonText, u.LastLogon);
        SetValor(UltimoLogoffText, u.LastLogoff);
        SetValor(EstacoesText, u.Workstations);
        SetValor(ScriptText, u.LogonScript);
        SetValor(PerfilText, u.ProfilePath);
        SetValor(HomeDirText, u.HomeDirectory);

        // Grupos locais
        var semLocais = u.LocalGroups.Count == 0;
        LocalGroupsList.Visibility = semLocais ? Visibility.Collapsed : Visibility.Visible;
        LocalGroupsEmpty.Visibility = semLocais ? Visibility.Visible : Visibility.Collapsed;
        if (!semLocais)
            LocalGroupsList.ItemsSource = u.LocalGroups;
        LocalGroupCountText.Text = u.LocalGroups.Count.ToString();

        // Grupos globais
        var semGlobais = u.GlobalGroups.Count == 0;
        GlobalGroupsList.Visibility = semGlobais ? Visibility.Collapsed : Visibility.Visible;
        GlobalGroupsEmpty.Visibility = semGlobais ? Visibility.Visible : Visibility.Collapsed;
        if (!semGlobais)
            GlobalGroupsList.ItemsSource = u.GlobalGroups;
        GlobalGroupCountText.Text = u.GlobalGroups.Count.ToString();

        EmptyState.Visibility    = Visibility.Collapsed;
        ContentScroll.Visibility = Visibility.Visible;

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
        icon.Glyph  = isGood ? "\uE73E" : "\uE711";

        var tom = (Brush)Application.Current.Resources[isGood
            ? "SystemFillColorSuccessBrush"
            : "SystemFillColorCriticalBrush"];
        var fundo = (Brush)Application.Current.Resources[isGood
            ? "SystemFillColorSuccessBackgroundBrush"
            : "SystemFillColorCriticalBackgroundBrush"];

        badge.Background = fundo;
        badge.BorderThickness = new Thickness(0);
        badge.CornerRadius = new CornerRadius(999);
        badge.Padding = new Thickness(10, 3, 10, 3);
        badge.HorizontalAlignment = HorizontalAlignment.Left;
        icon.Foreground = tom;
        label.Foreground = tom;
    }

    private static void SetValor(TextBlock campo, string? valor)
    {
        var texto = string.IsNullOrWhiteSpace(valor) ? "—" : valor;
        campo.Text = texto;
        campo.Foreground = (Brush)Application.Current.Resources[texto == "—"
            ? "DashboardMutedBrush"
            : "DashboardTitleBrush"];
    }

    private static string Iniciais(string nome)
    {
        var partes = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var uteis = partes.Where(p => p.Length > 1 && !Particula(p)).ToArray();
        if (uteis.Length == 0)
            uteis = partes;
        if (uteis.Length == 0)
            return "?";
        if (uteis.Length == 1)
            return char.ToUpperInvariant(uteis[0][0]).ToString();
        return string.Concat(char.ToUpperInvariant(uteis[0][0]), char.ToUpperInvariant(uteis[^1][0]));
    }

    private static bool Particula(string parte) =>
        parte.Equals("de", StringComparison.OrdinalIgnoreCase)
        || parte.Equals("da", StringComparison.OrdinalIgnoreCase)
        || parte.Equals("do", StringComparison.OrdinalIgnoreCase)
        || parte.Equals("dos", StringComparison.OrdinalIgnoreCase)
        || parte.Equals("das", StringComparison.OrdinalIgnoreCase)
        || parte.Equals("e", StringComparison.OrdinalIgnoreCase);
}
