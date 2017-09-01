using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Lite.DI;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System.Profile;

namespace Couchbase.Lite.Support
{
    internal sealed class UwpRuntimePlatform : IRuntimePlatform
    {
        public string OSDescription
        {
            get {
                var sv = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
                var v = ulong.Parse(sv);
                var v1 = (v & 0xFFFF000000000000L) >> 48;
                var v2 = (v & 0x0000FFFF00000000L) >> 32;
                var v3 = (v & 0x00000000FFFF0000L) >> 16;
                var v4 = (v & 0x000000000000FFFFL);
                return $"UWP {v1}.{v2}.{v3}.{v4}";
            }
        }

        public string HardwareName
        {
            get {
                var eas = new EasClientDeviceInformation();
                if(eas.SystemProductName.StartsWith(eas.SystemManufacturer)) {
                    return eas.SystemProductName;
                }

                return $"{eas.SystemManufacturer} {eas.SystemProductName}";
            }
        }
    }
}
