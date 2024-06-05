using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Libfmax;

namespace CaptivClient
{
    public partial class MainForm : Form
    {
        internal class TEAStreamer : Streamer
        {
            [JsonIgnore]
            public int Id { get; set; }
            [JsonIgnore]
            public int Port { get; set; }
            private AsyTcpClient asyClient;
            private int bufferOffset;
            private int bufferIndex;
            private byte[] buffer;

            public TEAStreamer(string name,
                                    string type,
                                    int nbChannel,
                                    ChannelFormat chFormat,
                                    double sRate,
                                    List<ChannelInfo> channels,
                                    HardwareInfo hardware,
                                    SyncInfo sync)
                                    : base(name, type, nbChannel, chFormat, sRate, channels, hardware, sync) { }

            /// <summary>
            /// Closes the connection to Asterisk server. This is called when server disconnects or is shutting down
            /// </summary>
            public void CloseServerConnection()
            {
                asyClient.Close();
            }

            /// <summary>
            /// Connects to the Asychronous Server. This is the first step in the connection process. It will try to connect to the server and if it fails it will return false
            /// </summary>
            /// <param name="ipAddress">The IP Address of the server</param>
            /// <returns>True if the connection was successful</returns>
            public bool ConnectToServer(string ipAddress)
            {
                asyClient = new AsyTcpClient(ipAddress, Port, token, 10000);
                asyClient.InfoMessageReceived += AsyInfoMessageReceived;
                asyClient.DataReceived += AsyDataReceived;

                // TODO: No need for this large buffer; reuse a buffer size of 1000 instead: 
                var bufferSize = (int)(8 * 60 * 60 * (SRate > 0 ? SRate : 1) * 8 * (NbChannel + 1));  // buffer for 8h of recording  

                buffer = new byte[bufferSize];

                var connected = asyClient.Connect();
                /// Start the asy client if connected.
                if (connected)
                {
                    asyClient.Start();
                }
                return connected;
            }

            /// <summary>
            /// Receives information about the Asy. It is called when an Info message is received. The message is passed to the InfoMessage constructor so we can add it to the message queue
            /// </summary>
            /// <param name="sender">The object that sent the info message</param>
            /// <param name="info">The info that was received from the Asy</param>
            private void AsyInfoMessageReceived(object sender, Info info)
            {
                InfoMessage(new Info($"{Name}:{Type}, " + info.msg, info.mode));
            }

            /// <summary>
            /// Receives data from AsyBrainz and converts it to Sample. This is called by Audacity to process the data
            /// </summary>
            /// <param name="sender">The sender of the event</param>
            /// <param name="receivedData">The data received from the AsyB</param>
            private void AsyDataReceived(object sender, byte[] receivedData)
            {
                /// Check if the buffer is full.
                if (buffer.Length < bufferOffset + receivedData.Length)
                {
                    InfoMessage(new Info($"{Name}:{Type}, " + "Data buffer is full", Info.Mode.Event));
                    return;
                }

                Buffer.BlockCopy(receivedData, 0, buffer, bufferOffset, receivedData.Length);
                bufferOffset += receivedData.Length;

                double[] dataSample = new double[NbChannel];
                double timestamp = 0.0;

                /// Reads the data from the buffer.
                while (bufferOffset - bufferIndex >= 8 * (NbChannel + 1))
                {
                    /// Reads the channel data from the buffer.
                    for (int j = 0; j < NbChannel + 1; j++)
                    {
                        var chData = BitConverter.ToDouble(buffer, bufferIndex);
                        /// Set the timestamp of the last sample.
                        if (j == 0)
                        {
                            timestamp = chData;
                        }
                        else
                        {
                            dataSample[j - 1] = chData;
                        }

                        bufferIndex = bufferIndex + 8;
                    }
                    
                    //TODO: need to do this hard coded if statement parametric later
                    /// This method is used to calculate the timestamp of the motion
                    if (Name.Contains("Motion") )
                    {
                        timestamp /= 1000.0;
                    }
                    NewSample(dataSample, timestamp);

                }

            }
        }
    }
}