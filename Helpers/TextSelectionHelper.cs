using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UserTrace.Views;

namespace UserTrace.Helpers;

/// <summary>
/// Política de cópia: fora do <see cref="UserInfoPanel"/>, nenhum <see cref="TextBlock"/> deve permitir seleção.
/// Dentro do painel, apenas valores explícitos são reativados em <see cref="UserInfoPanel.ShowUser"/>.
/// </summary>
public static class TextSelectionHelper
{
    /// <summary>Desliga <see cref="TextBlock.IsTextSelectionEnabled"/> em toda a subárvore, exceto dentro de <see cref="UserInfoPanel"/>.</summary>
    public static void DisableTextBlocksOutsideUserInfoPanel(DependencyObject? root)
    {
        if (root == null) return;
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is UserInfoPanel)
                continue;
            if (child is TextBlock tb)
                tb.IsTextSelectionEnabled = false;
            DisableTextBlocksOutsideUserInfoPanel(child);
        }
    }

    /// <summary>Define <see cref="TextBlock.IsTextSelectionEnabled"/> em todos os <see cref="TextBlock"/> da subárvore.</summary>
    public static void SetTextBlockSelectionRecursive(DependencyObject? root, bool enabled)
    {
        if (root == null) return;
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock tb)
                tb.IsTextSelectionEnabled = enabled;
            SetTextBlockSelectionRecursive(child, enabled);
        }
    }
}
