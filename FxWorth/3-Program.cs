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
        /// Logger for recording fatal exceptions that occur during application execution.
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// The main entry point for the application.
        [STAThread]
        static void Main()
        {
            // Get the current application domain.
            AppDomain currentDomain = AppDomain.CurrentDomain;
            // Attach an event handler to handle unhandled exceptions within the application domain.
            currentDomain.UnhandledException += HandleUnhandled;

            // Enable Windows Forms visual styles for a more modern look and feel.
            Application.EnableVisualStyles();
            // Use compatible text rendering for better visual consistency across different Windows versions.
            Application.SetCompatibleTextRenderingDefault(false);

            // Create an instance of the main form (`FxWorth`) and run the application, starting the message loop.
            Application.Run(new FxWorth());
        }

        /// <summary>
        /// Event handler for handling unhandled exceptions that occur within the application domain.
        /// Logs the exception details using NLog.
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">`UnhandledExceptionEventArgs` containing information about the unhandled exception.</param>
        /// </summary>
        private static void HandleUnhandled(object sender, UnhandledExceptionEventArgs e)
        {
            logger.Error((Exception)e.ExceptionObject, "Fatal exception");
        }
    }
}