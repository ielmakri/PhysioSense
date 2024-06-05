// 🟡🔴🟠🟢🔵🟣🟤⚫
//https://developer.empatica.com/windows-streaming-server-commands.html#protocol-example
//https://support.empatica.com/hc/en-us/articles/201608896-Data-export-and-formatting-from-E4-connect-

// STREAM DESCRIPTIONS
// The available stream subscriptions are:
// E4_Acc - Data from 3-axis acceleometer sensor in the range [-2g, 2g]. (sampled at 32 Hz)
// E4_Bvp - Data from photoplethysmograph (PPG). (sampled at 64 Hz)
// E4_Gsr - Data from the electrodermal activity sensor in μS. (sampled at 4 Hz) 
// E4_Temp - Data from temperature sensor expressed in degrees on the Celsius (°C) scale (sampled at 4 Hz)
// E4_Ibi - Inter beat intervals. (intermittent output with 1/64 second resolution)
// E4_Hr - Average heart rate values, computed in spans of 10 seconds
// E4_Battery - Device Battery
// E4_Tag - Tag taken from the device (by pressing the button)


using Libfmax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;

namespace KistlerClient
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
        private E4Configurator configurator;
        private static CancellationTokenSource tokenSource = new CancellationTokenSource();
        private static CancellationToken token = tokenSource.Token;
        private System.Diagnostics.Stopwatch stopWatch = new Stopwatch();

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_FormClosing(Object sender, FormClosingEventArgs e)
        {
            DisposeClient();
        }

        void DisposeClient()
        {
            if (!token.IsCancellationRequested)
            {
                tokenSource.Cancel();
                tokenSource.Dispose();
            }

            // 😬 I think, below code snippet not needed...
            // configurationClient?.Close();

            // if (sensors != null)
            // {
            //     foreach (var sensor in sensors)
            //     {
            //         sensor?.Close();
            //     }
            // }            

            tmFormUpdate.Dispose();
        }

        private void PrintInfo(Info info, InfoSource infoSource = InfoSource.Main)
        {
            var s = $"{DateTime.Now.ToString("HH:mm:ss.fff")}, " + info.msg;

            if ((info.mode & InfoMaskConsole[(int)infoSource]) == info.mode)
            {
                Console.WriteLine(s);
            }

            if ((info.mode & InfoMaskWinConsole[(int)infoSource]) == info.mode)
            {
                tbConsole.BeginInvoke(new Action(() =>
                {
                    tbConsole.Text += s + "\r\n";
                    tbConsole.SelectionStart = tbConsole.Text.Length;
                    tbConsole.ScrollToCaret();
                }));
            }

            if ((info.mode & InfoMaskSummary[(int)infoSource]) == info.mode)
            {
                lbStatus.BeginInvoke(new Action(() =>
                {
                    lbStatus.Text = $"Status: {info.msg.Replace("\t", "   ")}";
                }));
            }
        }

        private void ConfigurationInfoMessageReceived(object sender, Info info)
        {
            PrintInfo(info, InfoSource.Configurator);
        }

        private void tmFormUpdate_Tick(object sender, EventArgs e)
        {
            TimeSpan ts = stopWatch.Elapsed;
            lbElapsedTime.Text = $"Elapsed: {ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
            var recordedKBytes = configurator.RecordedKBytes;
            lbPacketCount.Text = "Data packets: " + ((recordedKBytes < 1024.0) ? $"{recordedKBytes:F1}kB" : $"{recordedKBytes / 1024.0:F3}MB");
        }

        private void btStart_Click(object sender, EventArgs e)
        {
            if (btStart.Text == "Start")
            {
                btStart.Enabled = false;
                try
                {
                    String line, configString = "";
                    //Pass the file path and file name to the StreamReader constructor
                    StreamReader sr = new StreamReader(Directory.GetCurrentDirectory() + ConfigFileName);
                    //Read the first line of text
                    line = sr.ReadLine();
                    //Continue to read until you reach end of file
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
                    configurator = JsonSerializer.Deserialize<E4Configurator>(configString, options);
                    configurator.InfoMessageReceived += ConfigurationInfoMessageReceived;
                }
                catch
                {
                    MessageBox.Show("Could not open the config file, application closing");
                    this.Close();
                }
                tmFormUpdate.Start();

                var connected = configurator.ConnectToInlet(token);
                if (connected)
                {
                    stopWatch.Start();
                    btStart.Text = "Stop";
                    btStart.Enabled = true;
                }
            }
            else
            {
                btStart.Enabled = false;
                PrintInfo(new Info("Session stopped.", Info.Mode.CriticalEvent));
                stopWatch.Stop();

                if (cbEnableLog.Checked)
                {
                    configurator.SaveRecordedData();
                }

                DisposeClient();
            }
        }

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

        private void lbShowDebugArea_Click(object sender, EventArgs e)
        {
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

        private void tbCommand_PreviewKeyDownEvent(object sender, PreviewKeyDownEventArgs e)
        {
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

        private void tbCommand_KeyDownEvent(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                commandHistory.Add(tbCommand.Text);
                commandHistoryIndex = commandHistory.Count - 1;
                configurator?.SendData(tbCommand.Text);
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Tab)
            {
                tbCommand.AppendText("\t");
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (commandHistoryIndex > 0)
                {
                    commandHistoryIndex--;
                    tbCommand.Text = commandHistory[commandHistoryIndex];
                    tbCommand.SelectionStart = tbCommand.Text.Length;
                    tbCommand.SelectionLength = 0;
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.KeyCode == Keys.Down)
            {
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

