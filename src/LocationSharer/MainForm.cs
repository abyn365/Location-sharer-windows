using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LocationSharer;

public sealed class MainForm : Form
{
    private readonly AppSettings _settings;
    private readonly LocationService _service;
    private readonly PersonalSiteService _siteService;
    private readonly NotifyIcon _tray;
    private bool _isExiting;

    // Settings UI Controls
    private readonly TextBox _endpointBox;
    private readonly TextBox _secretBox;
    private readonly NumericUpDown _intervalBox;
    private readonly Label _statusLabel;
    private readonly Button _toggleButton;
    private readonly Button _saveButton;
    private readonly Button _syncNowButton;

    // Monitor UI Controls (Visitor stats)
    private readonly Label _activeNum;
    private readonly Label _viewsNum;
    private readonly Label _uniquesNum;

    // Monitor UI Controls (Discord & Spotify status)
    private readonly Label _discordDot;
    private readonly Label _discordStatusLbl;
    private readonly Label _discordActivityLbl;
    private readonly PictureBox _spotifyArt;
    private readonly Label _spotifyTrack;
    private readonly Label _spotifyArtist;
    private readonly Label _spotifyAlbum;
    private readonly Button _playSpotifyBtn;

    private string? _currentArtUrl;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    // Windows DWM APIs for custom dark title bars and backdrop
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;
    private const int DWMWA_MICA_EFFECT = 1029; // For Build 22000
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38; // For Build 22621+

    // DWM Backdrop types
    private const int DWMSBT_AUTO = 0;
    private const int DWMSBT_MAINWINDOW = 2; // Mica
    private const int DWMSBT_TABBEDWINDOW = 3;
    private const int DWMSBT_ACRYLIC = 4;

