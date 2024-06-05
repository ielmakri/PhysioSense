// 🟡🔴🟠🟢🔵🟣🟤⚫
/*

Measures are grouped into streams(S1 to S6) as channels:

⛔ ABB	OBSOLETE
✅ S1: ACX	X Accelerometer (lateral)
✅ S1: ACY	Y Accelerometer  (longitudinal)
✅ S1: ACZ	Z Accelerometer (vertical)
✅ S6: BPP	Body Position (S (Supine), U (Upright), P (Prone), I (Inverted), SD (Side), UN (Unknown))
✅ S2: BRB	Breaths per minute
✅ S6: BRC	Breathing rate confidence (%)
⛔ BRE	OBSOLETE
⛔ BRI	OBSOLETE
⛔ CPT	Temperature from Respironics Core Pill (deprecated)
👁‍🗨 (DISPLAY IN GUI) -> DAI	Device alarms (see below)
✅ S3: GSR	Galvanic skin resistance if the GSR sensor is connected 
⛔ HFB	OBSOLETE
✅ S6: HRC	Heart Rate Confidence (%) – should be 95% + for a well fitted belt.
✅ S6: MOT	Motion (S=Stationary, L= slow, F = fast
✅ S6: OHS	Status B – device alert, G = no concern, Y = subject alert, R = significant subject alert, GY = no life signs
✅ Sy: RRI	R-R interval, the time in milliseconds between R wave peaks. This is sent each time an R wave is detected and so is  not metronomic
✅ S6: SAI	Subject Alarms
✅ S4: SKT	Skin temperature
✅ S5: HRE	Heart Rate – derived from ECG – the highest confidence heart rate
⛔ SP2	O2 saturation if the sensor is being used

Stream types (can be modified at config.json):
S1:  acc
S2:  brb
S3:  gsr
S4:  skt
S5:  hre
S6:  gen     


DAI - Device alarms can be decoded as follows

(1 digit Hexadecimal bit mask – values 0-F)
Bit 0 = Low Battery 
Bit 1 = Lead Off (usually poor belt fit)
Bit 2 = Sensor Fault
Bit 3 = Belt Off (SEM and belt not connected)

SAI – Subject Alarms

(3 digit Hexadecimal bit mask)
Heart Rate Alarms
Bit 0 = ECG HR High (0 = Clear, 1 = Set)
Bit 1 = ECG HR Low
Breathing Rate Alarms
Bit 3 = BR  High
Bit 4 = BR Low
Bit 5 = BR Apnea
Bit 6 = BR Belt High
Bit 7 = BR Belt Low
Fall Alarms
Bit 10 = Fall


▶ Ian from Equivital:

Data Rates - Lag

The 1.7s lag should be fixed. This is controlled by Pro. The system needs to parse the data from the SEM, 
then process it through Pro and pass it out over the socket interface. Pro adds a minimum lag to keep that consistent.

Note that the SDK eliminates a lot of that processing and associates every data point with the sensor timestamp 
so that even if there is a lag in communications you can detect it.

Data Rates – Disclosure

The SEM has two modes – partial and full disclosure.

In partial disclosure the individual measures (HR, BR, etc) are updated every 15 seconds from the SEM. 
So at 5hz, you wont see those values change very often. Other measures are sent outside that 15s, alerts for example. 
Others are sent as they are measured, R-R interval for example which publishes a millisecond value for each R wave peak.

In full disclosure data is sent at 25.6Hz. That contains one respiration waveform reading and 10 timestamped ECG readings for each ECG lead. 
Accelerometer data is also sent at 25.6Hz. However, the principal measurements (HR, BR, etc) are still only updated every 15 seconds.

Therefore your 5Hz rate is probably too frequent for partial disclosure data and not frequent enough for full disclosure,

The SDK allows you to subscribe to each measure and receive updates as events when they occur.

*/


using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using Libfmax;

namespace EquivitalClient
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
                Info.Mode.None};

        private const string ConfigFileName = @"\config.json";
        private List<string> commandHistory = new List<string>();
        private int commandHistoryIndex;
        private EQConfigurator configurator;
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

        //TODO: Check if below method has to be async or not
        private async void btStart_ClickAsync(object sender, EventArgs e)
        {
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
                    configurator = JsonSerializer.Deserialize<EQConfigurator>(configString, options);
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
                    PrintInfo(new Info("Streaming data.", Info.Mode.CriticalEvent));

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

