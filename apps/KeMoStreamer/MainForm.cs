using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using Libfmax;
using LSL;

namespace KeMoStreamer
{
    public partial class MainForm : Form
    {
        private const double SRATE = 100;
        private const int CHUNK_LEN = 2;
        private const int MAX_BUFFER = 3;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        public static WinApi.LowLevelKeyboardProc proc = HookCallback;
        public static IntPtr hookID = IntPtr.Zero;
        private static StreamOutlet outletKeyboard, outletMouse;
        private static bool linkedKeyboard, linkedMouse;

        public MainForm()
        {
            InitializeComponent();
        }

        ~MainForm()
        {
            linkedKeyboard = false;
            linkedMouse = false;
            if (outletKeyboard != null) outletKeyboard.Dispose();
            if (outletMouse != null) outletMouse.Dispose();
        }

        private void btLinkKeyboard_Click(object sender, EventArgs e)
        {
            if (btLinkKeyboard.Text == "Link Keyboard")
            {
                btLinkKeyboard.Text = "Unlink Keyboard";
                linkedKeyboard = true;
                if (outletKeyboard == null)
                {
                    // create stream info and outlet
                    StreamInfo info = new StreamInfo("Keyboard", "Markers", 1, 0, channel_format_t.cf_string);
                    XMLElement chns = info.desc().append_child("channels");                    
                    chns.append_child("channel")
                        .append_child_value("label", "key")
                        .append_child_value("unit", "")
                        .append_child_value("precision", "0");                    
                    outletKeyboard = new StreamOutlet(info);
                }
            }
            else
            {
                btLinkKeyboard.Text = "Link Keyboard";
                linkedKeyboard = false;
                outletKeyboard?.Close();
                outletKeyboard = null;
            }
        }

        private void btLinkMouse_Click(object sender, EventArgs e)
        {
            if (btLinkMouse.Text == "Link Mouse")
            {
                btLinkMouse.Text = "Unlink Mouse";
                linkedMouse = true;

                if (outletMouse == null)
                {
                    // create stream info and outlet
                    StreamInfo info = new StreamInfo("Mouse", "Position", 2, SRATE, channel_format_t.cf_int32);
                    XMLElement chns = info.desc().append_child("channels");
                    String[] labels = { "x", "y" };
                    for (int k = 0; k < labels.Length; k++)
                        chns.append_child("channel")
                            .append_child_value("label", labels[k])
                            .append_child_value("unit", "pixel")
                            .append_child_value("precision", "0");

                    //XMLElement xmlSync = info.desc().append_child("synchronization");
                    //xmlSync.append_child_value("offset_mean", $"{0}");
                    // xmlSync.append_child_value("offset_rms", $"{0}"); 
                    // xmlSync.append_child_value("offset_median", $"{0}"); 
                    // xmlSync.append_child_value("offset_5_centile", $"{0}"); 
                    // xmlSync.append_child_value("offset_95_centile", $"{0}"); 
                    //xmlSync.append_child_value("can_drop_samples", $"{true}");
                    //xmlSync.append_child_value("inlet_processing_options", $"{(byte)processing_options_t.proc_clocksync}");

                    outletMouse = new StreamOutlet(info, chunk_size: CHUNK_LEN, max_buffered: MAX_BUFFER);
                }

                Task.Run(async () => await MouseLoopAsync());

            }
            else
            {
                btLinkMouse.Text = "Link Mouse";
                linkedMouse = false;
                outletMouse?.Close();
                outletMouse = null;
            }
        }

        private async Task MouseLoopAsync()
        {
            // New point that will be updated by the function with the current coordinates
            Point defPnt = new Point();

            // LSL streamed data sample                    
            int[,] sample = new int[CHUNK_LEN, 2];
            double[] timestamp = new double[CHUNK_LEN];

            int chunkIndex = 0;

            WinApi.TimeBeginPeriod(1);

            // create and start a Stopwatch instance
            var watch = new Stopwatch();
            double frequency = Stopwatch.Frequency / (1000.0 * 1000.0);   // microsecond acccuracy for ticks
            double cycle = (1000.0 * 1000.0) / SRATE;  // in microseconds
            long count = 0;

            Console.WriteLine("Streaming mouse coordinates on the screen.");

            watch.Start();
            while (linkedMouse)
            {
                count++;
                //watch.Restart();

                // Call the function and pass the Point, defPnt
                WinApi.GetCursorPos(ref defPnt);
                // Now after calling the function, defPnt contains the coordinates which we can read
                //Console.WriteLine($"{(watch.ElapsedTicks / frequency / 1000.0):F3}");
                Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss.fff")}, {(watch.ElapsedTicks / frequency / 1000.0):F3}, X = {defPnt.X.ToString()}, Y = {defPnt.Y.ToString()}");

                sample[chunkIndex, 0] = defPnt.X;
                sample[chunkIndex, 1] = defPnt.Y;
                timestamp[chunkIndex] = LSL.LSL.local_clock();

                if (chunkIndex >= CHUNK_LEN - 1)
                {
                    outletMouse.push_chunk(sample, timestamp);

                    chunkIndex = 0;
                }
                else
                {
                    chunkIndex++;
                }

                while (watch.ElapsedTicks < count * cycle * frequency)
                {
                    Thread.SpinWait(10);
                }
            }

            WinApi.TimeEndPeriod(1);
            await Task.Delay(0);

        }

        public static IntPtr SetHook(WinApi.LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return WinApi.SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    WinApi.GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(
            int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Console.WriteLine((Keys)vkCode);
                if (linkedKeyboard)
                {
                    string[] sample = new string[1];
                    sample[0] = $"{(Keys)vkCode}";
                    outletKeyboard.push_sample(sample);
                }

            }
            return WinApi.CallNextHookEx(hookID, nCode, wParam, lParam);
        }

    }

}
