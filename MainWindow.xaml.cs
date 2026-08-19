using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WiFiStabilizer.Services;

namespace WiFiStabilizer;

public partial class MainWindow : Window
{
    private readonly WifiMonitor _wifiMonitor = new();
    private readonly NetworkMonitor _networkMonitor = new();

    private readonly DispatcherTimer _timer;

    private readonly List<double> _signalHistory = new();

    private const int MaxHistory = 80;

    private int _stableSamples;
    private int _totalSamples;

    public MainWindow()
    {
        InitializeComponent();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _timer.Tick += Timer_Tick;

        Loaded += (_, _) =>
        {
            _timer.Start();
        };

        Closed += (_, _) =>
        {
            _timer.Stop();
        };
    }

    private async void Timer_Tick(
        object? sender,
        EventArgs e)
    {
        try
        {
            var wifi = await _wifiMonitor.GetWifiInfoAsync();
            var network = await _networkMonitor.CheckAsync();

            UpdateWifi(wifi);
            UpdateNetwork(network);

            _totalSamples++;

            if (network.Connected)
            {
                _stableSamples++;
            }

            var stability = _totalSamples == 0
                ? 0
                : (_stableSamples * 100.0 / _totalSamples);

            StabilityText.Text =
                $"{Math.Round(stability)} %";

            LastUpdateText.Text =
                $"Última actualización: {DateTime.Now:HH:mm:ss}";
        }
        catch
        {
            StatusText.Text = "● ERROR";
            StatusText.Foreground =
                new SolidColorBrush(Color.FromRgb(248, 113, 113));
        }
    }

    private void UpdateWifi(WifiInfo info)
    {
        SignalText.Text = $"{info.Signal} %";

        SsidText.Text = info.Ssid;
        BssidText.Text = info.Bssid;
        ChannelText.Text = info.Channel;
        RadioText.Text = info.RadioType;
        RxText.Text = info.ReceiveRate;
        TxText.Text = info.TransmitRate;

        if (info.Signal >= 80)
        {
            SignalQualityText.Text = "Excelente";
            SignalQualityText.Foreground =
                new SolidColorBrush(Color.FromRgb(74, 222, 128));
        }
        else if (info.Signal >= 60)
        {
            SignalQualityText.Text = "Buena";
            SignalQualityText.Foreground =
                new SolidColorBrush(Color.FromRgb(250, 204, 21));
        }
        else if (info.Signal >= 40)
        {
            SignalQualityText.Text = "Regular";
            SignalQualityText.Foreground =
                new SolidColorBrush(Color.FromRgb(251, 146, 60));
        }
        else
        {
            SignalQualityText.Text = "Mala";
            SignalQualityText.Foreground =
                new SolidColorBrush(Color.FromRgb(248, 113, 113));
        }

        _signalHistory.Add(info.Signal);

        if (_signalHistory.Count > MaxHistory)
        {
            _signalHistory.RemoveAt(0);
        }

        DrawGraph();
    }

    private void UpdateNetwork(NetworkStatus status)
    {
        if (status.Connected)
        {
            PingText.Text = $"{status.PingMs} ms";
            LossText.Text = $"{status.PacketLoss} %";

            StatusText.Text = "● CONECTADO";

            StatusText.Foreground =
                new SolidColorBrush(Color.FromRgb(74, 222, 128));
        }
        else
        {
            PingText.Text = "--";
            LossText.Text = "100 %";

            StatusText.Text = "● SIN INTERNET";

            StatusText.Foreground =
                new SolidColorBrush(Color.FromRgb(248, 113, 113));

            if (AutoReconnectCheck.IsChecked == true)
            {
                _ = TryReconnectAsync();
            }
        }
    }

    private async System.Threading.Tasks.Task TryReconnectAsync()
    {
        try
        {
            await System.Threading.Tasks.Task.Delay(500);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan reconnect",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process =
                System.Diagnostics.Process.Start(psi);

            if (process != null)
            {
                await process.WaitForExitAsync();
            }
        }
        catch
        {
        }
    }

    private void DrawGraph()
    {
        GraphCanvas.Children.Clear();

        if (_signalHistory.Count < 2)
        {
            return;
        }

        var width = GraphCanvas.ActualWidth;

        if (width <= 0)
        {
            return;
        }

        var height = GraphCanvas.ActualHeight;

        var points = new PointCollection();

        for (var i = 0; i < _signalHistory.Count; i++)
        {
            var x =
                i * width /
                Math.Max(1, MaxHistory - 1);

            var value = _signalHistory[i];

            var y =
                height -
                (value / 100.0 * height);

            points.Add(new Point(x, y));
        }

        var line = new Polyline
        {
            Stroke = new SolidColorBrush(
                Color.FromRgb(56, 189, 248)),
            StrokeThickness = 3,
            Points = points
        };

        GraphCanvas.Children.Add(line);
    }

    private void OptimizeButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        MessageBox.Show(
            "La monitorización y reconexión automática están activas.\n\n" +
            "La aplicación vigilará la señal y la conexión continuamente.",
            "WiFi Stabilizer",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
