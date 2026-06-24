using System.Runtime.InteropServices;
using System.Threading;

namespace LocationSharer;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var mutex = new Mutex(initiallyOwned: true, name: @"Global\LocationSharerWindows", createdNew: out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "LocationSharer is already running.",
                "LocationSharer",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.Run(new MainForm());
    }
}
