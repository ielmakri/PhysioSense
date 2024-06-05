/// This is the main method that runs in order to create the DataManager. It's a bit complicated because we have to make sure that the order matters
using System;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using System.Globalization;
using System.Timers;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.Interpolation;
using Libfmax;
using LSL;

namespace DataManager
{
    public delegate void DataInitEventHandler(object sender, int index, bool blocking);
    public delegate void NewDataEventHandler(object sender, int index);

    public class DataStream : StreamMeta
    {
        #region constants

        public const int MAX_BUFFER = 3;   // 3 seconds of buffer  //TODO: get LSL MaxBuffer value from Stream / Common class or config file?
        private const int DATA_SIZE = 10000;

        #endregion


        #region variables
        [JsonIgnore]
        public string Host { get; set; }
        [JsonIgnore]
        public int RecordedBytes { get { return dataCount * (NbChannel + 1) * sizeof(double); } } // assuming here data size is same with double type...
        [JsonIgnore]
        public StreamInfo StreamInfo { get { return inlet.info(); } }
        public event InfoMessageDelegate InfoMessageReceived;
        public event DataInitEventHandler DataInitialized;
        public event NewDataEventHandler NewDataReceived;
        [JsonIgnore]
        public bool IsActive
        {
            get
            {
                bool status = !dataReceiveTask.IsCompleted &&
                                           !dataReceiveTask.IsCanceled &&
                                           !dataReceiveTask.IsFaulted &&
                                           !inlet.IsClosed &&
                                           !inlet.IsInvalid;
                                           
                return status;
            }
        }
        [JsonIgnore]
        public double[] Timestamps { get { return timestamps; } }
        [JsonIgnore]
        public double[][] Data { get { return data; } }
        [JsonIgnore]
        public string[][] MarkerData { get { return markerdata; } }
        private StreamInlet inlet;
        private int chunkLen;
        private double[] timestamps;
        private double[][] data;
        private string[][] markerdata;
        private double[] resTimestamps; //resampled
        private double[][] resData; //resampled
        private int resIndex;
        private int dataSize = DATA_SIZE;
        private int dataCount;
        private Task dataReceiveTask;
        private CancellationToken token;
        private readonly Object lockObj = new Object();
        private DateTime recordStartTime;

        #endregion

