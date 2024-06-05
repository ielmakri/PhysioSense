// 🟡🔴🟠🟢🔵🟣🟤⚫
// TCP communication code sample based on
// -> https://github.com/jchristn/TcpTest
//

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using Libfmax;

namespace CaptivClient
{
    public partial class MainForm : Form
    {
        // Info sources
        private enum InfoSource : int
        {
            Main = 0,
            Configurator = 1,
        };
        // Info masks defined over each UI information display element and for each info source 
        private readonly Info.Mode[] InfoMaskConsole = new Info.Mode[2]{
                Info.Mode.All,
                Info.Mode.All & ~Info.Mode.Data};
        private readonly Info.Mode[] InfoMaskWinConsole = new Info.Mode[2]{
                Info.Mode.All,
                Info.Mode.All & ~Info.Mode.Data & ~Info.Mode.Error};
        private readonly Info.Mode[] InfoMaskSummary = new Info.Mode[2]{
                Info.Mode.All & ~Info.Mode.Event,
                Info.Mode.CriticalEvent | Info.Mode.CriticalError};

        private const string ConfigFileName = @"\config.json";
        private List<string> commandHistory = new List<string>();
        private int commandHistoryIndex;
        private TEAConfigurator configurator;
        private static CancellationTokenSource tokenSource = new CancellationTokenSource();
        private static CancellationToken token = tokenSource.Token;
        private System.Diagnostics.Stopwatch stopWatch = new Stopwatch();

        public MainForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handles the FormClosing event of the MainForm control. Disposes the client
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. Not used</param>
        private void MainForm_FormClosing(Object sender, FormClosingEventArgs e)
        {
            DisposeClient();
        }

        /// <summary>
        /// Closes and disposes the client. This is called when the client is no longer needed
        /// </summary>
        void DisposeClient()
        {
            /// Cancels the token source and disposes the tokenSource.
            if (!token.IsCancellationRequested)
            {
                tokenSource.Cancel();
                tokenSource.Dispose();
            }        

            tmFormUpdate.Dispose();
        }

        /// <summary>
        /// Prints information to the console and status bar. This is called by PrintInfo when it is the first call to this method
        /// </summary>
        /// <param name="info">Info to be printed to the console and status bar</param>
        /// <param name="infoSource">Source of the information that is being printed</param>
        private void PrintInfo(Info info, InfoSource infoSource = InfoSource.Main)
        {
            var s = $"{DateTime.Now.ToString("HH:mm:ss.fff")}, " + info.msg;

            /// Prints a line to the console if the infoSource is in the infoMaskConsole mode.
            if ((info.mode & InfoMaskConsole[(int)infoSource]) == info.mode)
            {
                Console.WriteLine(s);
            }

            /// if infoSource is not in infoMode
            if ((info.mode & InfoMaskWinConsole[(int)infoSource]) == info.mode)
            {
                tbConsole.BeginInvoke(new Action(() =>
                {
                    tbConsole.Text += s + "\r\n";
                    tbConsole.SelectionStart = tbConsole.Text.Length;
                    tbConsole.ScrollToCaret();
                }));
            }

            /// Show the status of the info source.
            if ((info.mode & InfoMaskSummary[(int)infoSource]) == info.mode)
            {
                lbStatus.BeginInvoke(new Action(() =>
                {
                    lbStatus.Text = $"Status: {info.msg.Replace("\t", "   ")}";
                }));
            }
        }

        /// <summary>
        /// Prints information about the configuration. This is called when the ConfigurationInfoMessageReceived event is raised
        /// </summary>
        /// <param name="sender">Sender of this information message.</param>
        /// <param name="info">Information pertaining to the message being received or</param>
        private void ConfigurationInfoMessageReceived(object sender, Info info)
        {
            PrintInfo(info, InfoSource.Configurator);
        }

        /// <summary>
        /// Handles the Tick event of the tmFormUpdate control. Stores information in the form
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. Not used</param>
        private void tmFormUpdate_Tick(object sender, EventArgs e)
        {
            TimeSpan ts = stopWatch.Elapsed;
            lbElapsedTime.Text = $"Elapsed: {ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
            var recordedKBytes = configurator.RecordedKBytes;
            lbPacketCount.Text = "Data packets: " + ((recordedKBytes < 1024.0) ? $"{recordedKBytes:F1}kB" : $"{recordedKBytes / 1024.0:F3}MB");
        }

