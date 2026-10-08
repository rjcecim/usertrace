using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using System.Globalization;
using UserTrace.Helpers;
using UserTrace.Models;
using UserTrace.Services;

namespace UserTrace.Views;

public sealed partial class DashboardPage : Page
{
    private const int PreviewCount = 5;
    private static readonly CultureInfo PtBr = new("pt-BR");

    private CancellationTokenSource? _cts;
    private string[]? _labels7;
    private int[]? _distribution7;
    private int _expiringTodayCount;
    private int _expiringWeekCount;
    private int _lockedCount;
    private int _mustChangeCount;
    private int _demaisCount;
    private int _displayedTotal;

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

    private double _lastDonutSide;

    private void PieHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var side = e.NewSize.Height;
        if (double.IsNaN(side) || side < 80) return;
        if (Math.Abs(PieHost.Width - side) > 1)
        {
            PieHost.Width = side;
            return;
        }

        if (_labels7 is null || Math.Abs(side - _lastDonutSide) < 20) return;
        _lastDonutSide = side;
        RenderCharts();
    }

    private void DashboardPage_ActualThemeChanged(FrameworkElement sender, object args)
    {
        if (_labels7 is null || _distribution7 is null) return;
        RenderCharts();
    }

    private void SetLoading(bool loading)
    {
        LoadingRing.IsActive = loading;
        LoadingRing.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        StatusDot.Visibility = loading ? Visibility.Collapsed : Visibility.Visible;
        AtualizarButton.IsEnabled = !loading;
    }

    private void SetStatus(bool live, string text)
    {
        StatusTextBlock.Text = text;
        var brush = live
            ? (Brush)Application.Current.Resources["DashboardGreenBrush"]
            : (Brush)Application.Current.Resources["DashboardMutedBrush"];
        if (!live && text.StartsWith("Erro", StringComparison.Ordinal))
            brush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 220, 38, 38));

        StatusTextBlock.Foreground = brush;
        StatusDot.Fill = live
            ? (Brush)Application.Current.Resources["DashboardGreenBrush"]
            : brush;
    }

    private void AtualizarButton_Click(object sender, RoutedEventArgs e) =>
        _ = CarregarAsync();

    private void ContasBloqueadasCard_Click(object sender, RoutedEventArgs e) =>
        App.CurrentWindow?.NavigateToMenu("ContasBloqueadas");

    private void ExpiramHojeCard_Click(object sender, RoutedEventArgs e) =>
        App.CurrentWindow?.NavigateToMenu(
            "SenhasExpiradas",
            new SenhasExpiradasNavigationPreset(SenhasExpiradasTipoBuscaPreset.Hoje));

    private void ExpiramEmUmaSemanaCard_Click(object sender, RoutedEventArgs e) =>
        AbrirProximosSeteDias();

    private void TrocaProximoLogonCard_Click(object sender, RoutedEventArgs e) =>
        App.CurrentWindow?.NavigateToMenu(
            "SenhasExpiradas",
            new SenhasExpiradasNavigationPreset(SenhasExpiradasTipoBuscaPreset.ProximoLogon));

    private void VerExpiracoes_Click(object sender, RoutedEventArgs e) =>
        AbrirProximosSeteDias();

    private void VerBloqueadas_Click(object sender, RoutedEventArgs e) =>
        App.CurrentWindow?.NavigateToMenu("ContasBloqueadas");

    private void VerProximoLogon_Click(object sender, RoutedEventArgs e) =>
        App.CurrentWindow?.NavigateToMenu(
            "SenhasExpiradas",
            new SenhasExpiradasNavigationPreset(SenhasExpiradasTipoBuscaPreset.ProximoLogon));

    private static void AbrirProximosSeteDias()
    {
        var today = DateTime.Today;
        App.CurrentWindow?.NavigateToMenu(
            "SenhasExpiradas",
            new SenhasExpiradasNavigationPreset(
                SenhasExpiradasTipoBuscaPreset.Intervalo,
                new DateTimeOffset(today),
                new DateTimeOffset(today.AddDays(6))));
    }

    private void PreviewRow_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string usuario })
            Frame.NavigateToLoginWithSam(usuario);
    }

    private async Task CarregarAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            SetLoading(true);
            SetStatus(false, "Consultando o Active Directory…");
            BindPreview(ExpiringRepeater, ExpiringEmptyText, [], "Consultando…");
            BindPreview(LockedRepeater, LockedEmptyText, [], "Consultando…");
            BindPreview(MustChangeRepeater, MustChangeEmptyText, [], "Consultando…");

            var today = DateTime.Today;
            await LdapDirectory.RefreshPasswordPolicyAsync(ct);
            var policy = LdapDirectory.GetPasswordPolicy();

            var lockedTask = ActiveDirectorySearchService.GetLockedOutAccountsAsync(ct);
            var expiringWeekTask = ActiveDirectorySearchService.GetPasswordExpiringInRangeAsync(
                today,
                today.AddDays(6),
                ct);
            var mustChangeNextLogonTask =
                ActiveDirectorySearchService.GetMustChangePasswordAtNextLogonAsync(ct);
            var totalTask = ActiveDirectorySearchService.CountActiveUsersAsync(ct);

            await Task.WhenAll(lockedTask, expiringWeekTask, mustChangeNextLogonTask);

            var locked = await lockedTask;
            var expiringInWeek = await expiringWeekTask;
            var mustChange = await mustChangeNextLogonTask;
            if (ct.IsCancellationRequested) return;

            int? totalActive = null;
            try
            {
                totalActive = await totalTask;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                totalActive = null;
            }

            var expiringToday = expiringInWeek.Items.Where(x => x.Expira.Date == today).ToList();
            _lockedCount = locked.Items.Count;
            _expiringTodayCount = expiringToday.Count;
            _expiringWeekCount = expiringInWeek.Items.Count;
            _mustChangeCount = mustChange.Items.Count;

            if (totalActive is int total)
            {
                _demaisCount = Math.Max(
                    0,
                    total - _lockedCount - _expiringTodayCount - _expiringWeekCount - _mustChangeCount);
            }
            else
            {
                _demaisCount = 0;
            }

            _displayedTotal = _lockedCount + _expiringTodayCount + _expiringWeekCount + _mustChangeCount + _demaisCount;

            KpiLockedCountTextBlock.Text = _lockedCount.ToString(PtBr);
            KpiExpireTodayCountTextBlock.Text = _expiringTodayCount.ToString(PtBr);
            KpiExpireInWeekCountTextBlock.Text = _expiringWeekCount.ToString(PtBr);
            KpiMustChangeNextLogonCountTextBlock.Text = _mustChangeCount.ToString(PtBr);
            LegendLockedText.Text = _lockedCount.ToString("N0", PtBr);
            LegendTodayText.Text = _expiringTodayCount.ToString("N0", PtBr);
            LegendWeekText.Text = _expiringWeekCount.ToString("N0", PtBr);
            LegendMustChangeText.Text = _mustChangeCount.ToString("N0", PtBr);
            TotalContasText.Text = _displayedTotal.ToString("N0", PtBr);

            var byDay = expiringInWeek.Items
                .GroupBy(x => x.Expira.Date)
                .ToDictionary(g => g.Key, g => g.Count());

            var labels7 = new string[7];
            var distribution7 = new int[7];
            for (var i = 0; i < 7; i++)
            {
                var date = today.AddDays(i).Date;
                labels7[i] = i == 0 ? "Hoje" : RotuloDia(date);
                distribution7[i] = byDay.TryGetValue(date, out var cnt) ? cnt : 0;
            }

            _labels7 = labels7;
            _distribution7 = distribution7;
            RenderCharts();

            BindPreview(
                ExpiringRepeater,
                ExpiringEmptyText,
                expiringInWeek.Items.Take(PreviewCount).Select(item => ExpiryRow(item, today)).ToList(),
                "Nenhuma conta expira nos próximos 7 dias.");
            BindPreview(
                LockedRepeater,
                LockedEmptyText,
                locked.Items.Take(PreviewCount).Select(LockedRow).ToList(),
                "Nenhuma conta bloqueada agora.");
            BindPreview(
                MustChangeRepeater,
                MustChangeEmptyText,
                mustChange.Items.Take(PreviewCount).Select(MustChangeRow).ToList(),
                "Nenhuma conta precisa trocar a senha no próximo logon.");

            var status = "Dados obtidos diretamente do Active Directory (LDAP) no momento da consulta.";
            var avisos = new List<string>();
            if (locked.Truncated)
                avisos.Add($"bloqueadas no limite de {locked.Limit}");
            if (expiringInWeek.Truncated)
                avisos.Add($"senhas no limite de {expiringInWeek.Limit}");
            if (mustChange.Truncated)
                avisos.Add($"troca no logon no limite de {mustChange.Limit}");
            if (avisos.Count > 0)
                status = "Consulta parcial: " + string.Join("; ", avisos) + ".";

            UltimaConsultaText.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm", PtBr);
            ToolTipService.SetToolTip(StatusTextBlock, $"Política de senha: {policy.Describe()}");
            SetStatus(avisos.Count == 0, status);
            SetLoading(false);
            App.CurrentWindow?.SetDirectoryConnection(true);
        }
        catch (OperationCanceledException)
        {
            SetLoading(false);
        }
        catch (Exception ex)
        {
            SetLoading(false);
            SetStatus(false, $"Erro ao carregar dashboard: {ex.Message}");
            App.CurrentWindow?.SetDirectoryConnection(false);
        }
    }

    private static void BindPreview(
        ItemsRepeater repeater,
        TextBlock empty,
        IReadOnlyList<DashboardPreviewRow> rows,
        string emptyMessage)
    {
        repeater.ItemsSource = rows;
        empty.Text = emptyMessage;
        empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string RotuloDia(DateTime date)
    {
        var raw = PtBr.DateTimeFormat.GetAbbreviatedDayName(date.DayOfWeek).Trim().TrimEnd('.');
        if (raw.Length == 0)
            return "—";

        return char.ToUpper(raw[0], PtBr) + raw[1..] + ".";
    }

    private static DashboardPreviewRow ExpiryRow(SenhaExpiraItem item, DateTime today)
    {
        var days = Math.Max(0, (item.Expira.Date - today).Days);
        var (text, background, foreground) = days switch
        {
            0 => ("Hoje", "#FEE2E2", "#DC2626"),
            1 => ("1 dia", "#FFEDD5", "#C2410C"),
            <= 3 => ($"{days} dias", "#FEF3C7", "#B45309"),
            _ => ($"{days} dias", "#FEF9C3", "#A16207")
        };

        return Pessoa(item.SamAccountName, item.DisplayName, text, background, foreground, badge: true);
    }

    private static DashboardPreviewRow LockedRow(SearchResultItem item) =>
        Pessoa(
            item.SamAccountName,
            item.DisplayName,
            string.IsNullOrWhiteSpace(item.LockoutTime) ? "—" : item.LockoutTime,
            "#00000000",
            "#64748B",
            badge: false);

    private static DashboardPreviewRow MustChangeRow(SearchResultItem item) =>
        Pessoa(
            item.SamAccountName,
            item.DisplayName,
            "Troca no próximo logon",
            "#DCFCE7",
            "#15803D",
            badge: true);

    private static DashboardPreviewRow Pessoa(
        string usuario,
        string nome,
        string detalhe,
        string background,
        string foreground,
        bool badge) =>
        new()
        {
            Usuario = usuario,
            Nome = string.IsNullOrWhiteSpace(nome) ? "—" : nome,
            Detalhe = detalhe,
            BadgeBackground = Hex(background),
            BadgeForeground = Hex(foreground),
            BadgeVisibility = badge ? Visibility.Visible : Visibility.Collapsed,
            TextoVisibility = badge ? Visibility.Collapsed : Visibility.Visible
        };

    private static SolidColorBrush Hex(string hex)
    {
        var value = Convert.ToUInt32(hex.TrimStart('#'), 16);
        byte a = 255, r, g, b;
        if (hex.TrimStart('#').Length > 6)
        {
            a = (byte)((value >> 24) & 0xFF);
            r = (byte)((value >> 16) & 0xFF);
            g = (byte)((value >> 8) & 0xFF);
            b = (byte)(value & 0xFF);
        }
        else
        {
            r = (byte)((value >> 16) & 0xFF);
            g = (byte)((value >> 8) & 0xFF);
            b = (byte)(value & 0xFF);
        }

        return new SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
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

        var isDark = ActualTheme == ElementTheme.Dark;
        LiveCharts.Configure(config =>
        {
            if (isDark)
                config.AddDarkTheme();
            else
                config.AddLightTheme();
        });

        var axisLabelsColor = TryGetThemeColor(
            "DashboardMutedBrush",
            isDark ? new SKColor(148, 163, 184) : new SKColor(100, 116, 139));
        var labelColor = TryGetThemeColor(
            "DashboardTitleBrush",
            isDark ? new SKColor(248, 250, 252) : new SKColor(15, 23, 42));
        var gridColor = isDark
            ? new SKColor(51, 65, 85, 160)
            : new SKColor(226, 232, 240, 220);
        var barColor = TryGetThemeColor(
            "DashboardBlueBrush",
            new SKColor(37, 99, 235));

        var axisLabelsPaint = new SolidColorPaint(axisLabelsColor);
        var dataLabelsPaint = new SolidColorPaint(labelColor);
        var gridPaint = new SolidColorPaint(gridColor) { StrokeThickness = 1 };
        var fillPaint = new SolidColorPaint(barColor);

        var max = _distribution7.Length == 0 ? 0 : _distribution7.Max();
        var yMax = Math.Max(4, max + 1);

        var expiracoesPorDiaSeries = new ColumnSeries<int>
        {
            Name = "Expirações",
            Values = _distribution7,
            Fill = fillPaint,
            Stroke = null,
            MaxBarWidth = 42,
            Rx = 4,
            Ry = 4,
            DataLabelsPaint = dataLabelsPaint,
            DataLabelsSize = 12,
            DataLabelsPosition = DataLabelsPosition.Top,
            DataLabelsFormatter = point => point.Coordinate.PrimaryValue.ToString("0", CultureInfo.InvariantCulture)
        };
        expiracoesPorDiaSeries.ChartPointPointerDown += ExpiracoesPorDiaSeries_ChartPointPointerDown;

        ExpiracoesPorDiaChart.Series = new ISeries[] { expiracoesPorDiaSeries };
        ExpiracoesPorDiaChart.XAxes = new Axis[]
        {
            new()
            {
                Labels = _labels7,
                LabelsPaint = axisLabelsPaint,
                SeparatorsPaint = null,
                TextSize = 12
            }
        };
        ExpiracoesPorDiaChart.YAxes = new Axis[]
        {
            new()
            {
                MinLimit = 0,
                MaxLimit = yMax,
                MinStep = 1,
                Labeler = v =>
                {
                    var rounded = Math.Round(v);
                    return Math.Abs(v - rounded) < 0.01
                        ? rounded.ToString("0", CultureInfo.InvariantCulture)
                        : string.Empty;
                },
                LabelsPaint = axisLabelsPaint,
                SeparatorsPaint = gridPaint,
                TextSize = 12
            }
        };

        var hole = isDark ? new SKColor(17, 24, 39) : SKColors.White;
        var inner = DonutHole();
        var slices = new List<ISeries>();
        AddSlice(slices, "Contas bloqueadas", _lockedCount, new SKColor(37, 99, 235), hole, inner);
        AddSlice(slices, "Expiram hoje", _expiringTodayCount, new SKColor(249, 115, 22), hole, inner);
        AddSlice(slices, "Expiram nos próximos 7 dias", _expiringWeekCount, new SKColor(124, 58, 237), hole, inner);
        AddSlice(slices, "Troca no próximo logon", _mustChangeCount, new SKColor(22, 163, 74), hole, inner);
        StatusPieChart.Series = slices;
        StatusPieChart.InitialRotation = -90;
        StatusPieChart.DrawMargin = new Margin(2);
    }

    private double DonutHole()
    {
        var side = PieHost.ActualWidth;
        if (double.IsNaN(side) || side < 80)
            side = PieHost.ActualHeight;
        if (double.IsNaN(side) || side < 80)
            side = 180;
        return side * 0.31;
    }

    private static void AddSlice(List<ISeries> slices, string name, int value, SKColor color, SKColor hole, double innerRadius)
    {
        if (value <= 0) return;

        slices.Add(new PieSeries<int>
        {
            Name = name,
            Values = new[] { value },
            Fill = new SolidColorPaint(color),
            Stroke = new SolidColorPaint(hole) { StrokeThickness = 3 },
            InnerRadius = innerRadius,
            DataLabelsPaint = null,
            HoverPushout = 6
        });
    }

    private void ExpiracoesPorDiaSeries_ChartPointPointerDown(
        IChartView chart,
        ChartPoint<int, RoundedRectangleGeometry, LabelGeometry>? point)
    {
        if (point is null) return;

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
