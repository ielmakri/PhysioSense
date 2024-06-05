// 🟡🔴🟠🟢🔵🟣🟤⚫
//TODO: Add multiplot feature
//TODO: Wrap status and info labels: see (https://stackoverflow.com/questions/1204804/word-wrap-for-a-label-in-windows-forms)
//"In my case (label on a panel) I set label.AutoSize = false and label.Dock = Fill. And the label text is wrapped automatically."

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Linq;
using System.Text.RegularExpressions;
using Libfmax;

namespace DataManager
{
    public partial class MainForm : Form
    {
        // Info sources
        private enum InfoSource : int
        {
            Main = 0,
            Configurator = 1
        };
        // Info masks defined over each UI information display element and for each info source 
        private readonly Info.Mode[] InfoMaskConsole = new Info.Mode[2]{
                Info.Mode.All,
                Info.Mode.All & ~Info.Mode.Data};
        private readonly Info.Mode[] InfoMaskWinConsole = new Info.Mode[2]{
                Info.Mode.All& ~Info.Mode.Data,
                Info.Mode.All & ~Info.Mode.Data & ~Info.Mode.Error};
        private readonly Info.Mode[] InfoMaskSummary = new Info.Mode[2]{
                Info.Mode.All & ~Info.Mode.Data,
                Info.Mode.CriticalEvent | Info.Mode.CriticalError};

        private const string ConfigFilePath = @"\config\config.json";
        private DataStreamConfigurator configurator;
        private static CancellationTokenSource tokenSource = new CancellationTokenSource();
        private static CancellationToken token = tokenSource.Token;
        private System.Diagnostics.Stopwatch stopWatch = new Stopwatch();
        private DataStreamMultiplotter multiplotter;
        private int formHeight;

        public MainForm()
        {
            InitializeComponent();
            String line, configString = "";
            try
            {
                //Pass the file path and file name to the StreamReader constructor
                StreamReader sr = new StreamReader(Directory.GetCurrentDirectory() + ConfigFilePath);
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
                configurator = JsonSerializer.Deserialize<DataStreamConfigurator>(configString);
                configurator.InfoMessageReceived += ConfigurationInfoMessageReceived;
            }
            catch
            {
                MessageBox.Show("Could not open the config file, application closing");
                this.Close();
            }

            tbDataPath.Text = configurator.DataPath;

            this.formHeight = this.ClientSize.Height;
        }

