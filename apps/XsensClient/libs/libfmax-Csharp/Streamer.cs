
using System;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using LSL;
using System.Globalization;

namespace Libfmax
{
    public class StreamMeta
    {
        #region enums

        /// <summary>  
        /// Data format of a channel (each transmitted sample holds an array of channels).
        /// </summary>     
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum ChannelFormat : byte
        {
            Float32 = channel_format_t.cf_float32,     // For up to 24-bit precision measurements in the appropriate physical unit 
                                                       // (e.g., microvolts). Integers from -16777216 to 16777216 are represented accurately.
            Double64 = channel_format_t.cf_double64,    // For universal numeric data as long as permitted by network & disk budget. 
                                                        // The largest representable integer is 53-bit.
            String = channel_format_t.cf_string,      // For variable-length ASCII strings or data blobs, such as video frames,
                                                      // complex event descriptions, etc.
            Int32 = channel_format_t.cf_int32,       // For high-rate digitized formats that require 32-bit precision. Depends critically on 
                                                     // meta-data to represent meaningful units. Useful for application event codes or other coded data.
            Int16 = channel_format_t.cf_int16,       // For very high rate signals (40Khz+) or consumer-grade audio 
                                                     // (for professional audio float is recommended).
            Int8 = channel_format_t.cf_int8,        // For binary signals or other coded data. 
                                                    // Not recommended for encoding string data.
            Int64 = channel_format_t.cf_int64,       // For now only for future compatibility. Support for this type is not yet exposed in all languages. 
                                                     // Also, some builds of liblsl will not be able to send or receive data of this type.
            Undefined = channel_format_t.cf_undefined    // Can not be transmitted.
        };

        /// <summary>  
        /// Post-processing options for stream inlets. Should correspond to the same types in LSL library.
        /// </summary>       
        [Flags]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum InletProcessingOptions : byte
        {
            None = processing_options_t.proc_none,              // No automatic post-processing; return the ground-truth time stamps for manual
                                                                // post-processing. This is the default behavior of the inlet.

            Clocksync = processing_options_t.proc_clocksync,    // Perform automatic clock synchronization; equivalent to manually adding
                                                                // the time_correction() value to the received time stamps.

            Dejitter = processing_options_t.proc_dejitter,      // Remove jitter from time stamps.
                                                                // This will apply a smoothing algorithm to the received time stamps;
                                                                // the smoothing needs to see a minimum number of samples (30-120 seconds worst-case)
                                                                // until the remaining jitter is consistently below 1ms.

            Monotonize = processing_options_t.proc_monotonize,  // Force the time-stamps to be monotonically ascending.
                                                                // Only makes sense if timestamps are dejittered.

            Threadsafe = processing_options_t.proc_threadsafe,  // Post-processing is thread-safe (same inlet can be read from by multiple threads);
                                                                // uses somewhat more CPU.

            All = processing_options_t.proc_ALL                 // The combination of all possible post-processing options.
        };

        /// <summary>  
        /// Processing options for stream outlet (processed before sending out).
        /// </summary>
        [Flags]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum OutletProcessingOptions : byte
        {
            None = 0,
            Monotonize = 1,
            Dejitter = 2,       // Dejitter timestamp  when anomaly occurs for Mod 0 and Mod2
            Ets4Dsr = 4,        // Use equipment timestamp for dropped samples anomaly detection and recovery for Mod0
            All = 1 | 2 | 4
        };

        /// <summary>  
        /// Mod0️: Use Lsl timestamp. 
        /// Mod1: Use Equipment timestamp. 
        /// Mod2: Use Lsl timestamp for the start time and equipment timestamp for the elapsed time.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum TimeSource : int
        {
            Mod0 = 0,
            Mod1 = 1,
            Mod2 = 2
        }

        #endregion

        #region classes and structs

        public class ChannelInfo
        {
            public string Label { get; set; } = "";
            public string Unit { get; set; } = "";
            public int Precision { get; set; }
        }

        public class HardwareInfo
        {
            public string Manufacturer { get; set; } = "";
            public string Model { get; set; } = "";
            public string Serial { get; set; } = "";
            public string Config { get; set; } = "";
            public string Location { get; set; } = "";
        }

