using Libfmax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualBasic.Logging;
using System.Globalization;

namespace XsensClient
{
    public partial class MainForm : Form
    {
        private class XsensConfigurator
        {
            #region classes and structs

            public class XsensStreamer : Streamer
            {
                public string InletName { get; set; }
                [JsonIgnore]
                public bool SubscribeOK { get; set; }
                public XsensStreamer(string name,
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
            public List<XsensStreamer> Streamers { get; set; }
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

            //UDP variables to connect to Xsens
            private const int UdpPort = 9763; //port number has to be the same as the one in the OpenGo App
            private UdpClient UdpClient;

            //String variables to store Xsens data
            const int nb_segments = 23;
            const int nb_joints = 28;
            const int bytes_per_seg = 28; //28 = euler, 32 = unity
            const int bytes_per_joint = 20;
            const int nb_dataActRecogn = 8;
            String[] data_string;
            byte[,] data_segments = new byte[nb_segments,bytes_per_seg];
            byte[,] data_joints = new byte[nb_joints, bytes_per_joint];
            float[][] final_data = new float[4][];
            #endregion


            protected virtual void InfoMessage(Info info)
            {
                InfoMessageReceived?.Invoke(this, info);
            }

            public void CloseInlet()
            {
            }

            private void SubInfoMessageReceived(object sender, Info info)
            {
                InfoMessage(info);
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

            private void initData()
            {
                final_data[0] = new float[nb_segments * 3];
                final_data[1] = new float[nb_segments * 3];
                final_data[2] = new float[nb_joints * 3];
                final_data[3] = new float[nb_dataActRecogn];
            }

            private bool create_final_data(byte[] data)
            {
                if (data[4] == '0' && data[5] == '1') // '5' = 'unity' protocol, '2' = normal 'pos/quaternion' protocol, '1' = 'euler" protocol
                {
                    for (int i = 0; i < nb_segments; i++)
                    {
                        for (int j = 0; j < bytes_per_seg; j++) // bytes_per_seg: 28 = euler, 32 = Unity
                            data_segments[i, j] = data[24 + (i * bytes_per_seg) + j];

                        for (int s = 0; s < nb_segments; s++)
                        {
                            final_data[0][3 * s] = 1 / 100f * BitConverter.ToSingle(new byte[] { data_segments[s, 15], data_segments[s, 14], data_segments[s, 13], data_segments[s, 12] }, 0); // transX
                            final_data[0][3 * s + 1] = 1 / 100.0f * BitConverter.ToSingle(new byte[] { data_segments[s, 7], data_segments[s, 6], data_segments[s, 5], data_segments[s, 4] }, 0); // transY
                            final_data[0][3 * s + 2] = 1 / 100f * BitConverter.ToSingle(new byte[] { data_segments[s, 11], data_segments[s, 10], data_segments[s, 9], data_segments[s, 8] }, 0);// transZ
                            final_data[1][3 * s] = BitConverter.ToSingle(new byte[] { data_segments[s, 19], data_segments[s, 18], data_segments[s, 17], data_segments[s, 16] }, 0); // * (180 / 3.1415f); // rotX
                            final_data[1][3 * s + 1] = BitConverter.ToSingle(new byte[] { data_segments[s, 23], data_segments[s, 22], data_segments[s, 21], data_segments[s, 20] }, 0); //* (180 / 3.1415f); // rotY
                            final_data[1][3 * s + 2] = BitConverter.ToSingle(new byte[] { data_segments[s, 27], data_segments[s, 26], data_segments[s, 25], data_segments[s, 24] }, 0); //* (180 / 3.1415f); // rotZ

                            //System.Diagnostics.Debug.WriteLine(final_data[0][0]);
                        }
                    }

                    final_data[3][0] = final_data[0][2]; // Pelvis z.1

                    return true;
                }

                if (data[4] == '2' && data[5] == '0') // '5' = 'unity' protocol, '2' = normal 'pos/quaternion' protocol, '1' = 'euler" protocol, '20' = 14 = Joint angle protocol
                {
                    for (int i = 0; i < nb_joints; i++)
                    {
                        for (int j = 0; j < bytes_per_joint; j++) // 20 bytes per joint
                            data_joints[i, j] = data[24 + (i * bytes_per_joint) + j];

                        for (int s = 0; s < nb_joints; s++)
                        {
                            final_data[2][3 * s] = BitConverter.ToSingle(new byte[] { data_joints[s, 11], data_joints[s, 10], data_joints[s, 9], data_joints[s, 8] }, 0);// * (180 / 3.1415f); // rotX
                            final_data[2][3 * s + 1] = BitConverter.ToSingle(new byte[] { data_joints[s, 15], data_joints[s, 14], data_joints[s, 13], data_joints[s, 12] }, 0); //* (180 / 3.1415f); // rotY
                            final_data[2][3 * s + 2] = BitConverter.ToSingle(new byte[] { data_joints[s, 19], data_joints[s, 18], data_joints[s, 17], data_joints[s, 16] }, 0); //* (180 / 3.1415f); // rotZ
                        }
                    }

                    final_data[3][1] = final_data[2][3 * 25 + 1]; // Pelvis T8 x
                    final_data[3][2] = final_data[2][3 * 25]; // Pelvis T8 y
                    final_data[3][3] = final_data[2][3 * 25 + 2]; // Pelvis T8 z
                    final_data[3][4] = final_data[2][3 * 11]; // Left shoulder y
                    final_data[3][5] = final_data[2][3 * 11 + 2]; // Left shoulder z
                    final_data[3][6] = final_data[2][3 * 7]; // Right shoulder y
                    final_data[3][7] = final_data[2][3 * 7 + 2]; // Right shoulder z

                    //System.Diagnostics.Debug.WriteLine(final_data[2][1]);

                    return true;
                }

                else
                    return false;


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
                    
                    create_final_data(data);

                    string[] strInletNames = { "Human_pos", "Human_rot", "Human_joint_ang", "Human_ergo_features" };

                    foreach (var streamer in Streamers)
                    {
                       //System.Diagnostics.Debug.WriteLine(streamer.InletName);
                       if (strInletNames.Any(streamer.InletName.Contains))
                       {
                        //string[] ss = final_data[Array.FindIndex(strInletNames, x => x == streamer.InletName)];
                        double[] dataSample = new double[streamer.NbChannel]; //streamer.NbChannel = 50
                        /*for (int k = 0; k < streamer.NbChannel; k++) //streamer.NbChannel = 50
                        {
                            dataSample[k] = Double.Parse(ss[k]);
                            //System.Diagnostics.Debug.WriteLine(dataSample[0]);
                        }*/
                        string[] strDataValues = new string[streamer.NbChannel];
                        //Array.Copy(ss, 2, strDataValues, 0, streamer.NbChannel);

                        //if(streamer.InletName == "Pelvis_rotation")
                        int index = Array.FindIndex(strInletNames, x => x == streamer.InletName);
                        //if(index==0)
                        //    System.Diagnostics.Debug.WriteLine(streamer.InletName + " " + final_data[Array.FindIndex(strInletNames, x => x == streamer.InletName)][2]);
                        //System.Diagnostics.Debug.WriteLine(Array.FindIndex(strInletNames, x => x == streamer.InletName));

                        float[] newSample;

                        if (index == 0)
                            newSample = final_data[0];
                        else if(index == 1)
                            newSample = final_data[1];
                        else if(index == 2)
                            newSample = final_data[2];
                        else
                            newSample = final_data[3];

                        streamer.NewSample(newSample, 0);// Streamer.ConvertUnixEpoch2LSL((Double) DateTimeOffset.Now.ToUnixTimeSeconds()));

                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    return; // Connection closed
                }
            }

            public bool ConnectToInlet(CancellationToken token)
            {
                
                initData();
                UdpClient = new UdpClient(UdpPort);

                try
                {

                    UdpClient.BeginReceive(UdpDataReceived, null);

                    System.Diagnostics.Debug.WriteLine("UDP connection established");

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
                        streamer.InitOutlet(streamer.Type);
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


        }
    }
}