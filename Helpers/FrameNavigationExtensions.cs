using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using UserTrace.Views;

namespace UserTrace.Helpers;

/// <summary>Atalhos de navegação usados em várias páginas (duplo clique em listas).</summary>
public static class FrameNavigationExtensions
{
    public static void NavigateToLoginWithSam(this Frame frame, string? samAccountName)
    {
        if (string.IsNullOrWhiteSpace(samAccountName)) return;
        frame.Navigate(typeof(LoginPage), samAccountName.Trim(), new EntranceNavigationTransitionInfo());
    }
}
