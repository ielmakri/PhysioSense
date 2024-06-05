using System;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Globalization;
using Libfmax;

namespace EmpaticaClient
{
    public partial class MainForm : Form
    {
        private class E4Configurator
        {
            #region classes and structs

            public class E4Streamer : Streamer
            {
                public string InletName { get; set; }
                [JsonIgnore]
                public bool SubscribeOK { get; set; }
                public E4Streamer(string name,
                                        string type,
                                        int nbChannel,
                                        ChannelFormat chFormat,
                                        double sRate,
                                        List<ChannelInfo> channels,
                                        HardwareInfo hardware,
                                        SyncInfo sync)
                                        : base(name, type, nbChannel, chFormat, sRate, channels, hardware, sync) { }
            }
            #endregion

            #region  variables

            public string Name { get; set; }
            public string DeviceName { get; set; }
            public string APIKey { get; set; }
            public string Path { get; set; }
            public string Ip { get; set; }
            public int Port { get; set; }
            public event InfoMessageDelegate InfoMessageReceived;
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
            public List<E4Streamer> Streamers { get; set; }
            private AsyTcpClient asyClient;
            private const int BUFFER_SIZE = 1024;
            private int bufferIndex;
            private byte[] buffer = new byte[BUFFER_SIZE]; // receive buffer. 
            private byte[] zeros = new byte[BUFFER_SIZE]; // to reinit buffer to zero.     
            private string[] tagSample = new string[] { "T" };
            /// <summary>
            /// State transitions: 
            /// <para> device-connect -> [0]  </para>
            /// </summary>
            private AutoResetEvent[] deviceStateWaitHandle = new AutoResetEvent[1];

            #endregion


            protected virtual void InfoMessage(Info info)
            {
                InfoMessageReceived?.Invoke(this, info);
            }

            public void CloseInlet()
            {
                asyClient.Close();
            }

            public bool ConnectToInlet(CancellationToken token)
            {
                asyClient = new AsyTcpClient(Ip, Port, token);
                asyClient.InfoMessageReceived += SubInfoMessageReceived;
                asyClient.DataReceived += AsyDataReceived;

                for (int i = 0; i < deviceStateWaitHandle.Length; i++)
                {
                    deviceStateWaitHandle[i] = new AutoResetEvent(false);
                }

                var connected = asyClient.Connect();
                if (connected)
                {
                    asyClient.Start();

                    SendData("device_connect " + DeviceName + "\r\n");

                    if (deviceStateWaitHandle[0].WaitOne(3000))
                    {
                        InfoMessage(new Info("Device state [0] achieved", Info.Mode.Event));

                        var subscribeOK = false;
                        foreach (var streamer in Streamers)
                        {
                            SendData("device_subscribe " + streamer.Type + " ON" + "\r\n");
                            if (streamer.WaitHandle.WaitOne(1000))
                            {
                                InfoMessage(new Info("Subscribe to " + streamer.Type + "...OK.", Info.Mode.Event));
                                streamer.InfoMessageReceived += SubInfoMessageReceived;
                                streamer.Name = Name + "-" + DeviceName;
                                streamer.InitOutlet();
                                streamer.SubscribeOK = true;
                                subscribeOK = true;
                            }
                            else
                            {
                                InfoMessage(new Info("Subscribe to " + streamer.Type + "...NOK.", Info.Mode.CriticalError));
                            }
                        }

                        if (subscribeOK)
                        {
                            InfoMessage(new Info("Streaming data.", Info.Mode.CriticalEvent));
                            return true;
                        }
                    }
                    else
                    {
                        InfoMessage(new Info("Response to device_connect timed out.", Info.Mode.CriticalError));
                    }
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

                    if (receivedData[i] == '\n')
                    {
                        s = Encoding.ASCII.GetString(buffer, 0, bufferIndex + 1);
                        Buffer.BlockCopy(zeros, 0, buffer, 0, buffer.Length);
                        bufferIndex = 0;

                        string[] ss = s.Split(' ');

                        if (s == "R device_connect OK\n")   //device connection ok reply 
                        {
                            deviceStateWaitHandle[0].Set();
                        }
                        else if (ss.Length == 4 && ss[0] == "R" && ss[1] == "device_subscribe"
                                    && ss[3] == "OK\n")
                        {
                            foreach (var streamer in Streamers)
                            {
                                if (streamer.Type == ss[2])
                                {
                                    streamer.WaitHandle.Set();
                                }
                            }
                        }
                        else
                        {
                            foreach (var streamer in Streamers)
                            {
                                if (streamer.InletName == ss[0]
                                && ss.Length == streamer.NbChannel + 2)
                                {
                                    if (streamer.Type == "tag")  //sending tag
                                    {
                                        streamer.NewSample(tagSample, Streamer.ConvertUnixEpoch2LSL(Double.Parse(ss[1], System.Globalization.CultureInfo.InvariantCulture)));
                                    }
                                    else
                                    {
                                        double[] dataSample = new double[streamer.NbChannel];
                                        for (int k = 0; k < streamer.NbChannel; k++)
                                        {
                                            dataSample[k] = Double.Parse(ss[k + 2], System.Globalization.CultureInfo.InvariantCulture);
                                        }
                                        string[] strDataValues = new string[streamer.NbChannel];
                                        Array.Copy(ss, 2, strDataValues, 0, streamer.NbChannel);


                                        //string unlocalizedString = ss[1].Replace(",", ".");
                                        //streamer.NewSample(dataSample, Streamer.ConvertUnixEpoch2LSL(Double.Parse(unlocalizedString, System.Globalization.CultureInfo.InvariantCulture)));


                                        streamer.NewSample(dataSample, Streamer.ConvertUnixEpoch2LSL(Double.Parse(ss[1], System.Globalization.CultureInfo.CurrentCulture)));
                                    }
                                    break;
                                }
                            }

                        }
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
                if (asyClient.client != null && asyClient.client.Connected)
                {
                    asyClient.SendData(data, Encoding.UTF8);
                }
            }

            public void SaveRecordedData()
            {
                var directory = Directory.GetCurrentDirectory() + @"\Logs";
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                string filenames = "";
                foreach (var streamer in Streamers)
                {
                    if (streamer.SubscribeOK && streamer.RecordedBytes > 0)
                    {
                        var filename = streamer.Name + "-" + streamer.Type + ".csv";
                        filenames += filename;
                        File.WriteAllText(directory + @"\" + filename, streamer.Csv);
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