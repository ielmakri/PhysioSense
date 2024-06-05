using System;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Diagnostics;
using Libfmax;
using LSL;
using System.Net;
using System.Net.Sockets;
using System.Timers;

namespace DataManager
{
    internal class DataStreamConfigurator
    {
        private static System.Timers.Timer flushDataStreamsTimer;

        public class RecordMeta
        {
            public string DataPath { get; set; } = "";
            public string Project { get; set; } = "";
            public string Experiment { get; set; } = "";
            public string Session { get; set; } = "";
            public string Subject { get; set; } = "";
        }

        private const int PLOT_TIME_WINDOW = 5000;    // ms
        private const int PLOT_REFRESH_RATE = 30;   // ms

        public string Name { get; set; }
        public string DataPath { get; set; }
        public string UdpStreamIp { get; set; }
        public int UdpStreamPort { get; set; }
        public int FlushIntervalInSeconds { get; set; }
        public int BufferSize { get; set; }
        
        [JsonIgnore]
        public bool UdpStreamEnable { get; set; }
        [JsonIgnore]
        public RecordMeta Record { get; set; }
        [JsonIgnore]
        public List<DataStream> Streams { get; set; }
        [JsonIgnore]
        public List<DataStreamWriter> DataStreamWriters { get; set; }
        [JsonIgnore]
        public int NbSubscribed { get { return Streams.Count; } }
        [JsonIgnore]
        public StreamInfo[] StreamInfos { get; set; }
        public delegate void InfoMessageDelegate(object sender, Info info);
        public event InfoMessageDelegate InfoMessageReceived;
        public double RecordedKBytes
        {
            get
            {
                double recordedKBytes = 0;
                foreach (var stream in Streams)
                {
                    recordedKBytes += stream.RecordedBytes / 1024.0;
                }
                return recordedKBytes;
            }
        }
        private Dictionary<DataStream, FormPlotter> plotterDict;
        private UdpClient udpStreamClient;


        public DataStreamConfigurator()
        {
            Streams = new List<DataStream>();
            DataStreamWriters = new List<DataStreamWriter>();
            plotterDict = new Dictionary<DataStream, FormPlotter>();
            Record = new RecordMeta();
            udpStreamClient = new UdpClient();
        }

        ~DataStreamConfigurator()
        {
            // CHECK:what is needed here
            udpStreamClient.Close();
            udpStreamClient.Dispose();
        }

        /// <summary>
        /// Called when an info message is received. Override this to handle custom info messages. The default implementation invokes the InfoMessageReceived event.
        /// </summary>
        /// <param name="info">The info message received from the server or null</param>
        protected virtual void InfoMessage(Info info)
        {
            InfoMessageReceived?.Invoke(this, info);
        }

        /// <summary>
        /// Called when a sub info message is received. This is the message handler for the Info message that is sent to the user
        /// </summary>
        /// <param name="sender">The sender of the message</param>
        /// <param name="info">The info to be sent to the user ( can be null</param>
        private void SubInfoMessageReceived(object sender, Info info)
        {
            InfoMessage(info);

        }

        /// <summary>
        /// Called when new data is received. This is the method that will be called by the DataStream.
        /// </summary>
        /// <param name="sender">The sender of the event. It is an instance of DataStream</param>
        /// <param name="index">The index of the data</param>
        public void NewDataReceived(object sender, int index)
        {

            /// Send a UDP stream to the UDP stream.
            if (UdpStreamEnable)
            {
                var stream = (DataStream)sender;
                var udpstring = stream.Name + "-" + stream.Type + " ";
                /// udpstring is a string that will be used to encode the UDP data
                for (int i = 0; i < stream.Data.Length; i++)
                {
                    var dataArr = stream.Data[i];
                    var channelLabel = stream.Channels[i].Label == "" ? $"ch{i + 1}" : stream.Channels[i].Label;
                    udpstring += $"{channelLabel}={dataArr[index]}";
                    /// Add commas to udpstring if the stream is at the end of UDP string
                    if (i < stream.Data.Length - 1) udpstring += ",";
                }
                udpstring += $" {(ulong)(Streamer.ConvertLSL2UnixEpoch(stream.Timestamps[index]) * 1.0E09)}";  //converted to ns
                var udpbytes = ASCIIEncoding.ASCII.GetBytes(udpstring);
                udpStreamClient.Send(udpbytes, udpbytes.Length);
            }
        }

