namespace LocationSharer;

public sealed class ConsentDialog : Form
{
    public bool Accepted { get; private set; }

    public ConsentDialog()
    {
        Text = "Location sharing consent";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Width = 540;
        Height = 280;

        var title = new Label
        {
            Left = 20,
            Top = 20,
            Width = 480,
            Height = 24,
            Font = new Font(Font.FontFamily, 12, FontStyle.Bold),
            Text = "Allow this app to share your location?"
        };

        var body = new Label
        {
            Left = 20,
            Top = 55,
            Width = 480,
            Height = 95,
            Text = "This app only starts location sharing after you approve it. You can stop sharing any time from the tray icon or the main window. Location data is sent only to the endpoint you configure."
        };

        var accept = new Button
        {
            Text = "Allow",
            Width = 110,
            Height = 32,
            Left = 300,
            Top = 185,
            DialogResult = DialogResult.OK
        };
        accept.Click += (_, _) =>
        {
            Accepted = true;
            Close();
        };

        var decline = new Button
        {
            Text = "Decline",
            Width = 110,
            Height = 32,
            Left = 415,
            Top = 185,
            DialogResult = DialogResult.Cancel
        };
        decline.Click += (_, _) =>
        {
            Accepted = false;
            Close();
        };

        Controls.AddRange([title, body, accept, decline]);
        AcceptButton = accept;
        CancelButton = decline;
    }
}
