// Info for codecs //https://www.fourcc.org/codecs/

using DirectShowLib;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Libfmax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace CameraCapture
{
    public partial class CameraCapture : Form
    {
        #region Constants and Variables

        private VideoCapture camera = null; // Camera
        private VideoDevice[] camInfoList; // List containing all the camera available
        private Mat frame;
        private VideoWriter writer;
        private int frameCounter;
        private const int FrameWidth = 640;
        private const int FrameHeight = 480;
        private const int Fps = 30;
        private Stopwatch watch;
        private double elapsedMillisecondsMem;

        private Streamer videoStream;

        #endregion

        public CameraCapture()
        {
            InitializeComponent();

            DsDevice[] systemCameras = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);

            camInfoList = new VideoDevice[systemCameras.Length];

            /// Create video devices for all cameras
            for (int i = 0; i < systemCameras.Length; i++)
            {
                camInfoList[i] = new VideoDevice(i, systemCameras[i].Name, systemCameras[i].ClassID); // Fill web cam array
                cboCamSelect.Items.Add(systemCameras[i].Name);
            }

            /// Set the selected device to 0
            if (cboCamSelect.Items.Count > 0)
            {
                cboCamSelect.SelectedIndex = 0; // Set the selected device the default
                btStart.Enabled = true; // Enable the start
            }


            watch = new Stopwatch();

        }


        /// <summary>
        /// Called when a frame is received. This is where we get the frame from CvStream and process it
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="arg">The instance containing the event data. This is null</param>
        private void ProcessFrame(object sender, EventArgs arg)
        {
            /// This method is called by the camera.
            if (camera != null && camera.Ptr != IntPtr.Zero)
            {
                camera.Retrieve(frame, 0);
                Emgu.CV.Image<Bgr, Byte> img = frame.ToImage<Bgr, Byte>();

                /// Start the watch if elapsedMillisecondsMem is 0.
                if (elapsedMillisecondsMem == 0)
                {
                    watch.Start();
                }

                var cycle = watch.ElapsedMilliseconds - elapsedMillisecondsMem;

                CvInvoke.PutText(img, $"Local time:{DateTime.Now.ToString("HH:mm:ss.fff")}, cycle:{cycle}ms, fps:{((cycle <= 0) ? 0 : 1000.0 / cycle):0.#}Hz (set:{Fps}Hz)",
                    new Point(10, 20), FontFace.HersheySimplex, 0.5, new Bgr(Color.Blue).MCvScalar);

                /// Recording is recording or not.
                if (btRecord.Text == "Recording")
                {
                    /// Restart the watch if frameCounter is 0.
                    if (frameCounter == 0)
                    {
                        watch.Restart();
                    }

                    videoStream.NewSample(new int[] { frameCounter }, watch.ElapsedMilliseconds / 1000.0);

                    CvInvoke.PutText(img, $"Recording-> frame no:{frameCounter}, elapsed:{(watch.ElapsedMilliseconds / 1000.0):0.###}sec",
                        new Point(10, 40), FontFace.HersheySimplex, 0.5, new Bgr(Color.Red).MCvScalar);

                    iboCapture.Image = img;
                    writer?.Write(img);

                    frameCounter++;

                }
                else
                {
                    iboCapture.Image = img;
                }

                elapsedMillisecondsMem = watch.ElapsedMilliseconds;
            }
        }

        /// <summary>
        /// Sets up the camera and stream all data to the video. This is called from VideoCapture. cpp
        /// </summary>
        /// <param name="camIndex">Index of the camera to stream data to</param>
        private void SetupCapture(int camIndex)
        {
            /// Disposes the camera. This method is called by the camera when the camera is not yet connected.
            if (camera != null)
            {
                camera.Dispose();
            }
            try
            {
                CvInvoke.UseOpenCL = false;

                camera = new VideoCapture(camIndex, VideoCapture.API.DShow,
                    new Tuple<CapProp, int>(CapProp.FrameWidth, FrameWidth),
                    new Tuple<CapProp, int>(CapProp.FrameHeight, FrameHeight),
                    new Tuple<CapProp, int>(CapProp.Fps, Fps) //TODO: Get FPS from camera deafult instead, now default 30Hz assumed
                    );

                List<Streamer.ChannelInfo> channels = new List<Streamer.ChannelInfo>();
                Streamer.ChannelInfo chInfo = new Streamer.ChannelInfo()
                {
                    Label = "F#"
                };
                channels.Add(chInfo);
                Streamer.SyncInfo sync = new Streamer.SyncInfo()
                {
                    TimeSource = Streamer.TimeSource.Mod0,
                    OffsetMean = 0.0,
                    CanDropSamples = true,
                    Ipo = Streamer.InletProcessingOptions.Clocksync,
                    Opo = Streamer.OutletProcessingOptions.None
                };

                videoStream = new Streamer(name: "Video",
                                             type: $"{camInfoList[camIndex].Device_Name}_{cboCamSelect.SelectedIndex}",
                                             nbChannel: 1,
                                             chFormat: Streamer.ChannelFormat.Int64,
                                             sRate: 30,
                                             channels: channels,
                                             hardware: new Streamer.HardwareInfo(),
                                             sync: sync);
                videoStream.InitOutlet();

                camera.ImageGrabbed += ProcessFrame;

                frame = new Mat();
            }
            catch (NullReferenceException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// Handles the Click event of the btStart control. Starts or stops the capture. Disposes the writer and sets the text to " Stop "
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The instance containing the event data. Not used</param>
        private void btStart_Click(object sender, EventArgs e)
        {

            /// Starts the camera and stops the camera.
            if (btStart.Text == "Start")
            {
                WinApi.TimeBeginPeriod(1);

                SetupCapture(cboCamSelect.SelectedIndex);

                camera.Start();

                btStart.Text = "Stop";
            }
            else
            {
                WinApi.TimeEndPeriod(1);

                watch.Stop();
                camera.Stop();
                writer?.Dispose();
                writer = null;
                btStart.Enabled = false;
                btRecord.Enabled = false;
            }
        }

        /// <summary>
        /// Handles the Click event of the btRecord control. Attempts to record the video. If there is no camera or the user hasn't started recording the video will be stopped.
        /// </summary>
        /// <param name="sender">The source of the event. Cannot be null.</param>
        /// <param name="e">The instance containing the event data. Cannot be null</param>
        private void btRecord_Click(object sender, EventArgs e)
        {

            /// Stops the camera if it is not currently running.
            if (camera == null || btStart.Text != "Stop")
            {
                watch.Stop();
                return;
            }

            btRecord.Enabled = false;

            var fourcc = (int)camera.Get(CapProp.FourCC);
            var fps = (int)camera.Get(CapProp.Fps);
            var frameSize = new Size((int)camera.Get(CapProp.FrameWidth), (int)camera.Get(CapProp.FrameHeight));
            writer = new VideoWriter($"Video_{camInfoList[cboCamSelect.SelectedIndex].Device_Name}_{cboCamSelect.SelectedIndex}_Record.mp4",
                                        fourcc, fps, frameSize, true);
            btRecord.Text = "Recording";

        }

        /// <summary>
        /// Release resources used by the class. This is called when the class is no longer needed and should be re - used
        /// </summary>
        private void ReleaseData()
        {
            WinApi.TimeEndPeriod(1);

            /// Disposes the camera. This method is called by the camera when the camera is not yet connected.
            if (camera != null)
            {
                camera.Dispose();
            }

            /// Disposes the writer. Disposes the writer.
            if (writer != null && writer.IsOpened)
            {
                writer.Dispose();
            }
        }

    }

}
