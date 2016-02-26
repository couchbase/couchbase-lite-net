//
// Platform.cs
//
// Author:
//  Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Couchbase.Lite.Util
{
    internal struct utsname
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] sysname;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] nodename;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] release;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] version;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] machine;
    }

    #if __IOS__
    // Based on https://github.com/dannycabrera/Get-iOS-Model/blob/master/iOSHardware.cs
    internal static class iOSHardware
    {
        private const string HardwareProperty = "hw.machine";

        [DllImport(ObjCRuntime.Constants.SystemLibrary)]
        private static extern int sysctlbyname([MarshalAs(UnmanagedType.LPStr)] string property, // name of the property
            IntPtr output, // output
            IntPtr oldLen, // IntPtr.Zero
            IntPtr newp, // IntPtr.Zero
            uint newlen // 0
        );

        private static string Version
        {
            get
            {
                try {
                    // get the length of the string that will be returned
                    var pLen = Marshal.AllocHGlobal(sizeof(int));
                    sysctlbyname(HardwareProperty, IntPtr.Zero, pLen, IntPtr.Zero, 0);

                    var length = Marshal.ReadInt32(pLen);

                    // check to see if we got a length
                    if (length == 0) {
                        Marshal.FreeHGlobal(pLen);
                        return "Unknown";
                    }

                    // get the hardware string
                    var pStr = Marshal.AllocHGlobal(length);
                    sysctlbyname(HardwareProperty, pStr, pLen, IntPtr.Zero, 0);

                    // convert the native string into a C# string
                    var hardwareStr = Marshal.PtrToStringAnsi(pStr);

                    // cleanup
                    Marshal.FreeHGlobal(pLen);
                    Marshal.FreeHGlobal(pStr);

                    return hardwareStr;
                }
                catch (Exception ex) {
                    Console.WriteLine("DeviceHardware.Version Ex: " + ex.Message);
                }

                return "Unknown";
            }
        }

        public static Tuple<string, string> GetModelAndArch()
        {
            var hardware = Version;

            // http://support.apple.com/kb/HT3939
            if (hardware.StartsWith("iPhone"))
            {
                // ************
                // iPhone
                // ************
                // Model(s): A1203
                // Apple Tech specs: http://support.apple.com/kb/SP2
                if (hardware == "iPhone1,1")
                    return Tuple.Create("iPhone", "ARMv6");

                // ************
                // iPhone 3G
                // ************
                // Model(s): A1241 & A1324
                // Apple Tech specs: http://support.apple.com/kb/SP495
                if (hardware == "iPhone1,2")
                    return Tuple.Create("iPhone 3G", "ARMv6");

                // ************
                // iPhone 3GS
                // ************
                // Model(s): A1303 & A1325
                // Apple Tech specs: http://support.apple.com/kb/SP565
                if (hardware == "iPhone2,1")
                    return Tuple.Create("iPhone 3GS", "ARMv7");

                // ************
                // iPhone 4
                // ************
                // Model(s): A1332
                // Apple Tech specs: http://support.apple.com/kb/SP587
                if (hardware == "iPhone3,1" || hardware == "iPhone3,2")
                    return Tuple.Create("iPhone 4 GSM", "ARMv7");
                // Model(s): A1349
                if (hardware == "iPhone3,3") 
                    return Tuple.Create("iPhone 4 CDMA", "ARMv7");

                // ************
                // iPhone 4S
                // ************
                // Model(s): A1387 & A1431
                // Apple Tech specs: http://support.apple.com/kb/SP643
                if (hardware == "iPhone4,1")
                    return Tuple.Create("iPhone 4S", "ARMv7");

                // ************
                // iPhone 5
                // ************
                // Model(s): A1428
                // Apple Tech specs: http://support.apple.com/kb/SP655
                if (hardware == "iPhone5,1") 
                    return Tuple.Create("iPhone 5 GSM", "ARMv7s");

                // Model(s): A1429 & A1442
                if (hardware == "iPhone5,2")
                    return Tuple.Create("iPhone 5 Global", "ARMv7s");

                // ************
                // iPhone 5C
                // ************
                // Model(s): A1456 & A1532
                // Apple Tech specs: http://support.apple.com/kb/SP684
                if (hardware == "iPhone5,3") 
                    return Tuple.Create("iPhone 5C GSM", "ARMv7s");

                // Model(s): A1507, A1516, A1526 & A1529
                if (hardware == "iPhone5,4") 
                    return Tuple.Create("iPhone 5C Global", "ARMv7s");

                // ************
                // iPhone 5S
                // ************
                // Model(s): A1453 & A1533
                // Apple Tech specs: http://support.apple.com/kb/SP685
                if (hardware == "iPhone6,1")
                    return Tuple.Create("iPhone 5S GSM", "ARM64");

                // Model(s): A1457, A1518, A1528 & A1530    
                if (hardware == "iPhone6,2")
                    return Tuple.Create("iPhone 5S Global", "ARM64");

                // ************
                // iPhone 6
                // ************
                // Model(s): A1549, A1586 & A1589
                // Apple Tech specs: http://support.apple.com/kb/SP705
                if (hardware == "iPhone7,2")
                    return Tuple.Create("iPhone 6", "ARM64");

                // ************
                // iPhone 6 Plus
                // ************
                // Model(s): A1522, A1524 & A1593
                // Apple Tech specs: http://support.apple.com/kb/SP706
                if (hardware == "iPhone7,1")
                    return Tuple.Create("iPhone 6 Plus", "ARM64");

                // ************
                // iPhone 6S
                // ************
                // Model(s): A1633, A1688 & A1700
                // Apple Tech specs: http://support.apple.com/kb/SP726
                if (hardware == "iPhone8,1")
                    return Tuple.Create("iPhone 6S", "ARM64");

                // ************
                // iPhone 6S Plus
                // ************
                // Model(s): A1634, A1687 & A1699
                // Apple Tech specs: http://support.apple.com/kb/SP727
                if (hardware == "iPhone8,2")
                    return Tuple.Create("iPhone 6S Plus", "ARM64");

                return Tuple.Create(String.Format("Unknown iPhone ({0})", hardware.Substring(6)), "Unknown");
            }

            if (hardware.StartsWith("iPod"))
            {
                // ************
                // iPod touch
                // ************
                // Model(s): A1213
                // Apple Tech specs: http://support.apple.com/kb/SP3
                if (hardware == "iPod1,1") 
                    return Tuple.Create("iPod touch", "ARMv6");

                // ************
                // iPod touch 2G
                // ************
                // Model(s): A1288
                // Apple Tech specs: http://support.apple.com/kb/SP496
                if (hardware == "iPod2,1")
                    return Tuple.Create("iPod touch 2G", "ARMv6");

                // ************
                // iPod touch 3G
                // ************
                // Model(s): A1318
                // Apple Tech specs: http://support.apple.com/kb/SP570
                if (hardware == "iPod3,1")
                    return Tuple.Create("iPod touch 3G", "ARMv7");

                // ************
                // iPod touch 4G
                // ************
                // Model(s): A1367
                // Apple Tech specs: http://support.apple.com/kb/SP594
                if (hardware == "iPod4,1")
                    return Tuple.Create("iPod touch 4G", "ARMv7");

                // ************
                // iPod touch 5G
                // ************
                // Model(s): A1421 & A1509
                // Apple Tech specs: (A1421) http://support.apple.com/kb/SP657 & (A1509) http://support.apple.com/kb/SP675
                if (hardware == "iPod5,1")
                    return Tuple.Create("iPod touch 5G", "ARMv7");

                // ************
                // iPod touch 6G
                // ************
                // Model(s): A1574
                // Apple Tech specs: (A1574) https://support.apple.com/kb/SP720 
                if (hardware == "iPod7,1")
                    return Tuple.Create("iPod touch 6G", "ARM64");

                return Tuple.Create(String.Format("Unknown iPod ({0})", hardware.Substring(4)), "Unknown");
            }

            if (hardware.StartsWith("iPad"))
            {
                // ************
                // iPad
                // ************
                // Model(s): A1219 (WiFi) & A1337 (GSM)
                // Apple Tech specs: http://support.apple.com/kb/SP580
                if (hardware == "iPad1,1")
                    return Tuple.Create("iPad", "ARMv7");

                // ************
                // iPad 2
                // ************
                // Apple Tech specs: http://support.apple.com/kb/SP622
                // Model(s): A1395
                if (hardware == "iPad2,1" || hardware == "iPad2,4")
                    return Tuple.Create("iPad 2 WiFi", "ARMv7");
                // Model(s): A1396
                if (hardware == "iPad2,2")
                    return Tuple.Create("iPad 2 GSM", "ARMv7");
                // Model(s): A1397
                if (hardware == "iPad2,3")
                    return Tuple.Create("iPad 2 CDMA", "ARMv7");

                // ************
                // iPad 3
                // ************
                // Apple Tech specs: http://support.apple.com/kb/SP647
                // Model(s): A1416
                if (hardware == "iPad3,1")
                    return Tuple.Create("iPad 3 WiFi", "ARMv7");
                // Model(s): A1403
                if (hardware == "iPad3,2")
                    return Tuple.Create("iPad 3 Wi-Fi + Cellular (VZ)", "ARMv7");
                // Model(s): A1430
                if (hardware == "iPad3,3") 
                    return Tuple.Create("iPad 3 Wi-Fi + Cellular", "ARMv7");

                // ************
                // iPad 4
                // ************
                // Apple Tech specs: http://support.apple.com/kb/SP662
                // Model(s): A1458
                if (hardware == "iPad3,4")
                    return Tuple.Create("iPad 4 Wifi", "ARMv7s");
                // Model(s): A1459
                if (hardware == "iPad3,5")
                    return Tuple.Create("iPad 4 Wi-Fi + Cellular", "ARMv7s");
                // Model(s): A1460
                if (hardware == "iPad3,6") 
                    return Tuple.Create("iPad 4 Wi-Fi + Cellular (MM)", "ARMv7s");

                // ************
                // iPad Air
                // ************
                // Apple Tech specs: http://support.apple.com/kb/SP692
                // Model(s): A1474
                if (hardware == "iPad4,1")
                    return Tuple.Create("iPad Air Wifi", "ARM64");
                // Model(s): A1475
                if (hardware == "iPad4,2") 
                    return Tuple.Create("iPad Air Wi-Fi + Cellular", "ARM64");
                // Model(s): A1476
                if (hardware == "iPad4,3")
                    return Tuple.Create("iPad Air Wi-Fi + Cellular (TD-LTE)", "ARM64");

                // ************
                // iPad Air 2
                // ************
                // Apple Tech specs: 
                // Model(s): A1566, A1567
                if (hardware == "iPad5,3" || hardware == "iPad5,4") 
                    return Tuple.Create("iPad Air 2", "ARM64");

                // ************
                // iPad Pro
                // ************
                // Apple Tech specs: 
                // Model(s):
                if (hardware == "iPad") 
                    return Tuple.Create("iPad Pro", "ARM64");


                // ************
                // iPad mini
                // ************
                // Apple Tech specs: http://support.apple.com/kb/SP661
                // Model(s): A1432
                if (hardware == "iPad2,5")
                    return Tuple.Create("iPad mini Wifi", "ARMv7");
                // Model(s): A1454
                if (hardware == "iPad2,6")
                    return Tuple.Create("iPad mini Wi-Fi + Cellular", "ARMv7");
                // Model(s): A1455
                if (hardware == "iPad2,7")
                    return Tuple.Create("iPad mini Wi-Fi + Cellular (MM)", "ARMv7");

                // ************
                // iPad mini 2
                // ************
                // Apple Tech specs: http://support.apple.com/kb/SP693
                // Model(s): A1489
                if (hardware == "iPad4,4")
                    return Tuple.Create("iPad mini 2 Wifi", "ARM64");
                // Model(s): A1490
                if (hardware == "iPad4,5")
                    return Tuple.Create("iPad mini 2 Wi-Fi + Cellular", "ARM64");
                // Model(s): A1491
                if (hardware == "iPad4,6")
                    return Tuple.Create("iPad mini 2 Wi-Fi + Cellular (TD-LTE)", "ARM64");

                // ************
                // iPad mini 3
                // ************
                // Apple Tech specs: 
                // Model(s): A1599
                if (hardware == "iPad4,7")
                    return Tuple.Create("iPad mini 3 Wifi", "ARM64");
                // Model(s): A1600
                if (hardware == "iPad4,8")
                    return Tuple.Create("iPad mini 3 Wi-Fi + Cellular", "ARM64");
                // Model(s): A1601
                if (hardware == "iPad4,9")
                    return Tuple.Create("iPad mini 3 Wi-Fi + Cellular (TD-LTE)", "ARM64");

                // ************
                // iPad mini 4
                // ************
                // Apple Tech specs: http://support.apple.com/kb/SP725
                // Model(s): 
                if (hardware == "iPad5,1" || hardware == "iPad5,2")
                    return Tuple.Create("iPad mini 4", "ARM64");

                return Tuple.Create(String.Format("Unknown iPad ({0})", hardware.Substring(4)), "Unknown");
            }

            if(hardware.StartsWith("Watch")) {
                if(hardware == "Watch1,1") {
                    return Tuple.Create("Apple Watch 38mm", "ARMv7");
                }

                if(hardware == "Watch1,2") {
                    return Tuple.Create("Apple Watch 42mm", "ARMv7");
                }

                return Tuple.Create(String.Format("Unknown Watch ({0})", hardware.Substring(5)), "Unknown");
            }

            if(hardware.StartsWith("AppleTV")) {
                if(hardware == "AppleTV5,3")
                    return Tuple.Create("Apple TV 4G", "ARM64");

                return Tuple.Create(String.Format("Unknown Apple TV ({0})", hardware.Substring(7)), "Unknown");
            }

            if (hardware == "i386" || hardware == "x86_64")
                return Tuple.Create("iOS Simulator", hardware);

            return Tuple.Create(String.Format("Unknown Hardware ({0})", hardware), "Unknown");       
        }
    }
    #elif __ANDROID__
    internal static class AndroidHardware
    {
        public static Tuple<string, string> GetModelAndArch()
        {
            var manufacturer = global::Android.OS.Build.Manufacturer;
            var model = global::Android.OS.Build.Model;
            var modelString = String.Format("{0} {1}", manufacturer, model);

            var abi = global::Android.OS.Build.CpuAbi;
            return Tuple.Create(modelString, abi);
        }
    }

    #endif

    internal static class Platform
    {
        public static readonly string Name;
        public static readonly string Architecture;

        #if !__IOS__ && !__ANDROID__

        private static string GetWindowsName()
        {
            string result = string.Empty;
            using (var searcher = new System.Management.ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem")) {
                foreach (var os in searcher.Get()) {
                    result = os["Caption"].ToString();
                    break;
                }
                return result;
            }
        }

        private static string GetWindowsArchitecture()
        {
            string result = string.Empty;
            using (var searcher = new System.Management.ManagementObjectSearcher("SELECT Architecture FROM Win32_Processor")) {
                foreach (var cpu in searcher.Get()) {
                    var type = (ushort)cpu["Architecture"];
                    switch (type) {
                        case 0:
                            result = "x86";
                            break;
                        case 5:
                            result = "ARM";
                            break;
                        case 9:
                            result = "x86_64";
                            break;
                        default:
                            result = String.Format("Rare ({0})", type);
                            break;
                    }

                    break;
                }
                return result;
            }
        }

        #endif

        static Platform()
        {
            #if __IOS__
            var info = iOSHardware.GetModelAndArch();
            Name = String.Format("{0} {1}",
                info.Item1,
                UIKit.UIDevice.CurrentDevice.SystemVersion);
            Architecture = info.Item2;
            #elif __ANDROID__
            var info = AndroidHardware.GetModelAndArch();
            Name = String.Format("{0} API{1}", info.Item1,
                (int)global::Android.OS.Build.VERSION.SdkInt);
            Architecture = info.Item2;
            #else
            var isWindows = Path.DirectorySeparatorChar == '\\';
            if (isWindows) {
                Name = GetWindowsName();
                Architecture = GetWindowsArchitecture();
            } else {
                var buf = new utsname();
                if (uname(ref buf) != 0) {
                    Name = "Unknown";
                    Architecture = "Unknown";
                }

                Architecture = Encoding.ASCII.GetString(buf.machine.TakeWhile(x => x != 0).ToArray());
                var systemName = Encoding.ASCII.GetString(buf.sysname.TakeWhile(x => x != 0).ToArray());
                var version = Encoding.ASCII.GetString(buf.release.TakeWhile(x => x != 0).ToArray());

                if (systemName == "Darwin") {
                    systemName = "OS X";
                    var darwinVesion = Int32.Parse(version.Split('.').First());
                    version = String.Format("10.{0}", darwinVesion - 4);
                }

                Name = String.Format("{0} {1}", systemName, version);
            }
            #endif
        }

        [DllImport("libc")]
        private static extern int uname(ref utsname buf);
    }
}
   