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

        // Maximizar após a janela estar visível (Dispatcher garante que a janela já foi exibida).
        _window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (_window?.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter overlapped)
                overlapped.Maximize();
        });
    }
}