        public class SyncInfo
        {
            public TimeSource TimeSource { get; set; }
            public double OffsetMean { get; set; }
            public bool CanDropSamples { get; set; }
            public InletProcessingOptions Ipo { get; set; }
            public OutletProcessingOptions Opo { get; set; }
            public double DriftCoeff { get; set; }
            public double JitterCoeff { get; set; }
        }

        #endregion

        #region variables

        public string Name { get; set; }
        public string Type { get; set; }
        public int NbChannel { get; set; }
        public ChannelFormat ChFormat { get; set; }
        public double SRate { get; set; }
        public List<ChannelInfo> Channels { get; set; }
        public HardwareInfo Hardware { get; set; }
        public SyncInfo Sync { get; set; }

        #endregion
    }

    public class Streamer : StreamMeta
    {
        #region classes and structs

        class SampleTracker
        {
            public double Ini { get; set; } //start time
            public double Ocur { get; set; } // original timestamp received from the timestamp source
            public double Opre { get; set; } // the previous memory from original timestamp received from the timestamp source
            public double Cur { get; set; } //current time after it is being processed 
            public double Pre { get; set; } //previous time
            private double interval;
            private double drift;
            private double driftCoeff;
            private double jitterCoeff;
            public double Interval { get { return interval; } }
            public double Drift { get { return drift; } }
            public bool IsNotMonotonic { get { return Ocur - Opre <= 0; } }
            public bool DroppedSamples { get { return (interval > 0) ? (Ocur - Cur) > Interval * (1 + jitterCoeff) : false; } }

            public SampleTracker(double sRate, double jitterCoeff, double driftCoeff = 0)
            {
                this.interval = (sRate > 0) ? 1 / sRate : 0;
                this.driftCoeff = driftCoeff;
                this.jitterCoeff = jitterCoeff;
            }

            public void Iterate()
            {
                if (Interval > 0 && driftCoeff > 0 && (Ocur - Opre) > jitterCoeff * Interval) // check if there is a new lsl timestamp 
                {
                    drift = (1 - driftCoeff) * drift + (driftCoeff) * (Ocur - Cur);
                }

                if (Ocur >= Opre) Opre = Ocur;
                if (Cur >= Pre) Pre = Cur;

            }
        }

        #endregion

        #region constants

        public readonly static string DT_FORMAT = "HH:mm:ss.fff";
        private readonly static DateTime DT1970 = new DateTime(1970, 1, 1, 0, 0, 0);
        private const int MAX_MOD = 3;

        #endregion

        #region variables

        [JsonIgnore]
        public string Csv { get { return csv.ToString(); } }
        [JsonIgnore]
        public int RecordedBytes { get { return csv.Length; } }
        public AutoResetEvent WaitHandle { get; set; }  // generic wait handle
        public event InfoMessageDelegate InfoMessageReceived;
        private int chunkSize;
        private double[] timestamps;
        private string[,] samples;
        private SampleTracker[] sampleTrackers = new SampleTracker[MAX_MOD]; // time tracker for each mode 
        private int sampleIndex; //
        private double[] sampleMem { get; set; } // data sample memory
        private StringBuilder csv;  // csv string for data file write.         
        private StreamOutlet lslOutlet;

        #endregion

        public Streamer(string name,
                        string type,
                        int nbChannel,
                        ChannelFormat chFormat,
                        double sRate,
                        List<ChannelInfo> channels,
                        HardwareInfo hardware,
                        SyncInfo sync)
        {
            Name = name;
            Type = type;
            NbChannel = nbChannel;
            ChFormat = chFormat;
            SRate = sRate;
            Channels = channels;
            Hardware = hardware;
            Sync = sync;
            // calculate Lsl inlet chunk size:
            chunkSize = (int)Math.Floor(SRate / 100) + 1;
            timestamps = new double[chunkSize];
            samples = new string[chunkSize, NbChannel];
            sampleMem = new double[NbChannel];

            sampleTrackers[(int)TimeSource.Mod0] = new SampleTracker(SRate, Sync.JitterCoeff, Sync.DriftCoeff);
            sampleTrackers[(int)TimeSource.Mod1] = new SampleTracker(SRate, Sync.JitterCoeff);
            sampleTrackers[(int)TimeSource.Mod2] = new SampleTracker(SRate, Sync.JitterCoeff);

            // create csv builder
            csv = new StringBuilder();

            // create wait handler
            WaitHandle = new AutoResetEvent(false);
        }

