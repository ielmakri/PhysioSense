using Libfmax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows.Forms;
using System.Globalization;

namespace MoticonClient
{
    public partial class MainForm : Form
    {
        private class MoticonConfigurator
        {
            #region classes and structs

            public class MoticonStreamer : Streamer
            {
                public string InletName { get; set; }
                [JsonIgnore]
                public bool SubscribeOK { get; set; }
                public MoticonStreamer(string name,
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
            public List<MoticonStreamer> Streamers { get; set; }
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

            //UDP variables to connect to Moticon
            private const int UdpPort = 8888; //port number has to be the same as the one in the OpenGo App
            private UdpClient UdpClient;

            //String variables to store Moticon data
            String[] data_string;
            String[][] final_data = new string[10][]; // 10 = number of streamers (data types)
            #endregion


            protected virtual void InfoMessage(Info info)
            {
                InfoMessageReceived?.Invoke(this, info);
            }

            public void CloseInlet()
            {
                //asyClient.Close();
                UdpClient?.Close();
            }

            /*public bool ConnectToInlet(CancellationToken token)
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

            }*/

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

                                        streamer.NewSample(dataSample, Streamer.ConvertUnixEpoch2LSL(Double.Parse(ss[1], System.Globalization.CultureInfo.InvariantCulture)));
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
                        System.Diagnostics.Debug.WriteLine("b");
                    }
                }
                if (filenames != "")
                {
                    InfoMessage(new Info($"Data log Logs\\{filenames} successfully created.", Info.Mode.Event));
                }
            }

            public bool ConnectToInlet(CancellationToken token)
            {
                asyClient = new AsyTcpClient(Ip, Port, token);
                asyClient.InfoMessageReceived += SubInfoMessageReceived;
                asyClient.DataReceived += AsyDataReceived;

                initData();
                UdpClient = new UdpClient(UdpPort);

                try
                {
                    UdpClient.BeginReceive(UdpDataReceived, null);

                    System.Diagnostics.Debug.WriteLine("UDP connection established");

                    SendData("device_connect " + DeviceName + "\r\n");

                    InfoMessage(new Info("Device state [0] achieved", Info.Mode.Event));

                    var subscribeOK = false;

                    foreach (var streamer in Streamers)
                    {
                        //SendData("device_subscribe " + streamer.Type + " ON" + "\r\n");
                        //if (streamer.WaitHandle.WaitOne(1000))
                        //{
                        InfoMessage(new Info("Subscribe to " + streamer.Type + "...OK.", Info.Mode.Event));
                        streamer.InfoMessageReceived += SubInfoMessageReceived;
                        streamer.Name = Name + "-" + DeviceName;
                        streamer.InitOutlet();
                        streamer.SubscribeOK = true;
                        subscribeOK = true;

                        /*}
                        else
                        {
                            InfoMessage(new Info("Subscribe to " + streamer.Type + "...NOK.", Info.Mode.CriticalError));
                        }*/
                    }

                    if (subscribeOK)
                    {
                        InfoMessage(new Info("Streaming data.", Info.Mode.CriticalEvent));

                        return true;
                    }
                    else
                    {
                        InfoMessage(new Info("Response to device_connect timed out.", Info.Mode.CriticalError));
                    }

                    return false;
                }

                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());

                    return false;
                }
            }

            private void initData()
            {
                final_data[0] = new string[3]; //left_acceleration
                final_data[1] = new string[3]; //left_angular
                final_data[2] = new string[2]; //left_cop
                final_data[3] = new string[16]; //left_pressure
                final_data[4] = new string[1]; //left_total_force
                final_data[5] = new string[3]; //right_acceleration
                final_data[6] = new string[3]; //right_angular
                final_data[7] = new string[2]; //right_cop
                final_data[8] = new string[16]; //right_pressure
                final_data[9] = new string[1]; //right_total_force
            }

            private void UdpDataReceived(IAsyncResult ar)
            {
                IPEndPoint ip = new IPEndPoint(IPAddress.Any, UdpPort);
                byte[] data;
                try
                {
                    data = UdpClient.EndReceive(ar, ref ip);

                    if (data.Length == 0)
                        return; // No more to receive
                    UdpClient.BeginReceive(UdpDataReceived, null);

                    //System.Diagnostics.Debug.WriteLine(data[0]+" "+ data[1] + " " + data[2] + " " + data[3] + " " + data[4] + " " + data[5] + " " + data[6] + " " + data[7] + " " + data[8] + " " + data[9] + " " + data[10] + " " + data[11] + " " + data[12] + " " + data[13] + " " + data[14] + " " + data[15] + " " + data[16] + " " + data[17] + " " + data[18] + " " + data[19] + " " + data[20] + " " + data[21] + " " + data[22] + " " + data[23] + " " + data[24] + " " + data[25] + " " + data[26] + " " + data[27]);
                    data_string = Encoding.ASCII.GetString(data).Split(' ');

                    //left_acceleration
                    Array.Copy(data_string, 1, final_data[0], 0, 3);

                    //left_angular
                    Array.Copy(data_string, 4, final_data[1], 0, 3);

                    //left_cop
                    Array.Copy(data_string, 7, final_data[2], 0, 2);

                    //left_pressure
                    Array.Copy(data_string, 9, final_data[3], 0, 16);

                    //left_total_force
                    Array.Copy(data_string, 25, final_data[4], 0, 1);

                    //right_acceleration
                    Array.Copy(data_string, 26, final_data[5], 0, 3);

                    //right_angular
                    Array.Copy(data_string, 29, final_data[6], 0, 3);

                    //right_cop
                    Array.Copy(data_string, 32, final_data[7], 0, 2);

                    //right_pressure
                    Array.Copy(data_string, 34, final_data[8], 0, 16);

                    //right_total_force
                    Array.Copy(data_string, 50, final_data[9], 0, 1);

                    string[] strInletNames = { "Left_acc", "Left_ang", "Left_cop", "Left_pressure", "Left_total_force", "Right_acc", "Right_ang", "Right_cop", "Right_pressure", "Right_total_force" };
                    foreach (var streamer in Streamers)
                    {
                        //System.Diagnostics.Debug.WriteLine(streamer.InletName);
                        if (strInletNames.Any(streamer.InletName.Contains))
                        {
                            string[] ss = final_data[Array.FindIndex(strInletNames, x => x == streamer.InletName)];
                            double[] dataSample = new double[streamer.NbChannel]; //streamer.NbChannel = 50
                            for (int k = 0; k < streamer.NbChannel; k++) //streamer.NbChannel = 50
                            {
                                dataSample[k] = Double.Parse(ss[k]);
                                //System.Diagnostics.Debug.WriteLine(dataSample[0]);
                            }
                            string[] strDataValues = new string[streamer.NbChannel];
                            //Array.Copy(ss, 2, strDataValues, 0, streamer.NbChannel);

                            streamer.NewSample(dataSample, 0);// Streamer.ConvertUnixEpoch2LSL(Double.Parse(ss[1], CultureInfo.InvariantCulture)));
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    return; // Connection closed
                }
            }

        }
    }
}