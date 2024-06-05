using System;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Globalization;
using Libfmax;

namespace CaptivClient
{
    public partial class MainForm : Form
    {
        private class TEAConfigurator
        {
            #region  variables

            public string Name { get; set; }
            public string Ip { get; set; }
            public int Port { get; set; }
            public delegate void InfoMessageDelegate(object sender, Info info);
            public event InfoMessageDelegate InfoMessageReceived;
            [JsonIgnore]
            public double RecordedKBytes
            {
                get
                {
                    double recordedKBytes = 0;
                    foreach (var stream in Streamers)
                    {
                        recordedKBytes += stream.RecordedBytes / 1024.0;
                    }
                    return recordedKBytes;
                }
            }
            public List<TEAStreamer> Streamers { get; set; }
            private AsyTcpClient asyClient;
            /// <summary>Captiv setting: 0: 8Hz, 1: 16Hz, 2: 32Hz</summary>
            private readonly double[] motionServRefreshRates = { 8, 16, 32 };
            //<summary>From Captiv: Number of sensors to be streamed</summary>
            private int sensorCount;
            /// <summary>Captiv setting: 0: Text, 1: Binary</summary>
            private int serverMode;

            /// <summary> State transitions: [0] TEAPINGSRV ➡ [1] TEAInitRec ➡ [2] TEAListDevices ➡ [3] TEASetMotionServer </summary>
            private AutoResetEvent[] deviceStateWaitHandle = new AutoResetEvent[4];

            #endregion

            /// <summary>
            /// Called when an info message is received. Override this to handle custom info messages
            /// </summary>
            /// <param name="info">The info message to</param>
            protected virtual void InfoMessage(Info info)
            {
                InfoMessageReceived?.Invoke(this, info);
            }

            /// <summary>
            /// Closes the inlet. This is called when the user presses the close button
            /// </summary>
            public void CloseInlet()
            {
                asyClient.Close();
            }

