using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading;
using Libfmax;
using LSL;
using MathNet.Numerics.Statistics;
using System.Linq;
using ScottPlot.Plottable;

namespace DataManager
{

    public partial class FormPlotter : Form
    {
        public class SignalSet
        {
            public string Name { get; set; }
            public SignalPlotXY[] SignalPlot { get; set; }
            public double TimeWindow { get; set; }
            public int Index { get; set; }
            private int precision;

            public SignalSet(string name, SignalPlotXY[] signalPlot, double timeWindow, int index, int precision)
            {
                this.Name = name;
                this.SignalPlot = signalPlot;
                this.TimeWindow = timeWindow;
                this.Index = index;
                this.precision = precision;
                UpdateRenderWindow();
            }

            /// <summary>
            /// Updates the render window. This is called every frame to make sure that there is at least one signal that can be rendered
            /// </summary>
            /// <returns>The max timestamp of the</returns>
            public double UpdateRenderWindow()
            {
                double maxTs = 0;

                try
                {
                    foreach (var sig in SignalPlot)
                    {
                        sig.MaxRenderIndex = Math.Max(Math.Min(Index, sig.Xs.Length - 1), 0);
                        var minIndex = sig.MaxRenderIndex;
                        /// Get the max render index.
                        if (sig.Xs[sig.MaxRenderIndex] > maxTs) maxTs = sig.Xs[sig.MaxRenderIndex];
                        /// Find the minimum render index in the list.
                        while (minIndex > 0 && (sig.Xs[minIndex] > sig.Xs[sig.MaxRenderIndex] - TimeWindow))
                        {
                            minIndex--;
                        }
                        /// If the index is less than the maxRenderIndex the minIndex is increased.
                        if (minIndex < sig.MaxRenderIndex) minIndex += 1;
                        sig.MinRenderIndex = minIndex;

                    }
                }
                catch (Exception e)
                { }

                return maxTs;
            }
        }

        #region variables

        public static bool Run = true;
        public double TimeWindow { get; set; }  //in seconds
        public event EventHandler TimerUpdated;
        private List<SignalSet> SignalSets { get; set; } = new List<SignalSet>();
        private Dictionary<int, ScottPlot.Renderable.Axis> axisDict = new System.Collections.Generic.Dictionary<int, ScottPlot.Renderable.Axis>();
        private readonly Object lockObj = new Object();
        private CancellationToken token;
        private bool disposing;
        private IAsyncResult newDataReceived;
        private string lbInfoText = "";
        private List<double> markerLines = new List<double>();

        #endregion

        public FormPlotter(string title, double timeWindow, int tickInterval, CancellationToken token)
        {
            InitializeComponent();

            this.Text = "Plotter: " + title;
            this.TimeWindow = timeWindow / 1000.0;
            this.token = token;

            formsPlot1.Reset();
            formsPlot1.Plot.Legend();
            formsPlot1.Plot.XLabel("Timestamp[s]");
            formsPlot1.Plot.XAxis.TickLabelFormat("F3", dateTimeFormat: false);
            formsPlot1.Plot.YAxis.TickLabelFormat("F1", dateTimeFormat: false);
            formsPlot1.Plot.SetCulture(System.Globalization.CultureInfo.InvariantCulture);

            WinApi.TimeBeginPeriod(1);
            timerUpdate.Interval = tickInterval;       //default interval in miliseconds
            timerUpdate.Enabled = true;

            lbStatus.Visible = !Run;

        }

        /// <summary>
        /// Handles the FormClosing event of the FormPlotter control. Ends the timer and disposes the control
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. Not used</param>
        private void FormPlotter_FormClosing(object sender, EventArgs e)
        {
            WinApi.TimeEndPeriod(1);
            disposing = true;
            this.Dispose(true);
        }

        /// <summary>
        /// Raises the TimerUpdated event. Override this method to add custom handling for the timer update
        /// </summary>
        /// <param name="e">An EventArgs that contains the event</param>
        protected virtual void OnTimerUpdate(EventArgs e)
        {
            TimerUpdated?.Invoke(this, e);
        }

