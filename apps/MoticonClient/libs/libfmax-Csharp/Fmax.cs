using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Collections.Generic;
using LSL;

namespace Libfmax
{
    public delegate void InfoMessageDelegate(object sender, Info info);

    public class Info
    {
        [Flags]
        public enum Mode : byte
        {
            None = 0,
            CriticalError = 1,
            Error = 2,
            CriticalEvent = 4,
            Event = 8,
            CriticalData = 16,
            Data = 32,
            All = 1 | 2 | 4 | 8 | 16 | 32
        }

        public string msg;
        public Mode mode;

        public Info(string msg, Mode mode)
        {
            this.msg = msg;
            this.mode = mode;
        }
    }

    public static class Constants
    {

    }

    public class Quaternion
    {
        public double q1, q2, q3, q4;
    }

    public static class WinApi
    {

        ///  <summary> GetCursorPos(). Gets mouse cursor coordinates on the screen. </summary>
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(ref Point lpPoint);

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        /// <summary>TimeBeginPeriod(). See the Windows API documentation for details.</summary>
        //[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Interoperability", "CA1401:PInvokesShouldNotBeVisible"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage"), SuppressUnmanagedCodeSecurity]
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
        public static extern uint TimeBeginPeriod(uint uMilliseconds);

        /// <summary>TimeEndPeriod(). See the Windows API documentation for details.</summary>
        //[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Interoperability", "CA1401:PInvokesShouldNotBeVisible"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage"), SuppressUnmanagedCodeSecurity]
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
        public static extern uint TimeEndPeriod(uint uMilliseconds);


        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
    }

}