    private static void UseImmersiveDarkMode(IntPtr handle, bool enabled)
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            var attribute = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18985)
                ? DWMWA_USE_IMMERSIVE_DARK_MODE
                : DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
            int useDark = enabled ? 1 : 0;
            DwmSetWindowAttribute(handle, attribute, ref useDark, sizeof(int));
        }
    }

    private static void SetTitleBarColor(IntPtr handle, Color color)
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            int gdiColor = color.R | (color.G << 8) | (color.B << 16);
            DwmSetWindowAttribute(handle, DWMWA_CAPTION_COLOR, ref gdiColor, sizeof(int));
        }
    }

    private static void SetTitleTextColor(IntPtr handle, Color color)
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            int gdiColor = color.R | (color.G << 8) | (color.B << 16);
            DwmSetWindowAttribute(handle, DWMWA_TEXT_COLOR, ref gdiColor, sizeof(int));
        }
    }

    private static void SetWindowBackdrop(IntPtr handle)
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
        {
            // Try Acrylic (4) first, fall back to Mica (2)
            int backdropType = DWMSBT_ACRYLIC;
            int result = DwmSetWindowAttribute(handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
            if (result != 0)
            {
                backdropType = DWMSBT_MAINWINDOW;
                DwmSetWindowAttribute(handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
            }
        }
        else if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            // Fallback for original Windows 11 release (Build 22000)
            int trueValue = 1;
            DwmSetWindowAttribute(handle, DWMWA_MICA_EFFECT, ref trueValue, sizeof(int));
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UseImmersiveDarkMode(Handle, true);
        SetTitleTextColor(Handle, Color.White);
        SetWindowBackdrop(Handle);
    }

    public MainForm()
    {
        _settings = SettingsStore.Load();
        _service = new LocationService(_settings, SetStatus);
        _siteService = new PersonalSiteService(_settings);

        // Wire up personal site service events
        _siteService.VisitorStatsUpdated += OnVisitorStatsUpdated;
        _siteService.DiscordStatusUpdated += OnDiscordStatusUpdated;
        _siteService.StatusMessageUpdated += OnSiteStatusMessageUpdated;

        // Form settings
        Text = "LocationSharer Dashboard";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 880;
        Height = 560;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimumSize = new Size(880, 560);
        BackColor = Color.Black;
        DoubleBuffered = true;

        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

        // Font family
        var uiFont = new Font("Segoe UI", 9f);
        var boldFont = new Font("Segoe UI", 9f, FontStyle.Bold);

        // --- Left Panel: Settings (320px wide) ---
        var settingsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(170, 8, 8, 12), // Semi-transparent very dark surface
            Padding = new Padding(0)
        };

        // Use FlowLayoutPanel to completely handle vertical stacking dynamically and avoid any overlaps
        var leftFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            BackColor = Color.Transparent,
            Padding = new Padding(20)
        };

        var appTitle = new Label
        {
            Text = "LOCATION SHARER",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(14, 165, 233), // Sky-500 Accent
            Margin = new Padding(0, 0, 0, 2),
            AutoSize = true
        };

        var appSubtitle = new Label
        {
            Text = "Secure location tracking & status monitor",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(161, 161, 170), // Zinc-400
            Margin = new Padding(0, 0, 0, 20),
            AutoSize = true,
            UseMnemonic = false // Fix ampersand rendering
        };

        var endpointLabel = new Label
        {
            Text = "Endpoint URL",
            Font = boldFont,
            ForeColor = Color.FromArgb(161, 161, 170),
            Margin = new Padding(0, 0, 0, 5),
            AutoSize = true
        };

        _endpointBox = new TextBox
        {
            Width = 280,
            Text = _settings.EndpointUrl,
            Margin = new Padding(0, 0, 0, 15)
        };
        StyleTextBox(_endpointBox);

        var secretLabel = new Label
        {
            Text = "Shared Secret",
            Font = boldFont,
            ForeColor = Color.FromArgb(161, 161, 170),
            Margin = new Padding(0, 0, 0, 5),
            AutoSize = true
        };

        _secretBox = new TextBox
        {
            Width = 280,
            Text = _settings.Secret,
            UseSystemPasswordChar = true,
            Margin = new Padding(0, 0, 0, 15)
        };
        StyleTextBox(_secretBox);

        var intervalLabel = new Label
        {
            Text = "Interval (minutes)",
            Font = boldFont,
            ForeColor = Color.FromArgb(161, 161, 170),
            Margin = new Padding(0, 0, 0, 5),
            AutoSize = true
        };

        _intervalBox = new NumericUpDown
        {
            Width = 100,
            Minimum = 1,
            Maximum = 1440,
            Value = Math.Clamp(_settings.IntervalMinutes, 1, 1440),
            Margin = new Padding(0, 0, 0, 20)
        };
        StyleNumeric(_intervalBox);

        // Buttons Flow Panel (Side by Side)
        var buttonsPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Width = 280,
            Height = 38,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 12)
        };

        _toggleButton = new Button
        {
            Text = "Start sharing",
            Width = 135,
            Height = 36,
            Margin = new Padding(0, 0, 10, 0)
        };
        StyleButton(_toggleButton, Color.FromArgb(14, 165, 233), Color.FromArgb(3, 105, 161), Color.White); // Sky-500 & Sky-700
        _toggleButton.Click += async (_, _) => await ToggleSharingAsync();

        _saveButton = new Button
        {
            Text = "Save settings",
            Width = 135,
            Height = 36,
            Margin = new Padding(0)
        };
        StyleButton(_saveButton, Color.FromArgb(39, 39, 42), Color.FromArgb(63, 63, 70), Color.White);
        _saveButton.Click += (_, _) => SaveSettings();

        buttonsPanel.Controls.AddRange([_toggleButton, _saveButton]);

        _syncNowButton = new Button
        {
            Text = "Share Location Now",
            Width = 280,
            Height = 36,
            Margin = new Padding(0, 0, 0, 15)
        };
        StyleButton(_syncNowButton, Color.FromArgb(39, 39, 42), Color.FromArgb(63, 63, 70), Color.White);
        _syncNowButton.Click += async (_, _) =>
        {
            _syncNowButton.Enabled = false;
            _syncNowButton.Text = "Sending...";
            try
            {
                await _service.SendOnceAsync();
            }
            finally
            {
                _syncNowButton.Enabled = true;
                _syncNowButton.Text = "Share Location Now";
            }
        };

        _statusLabel = new Label
        {
            Text = "Ready.",
            Width = 280,
            Height = 48,
            MinimumSize = new Size(280, 48),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(161, 161, 170),
            Margin = new Padding(0, 4, 0, 0),
            AutoEllipsis = true
        };

        leftFlow.Controls.AddRange([
            appTitle, appSubtitle,
            endpointLabel, _endpointBox,
            secretLabel, _secretBox,
            intervalLabel, _intervalBox,
            buttonsPanel, _syncNowButton,
            _statusLabel
        ]);

        settingsPanel.Controls.Add(leftFlow);

        // --- Right Panel: Monitor Panel ---
        var monitorPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent, // Fully transparent to let DWM backdrop show through
            Padding = new Padding(20)
        };

        var monitorTitle = new Label
        {
            Text = "PERSONAL SITE MONITOR",
            Left = 20,
            Top = 20,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(161, 161, 170),
            AutoSize = true
        };

        // Visitor Stats Card (RoundedPanel) - widened for better stat spacing
        var analyticsCard = new RoundedPanel
        {
            Left = 20,
            Top = 50,
            Width = 500,
            Height = 140
        };

        // FlowLayoutPanel for the title and indicator side-by-side without overlaps
        var analyticsTitleFlow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Left = 15,
            Top = 12,
            Width = 470,
            Height = 22,
            BackColor = Color.Transparent
        };

        var analyticsTitle = new Label
        {
            Text = "Live Analytics",
            Font = boldFont,
            ForeColor = Color.FromArgb(161, 161, 170),
            BackColor = Color.Transparent,
            AutoSize = true,
            Margin = new Padding(0)
        };

        var liveIndicator = new Label
        {
            Text = "● Live",
            Font = boldFont,
            ForeColor = Color.FromArgb(16, 185, 129), // Emerald-500
            BackColor = Color.Transparent,
            AutoSize = true,
            Margin = new Padding(8, 0, 0, 0)
        };

        analyticsTitleFlow.Controls.AddRange([analyticsTitle, liveIndicator]);

        // Modular panels to hold the statistics cleanly side-by-side - widened to 150px
        var activePanel = CreateStatGroup("Active Visitors", Color.FromArgb(16, 185, 129), out _activeNum);
        activePanel.Left = 15;
        activePanel.Top = 38;

        var viewsPanel = CreateStatGroup("Pageviews Today", Color.White, out _viewsNum);
        viewsPanel.Left = 175;
        viewsPanel.Top = 38;

        var uniquesPanel = CreateStatGroup("Unique Visitors", Color.White, out _uniquesNum);
        uniquesPanel.Left = 335;
        uniquesPanel.Top = 38;

        analyticsCard.Controls.AddRange([analyticsTitleFlow, activePanel, viewsPanel, uniquesPanel]);

        // Discord & Spotify Card (RoundedPanel) - widened and height adjusted
        var activityCard = new RoundedPanel
        {
            Left = 20,
            Top = 205,
            Width = 500,
            Height = 280
        };

        var activityTitle = new Label
        {
            Text = "Discord & Spotify Presence",
            Left = 15,
            Font = boldFont,
            ForeColor = Color.FromArgb(161, 161, 170),
            BackColor = Color.Transparent,
            AutoSize = true,
            UseMnemonic = false // Fix ampersand rendering
        };

        _discordDot = new Label
        {
            Text = "●",
            Left = 15,
            Width = 18,
            Height = 24,
            Font = new Font("Segoe UI", 12f),
            ForeColor = Color.FromArgb(161, 161, 170),
            BackColor = Color.Transparent,
            AutoSize = false
        };

        _discordStatusLbl = new Label
        {
            Text = "Offline",
            Left = 35,
            Width = 440,
            Height = 24,
            Font = boldFont,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            AutoEllipsis = true,
            AutoSize = false
        };

        _discordActivityLbl = new Label
        {
            Text = "No active status details",
            Left = 15,
            Width = 460,
            AutoSize = true,
            MaximumSize = new Size(460, 0),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(161, 161, 170),
            BackColor = Color.Transparent,
            AutoEllipsis = true
        };

        var separator = new Panel
        {
            Height = 1,
            Width = 460,
            Left = 15,
            BackColor = Color.FromArgb(60, 255, 255, 255) // Subtle glass-divider
        };

        var spotifyHeader = new Label
        {
            Text = "CURRENTLY PLAYING",
            Left = 15,
            Width = 200,
            Height = 16,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(161, 161, 170),
            BackColor = Color.Transparent
        };

        _spotifyArt = new PictureBox
        {
            Left = 15,
            Width = 80,
            Height = 80,
            BackColor = Color.FromArgb(24, 24, 27), // Zinc-800
            SizeMode = PictureBoxSizeMode.Zoom
        };

        _spotifyTrack = new Label
        {
            Text = "Not listening to Spotify",
            Left = 110,
            Width = 360,
            Height = 28,
            MaximumSize = new Size(360, 28),
            Font = boldFont,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            AutoEllipsis = true
        };

        _spotifyArtist = new Label
        {
            Text = "-",
            Left = 110,
            Width = 360,
            Height = 22,
            MaximumSize = new Size(360, 22),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(161, 161, 170),
            BackColor = Color.Transparent,
            AutoEllipsis = true
        };

        _spotifyAlbum = new Label
        {
            Text = "-",
            Left = 110,
            Width = 360,
            Height = 22,
            MaximumSize = new Size(360, 22),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(161, 161, 170),
            BackColor = Color.Transparent,
            AutoEllipsis = true
        };

        _playSpotifyBtn = new Button
        {
            Text = "Play on Spotify",
            Left = 110,
            Width = 120,
            Height = 28,
            Visible = false
        };
        StyleButton(_playSpotifyBtn, Color.FromArgb(14, 165, 233), Color.FromArgb(3, 105, 161), Color.White); // Sky-500 & Sky-700
        _playSpotifyBtn.Click += (_, _) =>
        {
            if (_playSpotifyBtn.Tag is string url && !string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open link: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        };

        // Relative positioning to layout Discord & Spotify elements dynamically
        int actTop = 12;
        activityTitle.Top = actTop;
        actTop += activityTitle.Height + 8;

        _discordDot.Top = actTop - 2;
        _discordStatusLbl.Top = actTop;
        actTop += Math.Max(_discordDot.Height, _discordStatusLbl.Height) + 4;

        _discordActivityLbl.Top = actTop;
        // Use the auto-sized height for dynamic layout
        actTop += _discordActivityLbl.Height + 10;

        separator.Top = actTop;
        actTop += separator.Height + 10;

        spotifyHeader.Top = actTop;
        actTop += spotifyHeader.Height + 8;

        _spotifyArt.Top = actTop;

        int trackTop = actTop;
        _spotifyTrack.Top = trackTop;
        trackTop += _spotifyTrack.Height + 4;

        _spotifyArtist.Top = trackTop;
        trackTop += _spotifyArtist.Height + 4;

        _spotifyAlbum.Top = trackTop;
        trackTop += _spotifyAlbum.Height + 4;

        _playSpotifyBtn.Top = trackTop;

        activityCard.Controls.AddRange([
            activityTitle, _discordDot, _discordStatusLbl, _discordActivityLbl,
            separator, spotifyHeader, _spotifyArt, _spotifyTrack, _spotifyArtist, _spotifyAlbum, _playSpotifyBtn
        ]);

        monitorPanel.Controls.AddRange([monitorTitle, analyticsCard, activityCard]);

        // Main Grid Layout: Isolates the left settings pane from the right monitor panel completely
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Add panels to the grid cells
        mainLayout.Controls.Add(settingsPanel, 0, 0);
        mainLayout.Controls.Add(monitorPanel, 1, 0);

        Controls.Add(mainLayout);

        // Tray Icon setup
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
        VisibleChanged += OnVisibleChanged;
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
        };

        UpdateButtonStates();
    }

    private static Panel CreateStatGroup(string labelText, Color numColor, out Label numberLabel)
    {
        var panel = new Panel
        {
            Width = 150,
            Height = 80,
            BackColor = Color.Transparent
        };

        numberLabel = new Label
        {
            Text = "-",
            Dock = DockStyle.Top,
            Height = 50,
            Width = 150,
            MinimumSize = new Size(150, 50),
            MaximumSize = new Size(150, 50),
            Font = new Font("Segoe UI", 24f, FontStyle.Bold),
            ForeColor = numColor,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent,
            AutoEllipsis = true,
            AutoSize = false
        };

        var textLabel = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(161, 161, 170),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        };

        panel.Controls.Add(textLabel);
        panel.Controls.Add(numberLabel);

        return panel;
    }

    private static void StyleTextBox(TextBox textBox)
    {
        textBox.BackColor = Color.FromArgb(15, 15, 20); // Deep Zinc Slate
        textBox.ForeColor = Color.FromArgb(250, 250, 250); // Zinc-50
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.Font = new Font("Segoe UI", 10f);
    }

    private static void StyleNumeric(NumericUpDown numeric)
    {
        numeric.BackColor = Color.FromArgb(15, 15, 20);
        numeric.ForeColor = Color.FromArgb(250, 250, 250);
        numeric.BorderStyle = BorderStyle.FixedSingle;
        numeric.Font = new Font("Segoe UI", 10f);
    }

    private static void StyleButton(Button button, Color backColor, Color hoverColor, Color foreColor)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = Color.FromArgb(230, backColor.R, backColor.G, backColor.B);
        button.ForeColor = foreColor;
        button.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        var originalBackColor = backColor;
        button.MouseEnter += (_, _) => button.BackColor = Color.FromArgb(230, hoverColor.R, hoverColor.G, hoverColor.B);
        button.MouseLeave += (_, _) => button.BackColor = Color.FromArgb(230, originalBackColor.R, originalBackColor.G, originalBackColor.B);
    }

    private async void OnVisibleChanged(object? sender, EventArgs e)
    {
        if (Visible)
        {
            _siteService.Start();
        }
        else
        {
            await _siteService.StopAsync();
        }
    }

    private void OnVisitorStatsUpdated(VisitorStats? stats)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnVisitorStatsUpdated(stats));
            return;
        }

        if (stats == null)
        {
            _activeNum.Text = "-";
            _viewsNum.Text = "-";
            _uniquesNum.Text = "-";
            return;
        }

        _activeNum.Text = stats.Active.ToString();
        _viewsNum.Text = stats.Pageviews.ToString();
        _uniquesNum.Text = stats.Uniques.ToString();
    }

    private void OnDiscordStatusUpdated(DiscordStatus? status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnDiscordStatusUpdated(status));
            return;
        }

        if (status == null)
        {
            _discordDot.ForeColor = Color.FromArgb(161, 161, 170);
            _discordStatusLbl.Text = "Offline / Unavailable";
            _discordActivityLbl.Text = "No active details";
            _spotifyArt.Image = null;
            _spotifyTrack.Text = "Not listening to Spotify";
            _spotifyArtist.Text = "-";
            _spotifyAlbum.Text = "-";
            _playSpotifyBtn.Visible = false;
            _currentArtUrl = null;
            return;
        }

        if (status.IsOnline)
        {
            _discordStatusLbl.Text = $"Online on {status.ActiveDevice ?? "Device"}";
            switch (status.Status?.ToLowerInvariant())
            {
                case "online":
                    _discordDot.ForeColor = Color.FromArgb(16, 185, 129); // Emerald
                    break;
                case "idle":
                    _discordDot.ForeColor = Color.FromArgb(245, 158, 11); // Amber
                    break;
                case "dnd":
                    _discordDot.ForeColor = Color.FromArgb(239, 68, 68); // Red
                    _discordStatusLbl.Text = $"Do Not Disturb ({status.ActiveDevice ?? "Device"})";
                    break;
                default:
                    _discordDot.ForeColor = Color.FromArgb(16, 185, 129);
                    break;
            }
        }
        else
        {
            _discordDot.ForeColor = Color.FromArgb(161, 161, 170);
            _discordStatusLbl.Text = "Offline";
        }

        if (status.Activity != null)
        {
            var act = status.Activity;
            string details = "";
            if (!string.IsNullOrEmpty(act.Details)) details += act.Details;
            if (!string.IsNullOrEmpty(act.State))
            {
                if (details.Length > 0) details += " • ";
                details += act.State;
            }
            _discordActivityLbl.Text = $"{act.Name}: {(string.IsNullOrEmpty(details) ? "Active" : details)}";
        }
        else
        {
            _discordActivityLbl.Text = "No active status details";
        }

        if (status.Spotify != null)
        {
            var track = status.Spotify;
            _spotifyTrack.Text = track.Song;
            _spotifyArtist.Text = $"by {track.Artist}";
            _spotifyAlbum.Text = $"on {track.Album}";
            _playSpotifyBtn.Tag = track.SongUrl;
            _playSpotifyBtn.Visible = true;

            _ = LoadAlbumArtAsync(track.AlbumArtUrl);
        }
        else
        {
            _spotifyArt.Image = null;
            _spotifyTrack.Text = "Not listening to Spotify";
            _spotifyArtist.Text = "-";
            _spotifyAlbum.Text = "-";
            _playSpotifyBtn.Visible = false;
            _currentArtUrl = null;
        }
    }

    private void OnSiteStatusMessageUpdated(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[SiteMonitor] {message}");
    }

    private async Task LoadAlbumArtAsync(string url)
    {
        if (_currentArtUrl == url) return;
        _currentArtUrl = url;

        if (string.IsNullOrWhiteSpace(url))
        {
            _spotifyArt.Image = null;
            return;
        }

        try
        {
            var bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
            using var ms = new MemoryStream(bytes);
            var img = Image.FromStream(ms);

            if (InvokeRequired)
            {
                BeginInvoke(() => { _spotifyArt.Image = img; });
            }
            else
            {
                _spotifyArt.Image = img;
            }
        }
        catch
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => { _spotifyArt.Image = null; });
            }
            else
            {
                _spotifyArt.Image = null;
            }
        }
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

        SetStatus("Consent accepted. Sharing is stopped.");
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
            _isExiting = true;
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
        UpdateButtonStates();
        SetStatus("Sharing started.");
        await _service.SendOnceAsync();
    }

    private async Task StopSharingAsync()
    {
        await _service.StopAsync();
        UpdateButtonStates();
        SetStatus("Sharing stopped.");
    }

    private void UpdateButtonStates()
    {
        if (InvokeRequired)
        {
            BeginInvoke(UpdateButtonStates);
            return;
        }

        if (_service.IsRunning)
        {
            _toggleButton.Text = "Stop sharing";
            _toggleButton.BackColor = Color.FromArgb(230, 239, 68, 68); // Red-500 with alpha
            _toggleButton.MouseEnter += (_, _) => _toggleButton.BackColor = Color.FromArgb(230, 220, 38, 38);
            _toggleButton.MouseLeave += (_, _) => _toggleButton.BackColor = Color.FromArgb(230, 239, 68, 68);
        }
        else
        {
            _toggleButton.Text = "Start sharing";
            _toggleButton.BackColor = Color.FromArgb(230, 14, 165, 233); // Sky-500 with alpha
            _toggleButton.MouseEnter += (_, _) => _toggleButton.BackColor = Color.FromArgb(230, 3, 105, 161);
            _toggleButton.MouseLeave += (_, _) => _toggleButton.BackColor = Color.FromArgb(230, 14, 165, 233);
        }
    }

    private void SaveSettings()
    {
        _settings.EndpointUrl = _endpointBox.Text.Trim();
        _settings.Secret = _secretBox.Text.Trim();
        _settings.IntervalMinutes = (int)_intervalBox.Value;
        SettingsStore.Save(_settings);

        _service.UpdateSettings(_settings);
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
        if (!_isExiting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();

            _tray.BalloonTipTitle = "LocationSharer Dashboard";
            _tray.BalloonTipText = "App is still running in the system tray.";
            _tray.ShowBalloonTip(1500);
            return;
        }

        _tray.Visible = false;
        _tray.Dispose();
        _service.StopAsync().GetAwaiter().GetResult();
        _service.Dispose();
        _siteService.StopAsync().GetAwaiter().GetResult();
        _siteService.Dispose();
    }
}
