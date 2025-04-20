using System;
using System.Runtime.InteropServices;

namespace FxWorth
{
    /// <summary>
    /// Provides methods to control system power settings, specifically 
    /// preventing the system from sleeping or the display from turning off.
    /// </summary>
    internal class PowerManager
    {
        // Import the SetThreadExecutionState function from kernel32.dll
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        // Define the EXECUTION_STATE enumeration to specify the desired power state
        [FlagsAttribute]
        private enum EXECUTION_STATE : uint
        {
            // Prevents the system from automatically entering away mode.
            ES_AWAYMODE_REQUIRED = 0x00000040,

            // Sets the execution state to continuous, meaning the system will not sleep automatically.
            ES_CONTINUOUS = 0x80000000,

            // Indicates that display activity is being performed, preventing the display from turning off.
            ES_DISPLAY_REQUIRED = 0x00000002,

            // Indicates that system activity is being performed, preventing the system from going to sleep.
            ES_SYSTEM_REQUIRED = 0x00000001
        }

        /// Prevents the system from going to sleep and the display from turning off.
        public static void PreventSleep()
        {
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS |
                                     EXECUTION_STATE.ES_SYSTEM_REQUIRED |
                                     EXECUTION_STATE.ES_DISPLAY_REQUIRED);
        }

        /// Allows the system to sleep and the display to turn off according to system settings.
        public static void AllowSleep()
        {
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
        }
    }
}