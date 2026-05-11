namespace SwtorDailyTool;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new StartupContext());
    }

    private sealed class StartupContext : ApplicationContext
    {
        public StartupContext()
        {
            var splash = new SplashScreen();
            splash.Show();
            Application.DoEvents();

            // Build main form while splash holds so the wait feels intentional.
            splash.SetStatus("Loading data files…");
            Application.DoEvents();
            var main = new Form1();
            main.FormClosed += (_, _) => ExitThread();

            splash.StartFadeOut(() =>
            {
                main.Show();
                main.Activate();
            });
        }
    }
}