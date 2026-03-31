using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using System.Linq;
using System.Globalization;
using UserTrace.Models;
using UserTrace.Services;

namespace UserTrace.Views;

public sealed partial class DashboardPage : Page
{
    private CancellationTokenSource? _cts;

    private string[]? _labels7;
    private int[]? _distribution7;
    private int _expiringTodayCount;
    private int _nextDaysCount;
    private int _lockedCount;
    private int _mustChangeNextLogonCount;

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += DashboardPage_Loaded;
        ActualThemeChanged += DashboardPage_ActualThemeChanged;
    }

    private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= DashboardPage_Loaded;
        _ = CarregarAsync();
    }

    private void DashboardPage_ActualThemeChanged(FrameworkElement sender, object args)
    {
        if (_labels7 is null || _distribution7 is null) return;
        // Reaplica apenas estilo dos gráficos (sem refazer as consultas).
        RenderCharts();
    }

    private void SetLoading(bool loading, string? statusText = null)
    {
        LoadingRing.IsActive = loading;
        LoadingRing.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;

        var text = statusText ?? string.Empty;
        StatusTextBlock.Text = text;
        StatusTextBlock.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;

        AtualizarButton.IsEnabled = !loading;
    }

    private void AtualizarButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        _ = CarregarAsync();

    private void ContasBloqueadasCard_Click(object sender, RoutedEventArgs e) =>
        App.CurrentWindow?.NavigateToMenu("ContasBloqueadas");

    private void ExpiramHojeCard_Click(object sender, RoutedEventArgs e) =>
        App.CurrentWindow?.NavigateToMenu(
            "SenhasExpiradas",
            new SenhasExpiradasNavigationPreset(SenhasExpiradasTipoBuscaPreset.Hoje));

    private void ExpiramEmUmaSemanaCard_Click(object sender, RoutedEventArgs e)
    {
        var today = DateTime.Today;
        App.CurrentWindow?.NavigateToMenu(
            "SenhasExpiradas",
            new SenhasExpiradasNavigationPreset(
                SenhasExpiradasTipoBuscaPreset.Intervalo,
                new DateTimeOffset(today),
                new DateTimeOffset(today.AddDays(6))));
    }

    private void TrocaProximoLogonCard_Click(object sender, RoutedEventArgs e) =>
        App.CurrentWindow?.NavigateToMenu(
            "SenhasExpiradas",
            new SenhasExpiradasNavigationPreset(SenhasExpiradasTipoBuscaPreset.ProximoLogon));

    private async Task CarregarAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            SetLoading(true, "Carregando métricas e gráficos…");

            var today = DateTime.Today;

            // Faz as consultas em paralelo para reduzir o tempo total de carregamento.
            var lockedTask = ActiveDirectorySearchService.GetLockedOutAccountsAsync(ct);
            var expiringTodayTask = ActiveDirectorySearchService.GetPasswordExpiringTodayAsync(ct);
            var expiringInWeekTask = ActiveDirectorySearchService.GetPasswordExpiringInRangeAsync(
                today,
                today.AddDays(6),
                ct);
            var mustChangeNextLogonTask =
                ActiveDirectorySearchService.GetMustChangePasswordAtNextLogonAsync(ct);

            await Task.WhenAll(lockedTask, expiringTodayTask, expiringInWeekTask, mustChangeNextLogonTask);

            var lockedCount = lockedTask.Result.Count;
            var expiringToday = expiringTodayTask.Result;
            var expiringInWeek = expiringInWeekTask.Result;
            var mustChangeNextLogonCount = mustChangeNextLogonTask.Result.Count;

            _lockedCount = lockedCount;
            _expiringTodayCount = expiringToday.Count;
            _nextDaysCount = expiringInWeek.Count - expiringToday.Count;
            _mustChangeNextLogonCount = mustChangeNextLogonCount;

            KpiLockedCountTextBlock.Text = lockedCount.ToString();
            KpiExpireTodayCountTextBlock.Text = expiringToday.Count.ToString();
            KpiExpireInWeekCountTextBlock.Text = expiringInWeek.Count.ToString();
            KpiMustChangeNextLogonCountTextBlock.Text = mustChangeNextLogonCount.ToString();

            // Gráfico 1: distribuição diária (hoje até +6).
            var byDay = expiringInWeek
                .GroupBy(x => x.Expira.Date)
                .ToDictionary(g => g.Key, g => g.Count());

            var labels7 = new string[7];
            var distribution7 = new int[7];
            var culture = new CultureInfo("pt-BR");
            var abbreviatedDayNames = culture.DateTimeFormat.AbbreviatedDayNames;
            for (var i = 0; i < 7; i++)
            {
                var date = today.AddDays(i).Date;
                var dayIndex = (int)date.DayOfWeek;
                var dayName = (dayIndex >= 0 && dayIndex < abbreviatedDayNames.Length)
                    ? abbreviatedDayNames[dayIndex]
                    : date.ToString("ddd", culture);

                labels7[i] = i == 0
                    ? "Hoje"
                    : dayName;
                distribution7[i] = byDay.TryGetValue(date, out var cnt) ? cnt : 0;
            }

            _labels7 = labels7;
            _distribution7 = distribution7;

            RenderCharts();

            SetLoading(false, $"Atualizado: {DateTime.Now:dd/MM/yyyy HH:mm}");
        }
        catch (OperationCanceledException)
        {
            SetLoading(false);
        }
        catch (Exception ex)
        {
            SetLoading(false, $"Erro ao carregar dashboard: {ex.Message}");
        }
    }

    private static SKColor TryGetThemeColor(string resourceKey, SKColor fallback)
    {
        if (Application.Current?.Resources?.TryGetValue(resourceKey, out var value) == true &&
            value is SolidColorBrush solid)
        {
            var c = solid.Color;
            return new SKColor(c.R, c.G, c.B, c.A);
        }

        return fallback;
    }

    private void RenderCharts()
    {
        if (_labels7 is null || _distribution7 is null) return;

        var theme = ActualTheme;
        var isDark = theme == ElementTheme.Dark;

        LiveCharts.Configure(config =>
        {
            if (isDark)
                config.AddDarkTheme();
            else
                config.AddLightTheme();
        });

        var axisLabelsColor = TryGetThemeColor(
            "TextFillColorSecondaryBrush",
            isDark ? new SKColor(170, 170, 170) : new SKColor(90, 90, 90));

        var dataLabelsColor = TryGetThemeColor(
            "TextFillColorSecondaryBrush",
            axisLabelsColor);

        var accentFill = TryGetThemeColor(
            "AccentFillColorDefaultBrush",
            isDark ? new SKColor(79, 156, 255) : new SKColor(0, 120, 215));

        var axisLabelsPaint = new SolidColorPaint(axisLabelsColor);
        var dataLabelsPaint = new SolidColorPaint(dataLabelsColor);
        var fillAlpha = (byte)(accentFill.Alpha * 0.85f);
        var accentFillWithAlpha = new SKColor(accentFill.Red, accentFill.Green, accentFill.Blue, fillAlpha);
        var fillPaint = new SolidColorPaint(accentFillWithAlpha);

        // Gráfico 1: distribuição diária (hoje até +6).
        var expiracoesPorDiaSeries = new ColumnSeries<int>
        {
            Name = "Expirações",
            Values = _distribution7,
            Fill = fillPaint,
            Stroke = null,
            // Removemos data labels para evitar sobreposição em telas menores.
            DataLabelsPaint = null
        };
        expiracoesPorDiaSeries.ChartPointPointerDown += ExpiracoesPorDiaSeries_ChartPointPointerDown;

        ExpiracoesPorDiaChart.Series = new ISeries[]
        {
            expiracoesPorDiaSeries
        };

        ExpiracoesPorDiaChart.XAxes = new Axis[]
        {
            new Axis
            {
                Labels = _labels7,
                LabelsPaint = axisLabelsPaint
            }
        };
        ExpiracoesPorDiaChart.YAxes = new Axis[]
        {
            new Axis
            {
                Labeler = v => v.ToString("0"),
                LabelsPaint = axisLabelsPaint
            }
        };

        // Gráfico 2: Hoje vs Próximos dias (dentro da mesma janela de 7 dias).
        ExpireHojeVsProximosChart.Series = new ISeries[]
        {
            new ColumnSeries<int>
            {
                Name = "Expirações",
                Values = new[] { _expiringTodayCount, _nextDaysCount },
                Fill = fillPaint,
                Stroke = null,
                // Removemos data labels para manter o layout limpo.
                DataLabelsPaint = null
            }
        };

        ExpireHojeVsProximosChart.XAxes = new Axis[]
        {
            new Axis
            {
                Labels = new[] { "Hoje", "Próximos dias" },
                LabelsPaint = axisLabelsPaint
            }
        };
        ExpireHojeVsProximosChart.YAxes = new Axis[]
        {
            new Axis
            {
                Labeler = v => v.ToString("0"),
                LabelsPaint = axisLabelsPaint
            }
        };
    }

    private void ExpiracoesPorDiaSeries_ChartPointPointerDown(
        IChartView chart,
        ChartPoint<int, RoundedRectangleGeometry, LabelGeometry>? point)
    {
        if (point is null) return;

        // No gráfico diário, o índice 0..6 corresponde a hoje..+6.
        var idx = point.Index;
        if (idx < 0 || idx > 6) return;

        var data = DateTime.Today.AddDays(idx).Date;

        App.CurrentWindow?.NavigateToMenu(
            "SenhasExpiradas",
            new SenhasExpiradasNavigationPreset(
                SenhasExpiradasTipoBuscaPreset.DataEspecifica,
                new DateTimeOffset(data)));
    }
}

