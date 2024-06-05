using Libfmax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using ViconDataStreamSDK.DotNET;


namespace ViconClient
{
    public partial class MainForm : Form
    {
        // Program options
        static bool TransmitMulticast = false;
        static bool EnableHapticTest = false;
        static bool bReadCentroids = false;
        static bool bReadRayData = false;
        static bool bReadGreyscaleData = false;
        static bool bReadVideoData = false;
        static bool bMarkerTrajIds = false;
        static bool bLightweightSegments = false;
        static bool bSubjectFilterApplied = false;

        private class ViconConfigurator
        {
            #region classes and structs

            public class ViconStreamer : Streamer
            {
                public string InletName { get; set; }
                [JsonIgnore]
                public bool SubscribeOK { get; set; }
                public ViconStreamer(string name,
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
            public List<ViconStreamer> Streamers { get; set; }
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

            private System.Timers.Timer myTimer;
            const int maxMarkersLSL = 64;
            const int maxPlates = 4;
            const int maxEMG = 16;
            float[][] final_data = new float[14][];
            string[] markers_names = new string[maxMarkersLSL];
            string[] EMG_names = new string[maxEMG];

            List<String> HapticOnList = new List<String>();
            static uint ClientBufferSize = 0;
            static string AxisMapping = "ZUp";
            List<String> FilteredSubjects = new List<string>();

            int Counter = 1;

            static string HostName = "localhost:801";

            // Make a new client
            static ViconDataStreamSDK.DotNET.Client MyClient = new ViconDataStreamSDK.DotNET.Client();

            #endregion


            protected virtual void InfoMessage(Info info)
            {
                InfoMessageReceived?.Invoke(this, info);
            }

            public void CloseInlet()
            {
                // call stop (although, it should already be stopped)
                //daq.Stop();

                // place plate into reset
                //daq.MeasureOff();

                disconnectVicon();
            }

            public bool ConnectToInlet(CancellationToken token)
            {
                SetTimer();

                initData();

                try
                {
                    connectVicon();

                    var subscribeOK = false;

                    foreach (var streamer in Streamers)
                    {
                        InfoMessage(new Info("Subscribe to " + streamer.Type + "...OK.", Info.Mode.Event));
                        streamer.InfoMessageReceived += SubInfoMessageReceived;
                        streamer.Name = Name + "-" + DeviceName;
                        streamer.InitOutlet();
                        streamer.SubscribeOK = true;
                        subscribeOK = true;
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

            private void connectVicon()
            {
                // Connect to a server
                System.Diagnostics.Debug.Write("Connecting to {0} ...", HostName);

                while (!MyClient.IsConnected().Connected)
                {
                    // Direct connection
                    MyClient.Connect(HostName);

                    // Multicast connection
                    // MyClient.ConnectToMulticast( HostName, "224.0.0.0" );

                    System.Threading.Thread.Sleep(200);
                    System.Diagnostics.Debug.Write(".");
                }
                System.Diagnostics.Debug.WriteLine("");

                System.Diagnostics.Debug.WriteLine("Connection established");

                // Enable some different data types
                MyClient.EnableSegmentData();
                MyClient.EnableMarkerData();
                MyClient.EnableUnlabeledMarkerData();
                MyClient.EnableDeviceData();
                if (bReadCentroids)
                {
                    MyClient.EnableCentroidData();
                }
                if (bReadRayData)
                {
                    MyClient.EnableMarkerRayData();
                }
                if (bReadGreyscaleData)
                {
                    MyClient.EnableGreyscaleData();
                }
                if (bReadVideoData)
                {
                    MyClient.EnableVideoData();
                }
                if (bLightweightSegments)
                {
                    MyClient.EnableLightweightSegmentData();
                }
                System.Diagnostics.Debug.WriteLine("Segment Data Enabled: {0}", MyClient.IsSegmentDataEnabled().Enabled);
                System.Diagnostics.Debug.WriteLine("Lightweight Segment Data Enabled: {0}", MyClient.IsLightweightSegmentDataEnabled().Enabled);
                System.Diagnostics.Debug.WriteLine("Marker Data Enabled: {0}", MyClient.IsMarkerDataEnabled().Enabled);
                System.Diagnostics.Debug.WriteLine("Unlabeled Marker Data Enabled: {0}", MyClient.IsUnlabeledMarkerDataEnabled().Enabled);
                System.Diagnostics.Debug.WriteLine("Device Data Enabled: {0}", MyClient.IsDeviceDataEnabled().Enabled);
                System.Diagnostics.Debug.WriteLine("Centroid Data Enabled: {0}", MyClient.IsCentroidDataEnabled().Enabled);
                System.Diagnostics.Debug.WriteLine("Marker Ray Data Enabled: {0}", MyClient.IsMarkerRayDataEnabled().Enabled);
                System.Diagnostics.Debug.WriteLine("Greyscale Data Enabled: {0}", MyClient.IsGreyscaleDataEnabled().Enabled);
                System.Diagnostics.Debug.WriteLine("Video Data Enabled: {0}", MyClient.IsVideoDataEnabled().Enabled);
                System.Diagnostics.Debug.WriteLine("Debug Data Enabled: {0}", MyClient.IsDebugDataEnabled().Enabled);

                System.Diagnostics.Debug.WriteLine("");

                System.Diagnostics.Debug.WriteLine("Streaming data..");

                // Set the streaming mode
                MyClient.SetStreamMode(ViconDataStreamSDK.DotNET.StreamMode.ClientPull);
                // MyClient.SetStreamMode( ViconDataStreamSDK.DotNET.StreamMode.ClientPullPreFetch );
                // MyClient.SetStreamMode( ViconDataStreamSDK.DotNET.StreamMode.ServerPush );

                // Set the global up axis
                MyClient.SetAxisMapping(ViconDataStreamSDK.DotNET.Direction.Forward,
                                         ViconDataStreamSDK.DotNET.Direction.Left,
                                         ViconDataStreamSDK.DotNET.Direction.Up); // Z-up

                if (AxisMapping == "YUp")
                {
                    MyClient.SetAxisMapping(ViconDataStreamSDK.DotNET.Direction.Forward,
                                             ViconDataStreamSDK.DotNET.Direction.Up,
                                             ViconDataStreamSDK.DotNET.Direction.Right); // Y-up
                }
                else if (AxisMapping == "XUp")
                {
                    MyClient.SetAxisMapping(ViconDataStreamSDK.DotNET.Direction.Up,
                                             ViconDataStreamSDK.DotNET.Direction.Forward,
                                             ViconDataStreamSDK.DotNET.Direction.Left); // x-up
                }

                Output_GetAxisMapping _Output_GetAxisMapping = MyClient.GetAxisMapping();

                // Discover the version number
                Output_GetVersion _Output_GetVersion = MyClient.GetVersion();

                if (ClientBufferSize > 0)
                {
                    MyClient.SetBufferSize(ClientBufferSize);
                    Console.WriteLine("Setting client buffer size to " + ClientBufferSize);
                }

                if (TransmitMulticast)
                {
                    MyClient.StartTransmittingMulticast("localhost", "224.0.0.0");
                }

            }

            void disconnectVicon()
            {
                if (TransmitMulticast)
                {
                    MyClient.StopTransmittingMulticast();
                }

                MyClient.DisableSegmentData();
                MyClient.DisableMarkerData();
                MyClient.DisableUnlabeledMarkerData();
                MyClient.DisableDeviceData();
                if (bReadCentroids)
                {
                    MyClient.DisableCentroidData();
                }
                if (bReadRayData)
                {
                    MyClient.DisableMarkerRayData();
                }
                if (bReadGreyscaleData)
                {
                    MyClient.DisableGreyscaleData();
                }
                if (bReadVideoData)
                {
                    MyClient.DisableVideoData();
                }

                // Disconnect and dispose
                MyClient.Disconnect();
                MyClient = null;
            }

            private void SetTimer()
            {
                myTimer = new System.Timers.Timer(10); // Set the interval to 1 second
                myTimer.Elapsed += MyTimerCallback; // Set the callback function
                myTimer.AutoReset = true; // Make the timer repeat
                myTimer.Enabled = true; // Start the timer
            }

            private void MyTimerCallback(object source, ElapsedEventArgs e)
            {
                // Get a frame
                Console.Write("Waiting for new frame...");
                while (MyClient.GetFrame().Result != ViconDataStreamSDK.DotNET.Result.Success)
                {
                    System.Threading.Thread.Sleep(200);
                    Console.Write(".");
                }
                Console.WriteLine();

                if (!bSubjectFilterApplied)
                {
                    MyClient.ClearSubjectFilter();
                    foreach (String Subject in FilteredSubjects)
                    {
                        Output_AddToSubjectFilter Result = MyClient.AddToSubjectFilter(Subject);
                        bSubjectFilterApplied = bSubjectFilterApplied || Result.Result == ViconDataStreamSDK.DotNET.Result.Success;
                    }
                }

                // Get the frame number
                Output_GetFrameNumber _Output_GetFrameNumber = MyClient.GetFrameNumber();
                Console.WriteLine("Frame Number: {0}", _Output_GetFrameNumber.FrameNumber);
                
                Output_GetFrameRate _Output_GetFrameRate = MyClient.GetFrameRate();
                Console.WriteLine("Frame rate: {0}", _Output_GetFrameRate.FrameRateHz);


                for (uint FrameRateIndex = 0; FrameRateIndex < MyClient.GetFrameRateCount().Count; ++FrameRateIndex)
                {
                    string FrameRateName = MyClient.GetFrameRateName(FrameRateIndex).Name;
                    double FrameRateValue = MyClient.GetFrameRateValue(FrameRateName).Value;

                    Console.WriteLine("{0}: {1}Hz", FrameRateName, FrameRateValue);
                }
                Console.WriteLine();

                // Get the timecode
                Output_GetTimecode _Output_GetTimecode = MyClient.GetTimecode();

                Console.WriteLine();

                // Get the latency
                Console.WriteLine("Latency: {0}s", MyClient.GetLatencyTotal().Total);

                for (uint LatencySampleIndex = 0; LatencySampleIndex < MyClient.GetLatencySampleCount().Count; ++LatencySampleIndex)
                {
                    string SampleName = MyClient.GetLatencySampleName(LatencySampleIndex).Name;
                    double SampleValue = MyClient.GetLatencySampleValue(SampleName).Value;

                    Console.WriteLine("  {0} {1}s", SampleName, SampleValue);
                }
                Console.WriteLine();

                Output_GetHardwareFrameNumber _Output_GetHardwareFrameNumber = MyClient.GetHardwareFrameNumber();
                Console.WriteLine("Hardware Frame Number: {0} ", _Output_GetHardwareFrameNumber.HardwareFrameNumber);

                //Enable haptic devices
                if (EnableHapticTest == true)
                {
                    foreach (String HapticDevice in HapticOnList)
                    {
                        if (Counter % 2 == 0)
                        {
                            Output_SetApexDeviceFeedback Output = MyClient.SetApexDeviceFeedback(HapticDevice, true);
                            if (Output.Result == ViconDataStreamSDK.DotNET.Result.Success)
                            {
                                Console.WriteLine("Turn haptic feedback on for device: {0}\n", HapticDevice);
                            }
                            else if (Output.Result == ViconDataStreamSDK.DotNET.Result.InvalidDeviceName)
                            {
                                Console.WriteLine("Device doesn't exis: {0}\n", HapticDevice);
                            }
                        }
                        if (Counter % 20 == 0)
                        {
                            Output_SetApexDeviceFeedback Output = MyClient.SetApexDeviceFeedback(HapticDevice, false);

                            if (Output.Result == ViconDataStreamSDK.DotNET.Result.Success)
                            {
                                Console.WriteLine("Turn haptic feedback off for device: {0}\n", HapticDevice);
                            }
                        }
                    }
                }

                create_final_data();

                string[] strInletNames = { "Markers_position_x", "Markers_position_y", "Markers_position_z", "Markers_occluded", "Plate_force_x", "Plate_force_y", "Plate_force_z", "Plate_moment_x", "Plate_moment_y", "Plate_moment_z", "Plate_cop_x", "Plate_cop_y", "Plate_cop_z", "EMG"};

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
                        newSample = final_data[index];

                        //System.Diagnostics.Debug.WriteLine(final_data[10][0]);

                        if (streamer.InletName == "Markers_position_x" || streamer.InletName == "Markers_position_y" || streamer.InletName == "Markers_position_z" || streamer.InletName == "Markers_position_occl")
                        {
                            for (int k = 0; k < streamer.NbChannel; k++)
                            {
                                streamer.Channels[k].Label = markers_names[k];
                            }
                        }

                        if(streamer.InletName == "EMG")
                        {
                            for (int k = 0; k < streamer.NbChannel; k++)
                            {
                                streamer.Channels[k].Label = EMG_names[k];
                            }
                        }

                        //System.Diagnostics.Debug.WriteLine(newSample[0]);
                        //System.Diagnostics.Debug.WriteLine(DateTimeOffset.Now.ToUnixTimeSeconds());
                        if (streamer.InletName == "EMG")
                            System.Diagnostics.Debug.WriteLine(newSample[0]);
                        streamer.NewSample(newSample, 0);// Streamer.ConvertUnixEpoch2LSL((Double) DateTimeOffset.Now.ToUnixTimeSeconds()));
                                                            //counter++;
                    }
                }
            }

            private object Adapt(TimecodeStandard standard)
            {
                throw new NotImplementedException();
            }

            private void create_final_data()
            {
                uint SubjectCount = MyClient.GetSubjectCount().SubjectCount;
                Console.WriteLine("Subjects ({0}):", SubjectCount);
                for (uint SubjectIndex = 0; SubjectIndex < SubjectCount; ++SubjectIndex)
                {
                    Console.WriteLine("  Subject #{0}", SubjectIndex);

                    // Get the subject name
                    string SubjectName = MyClient.GetSubjectName(SubjectIndex).SubjectName;
                    Console.WriteLine("    Name: {0}", SubjectName);

                    // Get the root segment
                    string RootSegment = MyClient.GetSubjectRootSegmentName(SubjectName).SegmentName;
                    Console.WriteLine("    Root Segment: {0}", RootSegment);

                    // Count the number of segments
                    uint SegmentCount = MyClient.GetSegmentCount(SubjectName).SegmentCount;
                    Console.WriteLine("    Segments ({0}):", SegmentCount);
                    for (uint SegmentIndex = 0; SegmentIndex < SegmentCount; ++SegmentIndex)
                    {
                        Console.WriteLine("      Segment #{0}", SegmentIndex);

                        // Get the segment name
                        string SegmentName = MyClient.GetSegmentName(SubjectName, SegmentIndex).SegmentName;
                        Console.WriteLine("        Name: {0}", SegmentName);

                        // Get the segment parent
                        string SegmentParentName = MyClient.GetSegmentParentName(SubjectName, SegmentName).SegmentName;
                        Console.WriteLine("        Parent: {0}", SegmentParentName);

                        // Get the segment's children
                        uint ChildCount = MyClient.GetSegmentChildCount(SubjectName, SegmentName).SegmentCount;
                        Console.WriteLine("     Children ({0}):", ChildCount);
                        for (uint ChildIndex = 0; ChildIndex < ChildCount; ++ChildIndex)
                        {
                            string ChildName = MyClient.GetSegmentChildName(SubjectName, SegmentName, ChildIndex).SegmentName;
                            Console.WriteLine("       {0}", ChildName);
                        }

                        // Get the static segment translation
                        Output_GetSegmentStaticTranslation _Output_GetSegmentStaticTranslation =
                          MyClient.GetSegmentStaticTranslation(SubjectName, SegmentName);
                        Console.WriteLine("        Static Translation: ({0},{1},{2})",
                                           _Output_GetSegmentStaticTranslation.Translation[0],
                                           _Output_GetSegmentStaticTranslation.Translation[1],
                                           _Output_GetSegmentStaticTranslation.Translation[2]);

                        // Get the static segment rotation in helical co-ordinates
                        Output_GetSegmentStaticRotationHelical _Output_GetSegmentStaticRotationHelical =
                          MyClient.GetSegmentStaticRotationHelical(SubjectName, SegmentName);
                        Console.WriteLine("        Static Rotation Helical: ({0},{1},{2})",
                                           _Output_GetSegmentStaticRotationHelical.Rotation[0],
                                           _Output_GetSegmentStaticRotationHelical.Rotation[1],
                                           _Output_GetSegmentStaticRotationHelical.Rotation[2]);

                        // Get the static segment rotation as a matrix
                        Output_GetSegmentStaticRotationMatrix _Output_GetSegmentStaticRotationMatrix =
                          MyClient.GetSegmentStaticRotationMatrix(SubjectName, SegmentName);
                        Console.WriteLine("        Static Rotation Matrix: ({0},{1},{2},{3},{4},{5},{6},{7},{8})",
                                           _Output_GetSegmentStaticRotationMatrix.Rotation[0],
                                           _Output_GetSegmentStaticRotationMatrix.Rotation[1],
                                           _Output_GetSegmentStaticRotationMatrix.Rotation[2],
                                           _Output_GetSegmentStaticRotationMatrix.Rotation[3],
                                           _Output_GetSegmentStaticRotationMatrix.Rotation[4],
                                           _Output_GetSegmentStaticRotationMatrix.Rotation[5],
                                           _Output_GetSegmentStaticRotationMatrix.Rotation[6],
                                           _Output_GetSegmentStaticRotationMatrix.Rotation[7],
                                           _Output_GetSegmentStaticRotationMatrix.Rotation[8]);

                        // Get the static segment rotation in quaternion co-ordinates
                        Output_GetSegmentStaticRotationQuaternion _Output_GetSegmentStaticRotationQuaternion =
                          MyClient.GetSegmentStaticRotationQuaternion(SubjectName, SegmentName);
                        Console.WriteLine("        Static Rotation Quaternion: ({0},{1},{2},{3})",
                                           _Output_GetSegmentStaticRotationQuaternion.Rotation[0],
                                           _Output_GetSegmentStaticRotationQuaternion.Rotation[1],
                                           _Output_GetSegmentStaticRotationQuaternion.Rotation[2],
                                           _Output_GetSegmentStaticRotationQuaternion.Rotation[3]);

                        // Get the static segment rotation in EulerXYZ co-ordinates
                        Output_GetSegmentStaticRotationEulerXYZ _Output_GetSegmentStaticRotationEulerXYZ =
                          MyClient.GetSegmentStaticRotationEulerXYZ(SubjectName, SegmentName);
                        Console.WriteLine("        Static Rotation EulerXYZ: ({0},{1},{2})",
                                           _Output_GetSegmentStaticRotationEulerXYZ.Rotation[0],
                                           _Output_GetSegmentStaticRotationEulerXYZ.Rotation[1],
                                           _Output_GetSegmentStaticRotationEulerXYZ.Rotation[2]);

                        // Get the global segment translation
                        Output_GetSegmentGlobalTranslation _Output_GetSegmentGlobalTranslation =
                          MyClient.GetSegmentGlobalTranslation(SubjectName, SegmentName);
                        Console.WriteLine("        Global Translation: ({0},{1},{2}) {3}",
                                           _Output_GetSegmentGlobalTranslation.Translation[0],
                                           _Output_GetSegmentGlobalTranslation.Translation[1],
                                           _Output_GetSegmentGlobalTranslation.Translation[2],
                                           _Output_GetSegmentGlobalTranslation.Occluded);

                        // Get the global segment rotation in helical co-ordinates
                        Output_GetSegmentGlobalRotationHelical _Output_GetSegmentGlobalRotationHelical =
                          MyClient.GetSegmentGlobalRotationHelical(SubjectName, SegmentName);
                        Console.WriteLine("        Global Rotation Helical: ({0},{1},{2}) {3}",
                                           _Output_GetSegmentGlobalRotationHelical.Rotation[0],
                                           _Output_GetSegmentGlobalRotationHelical.Rotation[1],
                                           _Output_GetSegmentGlobalRotationHelical.Rotation[2],
                                           _Output_GetSegmentGlobalRotationHelical.Occluded);

                        // Get the global segment rotation as a matrix
                        Output_GetSegmentGlobalRotationMatrix _Output_GetSegmentGlobalRotationMatrix =
                          MyClient.GetSegmentGlobalRotationMatrix(SubjectName, SegmentName);
                        Console.WriteLine("        Global Rotation Matrix: ({0},{1},{2},{3},{4},{5},{6},{7},{8}) {9}",
                                           _Output_GetSegmentGlobalRotationMatrix.Rotation[0],
                                           _Output_GetSegmentGlobalRotationMatrix.Rotation[1],
                                           _Output_GetSegmentGlobalRotationMatrix.Rotation[2],
                                           _Output_GetSegmentGlobalRotationMatrix.Rotation[3],
                                           _Output_GetSegmentGlobalRotationMatrix.Rotation[4],
                                           _Output_GetSegmentGlobalRotationMatrix.Rotation[5],
                                           _Output_GetSegmentGlobalRotationMatrix.Rotation[6],
                                           _Output_GetSegmentGlobalRotationMatrix.Rotation[7],
                                           _Output_GetSegmentGlobalRotationMatrix.Rotation[8],
                                           _Output_GetSegmentGlobalRotationMatrix.Occluded);

                        // Get the global segment rotation in quaternion co-ordinates
                        Output_GetSegmentGlobalRotationQuaternion _Output_GetSegmentGlobalRotationQuaternion =
                          MyClient.GetSegmentGlobalRotationQuaternion(SubjectName, SegmentName);
                        Console.WriteLine("        Global Rotation Quaternion: ({0},{1},{2},{3}) {4}",
                                           _Output_GetSegmentGlobalRotationQuaternion.Rotation[0],
                                           _Output_GetSegmentGlobalRotationQuaternion.Rotation[1],
                                           _Output_GetSegmentGlobalRotationQuaternion.Rotation[2],
                                           _Output_GetSegmentGlobalRotationQuaternion.Rotation[3],
                                           _Output_GetSegmentGlobalRotationQuaternion.Occluded);

                        // Get the global segment rotation in EulerXYZ co-ordinates
                        Output_GetSegmentGlobalRotationEulerXYZ _Output_GetSegmentGlobalRotationEulerXYZ =
                          MyClient.GetSegmentGlobalRotationEulerXYZ(SubjectName, SegmentName);
                        Console.WriteLine("        Global Rotation EulerXYZ: ({0},{1},{2}) {3}",
                                           _Output_GetSegmentGlobalRotationEulerXYZ.Rotation[0],
                                           _Output_GetSegmentGlobalRotationEulerXYZ.Rotation[1],
                                           _Output_GetSegmentGlobalRotationEulerXYZ.Rotation[2],
                                           _Output_GetSegmentGlobalRotationEulerXYZ.Occluded);

                        // Get the local segment translation
                        Output_GetSegmentLocalTranslation _Output_GetSegmentLocalTranslation =
                          MyClient.GetSegmentLocalTranslation(SubjectName, SegmentName);
                        Console.WriteLine("        Local Translation: ({0},{1},{2}) {3}",
                                           _Output_GetSegmentLocalTranslation.Translation[0],
                                           _Output_GetSegmentLocalTranslation.Translation[1],
                                           _Output_GetSegmentLocalTranslation.Translation[2],
                                           _Output_GetSegmentLocalTranslation.Occluded);

                        // Get the local segment rotation in helical co-ordinates
                        Output_GetSegmentLocalRotationHelical _Output_GetSegmentLocalRotationHelical =
                          MyClient.GetSegmentLocalRotationHelical(SubjectName, SegmentName);
                        Console.WriteLine("        Local Rotation Helical: ({0},{1},{2}) {3}",
                                           _Output_GetSegmentLocalRotationHelical.Rotation[0],
                                           _Output_GetSegmentLocalRotationHelical.Rotation[1],
                                           _Output_GetSegmentLocalRotationHelical.Rotation[2],
                                           _Output_GetSegmentLocalRotationHelical.Occluded);

                        // Get the local segment rotation as a matrix
                        Output_GetSegmentLocalRotationMatrix _Output_GetSegmentLocalRotationMatrix =
                          MyClient.GetSegmentLocalRotationMatrix(SubjectName, SegmentName);
                        Console.WriteLine("        Local Rotation Matrix: ({0},{1},{2},{3},{4},{5},{6},{7},{8}) {9}",
                                           _Output_GetSegmentLocalRotationMatrix.Rotation[0],
                                           _Output_GetSegmentLocalRotationMatrix.Rotation[1],
                                           _Output_GetSegmentLocalRotationMatrix.Rotation[2],
                                           _Output_GetSegmentLocalRotationMatrix.Rotation[3],
                                           _Output_GetSegmentLocalRotationMatrix.Rotation[4],
                                           _Output_GetSegmentLocalRotationMatrix.Rotation[5],
                                           _Output_GetSegmentLocalRotationMatrix.Rotation[6],
                                           _Output_GetSegmentLocalRotationMatrix.Rotation[7],
                                           _Output_GetSegmentLocalRotationMatrix.Rotation[8],
                                           _Output_GetSegmentLocalRotationMatrix.Occluded);

                        // Get the local segment rotation in quaternion co-ordinates
                        Output_GetSegmentLocalRotationQuaternion _Output_GetSegmentLocalRotationQuaternion =
                          MyClient.GetSegmentLocalRotationQuaternion(SubjectName, SegmentName);
                        Console.WriteLine("        Local Rotation Quaternion: ({0},{1},{2},{3}) {4}",
                                           _Output_GetSegmentLocalRotationQuaternion.Rotation[0],
                                           _Output_GetSegmentLocalRotationQuaternion.Rotation[1],
                                           _Output_GetSegmentLocalRotationQuaternion.Rotation[2],
                                           _Output_GetSegmentLocalRotationQuaternion.Rotation[3],
                                           _Output_GetSegmentLocalRotationQuaternion.Occluded);

                        // Get the local segment rotation in EulerXYZ co-ordinates
                        Output_GetSegmentLocalRotationEulerXYZ _Output_GetSegmentLocalRotationEulerXYZ =
                          MyClient.GetSegmentLocalRotationEulerXYZ(SubjectName, SegmentName);
                        Console.WriteLine("        Local Rotation EulerXYZ: ({0},{1},{2}) {3}",
                                           _Output_GetSegmentLocalRotationEulerXYZ.Rotation[0],
                                           _Output_GetSegmentLocalRotationEulerXYZ.Rotation[1],
                                           _Output_GetSegmentLocalRotationEulerXYZ.Rotation[2],
                                           _Output_GetSegmentLocalRotationEulerXYZ.Occluded);
                    }

                    // Get the quality of the subject (object) if supported
                    Output_GetObjectQuality _Output_GetObjectQuality = MyClient.GetObjectQuality(SubjectName);
                    if (_Output_GetObjectQuality.Result == Result.Success)
                    {
                        double Quality = _Output_GetObjectQuality.Quality;
                        Console.WriteLine("    Quality: {0}", Quality);
                    }

                    // Count the number of markers
                    uint MarkerCount = MyClient.GetMarkerCount(SubjectName).MarkerCount;
                    Console.WriteLine("    Markers ({0}):", MarkerCount);
                    for (uint MarkerIndex = 0; MarkerIndex < MarkerCount; ++MarkerIndex)
                    {
                        // Get the marker name
                        string MarkerName = MyClient.GetMarkerName(SubjectName, MarkerIndex).MarkerName;

                        // Get the marker parent
                        string MarkerParentName = MyClient.GetMarkerParentName(SubjectName, MarkerName).SegmentName;

                        // Get the global marker translation
                        Output_GetMarkerGlobalTranslation _Output_GetMarkerGlobalTranslation =
                          MyClient.GetMarkerGlobalTranslation(SubjectName, MarkerName);

                        Console.WriteLine("      Marker #{0}: {1} ({2}, {3}, {4}) {5}",
                                           MarkerIndex,
                                           MarkerName,
                                           _Output_GetMarkerGlobalTranslation.Translation[0],
                                           _Output_GetMarkerGlobalTranslation.Translation[1],
                                           _Output_GetMarkerGlobalTranslation.Translation[2],
                                           _Output_GetMarkerGlobalTranslation.Occluded);

                        if (MarkerIndex < maxMarkersLSL)
                        {
                            final_data[0][MarkerIndex] = (float)_Output_GetMarkerGlobalTranslation.Translation[0];
                            final_data[1][MarkerIndex] = (float)_Output_GetMarkerGlobalTranslation.Translation[1];
                            final_data[2][MarkerIndex] = (float)_Output_GetMarkerGlobalTranslation.Translation[2];
                            final_data[3][MarkerIndex] = Convert.ToSingle(_Output_GetMarkerGlobalTranslation.Occluded);
                            markers_names[MarkerIndex] = MarkerName;
                        }


                        if (bReadRayData)
                        {
                            Output_GetMarkerRayContributionCount _Output_GetMarkerRayContributionCount
                              = MyClient.GetMarkerRayContributionCount(SubjectName, MarkerName);
                            if (_Output_GetMarkerRayContributionCount.Result == Result.Success)
                            {
                                String ContributionsOutput = "      Contributed to by: ";
                                for (uint ContributionIndex = 0; ContributionIndex < _Output_GetMarkerRayContributionCount.RayContributionsCount; ++ContributionIndex)
                                {
                                    Output_GetMarkerRayContribution _Output_GetMarkerRayContribution =
                                      MyClient.GetMarkerRayContribution(SubjectName, MarkerName, ContributionIndex);
                                    ContributionsOutput += "ID:" + _Output_GetMarkerRayContribution.CameraID + " Index:" + _Output_GetMarkerRayContribution.CentroidIndex + " ";
                                }
                                Console.WriteLine(ContributionsOutput);
                            }

                        }

                    }
                }

                // Count the number of force plates
                uint ForcePlateCount = MyClient.GetForcePlateCount().ForcePlateCount;

                for (uint ForcePlateIndex = 0; ForcePlateIndex < ForcePlateCount; ++ForcePlateIndex)
                {

                    uint ForcePlateSubsamples = MyClient.GetForcePlateSubsamples(ForcePlateIndex).ForcePlateSubsamples;

                    if (ForcePlateIndex < maxPlates)
                    {
                        final_data[4][ForcePlateIndex] = (float) MyClient.GetGlobalForceVector(ForcePlateIndex, ForcePlateSubsamples - 1).ForceVector[0];
                        final_data[5][ForcePlateIndex] = (float) MyClient.GetGlobalForceVector(ForcePlateIndex, ForcePlateSubsamples - 1).ForceVector[1];
                        final_data[6][ForcePlateIndex] = (float) MyClient.GetGlobalForceVector(ForcePlateIndex, ForcePlateSubsamples - 1).ForceVector[2];
                        final_data[7][ForcePlateIndex] = (float) MyClient.GetGlobalMomentVector(ForcePlateIndex, ForcePlateSubsamples - 1).MomentVector[0];
                        final_data[8][ForcePlateIndex] = (float) MyClient.GetGlobalMomentVector(ForcePlateIndex, ForcePlateSubsamples - 1).MomentVector[1];
                        final_data[9][ForcePlateIndex] = (float) MyClient.GetGlobalMomentVector(ForcePlateIndex, ForcePlateSubsamples - 1).MomentVector[2];
                        final_data[10][ForcePlateIndex] = (float) MyClient.GetGlobalCentreOfPressure(ForcePlateIndex, ForcePlateSubsamples - 1).CentreOfPressure[0] * 1000;
                        final_data[11][ForcePlateIndex] = (float) MyClient.GetGlobalCentreOfPressure(ForcePlateIndex, ForcePlateSubsamples - 1).CentreOfPressure[1] * 1000;
                        final_data[12][ForcePlateIndex] = (float) MyClient.GetGlobalCentreOfPressure(ForcePlateIndex, ForcePlateSubsamples - 1).CentreOfPressure[2] * 1000;
                        //System.Diagnostics.Debug.WriteLine(final_data[10][ForcePlateIndex]);

                    }
                }

                // Count the number of devices
                uint DeviceCount = MyClient.GetDeviceCount().DeviceCount;
                //System.Diagnostics.Debug.WriteLine("  Devices ({0}):", DeviceCount);
                for (uint DeviceIndex = 0; DeviceIndex < DeviceCount; ++DeviceIndex)
                {
                    Console.WriteLine("    Device #{0}:", DeviceIndex);

                    // Get the device name and type
                    Output_GetDeviceName _Output_GetDeviceName = MyClient.GetDeviceName(DeviceIndex);
                    //System.Diagnostics.Debug.WriteLine("      Name: {0}", _Output_GetDeviceName.DeviceName);
                    //System.Diagnostics.Debug.WriteLine("      Type: {0}", _Output_GetDeviceName.DeviceType);

                    if (_Output_GetDeviceName.DeviceName.ToString() == "EMG")
                    {
                        // Count the number of device outputs
                        uint DeviceOutputCount = MyClient.GetDeviceOutputCount(_Output_GetDeviceName.DeviceName).DeviceOutputCount;
                        //System.Diagnostics.Debug.WriteLine("      Device Outputs ({0}):", DeviceOutputCount);
                        for (uint DeviceOutputIndex = 0; DeviceOutputIndex < DeviceOutputCount; ++DeviceOutputIndex)
                        {
                            // Get the device output name and unit
                            Output_GetDeviceOutputComponentName _Output_GetDeviceOutputComponentName =
                              MyClient.GetDeviceOutputComponentName(_Output_GetDeviceName.DeviceName, DeviceOutputIndex);

                            // Get the number of subsamples for this output.
                            uint DeviceOutputSubsamples =
                              MyClient.GetDeviceOutputSubsamples(_Output_GetDeviceName.DeviceName,
                                                                  _Output_GetDeviceOutputComponentName.DeviceOutputName,
                                                                  _Output_GetDeviceOutputComponentName.DeviceOutputComponentName).DeviceOutputSubsamples;

                            Console.WriteLine("      Device Output #{0}:", DeviceOutputIndex);

                            Console.WriteLine("      Samples ({0}):", DeviceOutputSubsamples);

                            // Display all the subsamples.
                            for (uint DeviceOutputSubsample = 0; DeviceOutputSubsample < DeviceOutputSubsamples; ++DeviceOutputSubsample)
                            {
                                Console.WriteLine("        Sample #{0}:", DeviceOutputSubsample);

                                // Get the device output value
                                Output_GetDeviceOutputValue _Output_GetDeviceOutputValue =
                                  MyClient.GetDeviceOutputValue(_Output_GetDeviceName.DeviceName,
                                                                 _Output_GetDeviceOutputComponentName.DeviceOutputName,
                                                                 _Output_GetDeviceOutputComponentName.DeviceOutputComponentName,
                                                                 DeviceOutputSubsample);

                                /*System.Diagnostics.Debug.WriteLine("          '{0}' {1} {2}",
                                                   _Output_GetDeviceOutputComponentName.DeviceOutputComponentName,
                                                   _Output_GetDeviceOutputValue.Value,
                                                   _Output_GetDeviceOutputValue.Occluded);*/
                            }

                            if (DeviceOutputIndex < maxEMG)
                            {
                                final_data[13][DeviceOutputIndex] = (float)MyClient.GetDeviceOutputValue(_Output_GetDeviceName.DeviceName,
                                                                 _Output_GetDeviceOutputComponentName.DeviceOutputName,
                                                                 _Output_GetDeviceOutputComponentName.DeviceOutputComponentName,
                                                                 DeviceOutputSubsamples-1).Value * 1000000;
                                EMG_names[DeviceOutputIndex] = _Output_GetDeviceOutputComponentName.DeviceOutputComponentName;
                            }
                        }


                    }
                }

            }

            private void initData()
            {
                final_data[0] = new float[maxMarkersLSL]; //labeled_marker_pos_x
                final_data[1] = new float[maxMarkersLSL]; //labeled_marker_pos_y
                final_data[2] = new float[maxMarkersLSL]; //labeled_marker_pos_z
                final_data[3] = new float[maxMarkersLSL]; //labeled_marker_occluded

                
                final_data[4] = new float[maxPlates]; //plate_force_x
                final_data[5] = new float[maxPlates]; //plate_force_y
                final_data[6] = new float[maxPlates]; //plate_force_z
                final_data[7] = new float[maxPlates]; //plate_moment_x
                final_data[8] = new float[maxPlates]; //plate_moment_y
                final_data[9] = new float[maxPlates]; //plate_moment_z
                final_data[10] = new float[maxPlates]; //plate_cop_x
                final_data[11] = new float[maxPlates]; //plate_cop_y
                final_data[12] = new float[maxPlates]; //plate_cop_z

                final_data[13] = new float[maxEMG]; //EMG

            }
        }
    }
}