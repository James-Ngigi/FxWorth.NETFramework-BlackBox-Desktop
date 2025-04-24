using System;
using System.Windows.Forms;
using NLog;

namespace FxWorth
{
    /// <summary>
    /// The `Program` class contains the entry point (`Main` method) for the FxWorth Windows Forms application.
    /// It initializes the application, handles unhandled exceptions, and starts the main form.
    /// </summary>
    static class Program
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        [STAThread]
        static void Main()
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;

            currentDomain.UnhandledException += HandleUnhandled;

            Application.EnableVisualStyles();

            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new FxWorth());
        }

        // Handles unhandled exceptions in the application.
        private static void HandleUnhandled(object sender, UnhandledExceptionEventArgs e)
        {
            logger.Error((Exception)e.ExceptionObject, "Fatal exception");
        }
    }
}