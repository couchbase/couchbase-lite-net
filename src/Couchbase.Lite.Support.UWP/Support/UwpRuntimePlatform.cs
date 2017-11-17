// 
//  UwpRuntimePlatform.cs
// 
//  Author:
//   Jim Borden  <jim.borden@couchbase.com>
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 

using Couchbase.Lite.DI;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System.Profile;

namespace Couchbase.Lite.Support
{
    [CouchbaseDependency]
    internal sealed class UwpRuntimePlatform : IRuntimePlatform
    {
        #region Properties

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

        #endregion
    }
}