            /// <summary>
            /// Connects to inlet and starts listening for data. This is called by Teleop.
            /// </summary>
            /// <param name="token">Cancellation token to cancel connection. If it is cancelled the connection will be terminated.</param>
            /// <returns>True if connected else false on failure. If it is not connected it will return</returns>
            public bool ConnectToInlet(CancellationToken token)
            {
                foreach (var streamer in Streamers)
                {
                    streamer.Id = -1;   // -1 means non-active. Non-negative ID numbers are received from Captiv, then we know which sensor is active. 
                }

                asyClient = new AsyTcpClient(Ip, Port, token);
                asyClient.InfoMessageReceived += SubInfoMessageReceived;
                asyClient.DataReceived += AsyDataReceived;

                /// Creates a new AutoResetEvent object that will be notified when the device is ready to be ready for use.
                for (int i = 0; i < deviceStateWaitHandle.Length; i++)
                {
                    deviceStateWaitHandle[i] = new AutoResetEvent(false);
                }

                var connected = asyClient.Connect();
                /// This method is called by the Telea server to start the Telea server.
                if (connected)
                {
                    asyClient.Start();

                    SendData("TEAPINGSRV" + "\n");   // hello captiv

                    /// Wait for the device state to be achieved.
                    if (deviceStateWaitHandle[0].WaitOne(3000))
                    {
                        InfoMessage(new Info("Device state [0] achieved", Info.Mode.Event));

                        SendData("TEAInitRec" + "\t" + $"CNR{DateTime.Now.ToString("yyMMddHHmmss")}" + "\n");  // client new record init request                    
                        Thread.Sleep(10);
                        SendData("TEAisRecording" + "\n");  // client recording status request
                        Thread.Sleep(10);

                        /// Wait for the device state 1 device state 2 device state 3 device state 3 device state 4 device state 3 device state 4 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state 5 device state
                        if (deviceStateWaitHandle[1].WaitOne(3000))
                        {
                            InfoMessage(new Info("Device state [1] achieved", Info.Mode.Event));

                            SendData("TEAListDevices" + "\n");   // list devices

                            /// Wait for the device state to be achieved.
                            if (deviceStateWaitHandle[2].WaitOne(3000))
                            {
                                InfoMessage(new Info("Device state [2] achieved", Info.Mode.Event));

                                /// Connect to the TEASetMotionServer.
                                if (sensorCount > 0)
                                {
                                    string sensorEnableCommand = "";   // -1 if no change , -2 to disable all
                                    /// Enable the sensor for the sensor.
                                    for (int i = 0; i < sensorCount; i++)
                                    {

                                        sensorEnableCommand += $"{Streamers[i].Id}";
                                        /// Enable the sensor enable command.
                                        if (i < sensorCount - 1) sensorEnableCommand += ";";
                                    }
                                    sensorEnableCommand = "-1";// TODO: Decide if we allow user to select or we enable all sensors
                                    SendData("TEASetTServer" + "\t" + "1" + "\t" + sensorEnableCommand + "\n");   // 1: Binary mode + sensorEnableCommand for enable/disable
                                    Thread.Sleep(10);
                                    SendData("TEASetMotionServer" + "\t" + "-1" + "\t" + "-1" + "\t" + "-1" + "\t" + "-1" + "\n");
                                    /// Wait for device state 3 achieved and return true if the device state 3 achieved.
                                    if (deviceStateWaitHandle[3].WaitOne(3000))
                                    {
                                        InfoMessage(new Info("Device state [3] achieved", Info.Mode.Event));

                                        foreach (var streamer in Streamers)
                                        {
                                            /// Connect to the server if the streamer is connected to the server
                                            if (streamer.Id > -1 && streamer.Port > 0)
                                            {
                                                streamer.InfoMessageReceived += SubInfoMessageReceived;
                                                streamer.Name = Name + "-" + streamer.Name;
                                                streamer.InitOutlet();
                                                connected &= streamer.ConnectToServer(Ip);
                                            }
                                        }

                                        return connected;
                                    }
                                    else
                                    {
                                        InfoMessage(new Info("Response to TEASetMotionServer timed out.", Info.Mode.CriticalError));
                                    }
                                }

                            }
                            else
                            {
                                InfoMessage(new Info("Response to TEAListDevices timed out.", Info.Mode.CriticalError));
                            }
                        }
                        else
                        {
                            InfoMessage(new Info("Response to TEAInitRec timed out.", Info.Mode.CriticalError));
                        }
                    }
                    else
                    {
                        InfoMessage(new Info("Response to TEAPINGSRV timed out.", Info.Mode.CriticalError));
                    }
                }
                return false;

            }

            /// <summary>
            /// Called when a sub info message is received. This is the message handler for the Info message
            /// </summary>
            /// <param name="sender">The sender of the message</param>
            /// <param name="info">The info that was</param>
            private void SubInfoMessageReceived(object sender, Info info)
            {
                InfoMessage(info);
            }

