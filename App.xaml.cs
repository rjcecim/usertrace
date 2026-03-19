using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace UserTrace;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();

        // Maximizar após a janela estar visível.
        _window.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (_window?.AppWindow.Presenter is OverlappedPresenter overlapped)
                overlapped.Maximize();
        });
    }
}