        public DataStream(StreamInfo streamInfo, CancellationToken token)
        {
            this.token = token;

            Name = streamInfo.name();
            Host = streamInfo.hostname();
            Type = streamInfo.type();
            ChFormat = (ChannelFormat)streamInfo.channel_format();
            NbChannel = streamInfo.channel_count();
            SRate = streamInfo.nominal_srate();
            Channels = new List<ChannelInfo>(NbChannel);

            /* ❗ Important: if sRate is zero from the outlet and its chunkLen is larger than 1,
            then there is an 'gotcha' if timestamps are not defined and sent explicitly for the outlet. 
            The 'gotcha' is the data in the chunk will not be timestamped automatically in accordance with sample rate 
            which makes sense since sample rate is zero! */
            chunkLen = (int)Math.Floor(SRate / 100) + 1;  // chunkLen is increased accordingly when freq > 100 Hz

            // open inlet temporarily only to get its meta-data. Reason to do it this way: 
            // StreamInfo resulted from resolve_streams does not contain extended xml nodes! 
            inlet = new StreamInlet(streamInfo);

            // 👇 Uncomment to dump received meta-data from the stream
            // Console.WriteLine(inlet.info().as_xml().ToString());

            StreamMeta dsFromFile = LoadConfig();


            if (dsFromFile != null && dsFromFile.Channels != null)
            {
                Channels = dsFromFile.Channels.ToList();
                Console.WriteLine($"{Name}:{Type}, " + "Channel meta-data loaded from config file. ");
            }
            else
            {
                try
                {
                    XMLElement xmlChannels = inlet.info().desc().child("channels");
                    XMLElement xmlChannel = xmlChannels.first_child();
                    for (int i = 0; i < NbChannel; i++)
                    {
                        Channels.Add(new ChannelInfo()
                        {
                            Label = xmlChannel.child_value("label"),
                            Unit = xmlChannel.child_value("unit"),
                            Precision = Int32.Parse(xmlChannel.child_value("precision"))
                        });
                        xmlChannel = xmlChannel.next_sibling();
                    }
                    Console.WriteLine($"{Name}:{Type}, " + "Channel meta-data received from stream. ");
                }
                catch
                {
                    for (int i = 0; i < NbChannel; i++)
                    {
                        int precision = 0;
                        if (ChFormat == ChannelFormat.Double64 || ChFormat == ChannelFormat.Float32) precision = 1;
                        Channels.Add(new ChannelInfo()
                        {
                            Label = $"Ch{i + 1}",
                            Unit = "",
                            Precision = precision
                        });
                    }
                    Console.WriteLine($"{Name}:{Type}, " + "Channel meta-data could not be loaded, assuming default values. ");
                };
            }

            if (dsFromFile != null && dsFromFile.Hardware != null)
            {
                Hardware = dsFromFile.Hardware;
                Console.WriteLine($"{Name}:{Type}, " + "Hardware meta-data loaded from config file. ");
            }
            else
            {
                try
                {
                    XMLElement xmlHardware = inlet.info().desc().child("hardware");
                    Hardware = new HardwareInfo()
                    {
                        Manufacturer = xmlHardware.child_value("manufacturer"),
                        Model = xmlHardware.child_value("model"),
                        Serial = xmlHardware.child_value("serial"),
                        Config = xmlHardware.child_value("config"),
                        Location = xmlHardware.child_value("location")
                    };
                    Console.WriteLine($"{Name}:{Type}, " + "Hardware meta-data received from stream. ");
                }
                catch
                {
                    Hardware = new HardwareInfo();
                    Console.WriteLine($"{Name}:{Type}, " + "Hardware meta-data could not be loaded, assuming default values. ");
                };

            }

            if (dsFromFile != null && dsFromFile.Sync != null)
            {
                Sync = dsFromFile.Sync;
                Console.WriteLine($"{Name}:{Type}, " + "Synchronization meta-data loaded from config file. ");
            }
            else
            {
                try
                {
                    XMLElement xmlSync = inlet.info().desc().child("synchronization");

                    Sync = new SyncInfo()
                    {
                        TimeSource = (TimeSource)Enum.Parse(typeof(TimeSource),
                                xmlSync.child_value("time_source")),
                        OffsetMean = Double.Parse(xmlSync.child_value("offset_mean"), CultureInfo.InvariantCulture),
                        CanDropSamples = Boolean.Parse(xmlSync.child_value("can_drop_samples")),
                        Ipo = (InletProcessingOptions)Enum.Parse(typeof(InletProcessingOptions),
                                xmlSync.child_value("inlet_processing_options")),
                        Opo = (OutletProcessingOptions)Enum.Parse(typeof(OutletProcessingOptions),
                                xmlSync.child_value("outlet_processing_options")),
                        DriftCoeff = Double.Parse(xmlSync.child_value("outlet_drift_coeffificent"), CultureInfo.InvariantCulture),
                        JitterCoeff = Double.Parse(xmlSync.child_value("outlet_jitter_coeffificent"), CultureInfo.InvariantCulture)
                    };
                    Console.WriteLine($"{Name}:{Type}, " + "Synchronization meta-data received from stream. ");
                }
                catch
                {
                    Sync = new SyncInfo()
                    {
                        TimeSource = TimeSource.Mod0,
                        OffsetMean = 0,
                        CanDropSamples = true,
                        Ipo = InletProcessingOptions.Clocksync,
                        Opo = OutletProcessingOptions.None,
                        DriftCoeff = 0.0,
                        JitterCoeff = 0.0
                    };
                    Console.WriteLine($"{Name}:{Type}, " + "Synchronization meta-data could not be loaded, assuming default values. ");
                };
            }


            File.WriteAllText(Directory.GetCurrentDirectory() + $"\\config\\{this.Name}-{this.Type}.json", GetRecordMeta());

            inlet.close_stream();

            inlet = new StreamInlet(streamInfo, max_buflen: MAX_BUFFER, max_chunklen: chunkLen,
                            postproc_flags: (processing_options_t)Sync.Ipo);

            InitData();

            dataReceiveTask = Task.Run(async () => await DataReceiverAsync(this.token), this.token);

        }

