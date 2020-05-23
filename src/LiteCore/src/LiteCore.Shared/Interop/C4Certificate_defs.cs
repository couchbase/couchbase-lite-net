//
// C4Certificate_defs.cs
//
// Copyright (c) 2020 Couchbase, Inc All rights reserved.
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
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

using LiteCore.Util;

namespace LiteCore.Interop
{
    [Flags]
    internal enum C4CertUsage : byte
    {
        CertUsage_NotSpecified      = 0x00,
        CertUsage_TLSClient         = 0x80,
        CertUsage_TLSServer         = 0x40,
        CertUsage_Email             = 0x20,
        CertUsage_ObjectSigning     = 0x10,
        CertUsage_TLS_CA            = 0x04,
        CertUsage_Email_CA          = 0x02,
        CertUsage_ObjectSigning_CA  = 0x01
    }

    internal enum C4KeyPairAlgorithm : byte
    {
        RSA,
    }

	internal unsafe struct C4CertNameInfo
    {
        public C4CertNameAttributeID id;
        public C4StringResult value;
    }

	internal unsafe struct C4CertNameComponent
    {
        public C4CertNameAttributeID attributeID;
        public FLSlice value;
    }

	internal unsafe struct C4CertIssuerParameters
    {
        public unsigned validityInSeconds;
        public FLSlice serialNumber;
        public int maxPathLen;
        private byte _isCA;
        private byte _addAuthorityIdentifier;
        private byte _addSubjectIdentifier;
        private byte _addBasicConstraints;

        public bool isCA
        {
            get {
                return Convert.ToBoolean(_isCA);
            }
            set {
                _isCA = Convert.ToByte(value);
            }
        }

        public bool addAuthorityIdentifier
        {
            get {
                return Convert.ToBoolean(_addAuthorityIdentifier);
            }
            set {
                _addAuthorityIdentifier = Convert.ToByte(value);
            }
        }

        public bool addSubjectIdentifier
        {
            get {
                return Convert.ToBoolean(_addSubjectIdentifier);
            }
            set {
                _addSubjectIdentifier = Convert.ToByte(value);
            }
        }

        public bool addBasicConstraints
        {
            get {
                return Convert.ToBoolean(_addBasicConstraints);
            }
            set {
                _addBasicConstraints = Convert.ToByte(value);
            }
        }
    }
}