        public void InitOutlet(string _source_id = "")
        {
            StreamInfo info = new StreamInfo(Name, Type, NbChannel, SRate, (channel_format_t)ChFormat, source_id:_source_id);

            XMLElement xmlChannels = info.desc().append_child("channels");

            for (int i = 0; i < Channels.Count; i++)
            { 
                var ch = Channels[i];
                var xmlChannel = xmlChannels.append_child($"Ch{i + 1}");
                xmlChannel.append_child_value("label", $"{ch.Label}");
                xmlChannel.append_child_value("unit", $"{ch.Unit}");
                xmlChannel.append_child_value("precision", $"{ch.Precision}");
            }
            XMLElement xmlHardware = info.desc().append_child("hardware");
            xmlHardware.append_child_value("manufacturer", $"{Hardware.Manufacturer}");
            xmlHardware.append_child_value("model", $"{Hardware.Model}");
            xmlHardware.append_child_value("serial", $"{Hardware.Serial}");
            xmlHardware.append_child_value("config", $"{Hardware.Config}");
            xmlHardware.append_child_value("location", $"{Hardware.Location}");

            XMLElement xmlSync = info.desc().append_child("synchronization");
            xmlSync.append_child_value("time_source", $"{Sync.TimeSource}");
            xmlSync.append_child_value("offset_mean", $"{Sync.OffsetMean}");
            // xmlSync.append_child_value("offset_rms", $"{0}"); 
            // xmlSync.append_child_value("offset_median", $"{0}"); 
            // xmlSync.append_child_value("offset_5_centile", $"{0}"); 
            // xmlSync.append_child_value("offset_95_centile", $"{0}"); 
            xmlSync.append_child_value("can_drop_samples", $"{Sync.CanDropSamples}");
            xmlSync.append_child_value("inlet_processing_options", $"{Sync.Ipo}");
            xmlSync.append_child_value("outlet_processing_options", $"{Sync.Opo}");
            xmlSync.append_child_value("outlet_drift_coeffificent", $"{Sync.DriftCoeff}");
            xmlSync.append_child_value("outlet_jitter_coeffificent", $"{Sync.JitterCoeff}");

            // create stream outlet                
            lslOutlet = new StreamOutlet(info, chunk_size: chunkSize);
        }

        protected virtual void InfoMessage(Info info)
        {
            InfoMessageReceived?.Invoke(this, info);
        }

        public void NewSample(float[] sample, double tsEquipment = 0)
        {
            NewSample<float>(sample, tsEquipment);
        }

        public void NewSample(double[] sample, double tsEquipment = 0)
        {
            NewSample<double>(sample, tsEquipment);
        }

        public void NewSample(string[] sample, double tsEquipment = 0)
        {
            NewSample<string>(sample, tsEquipment);
        }

        public void NewSample(int[] sample, double tsEquipment = 0)
        {
            NewSample<int>(sample, tsEquipment);
        }

