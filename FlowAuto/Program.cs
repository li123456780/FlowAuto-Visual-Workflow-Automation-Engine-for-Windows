using FlowAuto;

namespace FlowAuto;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Application failed to start:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                "FlowAuto - Startup Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
