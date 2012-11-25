﻿using System;
using System.ComponentModel;
using System.Globalization;
using System.Management;
using System.Windows.Forms;

namespace NPowerTray
{
    internal static class PowerActions
    {
        private const string RunKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

        public static void Shutdown(ShutdownMode shutdownMode)
        {
            var mcWin32 = new ManagementClass("Win32_OperatingSystem");
            mcWin32.Get();

            // You can't shutdown without security privileges
            mcWin32.Scope.Options.EnablePrivileges = true;
            var mboShutdownParams = mcWin32.GetMethodParameters("Win32Shutdown");

            // Flag 1 means we want to shut down the system
            mboShutdownParams["Flags"] = ((int) shutdownMode).ToString(CultureInfo.InvariantCulture);
            mboShutdownParams["Reserved"] = "0";
            foreach (ManagementObject manObj in mcWin32.GetInstances())
            {
                manObj.InvokeMethod("Win32Shutdown", mboShutdownParams, null);
            }
        }

        public static void ShutdownHybrid()
        {
            var osv = Environment.OSVersion;

            if(osv.Platform == PlatformID.Win32NT && osv.Version >= new Version(6,2))
            {
                var hToken = IntPtr.Zero;
                var tkp = new NativeMethods.TokPriv1Luid {Count = 1, Attributes = NativeMethods.SePrivilegeEnabled};

                // Get a token for this process. 

                if (!NativeMethods.OpenProcessToken(NativeMethods.GetCurrentProcess(),
                     NativeMethods.TokenAdjustPrivileges | NativeMethods.TokenQuery, ref hToken))
                    return;

                NativeMethods.LookupPrivilegeValue(null, NativeMethods.SeShutdownName, ref tkp.Luid);
                NativeMethods.AdjustTokenPrivileges(hToken, false, ref tkp, 0, IntPtr.Zero, IntPtr.Zero);
                NativeMethods.InitiateShutdown(null, null, 0,
                                               InitiateShutdownFlags.Hybrid | InitiateShutdownFlags.PowerOff, 0x80000000);
            }
        }

        public static void SetPowerState(PowerState newState, bool force = false, bool disableWakeupEvents = false)
        {
            Application.SetSuspendState(newState, force, disableWakeupEvents);
        }

        public static bool StartupState
        {
            get
            {
                var startupKey = Program.BaseKey.OpenSubKey(RunKey);

                if (startupKey == null) return false;

                var text = (string) startupKey.GetValue(Application.ProductName);

                bool value = string.CompareOrdinal(text, Application.ExecutablePath) == 0;

                startupKey.Close();

                return value;
            }
            set
            {
                var startupKey = Program.BaseKey.OpenSubKey(RunKey, true);

                if (startupKey == null) return;

                var old = (string) startupKey.GetValue(Application.ProductName);
                var oldSame = old != null && string.CompareOrdinal(old, Application.ExecutablePath) == 0;

                if (value && oldSame)
                {
                    startupKey.Close();
                    return;
                }

                if (old != null)
                    startupKey.DeleteValue(Application.ProductName, false);
                else if (value)
                    startupKey.SetValue(Application.ProductName, Application.ExecutablePath);

                startupKey.Close();
            }
        }

        public static void FastUserSwitch()
        {
            if (!NativeMethods.WTSDisconnectSession(NativeMethods.WtsCurrentServerHandle,
                                      NativeMethods.WtsCurrentSession, false))
                throw new Win32Exception();
        }

        public static void Lock()
        {
            NativeMethods.LockWorkStation();
        }
    }
}