        ~DataStream()
        {
            inlet?.Dispose();
        }

        /// <summary>
        /// Called when an info message is received. Override this to handle custom info messages
        /// </summary>
        /// <param name="info">The info message to</param>
        protected virtual void InfoMessage(Info info)
        {
            InfoMessageReceived?.Invoke(this, info);
        }

        /// <summary>
        /// Called when data is initialized. By default does nothing. Override this to customize initialization behavior
        /// </summary>
        /// <param name="index">Index of the data to initialize</param>
        /// <param name="blocking">True if initialization should block until it is done</param>
        protected virtual void OnDataInitialize(int index, bool blocking)
        {
            DataInitialized?.Invoke(this, index, blocking);
        }

        /// <summary>
        /// Called when new data is received. This is the place where you can change the behavior of the event
        /// </summary>
        /// <param name="index">The index of the</param>
        protected virtual void OnNewData(int index)
        {
            NewDataReceived?.Invoke(this, index);
        }

        /// <summary>
        /// Initializes the data. This is called by Plotter to start recording data to
        /// </summary>
        public void InitData()
        {
            lock (lockObj)
            {
                Thread.Sleep(10);  //this is to ensure NewDataReceived of the plotter is finalized (it has BeginInvoke in it)
                dataSize = DATA_SIZE;
                timestamps = new double[dataSize];
                data = new double[NbChannel][];
                markerdata = new string[NbChannel][];
                /// Fills the data and marker data.
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = new double[dataSize];
                    markerdata[i] = new string[dataSize];
                }

                resTimestamps = new double[0];
                resData = new double[NbChannel][];
                //markerdata = new string[NbChannel][];  //TODO: include markers in resampling?...
                /// Sets resData to a new array of doubles
                for (int i = 0; i < data.Length; i++)
                {
                    resData[i] = new double[0];
                }

                recordStartTime = DateTime.Now;
                dataCount = 0;
                resIndex = 0;

                TriggerDataInit();
            }
        }

        /// <summary>
        /// Triggers the data initialization. This is called by the DataManager when it has a chance to initialize the
        /// </summary>
        public void TriggerDataInit()
        {
            OnDataInitialize(0, true);
        }

