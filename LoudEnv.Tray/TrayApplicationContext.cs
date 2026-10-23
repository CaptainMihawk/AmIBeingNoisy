using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using LoudEnv;

namespace LoudEnv.Tray;

internal enum IconState
{
    Gray,
    Green,
    Yellow,
    Red,
}

public sealed class TrayApplicationContext : ApplicationContext
{
    private const long AlertRepeatMs = 5000;
    private const int ScheduleCheckMs = 5000;
    private const int UiUpdateMs = 200;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly TrayIcons _icons;
    private readonly System.Windows.Forms.Timer _scheduleTimer;
    private readonly System.Windows.Forms.Timer _uiTimer;

    private AppSettings _settings;
    private bool _paused;
    private bool _settingsOpen;
    private DateTime _lastTextUpdate = DateTime.MinValue;
    private DateTime _nextRetry = DateTime.MinValue;
    private string? _lastError;
    private string? _captureError;
    private string? _failureStatus;
    private CancellationTokenSource? _cts;
    private Task? _capture;
    private MicLevelMonitor? _monitor;
    private AlertLog? _log;
    private ThresholdDetector? _detector;
    private Stopwatch? _clock;
    private float _lastRms;
    private IconState _state = IconState.Gray;
    private IconState _shownState = IconState.Gray;
    private string _status = "iniciando";

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load(out string? loadError);
        bool firstRun = !File.Exists(AppSettings.FilePath);

        if (string.IsNullOrWhiteSpace(_settings.DeviceName))
        {
            try
            {
                _settings.DeviceName = MicLevelMonitor.DefaultDeviceName();
            }
            catch
            {
                _settings.DeviceName = "";
            }
        }

        if (firstRun)
        {
            try
            {
                _settings.Save();
            }
            catch (Exception)
            {
                // Persistir os padrões é opcional; o app segue com os valores em memória.
            }
        }

        _icons = new TrayIcons();
        _pauseItem = new ToolStripMenuItem("Pausar", null, (_, _) => TogglePause());
        _menu = new ContextMenuStrip();
        _menu.Items.Add(new ToolStripMenuItem("Configurações...", null, (_, _) => OpenSettings()));
        _menu.Items.Add(_pauseItem);
        _menu.Items.Add(new ToolStripMenuItem("Abrir log", null, (_, _) => OpenLog()));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Sair", null, (_, _) => ExitThread()));

        _icon = new NotifyIcon
        {
            Icon = _icons.Gray,
            Text = "LoudEnv",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => OpenSettings();

        _scheduleTimer = new System.Windows.Forms.Timer { Interval = ScheduleCheckMs };
        _scheduleTimer.Tick += (_, _) => Evaluate();
        _scheduleTimer.Start();

        _uiTimer = new System.Windows.Forms.Timer { Interval = UiUpdateMs };
        _uiTimer.Tick += (_, _) => UpdateIcon();
        _uiTimer.Start();

        Evaluate();
        if (loadError is not null && _lastError is null)
            ShowBalloon("Configuração", loadError, ToolTipIcon.Warning);
        else if (firstRun && _lastError is null)
            ShowBalloon("LoudEnv", DescribeState(), ToolTipIcon.Info);
    }

    private void Evaluate()
    {
        string? captureError = Volatile.Read(ref _captureError);
        if (captureError is not null)
        {
            Volatile.Write(ref _captureError, null);
            WarnOnce(captureError);
            SetState(IconState.Gray, "sem microfone");
            UpdateTooltip();
        }

        if (_capture is not null && _capture.IsCompleted)
        {
            StopCapture();
            _nextRetry = DateTime.Now + RetryDelay;
        }

        bool inSchedule = Schedule.IsActive(
            TimeOnly.FromDateTime(DateTime.Now), _settings.ScheduleEnabled, _settings.Start, _settings.End);
        bool shouldCapture = inSchedule && !_paused;

        if (shouldCapture)
        {
            if (_capture is null && DateTime.Now >= _nextRetry)
                StartCapture();
        }
        else if (_capture is not null)
        {
            StopCapture();
        }

        if (_capture is null)
        {
            string status = _paused
                ? "pausado"
                : !inSchedule
                    ? "fora do horário"
                    : _failureStatus ?? "sem microfone";
            SetState(IconState.Gray, status);
            UpdateTooltip();
        }
    }

