using System.Drawing;
using System.Windows.Forms;

namespace LocationSharer;

public sealed class ConsentDialog : Form
{
    public bool Accepted { get; private set; }

    public ConsentDialog()
    {
        Text = "Location Sharing Consent";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Width = 540;
        Height = 280;
        BackColor = Color.Black; // True Black Background

        var title = new Label
        {
            Left = 20,
            Top = 20,
            Width = 480,
            Height = 28,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(99, 102, 241), // Indigo-500
            Text = "Allow this app to share your location?",
            BackColor = Color.Transparent
        };

        var body = new Label
        {
            Left = 20,
            Top = 55,
            Width = 480,
            Height = 110,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(161, 161, 170), // Zinc-400
            Text = "This app only starts location sharing after you approve it. You can stop sharing any time from the tray icon or the main window. Location data is sent only to the endpoint you configure. Additionally, this app allows you to monitor site analytics and Discord/Spotify status from your website's API endpoints.",
            BackColor = Color.Transparent
        };

        var accept = new Button
        {
            Text = "Allow",
            Width = 110,
            Height = 32,
            Left = 290,
            Top = 185,
            DialogResult = DialogResult.OK
        };
        StyleButton(accept, Color.FromArgb(99, 102, 241), Color.FromArgb(79, 70, 229), Color.White);
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
            Left = 405,
            Top = 185,
            DialogResult = DialogResult.Cancel
        };
        StyleButton(decline, Color.FromArgb(39, 39, 42), Color.FromArgb(63, 63, 70), Color.White);
        decline.Click += (_, _) =>
        {
            Accepted = false;
            Close();
        };

        Controls.AddRange([title, body, accept, decline]);
        AcceptButton = accept;
        CancelButton = decline;
    }

    private static void StyleButton(Button button, Color backColor, Color hoverColor, Color foreColor)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = backColor;
        button.ForeColor = foreColor;
        button.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.MouseEnter += (_, _) => button.BackColor = hoverColor;
        button.MouseLeave += (_, _) => button.BackColor = backColor;
    }
}