            /// <summary>
            /// Receives asy data from ASI. This is called when data is received from the ASI
            /// </summary>
            /// <param name="sender">The sender of the data</param>
            /// <param name="buffer">The buffer containing the data that was received from</param>
            private void AsyDataReceived(object sender, byte[] buffer)
            {
                string s = Encoding.Unicode.GetString(buffer);
                InfoMessage(new Info(s, Info.Mode.Event));

                /// Set up the wait handle for the device state.
                if (s.StartsWith("TEARPINGSRV"))
                {
                    deviceStateWaitHandle[0].Set();
                }
                /// Returns a wait handle for the device state.
                else if (s.StartsWith("TEARecording"))
                {
                    string[] ss = s.Split(new char[] { '\t' });
                    /// Set the device state to the wait handle.
                    if (ss[1] == "-1")
                    {
                        deviceStateWaitHandle[1].Set();
                    }
                }
                /// This method is used to set up the state of the device state.
                else if (s.StartsWith("TEADevices"))
                {
                    string[] ss = s.Split(new char[] { '\t' });

                    sensorCount = Int32.Parse(ss[1]);
                    serverMode = Int32.Parse(ss[2]);

                    /// This method will parse the sensor data from the sensor data.
                    if (sensorCount > 0)
                    {
                        /// This method will parse the sensor data from the sensor data.
                        for (int i = 0; i < sensorCount; i++)
                        {
                            string[] sss = ss[i + 3].Split(new char[] { ';' });
                            string[] ssss = sss[5].Split(new char[] { ',' });

                            foreach (var streamer in Streamers)
                            {
                                /// The streamer is the streamer type.
                                if (streamer.Type == sss[2])
                                {
                                    streamer.Id = Int32.Parse(sss[0]);
                                    streamer.Name = sss[1];
                                    streamer.NbChannel = Int32.Parse(sss[3]);
                                    streamer.SRate = Double.Parse(sss[4], CultureInfo.InvariantCulture);
                                    streamer.Port = Int32.Parse(sss[6]);
                                    /// Set the unit of each channel in the streamer.
                                    for (int j = 0; j < streamer.NbChannel; j++)
                                    {
                                        streamer.Channels[j].Unit = ssss[j];
                                    }
                                }
                            }

                        }
                    }

                    deviceStateWaitHandle[2].Set();
                }
                /// This method is called when the device is started.
                else if (s.StartsWith("TEAMotionServerStatus"))
                {
                    string[] ss = s.Split(new char[] { '\t' });
                    var port = Int32.Parse(ss[1]);
                    var outType = Int32.Parse(ss[2]);  //0: orientation, 1: position
                    var sRate = motionServRefreshRates[Int32.Parse(ss[3])];
                    var enabled = (Int32.Parse(ss[4]) == 1);
                    /// Set the streamers to the default values.
                    if (enabled)
                    {
                        foreach (var streamer in Streamers)
                        {
                            /// Set the streamer s state to the state of the stream.
                            if (outType == 0 && streamer.Type == "Orientation")
                            {
                                streamer.Id = 1000;
                                streamer.Name = "Motion";
                                streamer.NbChannel = 60;
                                streamer.SRate = sRate;
                                streamer.Port = port;

                            }
                            /// Set the streamer s state to the state of the streamer.
                            else if (outType == 1 && streamer.Type == "Position")
                            {
                                streamer.Id = 1001;
                                streamer.Name = "Motion";
                                streamer.NbChannel = 90;
                                streamer.SRate = sRate;
                                streamer.Port = port;
                            }

                        }
                    }

                    deviceStateWaitHandle[3].Set();
                }
            }

            /// <summary>
            /// Sends data to Asychoacoustic Speech. This is a blocking call.
            /// </summary>
            /// <param name="data">The data to send to Asychoic Speech</param>
            public void SendData(string data)
            {
                /// Send data to the client.
                if (asyClient.client != null && asyClient.client.Connected)
                {
                    asyClient.SendData(data, Encoding.Unicode);
                }
            }

            /// <summary>
            /// Saves the recorded data to CSV files. This is done by iterating through each streamer
            /// </summary>
            public void SaveRecordedData()
            {
                var directory = Directory.GetCurrentDirectory() + @"\Logs";
                /// Create a directory if it doesn t exist.
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                string filenames = "";
                foreach (var streamer in Streamers)
                {
                    /// Write the CSV file to the directory
                    if (streamer.RecordedBytes > 0)
                    {
                        var filename = streamer.Name + "-" + streamer.Type + ".csv";
                        filenames += filename;
                        File.WriteAllText(directory + @"\" + filename, streamer.Csv);
                    }
                }
                /// Creates the data log logs for filenames.
                if (filenames != "")
                {
                    InfoMessage(new Info($"Data log Logs\\{filenames} successfully created.", Info.Mode.Event));
                }                
            } 

        }
    }
}