        /// <summary>
        /// Handles the FormClosing event of the MainForm control. Disposes the resources used by the form
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. This is not used</param>
        private void MainForm_FormClosing(Object sender, FormClosingEventArgs e)
        {
            /// Cancels the token source and disposes the tokenSource.
            if (!token.IsCancellationRequested)
            {
                tokenSource.Cancel();
                tokenSource.Dispose();
            }
            tmFormUpdate.Dispose();
            
            configurator.EndRecording();

            configurator = null;
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

            /// This method is called when the infoSource is not in the infoMaskSummary.
            if ((info.mode & InfoMaskSummary[(int)infoSource]) == info.mode)
            {
                this.BeginInvoke(new Action(() =>
                {
                    lbStatus.Text = $"Status: {info.msg.Replace("\t", "   ")}";
                    /// Set ForeColor to red red or white.
                    if (info.mode == Info.Mode.Error || info.mode == Info.Mode.CriticalError)
                    {
                        lbStatus.ForeColor = System.Drawing.Color.Red;
                    }
                    /// Set the ForeColor of the ForeColor.
                    else if (info.mode == Info.Mode.CriticalEvent)
                    {
                        lbStatus.ForeColor = System.Drawing.Color.DarkOrange;
                    }
                    else
                    {
                        lbStatus.ForeColor = System.Drawing.Color.Black;
                    }
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
        /// Handles the Click event of the btSettings control. Opens the Notepad window
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. Not used</param>
        private void btSettings_Click(object sender, EventArgs e)
        {
            Process.Start("Notepad", Directory.GetCurrentDirectory() + ConfigFilePath);
        }

        /// <summary>
        /// Handles the Click event of the btListStreams control. Lists all streams in LSL
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. Cannot be null</param>
        private void btListStreams_Click(object sender, EventArgs e)
        {
            PrintInfo(new Info("Listing streams.", Info.Mode.Event));

            clbStreams.Items.Clear();

            // wait until all data streams are retrieved
            configurator.StreamInfos = LSL.LSL.resolve_streams().OrderBy(ob => ob.name()).ToArray();

            foreach (var si in configurator.StreamInfos)
            {
                string s = String.Format("{0} ({1}), {2}, {3}, {4}Hz, {5} ch", si.name(), si.hostname(), si.type(), si.channel_format(), si.nominal_srate(), si.channel_count());
                var ni = clbStreams.Items.Add(s, CheckState.Unchecked);
                /// Set the checked state of the stream to the checked state.
                if (configurator.GetStream(si) != null)
                {
                    clbStreams.SetItemChecked(ni, true);
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the btCheckStreams control. Makes the check all button checked or uncheck all button depending on the value of the TextBox
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. Not used</param>
        private void btCheckStreams_Click(object sender, EventArgs e)
        {
            /// Set the checked state of the items in the list
            if (clbStreams.Items.Count > 0)
            {
                /// Check all items in the list
                if (btCheckStreams.Text == "Check all")
                {
                    /// Sets the checked state of all items in the stream.
                    for (int i = 0; i < clbStreams.Items.Count; i++)
                    {
                        clbStreams.SetItemChecked(i, true);
                    }
                    btCheckStreams.Text = "Uncheck all";
                }
                else
                {
                    /// Sets all items checked to false
                    for (int i = 0; i < clbStreams.Items.Count; i++)
                    {
                        clbStreams.SetItemChecked(i, false);
                    }
                    btCheckStreams.Text = "Check all";
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the btSubscribe control. Adds / Unsubscribes streams
        /// </summary>
        /// <param name="sender">The source of the event. Cannot be null.</param>
        /// <param name="e">The instance containing the event data. Cannot be null</param>
        private void btSubscribe_Click(object sender, EventArgs e)
        {
            /// Add all streams to the configurator.
            for (int i = 0; i < clbStreams.Items.Count; i++)
            {
                /// Add a stream to the configurator.
                if (clbStreams.GetItemChecked(i))
                {
                    var si = configurator.StreamInfos[i];
                    configurator.AddStream(si, token);
                }
            }

            /// Removes all items from the clbStreams.
            for (int i = 0; i < clbStreams.Items.Count; i++)
            {
                /// Remove the item at the given index.
                if (!clbStreams.GetItemChecked(i))
                {
                    clbStreams.Items.RemoveAt(i);
                    configurator.StreamInfos = configurator.StreamInfos.Where((source, index) => index != i).ToArray();
                    i = i - 1;
                }
            }

            /// This method is called when the user subscribes to the streams.
            if (configurator.NbSubscribed > 0)
            {
                btListStreams.Enabled = false;
                btCheckStreams.Enabled = false;
                btSubscribeStreams.Enabled = false;
                clbStreams.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(clbStreams_ItemCheck);  //instead of clbStreams.Enabled = false;
                clbStreams.DoubleClick += new System.EventHandler(clbStreams_DoubleClick);
                PrintInfo(new Info("Subscribed to streams.", Info.Mode.Event));

                foreach (var si in configurator.StreamInfos)
                {
                    string s = String.Format("{0} ({1}), {2}, {3}, {4}Hz, {5} ch", si.name(), si.hostname(), si.type(), si.channel_format(), si.nominal_srate(), si.channel_count());
                    var ni = clbMultiplotStreams.Items.Add(s, CheckState.Unchecked);
                }

                multiplotter = new DataStreamMultiplotter();
                configurator.ConnectUdpStream();
                cbUdpStream.Enabled = true;
            }
        }

        /// <summary>
        /// Handles the CheckedChanged event of the cbUdpStream control. Allows the user to enable or disable UDP streams
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. Not used</param>
        private void cbUdpStream_CheckedChanged(object sender, EventArgs e)
        {
            configurator.UdpStreamEnable = ((CheckBox)sender).Checked;
        }

        /// <summary>
        /// Handles the Click event of the btMultiplot control. Adds or removes streams from the plotter
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. Not used</param>
        private void btMultiplot_Click(object sender, EventArgs e)
        {
            /// InitPlotter is called by the Multiplotter.
            if (multiplotter != null)
            {
                /// Adds or removes all streams that are currently in the Multiplotter.
                for (int i = 0; i < clbMultiplotStreams.Items.Count; i++)
                {
                    var si = configurator.StreamInfos[i];
                    var stream = configurator.GetStream(si);
                    /// Add or remove a stream from the list
                    if (clbMultiplotStreams.GetItemChecked(i))
                    {
                        /// Add a stream to the multiplotter.
                        if (stream != null && !multiplotter.Streams.Exists(e => (e.Name == stream.Name && e.Host == stream.Host && e.Type == stream.Type)))
                        {
                            multiplotter.Streams.Add(stream);
                        }
                    }
                    else
                    {
                        /// Removes the stream from the multiplotter.
                        if (stream != null)
                        {
                            multiplotter.Streams.Remove(stream);
                        }
                    }
                }
                multiplotter.InitPlotter();
            }
        }

        /// <summary>
        /// Handles the ItemCheck event of the clbStreams control. Stores the value of the control in e.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. This instance contains the event data</param>
        private void clbStreams_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            e.NewValue = e.CurrentValue;
        }

        /// <summary>
        /// Handles the DoubleClick event of the clbStreams control. Opens the plotter for the selected stream
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. Not used</param>
        private void clbStreams_DoubleClick(object sender, EventArgs e)
        {
            configurator?.ShowPlotter(clbStreams.SelectedIndex, token);
        }

        /// <summary>
        /// Handles the Click event of the btRecord control. Starts or stops recording depending on the value of the text
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. Not used</param>
        private void btRecord_Click(object sender, EventArgs e)
        {
            /// Enables recording of the record.
            if (configurator.NbSubscribed > 0)
            {
                /// This method is called when recording is enabled.
                if (btRecord.Text == "Record")
                {
                    btRecord.Enabled = false;
                    configurator.StartRecord();
                    tmFormUpdate.Start();
                    stopWatch.Start();
                    btRecord.Text = "Stop";
                    PrintInfo(new Info($"Recording started.", Info.Mode.Event));

                    btRecord.Enabled = true;
                }
                else
                {
                    btRecord.Enabled = false;
                    /// Cancels the token source and disposes the tokenSource.
                    if (!token.IsCancellationRequested)
                    {
                        tokenSource.Cancel();
                        tokenSource.Dispose();
                    }
                    PrintInfo(new Info($"Recording stopped.", Info.Mode.Event));
                    configurator.EndRecording();
                    // configurator.SaveRecord();
                    tmFormUpdate.Stop();
                    stopWatch.Stop();

                    btRecord.Text = "Record";
                    btRecord.Enabled = true;

                }
            }
        }

        /// <summary>
        /// Handles the Click event of the btDataPath control. Opens a FolderBrowserDialog to select a folder.
        /// </summary>
        /// <param name="sender">The source of the event. Can be null</param>
        /// <param name="e">The instance containing the event data. Can be</param>
        private void btDataPath_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                /// DialogResult. OK if dialog result is OK.
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {

                    tbDataPath.Text = fbd.SelectedPath.ToString();
                    tbProject.Focus();
                }
            }
        }

        /// <summary>
        /// Handles the KeyPress event of the tabRecord control. This is used to determine if the key is a punctuation separator or a symbol
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. This is also used to determine if the event has been handled</param>
        private void tabRecord_KeyPress(object sender, KeyPressEventArgs e)
        {
            /// Check if the sender is a valid key.
            if ((((TextBox)sender).Text.Length == 0))
            {
                e.Handled =
                            (Char.IsPunctuation(e.KeyChar) || Char.IsSeparator(e.KeyChar) || Char.IsSymbol(e.KeyChar));
            }
            else
            {
                e.Handled = e.KeyChar != '-' &&
                            e.KeyChar != '_' &&
                            (Char.IsPunctuation(e.KeyChar) || Char.IsSeparator(e.KeyChar) || Char.IsSymbol(e.KeyChar));
            }
        }

        /// <summary>
        /// Handles the PreviewKeyDown event of the tabRecord control. Allows the user to enter a name and save it to the config
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. This is not used in this method</param>
        private void tabRecord_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            /// This method is called when the user clicks enter.
            if (e.KeyValue == Convert.ToChar(Keys.Enter))
            {
                var regexItem = new Regex("^[a-zA-Z0-9 _/-]*$");

                /// This method is used to enable recording of the record.
                if (sender.Equals(tbProject))
                {
                    /// This method is used to set the project name
                    if (tbProject.Text != "" && regexItem.IsMatch(tbProject.Text))
                    {
                        configurator.Record.Project = tbProject.Text;
                        tbExperiment.Enabled = true;
                        tbExperiment.Focus();
                    }
                    else
                    {
                        PrintInfo(new Info("Please enter a valid project name", Info.Mode.Error));
                    }
                }
                /// This method is used to enable recording of the record.
                else if (sender.Equals(tbExperiment))
                {
                    /// This method is used to set the experiment name
                    if (tbExperiment.Text != "" && regexItem.IsMatch(tbExperiment.Text))
                    {
                        configurator.Record.Experiment = tbExperiment.Text;
                        tbSession.Enabled = true;
                        tbSession.Focus();
                    }
                    else
                    {
                        PrintInfo(new Info("Please enter a valid experiment name", Info.Mode.Error));
                    }
                }
                /// This method is called by the Record.
                else if (sender.Equals(tbSession))
                {
                    /// This method is used to set the session name
                    if (tbSession.Text != "" && regexItem.IsMatch(tbSession.Text))
                    {
                        configurator.Record.Session = tbSession.Text;
                        tbSubject.Enabled = true;
                        tbSubject.Focus();

                    }
                    else
                    {
                        PrintInfo(new Info("Please enter a valid session name", Info.Mode.Error));
                    }
                }
                /// This method is called when the user clicks confirm to enable recording.
                else if (sender.Equals(tbSubject))
                {
                    /// Confirm confirm to enable recording.
                    if (regexItem.IsMatch(tbSubject.Text))
                    {
                        configurator.Record.Subject = tbSubject.Text;
                        btConfirm.Enabled = true;
                        PrintInfo(new Info("Click confirm to enable recording. \n" +
                                 "⚠ Warning: Existing logs with the same reference will be overwritten.", Info.Mode.CriticalEvent));
                    }
                    else
                    {
                        PrintInfo(new Info("Please enter a valid subject name", Info.Mode.Error));
                    }
                }
            }
        }

        /// <summary>
        /// Handles the GotFocus event of the tabRecord control. This is used to add subdirs to autocompletion source
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. This is ignored</param>
        private void tabRecord_GotFocus(object sender, EventArgs e)
        {
            var currentPath = "";
            /// Returns the path to the current path.
            if (sender.Equals(tbProject))
            {
                currentPath = tbDataPath.Text;
            }
            /// Returns the path to the current path.
            else if (sender.Equals(tbExperiment))
            {
                currentPath = tbDataPath.Text + "\\" + tbProject.Text;
            }
            /// The path to the current path to the data path.
            else if (sender.Equals(tbSession))
            {
                currentPath = tbDataPath.Text + "\\" + tbProject.Text + "\\" + tbExperiment.Text;
            }
            /// If sender is equal to tbSubject the path to the current path is used.
            else if (sender.Equals(tbSubject))
            {
                currentPath = tbDataPath.Text + "\\" + tbProject.Text + "\\" + tbExperiment.Text + "\\" + tbSession.Text;
            }

            /// Set the source of the current directory.
            if (Directory.Exists(currentPath))
            {
                var source = new AutoCompleteStringCollection();
                string[] subdirs = Directory.GetDirectories(currentPath)
                        .Select(Path.GetFileName)
                        .ToArray();
                source.AddRange(subdirs);
                ((TextBox)sender).AutoCompleteCustomSource = source;
            }
        }

        /// <summary>
        /// Handles the LostFocus event of the tabRecord control. Displays the record's name
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. Not used</param>
        private void tabRecord_LostFocus(object sender, EventArgs e)
        {
            /// Set the text of the record.
            if (sender.Equals(tbProject))
            {
                tbProject.Text = configurator?.Record.Project;
            }
            /// Set the text of the record.
            else if (sender.Equals(tbExperiment))
            {
                tbExperiment.Text = configurator?.Record.Experiment;
            }
            /// Set the text of the record.
            else if (sender.Equals(tbSession))
            {
                tbSession.Text = configurator?.Record.Session;
            }
            /// Set the subject text to the subject.
            else if (sender.Equals(tbSubject))
            {
                tbSubject.Text = configurator?.Record.Subject;
            }
        }

        /// <summary>
        /// Handles the Click event of the btConfirm control. Disables all controls before submitting the form
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. Not used</param>
        private void btConfirm_Click(object sender, EventArgs e)
        {
            tbProject.Enabled = false;
            tbExperiment.Enabled = false;
            tbSession.Enabled = false;
            tbSubject.Enabled = false;
            btConfirm.Enabled = false;
            btDataPath.Enabled = false;
            btRecord.Enabled = true;

            configurator.Record.DataPath = tbDataPath.Text;

            this.Text += $" ***New Recording: {configurator.Record.Project} - {configurator.Record.Experiment} - {configurator.Record.Session} - {configurator.Record.Subject}";
            PrintInfo(new Info("Recording session is registered. Ready to record.", Info.Mode.Event));
        }

        /// <summary>
        /// Handles the Click event of the lbShowDebugArea control. Makes sure the text is scaled to fit the form
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. Not used</param>
        private void lbShowDebugArea_Click(object sender, EventArgs e)
        {
            /// Set the size of the debug area.
            if (lbShowDebugArea.Text == "Debug ⏬")
            {
                lbShowDebugArea.Text = "Debug ⏫";
                var ratio = ((double)(MAIN_HEIGHT1 + MAIN_HEIGHT2)) / ((double)MAIN_HEIGHT1);
                this.ClientSize = new System.Drawing.Size(this.ClientSize.Width, (int)((double)this.formHeight * ratio));
            }
            else
            {
                lbShowDebugArea.Text = "Debug ⏬";
                this.ClientSize = new System.Drawing.Size(this.ClientSize.Width, this.formHeight);
            }
        }

        /// <summary>
        /// Handles the Tick event of the tmFormUpdate control. Displays information about the time elapsed and how many packets have been recorded to the user
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. This is not used</param>
        private void tmFormUpdate_Tick(object sender, EventArgs e)
        {
            TimeSpan ts = stopWatch.Elapsed;
            lbElapsedTime.Text = $"Elapsed: {ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
            var recordedKBytes = configurator.RecordedKBytes;
            lbPacketCount.Text = "Data packets: " + ((recordedKBytes < 1024.0) ? $"{recordedKBytes:F1}kB" : $"{recordedKBytes / 1024.0:F3}MB");

            var lostStreams = "";
            foreach (var si in configurator.StreamInfos)
            {
                var stream = configurator.GetStream(si);
                /// Add a stream to lost streams.
                if (stream == null || !stream.IsActive)
                {
                    lostStreams += $"{si.name()}-{si.type()} ";
                }
            }
            /// Show the warning box with the lost streams.
            if (lostStreams != "")
            {
                lbWarning.Visible = true;
                PrintInfo(new Info($"{lostStreams}: Lost!", Info.Mode.CriticalError));
            }
            else
            {
                lbWarning.Visible = false;
            }
        }
    }
}