        /// <summary>
        /// Handles the Click event of the btStart control. Opens the TeaConfigurator and connects to the inlet
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. This is ignored</param>
        private void btStart_Click(object sender, EventArgs e)
        {
            /// This method is called by the user to start the session.
            if (btStart.Text == "Start")
            {
                btStart.Enabled = false;

                String line, configString = "";
                try
                {
                    //Pass the file path and file name to the StreamReader constructor
                    StreamReader sr = new StreamReader(Directory.GetCurrentDirectory() + ConfigFileName);
                    //Read the first line of text
                    line = sr.ReadLine();
                    //Continue to read until you reach end of file
                    /// Read the next line from the current line.
                    while (line != null)
                    {
                        configString += line;
                        //Read the next line
                        line = sr.ReadLine();
                    }
                    //close the file
                    sr.Close();

                    var options = new JsonSerializerOptions
                    {
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };
                    configurator = JsonSerializer.Deserialize<TEAConfigurator>(configString, options);
                    configurator.InfoMessageReceived += ConfigurationInfoMessageReceived;
                }
                catch
                {
                    MessageBox.Show("Could not open the config file, application closing");
                    this.Close();
                }
                tmFormUpdate.Start();

                var connected = configurator.ConnectToInlet(token);
                /// This method is called when the user is connected to the server.
                if (connected)
                {
                    configurator.SendData("TEAStartRec" + "\n");
                    PrintInfo(new Info("Streaming data.", Info.Mode.CriticalEvent));

                    stopWatch.Start();
                    btStart.Text = "Stop";
                    btStart.Enabled = true;
                }
            }
            else
            {
                btStart.Enabled = false;
                configurator.SendData("TEAStopRec" + "\n");
                PrintInfo(new Info("Session stopped.", Info.Mode.CriticalEvent));
                stopWatch.Stop();

                /// Save recorded data to the configurator.
                if (cbEnableLog.Checked)
                {
                    configurator.SaveRecordedData();
                }

                DisposeClient();
            }
        }

        /// <summary>
        /// Handles the Click event of the btSettings control. Opens a notepad to get settings
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. Not used</param>
        private void btSettings_Click(object sender, EventArgs e)
        {
            try
            {
                
                Process np = Process.Start("Notepad", Directory.GetCurrentDirectory() + @"\config.json");
                np.WaitForExit();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            };
        }

        /// <summary>
        /// Handles the Click event of the lbShowDebugArea control. Makes sure the text is centered on the top left corner
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. Not used</param>
        private void lbShowDebugArea_Click(object sender, EventArgs e)
        {
            /// Set the size of the debug area.
            if (lbShowDebugArea.Text == "Debug ⏬")
            {
                lbShowDebugArea.Text = "Debug ⏫";
                this.ClientSize = new System.Drawing.Size(this.ClientSize.Width, this.ClientSize.Height * 3);
            }
            else
            {
                lbShowDebugArea.Text = "Debug ⏬";
                this.ClientSize = new System.Drawing.Size(this.ClientSize.Width, this.ClientSize.Height / 3);
            }
        }

        /// <summary>
        /// Handles the PreviewKeyDownEvent event of the tbCommand control. Used to detect tab up and down keys
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. This instance is not used in this method</param>
        private void tbCommand_PreviewKeyDownEvent(object sender, PreviewKeyDownEventArgs e)
        {
            /// Set the IsInputKey to true if the key is pressed.
            switch (e.KeyCode)
            {
                case Keys.Tab:
                    e.IsInputKey = true;
                    break;
                case Keys.Up:
                    e.IsInputKey = true;
                    break;
                case Keys.Down:
                    e.IsInputKey = true;
                    break;
            }
        }

        /// <summary>
        /// Handles the KeyDownEvent event of the tbCommand control. Manages adding the command to the command history and moving the cursor up or down depending on the key pressed
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. Ignored</param>
        private void tbCommand_KeyDownEvent(object sender, KeyEventArgs e)
        {
            /// This method is called when the user presses the key press.
            if (e.KeyCode == Keys.Enter)
            {
                commandHistory.Add(tbCommand.Text);
                commandHistoryIndex = commandHistory.Count - 1;
                configurator?.SendData(tbCommand.Text + "\n");
                e.SuppressKeyPress = true;
            }
            /// This method is called when the user presses a key press.
            else if (e.KeyCode == Keys.Tab)
            {
                tbCommand.AppendText("\t");
                e.SuppressKeyPress = true;
            }
            /// This method is called when the user presses a key down.
            else if (e.KeyCode == Keys.Up)
            {
                /// This method is called by the commandHistoryIndex.
                if (commandHistoryIndex > 0)
                {
                    commandHistoryIndex--;
                    tbCommand.Text = commandHistory[commandHistoryIndex];
                    tbCommand.SelectionStart = tbCommand.Text.Length;
                    tbCommand.SelectionLength = 0;
                    e.SuppressKeyPress = true;
                }
            }
            /// This method is called when the key is pressed.
            else if (e.KeyCode == Keys.Down)
            {
                /// This method is called by the commandHistory.
                if (commandHistoryIndex < commandHistory.Count - 1)
                {
                    commandHistoryIndex++;
                    tbCommand.Text = commandHistory[commandHistoryIndex];
                    tbCommand.SelectionStart = tbCommand.Text.Length;
                    tbCommand.SelectionLength = 0;
                    e.SuppressKeyPress = true;
                }
            }
        }

    }
}

