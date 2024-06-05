using System;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Globalization;
using Libfmax;

namespace EquivitalClient
{
    public partial class MainForm : Form
    {
        private class EQConfigurator
        {
            #region classes and structs

            public class E4Device
            {
                public string Name { get; set; }
                [JsonIgnore]
                public bool Active { get; set; }
                public List<Streamer> Streamers { get; set; }
            }

            #endregion

            #region variables

            public string Name { get; set; }
            public string Ip { get; set; }
            public int Port { get; set; }
            public delegate void InfoMessageDelegate(object sender, Info info);
            public event InfoMessageDelegate InfoMessageReceived;
            public double RecordedKBytes
            {
                get
                {
                    double recordedKBytes = 0;
                    foreach (var device in Devices)
                    {
                        foreach (var stream in device.Streamers)
                        {
                            recordedKBytes += stream.RecordedBytes / 1024.0;
                        }

                    }
                    return recordedKBytes;
                }
            }
            public List<E4Device> Devices { get; set; }
            private AsyTcpListener asyListener;
            private const int BufferSize = 1024;
            private int bufferIndex;
            private byte[] buffer = new byte[BufferSize]; // receive buffer. 
            private byte[] zeros = new byte[BufferSize]; // to reinit buffer to zero.     
            /// <summary>
            /// State transitions: 
            /// <para> Wait to receive data from attached sensors </para>
            /// </summary>
            private AutoResetEvent[] deviceStateWaitHandle = new AutoResetEvent[1];
            private bool checkActive = false;
            private bool streamActive = false;

            #endregion


            protected virtual void InfoMessage(Info info)
            {
                InfoMessageReceived?.Invoke(this, info);
            }

            public void CloseInlet()
            {
                asyListener.Close();
            }

            public bool ConnectToInlet(CancellationToken token)
            {
                foreach (var device in Devices)
                {
                    device.Active = false;
                }

                asyListener = new AsyTcpListener(Ip, Port, token, 10000);
                asyListener.InfoMessageReceived += SubInfoMessageReceived;
                asyListener.DataReceived += AsyDataReceived;

                for (int i = 0; i < deviceStateWaitHandle.Length; i++)
                {
                    deviceStateWaitHandle[i] = new AutoResetEvent(false);
                }

                Task.Run(() => asyListener.Start(), token);

                InfoMessage(new Info("Waiting for receiving configuration.", Info.Mode.CriticalEvent));

                //below line is required to avoid rubbish data being dumped at the beginning
                Thread.Sleep(2000);  //somehow Task.Delay(2000) did not work for this...

                checkActive = true;
                if (deviceStateWaitHandle[0].WaitOne(2000))
                {
                    InfoMessage(new Info("Device state [0] achieved", Info.Mode.Event));
                    foreach (var device in Devices)
                    {
                        if (device.Active)
                        {
                            foreach (var streamer in device.Streamers)
                            {
                                streamer.InfoMessageReceived += SubInfoMessageReceived;
                                streamer.Name = Name + "-" + device.Name;
                                streamer.InitOutlet();
                            }
                            InfoMessage(new Info($"Device {device.Name} connected.", Info.Mode.CriticalEvent));
                        }
                        else
                        {
                            InfoMessage(new Info($"Device {device.Name} not connected.", Info.Mode.CriticalEvent));
                        }
                    }
                    streamActive = true;
                    return true;
                }
                else
                {
                    InfoMessage(new Info("Retrieve configuration process timed out.", Info.Mode.CriticalError));
                }
                return false;
            }

            private void SubInfoMessageReceived(object sender, Info info)
            {
                InfoMessage(info);
            }

            private void AsyDataReceived(object sender, byte[] receivedData)
            {
                string s = "";

                int i = 0;
                while (i < receivedData.Length)
                {
                    buffer[bufferIndex] = receivedData[i];

                    if (receivedData[i] == ';')
                    {
                        s = Encoding.ASCII.GetString(buffer, 0, bufferIndex + 1);
                        //Console.WriteLine(DateTime.Now.ToString() + " - " + s);

                        var ss = s.Split(',');

                        var timestamp = ss[2].Split('=');
                        string dtPattern = "dd:MM:yyyy:HH:mm:ss:fff";

                        var dt = DateTime.ParseExact(timestamp[1], dtPattern, new System.Globalization.CultureInfo("en-US"), System.Globalization.DateTimeStyles.None);

                        if (checkActive)
                        {
                            foreach (var device in Devices)
                            {
                                if (device.Name == ss[0].Split('=')[1])
                                {
                                    device.Active = true;
                                    foreach (var streamer in device.Streamers)
                                    {
                                        string[] sample = new string[streamer.NbChannel];
                                        for (int j = 0; j < streamer.Channels.Count; j++)
                                        {
                                            var channel = streamer.Channels[j];
                                            for (int k = 3; k < ss.Length; k++)
                                            {
                                                if (channel.Label == ss[k].Split('=')[0])
                                                {
                                                    sample[j] = ss[k].Split('=')[1];
                                                }
                                            }
                                        }
                                        if (streamActive)
                                        {
                                            if (streamer.ChFormat != Streamer.ChannelFormat.String)
                                            {
                                                double[] dataSample = new double[streamer.NbChannel];
                                                for (int k = 0; k < streamer.NbChannel; k++)
                                                {
                                                    dataSample[k] = Double.Parse(sample[k], System.Globalization.CultureInfo.InvariantCulture);
                                                }
                                                streamer.NewSample(dataSample, Streamer.ConvertDT2LSL(dt));
                                            }
                                            else
                                            {
                                                streamer.NewSample(sample, Streamer.ConvertDT2LSL(dt));
                                            }
                                        }
                                    }
                                }
                            }
                            deviceStateWaitHandle[0].Set();
                        }

                        Buffer.BlockCopy(zeros, 0, buffer, 0, buffer.Length);
                        bufferIndex = 0;

                    }
                    else
                    {
                        bufferIndex++;  //TODO: Display error msg when bufferIndex exceeds bufferLength?
                    }

                    i++;
                }
            }

            public void SendData(string data)
            {
                if (asyListener.client != null && asyListener.client.Connected)
                {
                    asyListener.SendData(data);
                }
            }

            public void SaveRecordedData()
            {
                var directory = Directory.GetCurrentDirectory() + @"\Logs";
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                string filenames = "";
                foreach (var device in Devices)
                {
                    foreach (var streamer in device.Streamers)
                    {
                        if (streamer.RecordedBytes > 0)
                        {
                            var filename = streamer.Name + "-" + streamer.Type + ".csv";
                            filenames += filename;
                            File.WriteAllText(directory + @"\" + filename, streamer.Csv);
                        }
                    }
                }
                if (filenames != "")
                {
                    InfoMessage(new Info($"Data log Logs\\{filenames} successfully created.", Info.Mode.Event));
                }
            }
        }
    }
}