        /// <summary>
        /// Connects to the UDP stream and starts listening for messages. This is called by the constructor and should not be called directly
        /// </summary>
        public void ConnectUdpStream()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(UdpStreamIp), UdpStreamPort); // endpoint where server is listening                
            udpStreamClient.Connect(ep);
        }

        /// <summary>
        /// Gets the stream with the specified UID. If the stream doesn't exist null is returned. This is useful for debugging the stream
        /// </summary>
        /// <param name="streamInfo">The stream to look</param>
        public DataStream GetStream(StreamInfo streamInfo)
        {
            try
            {
                foreach (var stream in Streams)
                {
                    /// Returns the stream object that is the same stream.
                    if (streamInfo.uid() == stream.StreamInfo.uid()) return stream;
                }
            }
            catch (Exception e)
            {
                InfoMessage(new Info($"Stream {streamInfo.name()}-{streamInfo.type()} could not be found", Info.Mode.Error));
            }

            return null;
        }

        /// <summary>
        /// Adds a stream to the list of streams. This is the first time you call GetStream and the stream is added to the list.
        /// </summary>
        /// <param name="streamInfo">The stream to add. This must be a StreamInfo object</param>
        /// <returns>True if the stream was added false</returns>
        public bool AddStream(StreamInfo streamInfo, CancellationToken token)
        {
            /// Add a stream to the list of streams.
            if (GetStream(streamInfo) == null)
            {

                var stream = new DataStream(streamInfo, token);
                stream.InfoMessageReceived += SubInfoMessageReceived;
                stream.NewDataReceived += NewDataReceived;
                Streams.Add(stream);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Shows the plotter for the specified stream. This is a blocking call. If you want to wait for the plotter to disappear call ShowPlotter ()
        /// </summary>
        /// <param name="index">The index of the stream</param>
        public void ShowPlotter(int index, CancellationToken token)
        {
            /// Show the plotter and show the plotter
            if (index >= 0)
            {
                var stream = GetStream(StreamInfos[index]);

                /// Show the plotter for the given stream.
                if (stream != null && (!plotterDict.ContainsKey(stream) || (plotterDict.ContainsKey(stream) && plotterDict[stream].IsDisposed)))
                {
                    var plotter = new FormPlotter($"{stream.Name}, {stream.Type}, {stream.SRate}Hz, {stream.NbChannel} channels",
                                                        PLOT_TIME_WINDOW, PLOT_REFRESH_RATE, token);
                    plotter.Show();
                    plotter.WindowState = FormWindowState.Normal;
                    stream.DataInitialized += plotter.DataInitialized;
                    stream.NewDataReceived += plotter.NewDataReceived;
                    stream.TriggerDataInit();
                }
            }
        }

        /// <summary>
        /// Starts recording data. This is called by Plotter. Record () and can be called multiple times to re - initialize
        /// </summary>
        public void StartRecord()
        {
            var formPlotterRun = FormPlotter.Run;
            FormPlotter.Run = false;
            foreach (var stream in Streams)
            {
                stream.InitData();
                
                var directory = $"{Record.DataPath}\\{Record.Project}\\{Record.Experiment}\\{Record.Session}\\{Record.Subject}";
                var dataStreamWriter = new DataStreamWriter(stream, directory, Record.Subject, Record.Session, BufferSize);
                dataStreamWriter.StartRecording();

                DataStreamWriters.Add(dataStreamWriter);


            }
            FormPlotter.Run = formPlotterRun;

            SetupFlushTimer();

        }

        private void SetupFlushTimer()
        {
            flushDataStreamsTimer = new System.Timers.Timer();
            // Interval in milliseconds (1s = 1000, 10 min = 600000)
            flushDataStreamsTimer.Interval = FlushIntervalInSeconds * 1000;

            flushDataStreamsTimer.Elapsed += OnTimedEvent;

            flushDataStreamsTimer.AutoReset = true;

            flushDataStreamsTimer.Enabled = true;
        }

        private void StopFlushTimer()
        {
            if (flushDataStreamsTimer != null)
            {
                flushDataStreamsTimer.Enabled = false;
                flushDataStreamsTimer.Elapsed -= OnTimedEvent;
                flushDataStreamsTimer = null;
            }
        }


        public void EndRecording()
        {
            StopFlushTimer();

            foreach (var dataStreamWriter in DataStreamWriters)
            {
                dataStreamWriter.EndRecording();
            }
        }

        private void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            foreach (var dataStreamWriter in DataStreamWriters)
            {
                dataStreamWriter.Flush();
            }
        }


        /// <summary>
        /// Saves the record to disk. This is called by LogManager when it is time to save the record
        /// </summary>
        public void SaveRecord()
        {
            var directory = $"{Record.DataPath}\\{Record.Project}\\{Record.Experiment}\\{Record.Session}\\{Record.Subject}";
            /// Create a directory if it doesn t exist.
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            string filenames = "";
            foreach (var stream in Streams)
            {
                /// Save the stream data to the CSV file
                if (stream.RecordedBytes > 0)
                {
                    var subject = (Record.Subject != "") ? $"_{Record.Subject}" : "";
                    var filename = $"{Record.Session}{subject}_{stream.Name} ({stream.Host})_{stream.Type}";
                    filenames += filename + " ";
                    File.WriteAllText(directory + @"\" + filename + ".csv", stream.GetRecordData());
                    File.WriteAllText(directory + @"\" + filename + ".json", stream.GetRecordMeta());
                }
            }
            /// Creates the log files.
            if (filenames != "")
            {
                InfoMessage(new Info($"Log file(s) {filenames} successfully created.", Info.Mode.Event));
                Process.Start("explorer.exe", directory);
            }
        }
    }
}