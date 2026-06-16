using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using UserTrace.Models;

namespace UserTrace.Services;

/// <summary>
/// Gera bytes em memória nos formatos TXT, DOCX e PDF a partir dos dados de pesquisa do UserTrace.
/// Todos os métodos públicos retornam byte[] para serem gravados via StorageFile (WinRT).
/// </summary>
public static class ExportService
{
    // ── TXT ─────────────────────────────────────────────────────────────────

    public static byte[] ExportarUsuarioTxt(UserInfo u) =>
        LinhasParaBytes(BuildUserInfoLines(u));

    public static byte[] ExportarListaTxt(string titulo, IEnumerable<SearchResultItem> itens) =>
        LinhasParaBytes(BuildListaSearchResultLines(titulo, itens));

    public static byte[] ExportarListaUsuariosTxt(string titulo, IEnumerable<SearchResultItem> itens) =>
        LinhasParaBytes(BuildListaUsuariosLines(titulo, itens));

    public static byte[] ExportarSenhasExpiradasTxt(IEnumerable<SenhaExpiraDisplay> itens) =>
        LinhasParaBytes(BuildSenhasExpiradasLines(itens));

    public static byte[] ExportarGruposTxt(string filtro, IEnumerable<GroupItem> grupos) =>
        LinhasParaBytes(BuildGruposLines(filtro, grupos));

    // ── DOCX ────────────────────────────────────────────────────────────────

    public static byte[] ExportarUsuarioDocx(UserInfo u) =>
        GerarDocxBytes(BuildUserInfoLines(u));

    public static byte[] ExportarListaDocx(string titulo, IEnumerable<SearchResultItem> itens) =>
        GerarDocxBytes(BuildListaSearchResultLines(titulo, itens));

    public static byte[] ExportarListaUsuariosDocx(string titulo, IEnumerable<SearchResultItem> itens) =>
        GerarDocxBytes(BuildListaUsuariosLines(titulo, itens));

    public static byte[] ExportarSenhasExpiradasDocx(IEnumerable<SenhaExpiraDisplay> itens) =>
        GerarDocxBytes(BuildSenhasExpiradasLines(itens));

    public static byte[] ExportarGruposDocx(string filtro, IEnumerable<GroupItem> grupos) =>
        GerarDocxBytes(BuildGruposLines(filtro, grupos));

    // ── PDF ─────────────────────────────────────────────────────────────────

    public static byte[] ExportarUsuarioPdf(UserInfo u) =>
        GerarPdfBytes(BuildUserInfoLines(u));

    public static byte[] ExportarListaPdf(string titulo, IEnumerable<SearchResultItem> itens) =>
        GerarPdfBytes(BuildListaSearchResultLines(titulo, itens));

    public static byte[] ExportarListaUsuariosPdf(string titulo, IEnumerable<SearchResultItem> itens) =>
        GerarPdfBytes(BuildListaUsuariosLines(titulo, itens));

    public static byte[] ExportarSenhasExpiradasPdf(IEnumerable<SenhaExpiraDisplay> itens) =>
        GerarPdfBytes(BuildSenhasExpiradasLines(itens));

    public static byte[] ExportarGruposPdf(string filtro, IEnumerable<GroupItem> grupos) =>
        GerarPdfBytes(BuildGruposLines(filtro, grupos));

    // ── Construtores de conteúdo ─────────────────────────────────────────────

