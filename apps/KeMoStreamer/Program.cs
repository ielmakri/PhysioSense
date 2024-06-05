using System;
using System.Windows.Forms;
using System.Threading;
using Libfmax;

namespace KeMoStreamer
{
    static class Program
    {
        private static Mutex mutex = null;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            const string appName = "KemoStreamer";
            bool createdNew;

            mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("App is already running! Exiting the application");
                return;
            }
            MainForm.hookID = MainForm.SetHook(MainForm.proc);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            WinApi.UnhookWindowsHookEx(MainForm.hookID);
        }

    }
}