        /// <summary>
        /// Receives data from Inlet and processes it. This is the data receiver that will be called in a loop
        /// </summary>
        /// <param name="token">Cancellation token to stop</param>
        private async Task DataReceiverAsync(CancellationToken token)
        {
            string[,] sampleChunk = new string[chunkLen, NbChannel]; // read sample chunk
            double[] timestampChunk = new double[chunkLen];
            try
            {
                /// Pulls the sample chunk and updates data and marker data.
                while (!token.IsCancellationRequested)
                {
                    var num = inlet.pull_chunk(sampleChunk, timestampChunk, timeout: LSL.LSL.FOREVER);

                    /// This method is called by the main loop to handle the number of samples.
                    if (num > 0)
                    {
                        lock (lockObj)
                        {
                            /// This method is called by the main loop.
                            for (int i = 0; i < num; i++)
                            {
                                timestamps[dataCount] = timestampChunk[i] - Sync.OffsetMean; //  previously, before introducing processsing flags, this was: + inlet.time_correction();  

                                var infoMsg = Streamer.ConvertLSL2DT(timestamps[dataCount]).ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
                                /// This method is used to store the sample chunk in the infoMsg.
                                for (int j = 0; j < sampleChunk.GetLength(1); j++)
                                {
                                    /// Set the sample chunk data count.
                                    if (ChFormat != ChannelFormat.String && Double.TryParse(sampleChunk[i, j], NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                                    {
                                        /// Set result to 0.
                                        if (Double.IsNaN(result))
                                        {
                                            result = 0;
                                        }
                                        data[j][dataCount] = result;
                                    }
                                    else
                                    {
                                        markerdata[j][dataCount] = sampleChunk[i, j];
                                    }
                                    infoMsg += $",{sampleChunk[i, j]}";
                                }

                                InfoMessage(new Info($"{Name}:{Type}, " + infoMsg, Info.Mode.Data));
                                OnNewData(dataCount);

                                /// Increments the data count by the maximum data size.
                                if (dataCount < int.MaxValue)
                                {
                                    dataCount++;
                                }
                                else
                                {
                                    InfoMessage(new Info($"{Name}:{Type}, " + "Maximum data size is exceeded, stopping.", Info.Mode.CriticalEvent));
                                    throw new Exception("Maximum data size is exceeded");
                                }

                                /// This method is called when the data is initialized.
                                if (dataCount >= dataSize)
                                {
                                    dataSize = (dataSize >= int.MaxValue / 2) ? int.MaxValue : 2 * dataSize;
                                    Array.Resize<double>(ref timestamps, dataSize);
                                    /// Resize the data to the data size
                                    for (int j = 0; j < data.Length; j++)
                                    {
                                        Array.Resize<double>(ref data[j], dataSize);
                                        Array.Resize<string>(ref markerdata[j], dataSize);
                                    }

                                    OnDataInitialize(dataCount - 1, true);
                                }

                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (!(e is System.ObjectDisposedException))
                    InfoMessage(new Info($"{Name}:{Type}, " + "An exception occurred in the data receive loop", Info.Mode.CriticalError));
            }
            await Task.Delay(0);  //to prevent warning for async function with no await operator
        }

        /// <summary>
        /// Resamples the data to a given frequency. This is useful for resampling a set of data in the event of a change in the value of a sensor
        /// </summary>
        /// <param name="resFreq">The frequency to resample</param>
        public void ResampleData(int resFreq)
        {
            try
            {
                var dataCountMem = dataCount;
                var interpolWindow = dataCountMem - resIndex;

                /// This method is used to compute the roi data.
                if (interpolWindow >= 5)
                {
                    var appendIndex = resTimestamps.Length;
                    /// Removes the oldest record from the resTimestamps array.
                    while (appendIndex > 0 && resTimestamps[appendIndex - 1] > timestamps[resIndex])
                    {
                        appendIndex--;
                    }

                    var appendTimestamp = timestamps[resIndex];
                    /// append timestamp to the appendIndex.
                    if (appendIndex > 0 && appendIndex < resTimestamps.Length)
                    {
                        appendTimestamp = resTimestamps[appendIndex];
                    }

                    var tarr = new double[interpolWindow];
                    Array.Copy(timestamps, resIndex, tarr, 0, interpolWindow);

                    var roiTs = Enumerable.Range(0, (int)(resFreq * ((tarr[tarr.Length - 1] - appendTimestamp))) + 1)
                                                      .Select(p => p / (double)resFreq + appendTimestamp).ToArray();
                    var roiData = new double[data.Length][];
                    LinearSpline[] roiDataLinSpline = new LinearSpline[data.Length];
                    /// Interpolate the data in the data array.
                    for (int i = 0; i < data.Length; i++)
                    {
                        var darr = new double[interpolWindow];
                        Array.Copy(data[i], resIndex, darr, 0, interpolWindow);
                        roiDataLinSpline[i] = LinearSpline.Interpolate(tarr, darr);
                        roiData[i] = roiTs.Select(p => roiDataLinSpline[i].Interpolate(p)).ToArray();
                    }

                    // TEST:  overflow (Int32.MaxValue)
                    var resize = resTimestamps.Length + (appendIndex - resTimestamps.Length) + roiTs.Length;

                    Array.Resize<double>(ref resTimestamps, resize);
                    Array.Copy(roiTs, 0, resTimestamps, appendIndex, roiTs.Length);
                    /// Copy the ROI data to the data.
                    for (int i = 0; i < data.Length; i++)
                    {
                        Array.Resize<double>(ref resData[i], resize);
                        Array.Copy(roiData[i], 0, resData[i], appendIndex, roiData[i].Length);
                    }

                    resIndex = dataCountMem - 1;
                }
            }
            catch (Exception e)
            { }
        }

        public string FormatForOutput(int dataPosition)
        {
            var s = Streamer.ConvertLSL2DT(timestamps[dataPosition]).ToString("HH:mm:ss.fff");
            /// Returns a string representation of the channel data.
            for (int j = 0; j < NbChannel; j++)
            {
                s += (ChFormat != ChannelFormat.String) ? $",{Convert.ToString(data[j][dataPosition], CultureInfo.InvariantCulture)}" : $",{markerdata[j][dataPosition]}";
            }

            return s;
        }

        public string GetHeaderData()
        {
            StringBuilder csv = new StringBuilder();

            csv.AppendLine("Name: " + Name);
            csv.AppendLine("Type: " + Type);
            csv.AppendLine("Channels: " + NbChannel);
            csv.AppendLine("SRate: " + SRate + "Hz");
            csv.AppendLine("Start time: " + recordStartTime.ToString("dd-MMM-yyyy HH:mm:ss.fff"));
            csv.AppendLine(FormatEndTimeLine() + "\n");

            string csvTitle = "Time[HH:mm:ss.fff]";

            /// Generates csv title for each channel
            for (int k = 0; k < NbChannel; k++)
            {
                var label = (Channels[k].Label == "") ? $"Ch{k + 1}" : $"{Channels[k].Label}";
                csvTitle += $",{label}[{Channels[k].Unit}]";
            }
            csv.AppendLine(csvTitle);

            return csv.ToString();
        }

        private string FormatEndTimeLine(DateTime? timeStamp = null)
        {
            if (!timeStamp.HasValue)
            {
                return "End time: dd-MMM-yyyy HH:mm:ss.fff";
            }
            else
            {
                return "End time: " + timeStamp.Value.ToString("dd-MMM-yyyy HH:mm:ss.fff");
            }
        }

        public void ReplaceEndTimeInHeader(string filename)
        {
            try
            {
                FileUtility.ReplaceInFile(filename, 10, FormatEndTimeLine(), FormatEndTimeLine(DateTime.Now));
            }
            catch (Exception e)
            {
                InfoMessage(new Info($"Something went wrong while writing the End time: {e.Message}.", Info.Mode.Error));
            }
        }

        /// <summary>
        /// Gets the record data. This is a CSV string with the following fields : Name Type Channels SRate Start Time End Time
        /// </summary>
        /// <returns>A comma separated string of</returns>
        public string GetRecordData()
        {
            StringBuilder csv = new StringBuilder();

            csv.Append(this.GetHeaderData());

            /// Append to csv file.
            for (int i = 0; i < dataCount; i++)
            {
                var s = Streamer.ConvertLSL2DT(timestamps[i]).ToString("HH:mm:ss.fff");
                /// Returns a string representation of the channel data.
                for (int j = 0; j < NbChannel; j++)
                {
                    s += (ChFormat != ChannelFormat.String) ? $",{Convert.ToString(data[j][i], CultureInfo.InvariantCulture)}" : $",{markerdata[j][i]}";
                }
                csv.AppendLine(s);
            }

            return csv.ToString();
        }

        /// <summary>
        /// Gets the meta data for this record. This can be used to determine how data should be stored in the data source.
        /// </summary>
        /// <returns>The meta data for this record as a JSON string</returns>
        public string GetRecordMeta()
        {
            var options = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };
            string jsonString = JsonSerializer.Serialize<StreamMeta>(this, options);

            return jsonString;
        }

        /// <summary>
        /// Loads the config file and returns the meta data for the config file. This is used to save the
        /// </summary>
        public StreamMeta LoadConfig()
        {
            StreamMeta ds = null;

            String line, configString = "";
            try
            {
                //Pass the file path and file name to the StreamReader constructor
                StreamReader sr = new StreamReader(Directory.GetCurrentDirectory() + $"\\config\\{this.Name}-{this.Type}.json");
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
                ds = JsonSerializer.Deserialize<StreamMeta>(configString, options);

            }
            catch
            {
                InfoMessage(new Info($"{Name}:{Type}, " + "Could not open the config file " + $"\\config\\{this.Name}-{this.Type}.json", Info.Mode.Error));
            }

            return ds;

        }

    }
}