        /// <summary>
        /// Called when data is initialized. This is the first time the DataHandler is called in order to get the data from the data stream
        /// </summary>
        /// <param name="sender">The sender of the event</param>
        /// <param name="index">The index of the data in the data stream</param>
        /// <param name="blocking">If true the method will wait for the event to</param>
        public void DataInitialized(object sender, int index, bool blocking)
        {
            /// This method is called when the signal set is created.
            if (this.IsHandleCreated)
            {
                IAsyncResult result = this.BeginInvoke(new Action(() =>
                {
                    lock (lockObj)
                    {
                        var stream = (DataStream)sender;

                        var found = false;
                        foreach (var signalSet in SignalSets)
                        {
                            /// if signalSet. Name is signalSet. Name stream. Host stream. Type signalSet. Name signalSet. Host signalSet. Type signalSet. Name signalSet. Name signalSet. Host signalSet. Type signalSet. SignalPlot. Length signalSet. SignalPlot. Length signalSet. SignalPlot. Length signalSet. SignalPlot. Length signalSet. Xs signalPlot. Xs signalPlot. Xs signalPlot. Xs signalPlot. Ys signalPlot. Xs signalPlot. Xs signalPlot. Xs signalPlot. Xs signalPlot.
                            if (signalSet.Name == $"{stream.Name} ({stream.Host})-{stream.Type}")
                            {
                                found = true;

                                /// Updates the signal plot data.
                                for (int i = 0; i < signalSet.SignalPlot.Length; i++)
                                {
                                    var signalPlot = signalSet.SignalPlot[i];
                                    signalPlot.Xs = stream.Timestamps;
                                    signalPlot.Ys = stream.Data[i];
                                }
                                signalSet.Index = index;

                                break;
                            }
                        }

                        /// Set the signal set to the stream.
                        if (!found) NewSignalSet(stream, index);

                    }
                }));
                /// If blocking is true the result is sent to the end invoke.
                if (blocking) this.EndInvoke(result);
            }
        }

        /// <summary>
        /// Called when new data arrives. This is the method that will be called by DataStream. OnNewDataReceived
        /// </summary>
        /// <param name="sender">The sender of the signal</param>
        /// <param name="index">The index of the</param>
        public void NewDataReceived(object sender, int index)
        {
            /// This method is called when the handle has been created.
            if (this.IsHandleCreated)
            {
                // these lines can be added at the cost of lost markers: if (newDataReceived == null || newDataReceived.IsCompleted)  
                newDataReceived = this.BeginInvoke(new Action(() =>
                {
                    lock (lockObj)
                    {
                        var stream = ((DataStream)sender);
                        foreach (var signalSet in SignalSets)
                        {
                            /// Set signal set index to the signal set
                            if (signalSet.Name == $"{stream.Name} ({stream.Host})-{stream.Type}")
                            {
                                signalSet.Index = index;
                            }
                        }
                        var ts = String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F3}", stream.Timestamps[index]);
                        var lbInfoTextTemp = $"{stream.Name} ({stream.Host})-{stream.Type}" + $": {ts}";
                        /// Add marker lines to the markerLines.
                        if (stream.ChFormat == StreamMeta.ChannelFormat.String)
                        {
                            foreach (var mdata in stream.MarkerData)
                            {
                                markerLines.Add(stream.Timestamps[index]);
                                lbInfoTextTemp += ", " + mdata[index];
                            }
                        }
                        else
                        {
                            /// This function will print the data in a string with the format 0 F precision
                            for (int i = 0; i < stream.Data.Length; i++)
                            {
                                var data = stream.Data[i];
                                var precision = stream.Channels[i].Precision;
                                var format = "{" + $"0:F{precision}" + "}";
                                lbInfoTextTemp += ", " + String.Format(System.Globalization.CultureInfo.InvariantCulture, format, data[index]);
                            }
                        }

                        var s = lbInfoText.Split('\n');
                        var found = false;
                        /// look for lbInfoTextTemp in stream. Name stream. Host stream. Type
                        for (int i = 0; i < s.Length; i++)
                        {
                            /// look for lbInfoTextTemp in stream. Name stream. Host stream. Type
                            if (s[i].Contains($"{stream.Name} ({stream.Host})-{stream.Type}" + ":"))
                            {
                                s[i] = lbInfoTextTemp;
                                found = true;
                                break;
                            }
                        }
                        lbInfoText = found ? String.Join('\n', s) : lbInfoText + lbInfoTextTemp + "\n";
                    }
                }));
            }
        }

        /// <summary>
        /// Clears and reinitializes the plot to the initial state. This is called when the user clicks the Reset button
        /// </summary>
        private void ResetPlot()
        {
            lock (lockObj)
            {
                formsPlot1.Plot.Clear();
                foreach (var axis in axisDict.Values)
                {
                    formsPlot1.Plot.RemoveAxis(axis);
                }
                axisDict.Clear();
                SignalSets.Clear();
            }
        }

