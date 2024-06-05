using System;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Globalization;
using System.IO.Ports;
using Libfmax;

namespace ErgoPAKClient
{
    public partial class MainForm : Form
    {
        private class ErgoPAKConfigurator
        {
            #region classes and structs

            public class ErgoPAKStreamer : Streamer
            {
                [JsonIgnore]
                public bool SubscribeOK { get; set; }
                public ErgoPAKStreamer(string name,
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

            public string PortName { get; set; }
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
            public List<ErgoPAKStreamer> Streamers { get; set; }
            private SerialPort serialPort;
            private const int BUFFER_SIZE = 1024;
            private int bufferIndex;
            private byte[] buffer = new byte[BUFFER_SIZE]; // receive buffer. 
            private byte[] data = new byte[10];

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
                serialPort.Close();
            }

            public bool ConnectToInlet(CancellationToken token)
            {
                // Create a new SerialPort object with default settings.
                serialPort = new SerialPort();
                serialPort.PortName = PortName;
                serialPort.BaudRate = 9600;
                serialPort.Parity = Parity.None;
                serialPort.DataBits = 8;
                serialPort.StopBits = StopBits.One;
                serialPort.Handshake = Handshake.None;

                serialPort.Open();
                serialPort.DataReceived += new SerialDataReceivedEventHandler(SerialDataReceived);

                if (serialPort.IsOpen)
                {
                    Streamers[0].InitOutlet();
                }
                return serialPort.IsOpen;

            }

            private void SerialDataReceived(
                                object sender,
                                SerialDataReceivedEventArgs e)
            {
                SerialPort sp = (SerialPort)sender;
                bufferIndex += sp.Read(buffer, bufferIndex, sp.BytesToRead);

                int i = 0;
                while (i <= bufferIndex && buffer[i] >= 0)
                {
                    if (i >= 1 && buffer[i] == 0x7C && buffer[i - 1] == 0xFF)
                    {
                        for (int j = 0; j < 10 && i - j - 1 > 0; j++)
                        {
                            data[9 - j] = buffer[i - j - 2];
                        }
                        Buffer.BlockCopy(buffer, i, buffer, 0, bufferIndex);
                        bufferIndex -= i;
                        i = -1;
                    }
                    i++;
                }

                // LSL streamed data sample                    
                double[] sample = new double[8];
                double offset = 72;
                double coeff = 0.6675;
                for (int j = 1; j < 5; j++)
                {
                    sample[j - 1] = (data[j] - offset) * coeff;
                }
                for (int j = 6; j < 10; j++)
                {
                    sample[j - 2] = (data[j] - offset) * coeff;
                }
                
                Streamers[0].NewSample(sample);                
                Streamers[0].SubscribeOK = true;
            }

            
            public void SendData(string data)
            {
                if (serialPort.IsOpen)
                {
                    serialPort.WriteLine(data);
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