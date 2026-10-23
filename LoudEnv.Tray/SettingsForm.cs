namespace LoudEnv.Tray;

public sealed class SettingsForm : Form
{
    private readonly NumericUpDown _threshold;
    private readonly ComboBox _device;
    private readonly CheckBox _useSchedule;
    private readonly DateTimePicker _start;
    private readonly DateTimePicker _end;
    private readonly TextBox _logPath;
    private readonly CheckBox _sound;
    private readonly CheckBox _startWithWindows;

    public AppSettings Settings { get; private set; } = new();

    public SettingsForm(AppSettings current, IReadOnlyList<string> devices)
    {
        Text = "LoudEnv — Configurações";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(470, 310);
        Font = new Font("Segoe UI", 9F);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 8,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _threshold = new NumericUpDown
        {
            DecimalPlaces = 2,
            Increment = 0.01m,
            Minimum = 0.01m,
            Maximum = 0.99m,
            Value = (decimal)Math.Clamp(current.Threshold, 0.01f, 0.99f),
            Width = 90,
        };

        var deviceNames = devices.ToList();
        if (!string.IsNullOrWhiteSpace(current.DeviceName) &&
            !deviceNames.Contains(current.DeviceName, StringComparer.OrdinalIgnoreCase))
        {
            deviceNames.Insert(0, current.DeviceName);
        }

        _device = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
        };
        _device.Items.AddRange(deviceNames.Cast<object>().ToArray());
        if (deviceNames.Count > 0)
        {
            int index = deviceNames.FindIndex(
                name => string.Equals(name, current.DeviceName, StringComparison.OrdinalIgnoreCase));
            _device.SelectedIndex = index >= 0 ? index : 0;
        }

        _useSchedule = new CheckBox
        {
            Text = "Usar horário",
            Checked = current.ScheduleEnabled,
            AutoSize = true,
        };

        _start = new DateTimePicker
        {
            Format = DateTimePickerFormat.Time,
            ShowUpDown = true,
            Value = DateTime.Today.Add(current.Start.ToTimeSpan()),
            Width = 90,
        };
        _end = new DateTimePicker
        {
            Format = DateTimePickerFormat.Time,
            ShowUpDown = true,
            Value = DateTime.Today.Add(current.End.ToTimeSpan()),
            Width = 90,
        };

        _logPath = new TextBox
        {
            Text = current.LogPath,
            Dock = DockStyle.Fill,
        };
        var browse = new Button
        {
            Text = "...",
            Width = 30,
            Dock = DockStyle.Fill,
        };
        browse.Click += (_, _) => BrowseLog();

        _sound = new CheckBox
        {
            Text = "Som do alerta",
            Checked = current.AlertSound,
            AutoSize = true,
        };

        _startWithWindows = new CheckBox
        {
            Text = "Iniciar com o Windows",
            Checked = StartupRegistration.IsEnabled(),
            AutoSize = true,
        };

        var timePanel = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0),
        };
        timePanel.Controls.Add(new Label { Text = "Início", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        timePanel.Controls.Add(_start);
        timePanel.Controls.Add(new Label { Text = "Fim", AutoSize = true, Margin = new Padding(10, 8, 4, 0) });
        timePanel.Controls.Add(_end);

        var logPanel = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Margin = new Padding(0),
        };
        logPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        logPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
        logPanel.Controls.Add(_logPath, 0, 0);
        logPanel.Controls.Add(browse, 1, 0);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
        };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Width = 90 };
        var save = new Button { Text = "Salvar", Width = 90 };
        save.Click += (_, _) => SaveClicked();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);

        layout.Controls.Add(new Label { Text = "Limiar (RMS):", AutoSize = true, Margin = new Padding(0, 5, 0, 0) }, 0, 0);
        layout.Controls.Add(_threshold, 1, 0);
        layout.Controls.Add(new Label { Text = "Dispositivo:", AutoSize = true, Margin = new Padding(0, 5, 0, 0) }, 0, 1);
        layout.Controls.Add(_device, 1, 1);
        layout.Controls.Add(_useSchedule, 1, 2);
        layout.Controls.Add(new Label { Text = "Horário:", AutoSize = true, Margin = new Padding(0, 5, 0, 0) }, 0, 3);
        layout.Controls.Add(timePanel, 1, 3);
        layout.Controls.Add(new Label { Text = "Log:", AutoSize = true, Margin = new Padding(0, 5, 0, 0) }, 0, 4);
        layout.Controls.Add(logPanel, 1, 4);
        layout.Controls.Add(_sound, 1, 5);
        layout.Controls.Add(_startWithWindows, 1, 6);
        layout.Controls.Add(buttons, 1, 7);

        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
        _useSchedule.CheckedChanged += (_, _) => UpdateScheduleEnabled();
        UpdateScheduleEnabled();
    }

    private void UpdateScheduleEnabled()
    {
        bool enabled = _useSchedule.Checked;
        _start.Enabled = enabled;
        _end.Enabled = enabled;
    }

    private void BrowseLog()
    {
        try
        {
            Directory.CreateDirectory(AppSettings.FolderPath);
        }
        catch (Exception)
        {
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv|Todos os arquivos (*.*)|*.*",
            FileName = Path.GetFileName(_logPath.Text),
            InitialDirectory = AppSettings.FolderPath,
            OverwritePrompt = false,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _logPath.Text = dialog.FileName;
    }

    private void SaveClicked()
    {
        if (_useSchedule.Checked && _start.Value.TimeOfDay == _end.Value.TimeOfDay)
        {
            MessageBox.Show(this, "Início e fim não podem ser iguais quando o horário está ativo.",
                "LoudEnv", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_device.SelectedItem is not string device)
            device = "";

        string logPath = _logPath.Text.Trim();
        if (logPath.Length == 0)
        {
            MessageBox.Show(this, "Informe o caminho do log.",
                "LoudEnv", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(logPath);
            string? folder = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or ArgumentException or NotSupportedException)
        {
            MessageBox.Show(this, $"Não foi possível usar o caminho do log: {ex.Message}",
                "LoudEnv", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Settings = new AppSettings
        {
            Threshold = (float)_threshold.Value,
            DeviceName = device,
            ScheduleEnabled = _useSchedule.Checked,
            Start = TimeOnly.FromDateTime(_start.Value),
            End = TimeOnly.FromDateTime(_end.Value),
            LogPath = fullPath,
            AlertSound = _sound.Checked,
        };

        try
        {
            StartupRegistration.SetEnabled(_startWithWindows.Checked);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
                                       or System.Security.SecurityException
                                       or IOException
                                       or InvalidOperationException)
        {
            MessageBox.Show(this, $"Não foi possível alterar a inicialização com o Windows: {ex.Message}",
                "LoudEnv", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            Settings.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Não foi possível salvar as configurações: {ex.Message}",
                "LoudEnv", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