        /// <summary>
        /// Creates a new SignalSet from the data stream. This is called by the constructor and also by the SetSignalSet
        /// </summary>
        /// <param name="stream">The stream to use for the new SignalSet</param>
        /// <param name="index">The index of the new SignalSet in the</param>
        private SignalSet NewSignalSet(DataStream stream, int index)
        {
            lock (lockObj)
            {
                SignalSet signalSet = null;

                /// Adds a signal set to the signal sets.
                if (stream.Timestamps.Length > 0)
                {
                    var signalPlot = new SignalPlotXY[stream.Data.Length];
                    ScottPlot.Renderable.Axis axis = formsPlot1.Plot.YAxis;
                    /// Adds the SignalSets to the axis.
                    if (SignalSets.Count > 0)
                    {
                        axis = formsPlot1.Plot.AddAxis(ScottPlot.Renderable.Edge.Left, axisIndex: SignalSets.Count + 1);
                        /// Add axis to the axisDict.
                        if (!axisDict.ContainsKey(SignalSets.Count + 1))
                            axisDict.Add(SignalSets.Count + 1, axis);
                    }
                    axis.Label($"{stream.Name} ({stream.Host})-{stream.Type}");

                    /// Adds a signal plot to the signal plot.
                    for (int i = 0; i < stream.Data.Length; i++)
                    {
                        signalPlot[i] = formsPlot1.Plot.AddSignalXY(stream.Timestamps, stream.Data[i], label: $"{stream.Name} ({stream.Host})-{stream.Type}" + $": {stream.Channels[i].Label} [{stream.Channels[i].Unit}]");

                        /// Set the y axis index to the last signal set.
                        if (SignalSets.Count > 0)
                        {
                            signalPlot[i].YAxisIndex = SignalSets.Count + 1;
                        }
                    }

                    signalSet = new SignalSet($"{stream.Name} ({stream.Host})-{stream.Type}", signalPlot, TimeWindow, index, stream.Channels[0].Precision);
                    SignalSets.Add(signalSet);

                }

                return signalSet;
            }
        }

        private SignalSet this[int index]
        {
            get => (index < SignalSets.Count && index >= 0) ? SignalSets[index] : null;
        }

        /// <summary>
        /// Adds a vertical line to the plot. It is assumed that the time is in seconds
        /// </summary>
        /// <param name="timestamp">The timestamp of the</param>
        private void AddMarkerLine(double timestamp)
        {
            lock (lockObj)
            {
                var markerLine = formsPlot1.Plot.AddVerticalLine(timestamp, System.Drawing.Color.BlueViolet, 2);
                markerLine.IgnoreAxisAuto = true;
                markerLine.PositionLabel = true;
                markerLine.PositionLabelBackground = markerLine.Color;
                Func<double, string> xFormatter = x => $"X={x:F3}";
                markerLine.PositionFormatter = xFormatter;
            }
        }

        /// <summary>
        /// Handles the Tick event of the timerUpdate control. Stops and starts the timer update if Run is true
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. This is null for the first call</param>
        private void timerUpdate_Tick(Object sender, EventArgs e)
        {
            timerUpdate?.Stop();

            /// Update the plot and plot axes
            if (Run)
            {
                OnTimerUpdate(e);

                lock (lockObj)
                {
                    double maxTs = 0;
                    foreach (var sig in SignalSets)
                    {
                        var ts = sig.UpdateRenderWindow();
                        /// Set the maximum time in milliseconds since the epoch.
                        if (ts > maxTs) maxTs = ts;
                    }

                    lbInfo.Text = lbInfoText;

                    foreach (var marker in markerLines)
                    {
                        AddMarkerLine(marker);
                    }
                    markerLines.Clear();

                    try
                    {
                        formsPlot1.Plot.AxisAuto();
                        formsPlot1.Plot.SetAxisLimitsX(maxTs - TimeWindow, maxTs);

                        formsPlot1.Render();
                    }
                    catch (Exception ex)
                    { };
                }
            }

            /// If the cancellation is requested or disposing is enabled start timerUpdate.
            if (!token.IsCancellationRequested && !disposing) timerUpdate?.Start();
        }

        /// <summary>
        /// Handles the DoubleClick event of the formsPlot1 control. 
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. Not used</param>
        private void formsPlot1_DoubleClick(Object sender, EventArgs e)
        {
            Run = !Run;
            lbStatus.Visible = !Run;

            /// Adds a marker line to the plot.
            if (!Run)
            {
                lbStatus.Location = new System.Drawing.Point(this.Width / 2 - 50, lbStatus.Location.Y);

                foreach (var signalSet in SignalSets)
                {
                    foreach (var signalPlot in signalSet.SignalPlot)
                    {
                        var markerLine = formsPlot1.Plot.AddVerticalLine(signalPlot.Xs[signalPlot.MaxRenderIndex], System.Drawing.Color.Red, 2);
                        markerLine.IgnoreAxisAuto = true;
                        markerLine.PositionLabel = true;
                        markerLine.PositionLabelBackground = markerLine.Color;
                        Func<double, string> xFormatter = x => $"X={x:F3}";
                        markerLine.PositionFormatter = xFormatter;
                        markerLine.DragEnabled = true;
                    }
                }
            }
        }
    }
}
