using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UserTrace.Models;
using UserTrace.Services;
using DomainGroupItem = UserTrace.Models.GroupItem;

namespace UserTrace.Helpers;

/// <summary>
/// Abre diálogo "Salvar como" e grava o arquivo exportado.
/// </summary>
public static class ExportHelper
{
    public static void IniciarExportacao(FrameworkElement elemento, Func<string, Task> exportar, string formato)
    {
        elemento.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await exportar(formato);
            }
            catch (Exception ex)
            {
                await MostrarErroAsync(elemento.XamlRoot, ex.Message);
            }
        });
    }

    public static void IniciarExportacaoMenu(FrameworkElement elemento, object sender, Func<string, Task> exportar)
    {
        if (sender is not MenuFlyoutItem item)
            return;

        var formato = item.Tag as string;
        if (string.IsNullOrWhiteSpace(formato))
            return;

        IniciarExportacao(elemento, exportar, formato);
    }

    public static async Task ExportarUsuarioDiretoAsync(
        UserInfo u,
        string formato,
        string nomeBase,
        XamlRoot xamlRoot,
        Window window)
    {
        var caminho = NativeSaveFileDialog.Show(window, nomeBase, formato);
        if (caminho is null)
            return;

        try
        {
            var bytes = formato switch
            {
                "txt" => ExportService.ExportarUsuarioTxt(u),
                "docx" => ExportService.ExportarUsuarioDocx(u),
                "pdf" => ExportService.ExportarUsuarioPdf(u),
                _ => throw new InvalidOperationException($"Formato desconhecido: {formato}")
            };

            await File.WriteAllBytesAsync(caminho, bytes);
            await MostrarSucessoAsync(xamlRoot, caminho);
        }
        catch (Exception ex)
        {
            await MostrarErroAsync(xamlRoot, ex.Message);
        }
    }

    public static async Task ExportarListaDiretoAsync(
        string titulo,
        IEnumerable<SearchResultItem> itens,
        string nomeBase,
        string formato,
        XamlRoot xamlRoot,
        Window window)
    {
        var lista = itens as IReadOnlyCollection<SearchResultItem> ?? itens.ToList();
        if (lista.Count == 0)
        {
            await MostrarAvisoAsync(xamlRoot, "Não há dados para exportar. Execute uma busca primeiro.");
            return;
        }

        var caminho = NativeSaveFileDialog.Show(window, nomeBase, formato);
        if (caminho is null)
            return;

        try
        {
            var bytes = formato switch
            {
                "txt" => ExportService.ExportarListaTxt(titulo, lista),
                "docx" => ExportService.ExportarListaDocx(titulo, lista),
                "pdf" => ExportService.ExportarListaPdf(titulo, lista),
                _ => throw new InvalidOperationException($"Formato desconhecido: {formato}")
            };

            await File.WriteAllBytesAsync(caminho, bytes);
            await MostrarSucessoAsync(xamlRoot, caminho);
        }
        catch (Exception ex)
        {
            await MostrarErroAsync(xamlRoot, ex.Message);
        }
    }

    /// <summary>Exporta lista de usuários com apenas Login e Nome (sem coluna Bloqueio).</summary>
    public static async Task ExportarListaUsuariosDiretoAsync(
        string titulo,
        IEnumerable<SearchResultItem> itens,
        string nomeBase,
        string formato,
        XamlRoot xamlRoot,
        Window window,
        string? avisoSemDados = null)
    {
        var lista = itens as IReadOnlyCollection<SearchResultItem> ?? itens.ToList();
        if (lista.Count == 0)
        {
            await MostrarAvisoAsync(xamlRoot, avisoSemDados ?? "Não há dados para exportar. Execute uma busca primeiro.");
            return;
        }

        var caminho = NativeSaveFileDialog.Show(window, nomeBase, formato);
        if (caminho is null)
            return;

        try
        {
            var bytes = formato switch
            {
                "txt" => ExportService.ExportarListaUsuariosTxt(titulo, lista),
                "docx" => ExportService.ExportarListaUsuariosDocx(titulo, lista),
                "pdf" => ExportService.ExportarListaUsuariosPdf(titulo, lista),
                _ => throw new InvalidOperationException($"Formato desconhecido: {formato}")
            };

            await File.WriteAllBytesAsync(caminho, bytes);
            await MostrarSucessoAsync(xamlRoot, caminho);
        }
        catch (Exception ex)
        {
            await MostrarErroAsync(xamlRoot, ex.Message);
        }
    }

    public static async Task ExportarSenhasExpiradasDiretoAsync(
        IEnumerable<SenhaExpiraDisplay> itens,
        string formato,
        XamlRoot xamlRoot,
        Window window)
    {
        var lista = itens as IReadOnlyCollection<SenhaExpiraDisplay> ?? itens.ToList();
        if (lista.Count == 0)
        {
            await MostrarAvisoAsync(xamlRoot, "Não há dados para exportar. Execute uma busca primeiro.");
            return;
        }

        var nomeBase = $"senhas_expirando_{DateTime.Now:yyyyMMdd_HHmm}";
        var caminho = NativeSaveFileDialog.Show(window, nomeBase, formato);
        if (caminho is null)
            return;

        try
        {
            var bytes = formato switch
            {
                "txt" => ExportService.ExportarSenhasExpiradasTxt(lista),
                "docx" => ExportService.ExportarSenhasExpiradasDocx(lista),
                "pdf" => ExportService.ExportarSenhasExpiradasPdf(lista),
                _ => throw new InvalidOperationException($"Formato desconhecido: {formato}")
            };

            await File.WriteAllBytesAsync(caminho, bytes);
            await MostrarSucessoAsync(xamlRoot, caminho);
        }
        catch (Exception ex)
        {
            await MostrarErroAsync(xamlRoot, ex.Message);
        }
    }

    public static async Task ExportarGruposDiretoAsync(
        string filtro,
        IEnumerable<DomainGroupItem> grupos,
        string formato,
        XamlRoot xamlRoot,
        Window window)
    {
        var lista = grupos as IReadOnlyCollection<DomainGroupItem> ?? grupos.ToList();
        if (lista.Count == 0)
        {
            await MostrarAvisoAsync(xamlRoot, "Não há grupos para exportar. Liste os grupos primeiro.");
            return;
        }

        var nomeBase = $"grupos_ad_{DateTime.Now:yyyyMMdd_HHmm}";
        var caminho = NativeSaveFileDialog.Show(window, nomeBase, formato);
        if (caminho is null)
            return;

        try
        {
            var bytes = formato switch
            {
                "txt" => ExportService.ExportarGruposTxt(filtro, lista),
                "docx" => ExportService.ExportarGruposDocx(filtro, lista),
                "pdf" => ExportService.ExportarGruposPdf(filtro, lista),
                _ => throw new InvalidOperationException($"Formato desconhecido: {formato}")
            };

            await File.WriteAllBytesAsync(caminho, bytes);
            await MostrarSucessoAsync(xamlRoot, caminho);
        }
        catch (Exception ex)
        {
            await MostrarErroAsync(xamlRoot, ex.Message);
        }
    }

    private static async Task MostrarSucessoAsync(XamlRoot xamlRoot, string caminho)
    {
        var dialog = new ContentDialog
        {
            Title = "Exportação concluída",
            Content = $"Arquivo salvo em:\n{caminho}",
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
        };
        await dialog.ShowAsync();
    }

    public static async Task MostrarAvisoAsync(XamlRoot xamlRoot, string mensagem)
    {
        var dialog = new ContentDialog
        {
            Title = "Exportar",
            Content = mensagem,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
        };
        await dialog.ShowAsync();
    }

    public static async Task MostrarErroAsync(XamlRoot xamlRoot, string mensagem)
    {
        var dialog = new ContentDialog
        {
            Title = "Erro ao exportar",
            Content = mensagem,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
        };
        await dialog.ShowAsync();
    }
}