        private void NewSample<T>(T[] sample, double tsEquipment = 0)
        {
            double tsLocal = LSL.LSL.local_clock();

            var reqMonotonize = (Sync.Opo & OutletProcessingOptions.Monotonize) == OutletProcessingOptions.Monotonize;
            var reqDejitter = (Sync.Opo & OutletProcessingOptions.Dejitter) == OutletProcessingOptions.Dejitter;
            var reqUseET4DSA = (Sync.Opo & OutletProcessingOptions.Ets4Dsr) == OutletProcessingOptions.Ets4Dsr;

            string anomalyMsg = "";
            if (sampleIndex == 0)
            {
                sampleTrackers[(int)TimeSource.Mod0].Ini = tsLocal;
                sampleTrackers[(int)TimeSource.Mod1].Ini = tsEquipment;
                sampleTrackers[(int)TimeSource.Mod2].Ini = tsLocal;

                for (int i = 0; i < MAX_MOD; i++)
                {
                    sampleTrackers[i].Ocur = sampleTrackers[i].Ini;
                    sampleTrackers[i].Opre = sampleTrackers[i].Ini;
                    sampleTrackers[i].Cur = sampleTrackers[i].Ini;
                    sampleTrackers[i].Pre = sampleTrackers[i].Ini;
                }

                csv.AppendLine("Name: " + Name);
                csv.AppendLine("Type: " + Type);
                csv.AppendLine("Channel count: " + NbChannel);
                csv.AppendLine("Sample rate [Hz]: " + SRate);
                csv.AppendLine("Mode: " + Sync.TimeSource);
                csv.AppendLine("Mean latency [s]: " + Sync.OffsetMean);
                csv.AppendLine($"Start time [{DT_FORMAT}]: " + ConvertLSL2DT(tsLocal).ToString(DT_FORMAT));
                csv.AppendLine("");

                string csvTitle = "Local Time,Type,Interval";
                for (int i = 0; i < MAX_MOD; i++)
                {
                    csvTitle += $",Ts{i}";
                }

                for (int i = 0; i < NbChannel; i++)
                {
                    csvTitle += $",{Channels[i].Label}[{Channels[i].Unit}]";
                }
                csv.AppendLine(csvTitle);

                PushIt(sample, sampleIndex);
            }
            else
            {
                sampleTrackers[(int)TimeSource.Mod0].Ocur = tsLocal;
                sampleTrackers[(int)TimeSource.Mod1].Ocur = tsEquipment;
                sampleTrackers[(int)TimeSource.Mod2].Ocur = sampleTrackers[(int)TimeSource.Mod0].Ini
                                                            + tsEquipment
                                                            - sampleTrackers[(int)TimeSource.Mod1].Ini;

                if (sampleTrackers[(int)TimeSource.Mod1].IsNotMonotonic)
                    anomalyMsg += ",Device timestamp not monotonic";

                if (sampleTrackers[(int)TimeSource.Mod1].DroppedSamples)
                    anomalyMsg += ",Device timestamp delayed. Dropped samples?";

                if (sampleTrackers[(int)TimeSource.Mod1].IsNotMonotonic && (reqMonotonize || reqDejitter))
                {
                    sampleTrackers[(int)TimeSource.Mod1].Cur += sampleTrackers[(int)TimeSource.Mod1].Interval;
                    if (Sync.TimeSource == TimeSource.Mod1)
                    {
                        PushIt(sample, sampleIndex);
                    }
                }
                else if (SRate > 0 && sampleTrackers[(int)TimeSource.Mod1].DroppedSamples && reqDejitter && Sync.CanDropSamples)
                {
                    var gap = sampleTrackers[(int)TimeSource.Mod1].Ocur - sampleTrackers[(int)TimeSource.Mod1].Cur;
                    var interpolatedData = new double[sample.Length];
                    while (true)
                    {
                        sampleTrackers[(int)TimeSource.Mod1].Cur += sampleTrackers[(int)TimeSource.Mod1].Interval;
                        if (Sync.TimeSource == TimeSource.Mod1)
                        {
                            if (typeof(T) != typeof(string)) // assume then data is numeric 
                            {
                                var ratio = (sampleTrackers[(int)TimeSource.Mod1].Cur - sampleTrackers[(int)TimeSource.Mod1].Pre) / gap;
                                for (int i = 0; i < interpolatedData.Length; i++)
                                {
                                    interpolatedData[i] = ratio * ((double)((object)sample[i]) - sampleMem[i]) + sampleMem[i];
                                }
                                PushIt(interpolatedData, sampleIndex);
                            }
                            else
                            {
                                PushIt(sample, sampleIndex);
                            }
                        }

                        if (sampleTrackers[(int)TimeSource.Mod1].Cur <
                                sampleTrackers[(int)TimeSource.Mod1].Pre + gap - Sync.JitterCoeff * sampleTrackers[(int)TimeSource.Mod1].Interval)
                        {
                            sampleIndex++;
                        }
                        else break;
                    }
                }
                else
                {
                    sampleTrackers[(int)TimeSource.Mod1].Cur = sampleTrackers[(int)TimeSource.Mod1].Ocur;
                    if (Sync.TimeSource == TimeSource.Mod1)
                    {
                        PushIt(sample, sampleIndex);
                    }
                }

                if (sampleTrackers[(int)TimeSource.Mod2].IsNotMonotonic && (reqMonotonize || reqDejitter))
                {
                    sampleTrackers[(int)TimeSource.Mod2].Cur += sampleTrackers[(int)TimeSource.Mod2].Interval;
                    if (Sync.TimeSource == TimeSource.Mod2)
                    {
                        PushIt(sample, sampleIndex);
                    }
                }
                else if (SRate > 0 && sampleTrackers[(int)TimeSource.Mod2].DroppedSamples && reqDejitter && Sync.CanDropSamples)
                {
                    var gap = sampleTrackers[(int)TimeSource.Mod2].Ocur - sampleTrackers[(int)TimeSource.Mod2].Cur;
                    var interpolatedData = new double[sample.Length];
                    while (true)
                    {
                        sampleTrackers[(int)TimeSource.Mod2].Cur += sampleTrackers[(int)TimeSource.Mod2].Interval;
                        if (Sync.TimeSource == TimeSource.Mod2)
                        {
                            if (typeof(T) != typeof(string)) // assume then data is numeric 
                            {
                                var ratio = (sampleTrackers[(int)TimeSource.Mod2].Cur - sampleTrackers[(int)TimeSource.Mod2].Pre) / gap;
                                for (int i = 0; i < interpolatedData.Length; i++)
                                {
                                    interpolatedData[i] = ratio * ((double)((object)sample[i]) - sampleMem[i]) + sampleMem[i];
                                }

                                PushIt(interpolatedData, sampleIndex);
                            }
                            else
                            {
                                PushIt(sample, sampleIndex);
                            }
                        }

                        if (sampleTrackers[(int)TimeSource.Mod2].Cur <
                                sampleTrackers[(int)TimeSource.Mod2].Pre + gap - Sync.JitterCoeff * sampleTrackers[(int)TimeSource.Mod2].Interval)
                        {
                            sampleIndex++;
                        }
                        else break;
                    }
                }
                else
                {
                    sampleTrackers[(int)TimeSource.Mod2].Cur = sampleTrackers[(int)TimeSource.Mod2].Ocur;
                    if (Sync.TimeSource == TimeSource.Mod2)
                    {
                        PushIt(sample, sampleIndex);
                    }
                }

                if (SRate > 0 && reqDejitter && Sync.CanDropSamples)
                {
                    var gap = reqUseET4DSA ? sampleTrackers[(int)TimeSource.Mod2].Cur - sampleTrackers[(int)TimeSource.Mod2].Pre :
                                             sampleTrackers[(int)TimeSource.Mod0].Ocur - sampleTrackers[(int)TimeSource.Mod0].Cur;
                    var interpolatedData = new double[sample.Length];
                    while (true)
                    {
                        sampleTrackers[(int)TimeSource.Mod0].Cur += sampleTrackers[(int)TimeSource.Mod0].Interval;
                        if (Sync.TimeSource == TimeSource.Mod0)
                        {
                            if (typeof(T) != typeof(string)) // assume then data is numeric 
                            {
                                var ratio = (sampleTrackers[(int)TimeSource.Mod0].Cur - sampleTrackers[(int)TimeSource.Mod0].Pre) / gap;
                                for (int i = 0; i < interpolatedData.Length; i++)
                                {
                                    interpolatedData[i] = ratio * (Convert.ToDouble((object)sample[i]) - sampleMem[i]) + sampleMem[i];
                                }


                                PushIt(interpolatedData, sampleIndex);
                            }
                            else
                            {
                                PushIt(sample, sampleIndex);
                            }
                        }

                        if (sampleTrackers[(int)TimeSource.Mod0].Cur <
                                sampleTrackers[(int)TimeSource.Mod0].Pre + gap - Sync.JitterCoeff * sampleTrackers[(int)TimeSource.Mod0].Interval)
                        {
                            sampleIndex++;
                        }
                        else break;
                    }
                }
                else if (SRate > 0 && reqDejitter)
                {
                    sampleTrackers[(int)TimeSource.Mod0].Cur = sampleTrackers[(int)TimeSource.Mod0].Pre + sampleTrackers[(int)TimeSource.Mod0].Interval;
                    if (Sync.TimeSource == TimeSource.Mod0)
                    {
                        PushIt(sample, sampleIndex);
                    }
                }
                else
                {
                    sampleTrackers[(int)TimeSource.Mod0].Cur = sampleTrackers[(int)TimeSource.Mod0].Ocur;
                    if (Sync.TimeSource == TimeSource.Mod0)
                    {
                        PushIt(sample, sampleIndex);
                    }
                }
            }

            if (anomalyMsg != "")
            {
                anomalyMsg = $"{DateTime.Now.ToString(DT_FORMAT)},{Type}" + anomalyMsg;
                InfoMessage(new Info(anomalyMsg, Info.Mode.Error));
            }

            for (int i = 0; i < MAX_MOD; i++)
            {
                sampleTrackers[i].Iterate();
            }

            if (typeof(T) != typeof(string) && SRate > 0 && reqDejitter && Sync.CanDropSamples) // if data is numeric take a copy of sample which could be used for interpolation
            {
                Array.Copy(sample, sampleMem, sample.Length);
            }

            sampleIndex++;

        }

