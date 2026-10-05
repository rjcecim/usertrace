using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace UserTrace.Models;

public sealed class DashboardPreviewRow
{
    public string Usuario { get; init; } = string.Empty;
    public string Nome { get; init; } = string.Empty;
    public string Detalhe { get; init; } = string.Empty;
    public SolidColorBrush BadgeBackground { get; init; } = new(Windows.UI.Color.FromArgb(0, 0, 0, 0));
    public SolidColorBrush BadgeForeground { get; init; } = new(Windows.UI.Color.FromArgb(255, 15, 23, 42));
    public Visibility BadgeVisibility { get; init; } = Visibility.Collapsed;
    public Visibility TextoVisibility { get; init; } = Visibility.Visible;
}
