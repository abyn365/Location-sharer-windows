namespace LocationSharer;

public sealed class MainForm : Form
{
    private readonly AppSettings _settings;
    private readonly LocationService _service;
    private readonly NotifyIcon _tray;
    private readonly TextBox _endpointBox;
    private readonly TextBox _secretBox;
    private readonly NumericUpDown _intervalBox;
    private readonly Label _statusLabel;
    private readonly Button _toggleButton;
    private readonly Button _saveButton;

    public MainForm()
    {
        _settings = SettingsStore.Load();

        Text = "LocationSharer";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 720;
        Height = 420;
        MinimumSize = new Size(640, 360);

        var header = new Label
        {
            Left = 20,
            Top = 16,
            Width = 640,
            Height = 30,
            Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
            Text = "LocationSharer"
        };

        var intro = new Label
        {
            Left = 20,
            Top = 50,
            Width = 650,
            Height = 36,
            Text = "Visible tray app with explicit consent, Start / Stop sharing controls, and a fallback from Windows Location API to IP geolocation."
        };

        var endpointLabel = new Label { Left = 20, Top = 105, Width = 200, Text = "Endpoint URL" };
        _endpointBox = new TextBox { Left = 20, Top = 128, Width = 660, Text = _settings.EndpointUrl };

        var secretLabel = new Label { Left = 20, Top = 164, Width = 200, Text = "Shared secret" };
        _secretBox = new TextBox { Left = 20, Top = 187, Width = 660, Text = _settings.Secret, UseSystemPasswordChar = true };

        var intervalLabel = new Label { Left = 20, Top = 223, Width = 220, Text = "Update interval (minutes)" };
        _intervalBox = new NumericUpDown
        {
            Left = 20,
            Top = 246,
            Width = 120,
            Minimum = 1,
            Maximum = 1440,
            Value = Math.Clamp(_settings.IntervalMinutes, 1, 1440)
        };

        _toggleButton = new Button
        {
            Left = 20,
            Top = 290,
            Width = 160,
            Height = 36,
            Text = "Start sharing"
        };
        _toggleButton.Click += async (_, _) => await ToggleSharingAsync();

        _saveButton = new Button
        {
            Left = 190,
            Top = 290,
            Width = 140,
            Height = 36,
            Text = "Save settings"
        };
        _saveButton.Click += (_, _) => SaveSettings();

        _statusLabel = new Label
        {
            Left = 20,
            Top = 334,
            Width = 660,
            Height = 28,
            Text = "Ready."
        };

        Controls.AddRange([
            header, intro,
            endpointLabel, _endpointBox,
            secretLabel, _secretBox,
            intervalLabel, _intervalBox,
            _toggleButton, _saveButton,
            _statusLabel
        ]);

        _service = new LocationService(_settings, SetStatus);

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "LocationSharer",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        _tray.DoubleClick += (_, _) => ShowWindow();

        Shown += OnShown;
        FormClosing += OnFormClosing;
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
        };
    }

    private async void OnShown(object? sender, EventArgs e)
    {
        if (!_settings.ConsentAccepted)
        {
            using var dialog = new ConsentDialog();
            var result = dialog.ShowDialog(this);
            if (!dialog.Accepted || result != DialogResult.OK)
            {
                Close();
                return;
            }

            _settings.ConsentAccepted = true;
            SettingsStore.Save(_settings);
        }

        Hide();
        ShowInTaskbar = false;
        _tray.BalloonTipTitle = "LocationSharer";
        _tray.BalloonTipText = "Ready. Open the tray menu to start sharing.";
        _tray.ShowBalloonTip(1500);

        SetStatus("Consent accepted. Sharing is stopped until you press Start sharing.");
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("Open");
        showItem.Click += (_, _) => ShowWindow();

        var startItem = new ToolStripMenuItem("Start sharing");
        startItem.Click += async (_, _) => await StartSharingAsync();

        var stopItem = new ToolStripMenuItem("Stop sharing");
        stopItem.Click += async (_, _) => await StopSharingAsync();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += async (_, _) =>
        {
            await StopSharingAsync();
            Close();
        };

        menu.Items.AddRange([showItem, startItem, stopItem, new ToolStripSeparator(), exitItem]);
        return menu;
    }

    private void ShowWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        ShowInTaskbar = true;
        Activate();
        BringToFront();
    }

    private async Task ToggleSharingAsync()
    {
        if (_service.IsRunning)
        {
            await StopSharingAsync();
        }
        else
        {
            await StartSharingAsync();
        }
    }

    private async Task StartSharingAsync()
    {
        SaveSettings();

        if (string.IsNullOrWhiteSpace(_settings.Secret))
        {
            SetStatus("Add a shared secret before starting.");
            return;
        }

        if (!_settings.ConsentAccepted)
        {
            SetStatus("Consent is required.");
            return;
        }

        _service.Start();
        _toggleButton.Text = "Stop sharing";
        SetStatus("Sharing started.");
        await _service.SendOnceAsync();
    }

    private async Task StopSharingAsync()
    {
        await _service.StopAsync();
        _toggleButton.Text = "Start sharing";
        SetStatus("Sharing stopped.");
    }

    private void SaveSettings()
    {
        _settings.EndpointUrl = _endpointBox.Text.Trim();
        _settings.Secret = _secretBox.Text.Trim();
        _settings.IntervalMinutes = (int)_intervalBox.Value;
        SettingsStore.Save(_settings);
        SetStatus("Settings saved.");
    }

    private void SetStatus(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetStatus(message)));
            return;
        }

        _statusLabel.Text = message;
        _tray.Text = message.Length <= 63 ? message : message[..63];
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _tray.Visible = false;
        _tray.Dispose();
        _service.StopAsync().GetAwaiter().GetResult();
        _service.Dispose();
    }
}
