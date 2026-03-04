using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace UserTrace.Converters;

/// <summary>
/// Converte nome do grupo em Brush: destaque (accent) para AcessoWifi, AcessoVDI-Floating, VPN-usuario;
/// caso contrário, cor normal de borda ou texto conforme o parâmetro.
/// </summary>
public sealed class HighlightedGroupToBrushConverter : IValueConverter
{
    private static readonly HashSet<string> HighlightedGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "AcessoWifi",
        "AcessoVDI-Floating",
        "VPN-usuario"
    };

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var app = Application.Current;
        if (app?.Resources == null) return new SolidColorBrush(Colors.Gray);

        bool isHighlighted = value is string name && HighlightedGroups.Contains(name);
        string param = parameter?.ToString() ?? "";

        if (isHighlighted)
        {
            return (Brush)app.Resources["AccentFillColorDefaultBrush"];
        }

        // Cor normal: borda ou texto
        if (param.Equals("Text", StringComparison.OrdinalIgnoreCase))
            return (Brush)app.Resources["TextFillColorSecondaryBrush"];
        return (Brush)app.Resources["CardStrokeColorDefaultBrush"];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
