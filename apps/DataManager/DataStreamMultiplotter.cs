using System;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Diagnostics;
using Libfmax;
using LSL;


namespace DataManager
{
    public partial class MainForm : Form
    {
        private class DataStreamMultiplotter
        {

            #region constant

            private const int PLOT_TIME_WINDOW = 5000;    // ms
            private const int PLOT_REFRESH_RATE = 30;   // ms

            #endregion


            #region variables

            public List<DataStream> Streams { get; set; } = new List<DataStream>();
            private FormPlotter plotter;
            public delegate void InfoMessageDelegate(object sender, Info info);
            public event InfoMessageDelegate InfoMessageReceived;

            #endregion
            

            /// <summary>
            /// Called when an info message is received. Override this to handle custom info messages. The default implementation invokes the InfoMessageReceived event.
            /// </summary>
            /// <param name="info">The info message received from the server or null</param>
            protected virtual void InfoMessage(Info info)
            {
                InfoMessageReceived?.Invoke(this, info);
            }

            /// <summary>
            /// Initializes the plotter and all streams. This is called from the constructor and can be called multiple times
            /// </summary>
            public void InitPlotter()
            {
                /// Closes the plotter and closes the plotter.
                if (plotter != null && !plotter.IsDisposed)
                {
                    plotter.Close();
                    plotter = null;
                }

                plotter = new FormPlotter("Multiplot", PLOT_TIME_WINDOW, PLOT_REFRESH_RATE, token);
                plotter.Show();
                plotter.WindowState = FormWindowState.Normal;

                foreach (var stream in Streams)
                {
                    stream.DataInitialized += plotter.DataInitialized;
                    stream.NewDataReceived += plotter.NewDataReceived;
                    stream.TriggerDataInit();
                }
            }
        }
    }
}