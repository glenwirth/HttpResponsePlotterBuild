namespace HttpResponsePlotter;

internal static class Program
{
    /// <summary>
    /// Usage: HttpResponsePlotter.exe [--config &lt;path-to-settings.json&gt;]
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        string settingsPath = AppSettings.DefaultPath;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--config", StringComparison.OrdinalIgnoreCase) ||
                args[i].Equals("-c", StringComparison.OrdinalIgnoreCase))
            {
                settingsPath = Path.GetFullPath(args[i + 1]);
            }
        }

        Application.Run(new MainForm(settingsPath));
    }
}