    private void StartCapture()
    {
        Volatile.Write(ref _captureError, null);
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.DeviceName))
                _settings.DeviceName = MicLevelMonitor.DefaultDeviceName();

            _monitor = new MicLevelMonitor(_settings.DeviceName);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or COMException)
        {
            WarnOnce(ex.Message);
            _nextRetry = DateTime.Now + RetryDelay;
            _failureStatus = "sem microfone";
            SetState(IconState.Gray, "sem microfone");
            UpdateTooltip();
            return;
        }

        try
        {
            string? folder = Path.GetDirectoryName(Path.GetFullPath(_settings.LogPath));
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            _log = new AlertLog(_settings.LogPath, _monitor.DeviceName, _settings.Threshold);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or ArgumentException or NotSupportedException)
        {
            _log = null;
            try
            {
                _monitor?.Dispose();
            }
            catch
            {
            }

            _monitor = null;
            WarnOnce($"Sem log: {ex.Message}");
            _nextRetry = DateTime.Now + RetryDelay;
            _failureStatus = "sem log";
            SetState(IconState.Gray, "sem log");
            UpdateTooltip();
            return;
        }

        _detector = new ThresholdDetector(_settings.Threshold, AlertRepeatMs);
        _clock = Stopwatch.StartNew();
        _cts = new CancellationTokenSource();
        _capture = RunCaptureAsync(_cts.Token);
        _failureStatus = null;

        if (_log is not null)
            _lastError = null;

        SetState(IconState.Gray, "ativo");
        UpdateTooltip();
    }

    private async Task RunCaptureAsync(CancellationToken ct)
    {
        try
        {
            await _monitor!.RunAsync(new InlineProgress<LevelReading>(OnReading), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Volatile.Write(ref _captureError, ex.Message);
        }
    }

    private void OnReading(LevelReading reading)
    {
        _lastRms = reading.Rms;

        if (_detector is null || _clock is null)
            return;

        bool above = reading.Rms >= _settings.Threshold;
        _log?.Observe(reading.Rms, above);

        if (_detector.Sample(reading.Rms, _clock.ElapsedMilliseconds))
        {
            _log?.NoteAlert();

            if (_settings.AlertSound)
                AlertSound.Play();
        }
    }

    private void UpdateIcon()
    {
        if (_capture is null || _capture.IsCompleted)
            return;

        float threshold = _settings.Threshold;
        IconState state = _lastRms >= threshold
            ? IconState.Red
            : _lastRms >= threshold * 0.75f
                ? IconState.Yellow
                : IconState.Green;

        DateTime now = DateTime.Now;
        if (state != _state || (now - _lastTextUpdate).TotalSeconds >= 1)
        {
            SetState(state, $"ativo {_lastRms.ToString("0.000", CultureInfo.InvariantCulture)}");
            UpdateTooltip();
            _lastTextUpdate = now;
        }
    }

    private void SetState(IconState state, string status)
    {
        _state = state;
        _status = status;
    }

    private void UpdateTooltip()
    {
        if (_state != _shownState)
        {
            _icon.Icon = _state switch
            {
                IconState.Green => _icons.Green,
                IconState.Yellow => _icons.Yellow,
                IconState.Red => _icons.Red,
                _ => _icons.Gray,
            };
            _shownState = _state;
        }

        string text = "LoudEnv: " + _status;
        if (text.Length > 63)
            text = text[..63];

        if (_icon.Text != text)
            _icon.Text = text;
    }

    private void StopCapture()
    {
        var cts = _cts;
        var capture = _capture;
        if (cts is null || capture is null)
            return;

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        bool stopped;
        try
        {
            stopped = capture.Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex) when (ex is AggregateException or ObjectDisposedException)
        {
            stopped = true;
        }

        if (!stopped)
            return;

        var log = _log;
        var monitor = _monitor;
        _cts = null;
        _capture = null;
        _log = null;
        _monitor = null;
        _detector = null;
        _clock = null;
        _lastRms = 0f;

        try
        {
            log?.Dispose();
        }
        catch (Exception ex)
        {
            WarnOnce($"Falha ao gravar o log: {ex.Message}");
        }

        try
        {
            monitor?.Dispose();
        }
        catch
        {
        }

        try
        {
            cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }    }

    private void TogglePause()
    {
        _paused = !_paused;
        _pauseItem.Text = _paused ? "Retomar" : "Pausar";

        if (_paused)
        {
            StopCapture();
            SetState(IconState.Gray, "pausado");
            UpdateTooltip();
        }
        else
        {
            Evaluate();
        }
    }

    private void OpenSettings()
    {
        if (_settingsOpen)
            return;

        _settingsOpen = true;
        try
        {
            IReadOnlyList<string> devices;
            try
            {
                devices = MicLevelMonitor.ListCaptureEndpoints().Select(endpoint => endpoint.Name).ToList();
            }
            catch
            {
                devices = Array.Empty<string>();
            }

            using var form = new SettingsForm(_settings, devices);
            if (form.ShowDialog() != DialogResult.OK)
                return;

            StopCapture();
            _settings = form.Settings;
            _nextRetry = DateTime.MinValue;
            _lastError = null;
            Evaluate();
        }
        finally
        {
            _settingsOpen = false;
        }
    }

    private void OpenLog()
    {
        string path = _settings.LogPath;
        if (!File.Exists(path))
        {
            MessageBox.Show($"O log ainda não existe: {path}", "LoudEnv",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível abrir o log: {ex.Message}", "LoudEnv",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void WarnOnce(string message)
    {
        if (_lastError == message)
            return;

        _lastError = message;
        ShowBalloon("LoudEnv", message, ToolTipIcon.Warning);
    }

    private void ShowBalloon(string title, string text, ToolTipIcon icon)
    {
        if (_icon is null)
            return;

        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = text;
        _icon.BalloonTipIcon = icon;
        _icon.ShowBalloonTip(4000);
    }

    private string DescribeState()
    {
        if (_paused)
            return "pausado";

        bool inSchedule = Schedule.IsActive(
            TimeOnly.FromDateTime(DateTime.Now), _settings.ScheduleEnabled, _settings.Start, _settings.End);

        if (!inSchedule)
        {
            return $"fora do horário ({_settings.Start.ToString("HH:mm", CultureInfo.InvariantCulture)}-" +
                   $"{_settings.End.ToString("HH:mm", CultureInfo.InvariantCulture)})";
        }

        return _capture is null
            ? "sem microfone"
            : $"ativo {_lastRms.ToString("0.000", CultureInfo.InvariantCulture)}";
    }

    protected override void ExitThreadCore()
    {
        _scheduleTimer.Stop();
        _uiTimer.Stop();
        try
        {
            StopCapture();
        }
        finally
        {
            _scheduleTimer.Dispose();
            _uiTimer.Dispose();
            _icon.Visible = false;
            _icon.Dispose();
            _menu.Dispose();
            _icons.Dispose();
            base.ExitThreadCore();
        }
    }
}