    private static List<string> BuildUserInfoLines(UserInfo u)
    {
        var nome = string.IsNullOrWhiteSpace(u.FullName) ? u.SamAccountName : u.FullName;
        var linhas = new List<string>
        {
            "═══════════════════════════════════════════",
            $"  RELATÓRIO DE USUÁRIO — {DateTime.Now:dd/MM/yyyy HH:mm}",
            "═══════════════════════════════════════════",
            "",
            "── IDENTIDADE ──────────────────────────────",
            $"  Nome completo      : {nome}",
            $"  Login (SAM)        : {u.SamAccountName}",
            $"  Domínio            : {u.Domain}",
            $"  E-mail             : {(string.IsNullOrWhiteSpace(u.Email)    ? "—" : u.Email)}",
            $"  Telefone           : {(string.IsNullOrWhiteSpace(u.PhoneNumber) ? "—" : u.PhoneNumber)}",
            $"  Setor / Localidade : {(string.IsNullOrWhiteSpace(u.Office)   ? "—" : u.Office)}",
            $"  Unidade Org.       : {(string.IsNullOrWhiteSpace(u.OrganizationalUnit) ? "—" : u.OrganizationalUnit)}",
            "",
            "── STATUS DA CONTA ─────────────────────────",
            $"  Conta ativa        : {(u.AccountActive  ? "Sim" : "Não")}",
            $"  Conta expira em    : {u.AccountExpires}",
            $"  Senha expirada     : {(u.PasswordExpired ? "Sim" : "Não")}",
            $"  Smartcard          : {(u.SmartcardRequired ? "Obrigatório" : "Não requerido")}",
            "",
            "── SENHA ───────────────────────────────────",
            $"  Definida em        : {u.PasswordLastSet}",
            $"  Expira em          : {(string.IsNullOrWhiteSpace(u.PasswordExpiresOn) ? (u.PasswordNeverExpires ? "Nunca" : "Conforme política") : u.PasswordExpiresOn)}",
            $"  Dias para expirar  : {(string.IsNullOrWhiteSpace(u.PasswordDaysToExpire) ? "—" : u.PasswordDaysToExpire)}",
            $"  Tentativas falhas  : {u.BadPasswordCount}",
            $"  Última tent. falha : {u.BadPasswordTime}",
            $"  Bloqueio           : {(string.IsNullOrWhiteSpace(u.LockoutTime) ? "—" : u.LockoutTime)}",
            $"  Alterável          : {(u.PasswordChangeable ? "Sim" : "Não")}",
            $"  Obrigatória        : {(u.PasswordRequired   ? "Sim" : "Não")}",
            $"  Nunca expira       : {(u.PasswordNeverExpires ? "Sim" : "Não")}",
            "",
            "── LOGON ───────────────────────────────────",
            $"  Último logon       : {u.LastLogon}",
            $"  Último logoff      : {u.LastLogoff}",
            $"  Estações           : {u.Workstations}",
            $"  Script de logon    : {(string.IsNullOrEmpty(u.LogonScript)  ? "—" : u.LogonScript)}",
            $"  Perfil             : {(string.IsNullOrEmpty(u.ProfilePath)  ? "—" : u.ProfilePath)}",
            $"  Diretório home     : {(string.IsNullOrEmpty(u.HomeDirectory) ? "—" : u.HomeDirectory)}",
            "",
            "── GRUPOS LOCAIS ───────────────────────────",
        };

        if (u.LocalGroups.Count == 0)
            linhas.Add("  (nenhum)");
        else
            foreach (var g in u.LocalGroups)
                linhas.Add($"  • {g}");

        linhas.Add("");
        linhas.Add("── GRUPOS GLOBAIS ──────────────────────────");

        if (u.GlobalGroups.Count == 0)
            linhas.Add("  (nenhum)");
        else
            foreach (var g in u.GlobalGroups)
                linhas.Add($"  • {g}");

        linhas.Add("");
        linhas.Add("═══════════════════════════════════════════");
        linhas.Add($"  Gerado por UserTrace em {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        linhas.Add("═══════════════════════════════════════════");

        return linhas;
    }

    private static List<string> BuildListaSearchResultLines(string titulo, IEnumerable<SearchResultItem> itens)
    {
        var lista = itens.ToList();
        var linhas = new List<string>
        {
            "═══════════════════════════════════════════",
            $"  {titulo.ToUpper()} — {DateTime.Now:dd/MM/yyyy HH:mm}",
            "═══════════════════════════════════════════",
            $"  Total: {lista.Count} registro(s)",
            "",
            $"  {"Login",-25} {"Nome",-40} {"Bloqueio",-20}",
            $"  {new string('-', 25)} {new string('-', 40)} {new string('-', 20)}",
        };

        foreach (var item in lista)
        {
            var lockout = string.IsNullOrWhiteSpace(item.LockoutTime) ? "" : item.LockoutTime;
            linhas.Add($"  {item.SamAccountName,-25} {item.DisplayName,-40} {lockout,-20}");
        }

        linhas.Add("");
        linhas.Add("═══════════════════════════════════════════");
        linhas.Add($"  Gerado por UserTrace em {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        linhas.Add("═══════════════════════════════════════════");

        return linhas;
    }

    private static List<string> BuildListaUsuariosLines(string titulo, IEnumerable<SearchResultItem> itens)
    {
        var lista = itens.ToList();
        var linhas = new List<string>
        {
            "═══════════════════════════════════════════",
            $"  {titulo.ToUpper()} — {DateTime.Now:dd/MM/yyyy HH:mm}",
            "═══════════════════════════════════════════",
            $"  Total: {lista.Count} registro(s)",
            "",
            $"  {"Login",-25} {"Nome",-40}",
            $"  {new string('-', 25)} {new string('-', 40)}",
        };

        foreach (var item in lista)
            linhas.Add($"  {item.SamAccountName,-25} {item.DisplayName,-40}");

        linhas.Add("");
        linhas.Add("═══════════════════════════════════════════");
        linhas.Add($"  Gerado por UserTrace em {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        linhas.Add("═══════════════════════════════════════════");

        return linhas;
    }

    private static List<string> BuildSenhasExpiradasLines(IEnumerable<SenhaExpiraDisplay> itens)
    {
        var lista = itens.ToList();
        var linhas = new List<string>
        {
            "═══════════════════════════════════════════",
            $"  SENHAS EXPIRANDO — {DateTime.Now:dd/MM/yyyy HH:mm}",
            "═══════════════════════════════════════════",
            $"  Total: {lista.Count} registro(s)",
            "",
            $"  {"Login",-25} {"Nome",-40} {"Data de Expiração",-22}",
            $"  {new string('-', 25)} {new string('-', 40)} {new string('-', 22)}",
        };

        foreach (var item in lista)
        {
            var expira = string.IsNullOrWhiteSpace(item.DataExpira) ? "Troca no próximo logon" : item.DataExpira;
            linhas.Add($"  {item.SamAccountName,-25} {item.DisplayName,-40} {expira,-22}");
        }

        linhas.Add("");
        linhas.Add("═══════════════════════════════════════════");
        linhas.Add($"  Gerado por UserTrace em {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        linhas.Add("═══════════════════════════════════════════");

        return linhas;
    }

    private static List<string> BuildGruposLines(string filtro, IEnumerable<GroupItem> grupos)
    {
        var lista = grupos.ToList();
        var linhas = new List<string>
        {
            "═══════════════════════════════════════════",
            $"  GRUPOS DO AD — {DateTime.Now:dd/MM/yyyy HH:mm}",
            "═══════════════════════════════════════════",
        };

        if (!string.IsNullOrWhiteSpace(filtro))
            linhas.Add($"  Filtro: {filtro}");

        linhas.Add($"  Total: {lista.Count} grupo(s)");
        linhas.Add("");
        linhas.Add($"  {"Nome",-40} {"Tipo",-12} {"Descrição",-50}");
        linhas.Add($"  {new string('-', 40)} {new string('-', 12)} {new string('-', 50)}");

        foreach (var g in lista)
            linhas.Add($"  {g.Name,-40} {g.GroupType,-12} {g.Description,-50}");

        linhas.Add("");
        linhas.Add("═══════════════════════════════════════════");
        linhas.Add($"  Gerado por UserTrace em {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        linhas.Add("═══════════════════════════════════════════");

        return linhas;
    }

    // ── Helpers de serialização ──────────────────────────────────────────────

    private static byte[] LinhasParaBytes(List<string> linhas) =>
        System.Text.Encoding.UTF8.GetBytes(string.Join("\r\n", linhas));

    // ── Geradores de formato ─────────────────────────────────────────────────

    private static byte[] GerarDocxBytes(List<string> linhas)
    {
        var ms = new MemoryStream();

        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, autoSave: true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            foreach (var linha in linhas)
            {
                var para = new Paragraph();
                var run  = new Run();
                var rpr  = new RunProperties();
                rpr.Append(new RunFonts { Ascii = "Courier New", HighAnsi = "Courier New" });
                rpr.Append(new FontSize { Val = "18" }); // 9pt
                run.Append(rpr);
                run.Append(new Text(linha) { Space = SpaceProcessingModeValues.Preserve });
                para.Append(run);
                body.Append(para);
            }

            mainPart.Document.Save();
        } // doc.Dispose() fecha e flush o MemoryStream antes de lermos os bytes

        return ms.ToArray();
    }

    private static byte[] GerarPdfBytes(List<string> linhas)
    {
        var document = new PdfDocument();
        document.Info.Title   = "UserTrace — Relatório";
        document.Info.Creator = "UserTrace";

        const double margem       = 40;
        const double largura      = 595;  // A4
        const double altura       = 842;  // A4
        const double tamanhoFonte = 8;
        const double alturaLinha  = 11;

        var fonte = new XFont("Courier New", tamanhoFonte, XFontStyle.Regular);

        PdfPage?   pagina = null;
        XGraphics? gfx    = null;
        double y = 0;

        void NovaPagina()
        {
            pagina = document.AddPage();
            pagina.Width  = largura;
            pagina.Height = altura;
            gfx = XGraphics.FromPdfPage(pagina);
            y   = margem;
        }

        NovaPagina();

        foreach (var linha in linhas)
        {
            if (y + alturaLinha > altura - margem)
                NovaPagina();

            gfx!.DrawString(linha, fonte, XBrushes.Black,
                new XRect(margem, y, largura - 2 * margem, alturaLinha),
                XStringFormats.TopLeft);

            y += alturaLinha;
        }

        using var ms = new MemoryStream();
        document.Save(ms, closeStream: false);
        return ms.ToArray();
    }
}