        public void PushIt<T>(T[] sample, int index)
        {
            timestamps[index % chunkSize] = sampleTrackers[((int)Sync.TimeSource)].Cur + sampleTrackers[((int)Sync.TimeSource)].Drift;

            for (int i = 0; i < sample.Length; i++)
            {
                samples[index % chunkSize, i] = ((object)sample[i]).ToString().Replace(',', '.');
                //samples[index % chunkSize, i].Replace(',', '.');
                //System.Diagnostics.Debug.WriteLine(samples[index % chunkSize, i]);
            }

            if (index % chunkSize == chunkSize - 1)
            {
                lslOutlet?.push_chunk(samples, timestamps);
                //System.Diagnostics.Debug.WriteLine(samples);
            }

            var infoMsg = $"{DateTime.Now.ToString(DT_FORMAT)},{Type},{sampleTrackers[(int)TimeSource.Mod0].Interval * 1000:F2}, {sampleTrackers[(int)TimeSource.Mod0].Drift * 1000:F2}";
            for (int i = 0; i < MAX_MOD; i++)
            {
                infoMsg += $",{ConvertLSL2DT(sampleTrackers[i].Cur + sampleTrackers[i].Drift).ToString(DT_FORMAT)}";
            }
            for (int i = 0; i < sample.Length; i++)
            {
                var format = (Channels[i].Precision >= 0) ? $"F{Channels[i].Precision}" : "";
                var formattedData = "{" + $"0:F{Channels[i].Precision}" + "}";
                infoMsg += "," + String.Format(formattedData, sample[i]);
                //var formattedData = String.Format(CultureInfo.InvariantCulture, "{0:" + format + "}", sample[i]);
                //infoMsg += "," + formattedData;
                //System.Diagnostics.Debug.WriteLine(infoMsg);
            }

            csv.AppendLine(infoMsg);
            InfoMessage(new Info(infoMsg, Info.Mode.Data));

        }

