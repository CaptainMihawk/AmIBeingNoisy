namespace LoudEnv.Tray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "LoudEnv.Tray.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("O LoudEnv já está em execução na bandeja.", "LoudEnv",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