        public static double ConvertDT2LSL(DateTime dt)
        {
            var ticks = Stopwatch.GetTimestamp();
            var uptime = ((double)ticks) / Stopwatch.Frequency;
            var uptimeSpan = TimeSpan.FromSeconds(uptime);
            var origin = DateTime.Now.AddSeconds(-uptimeSpan.TotalSeconds);
            return (dt - origin).TotalSeconds;
        }

        public static DateTime ConvertLSL2DT(double ts)
        {
            var ticks = Stopwatch.GetTimestamp();
            var uptime = ((double)ticks) / Stopwatch.Frequency;
            var uptimeSpan = TimeSpan.FromSeconds(uptime);

            return DateTime.Now.AddSeconds(-uptimeSpan.TotalSeconds + ts);
        }

        public static double ConvertLSL2UnixEpoch(double ts)
        {
            var ticks = Stopwatch.GetTimestamp();
            var uptime = ((double)ticks) / Stopwatch.Frequency;
            var uptimeSpan = TimeSpan.FromSeconds(uptime);
            var epochTimeNow = DateTime.UtcNow - DT1970;
            return epochTimeNow.TotalSeconds - uptimeSpan.TotalSeconds + ts;
        }

        public static double ConvertUnixEpoch2LSL(double ts)
        {
            var ticks = Stopwatch.GetTimestamp();
            var uptime = ((double)ticks) / Stopwatch.Frequency;
            var uptimeSpan = TimeSpan.FromSeconds(uptime);
            var epochTimeNow = DateTime.UtcNow - DT1970;
            return  uptimeSpan.TotalSeconds - epochTimeNow.TotalSeconds + ts;
        }        
    }
}