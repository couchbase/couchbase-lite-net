//
// C4Certificate_native.cs
//
// Copyright (c) 2023 Couchbase, Inc All rights reserved.
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

using LiteCore.Util;

namespace LiteCore.Interop
{

    internal unsafe static partial class Native
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4cert_getValidTimespan(C4Cert* cert, long* outCreated, long* outExpires);

        public static C4Cert* c4cert_fromData(byte[]? certData, C4Error* outError)
        {
            fixed(byte *certData_ = certData) {
                return NativeRaw.c4cert_fromData(new FLSlice(certData_, certData == null ? 0 : (ulong)certData.Length), outError);
            }
        }

        public static byte[]? c4cert_copyData(C4Cert* x, bool pemEncoded)
        {
            using(var retVal = NativeRaw.c4cert_copyData(x, pemEncoded)) {
                return ((FLSlice)retVal).ToArrayFast();
            }
        }

        public static C4Cert* c4cert_createRequest(C4CertNameComponent* nameComponents, ulong nameCount, C4CertUsage certUsages, C4KeyPair* subjectKey, C4Error* outError)
        {
            return NativeRaw.c4cert_createRequest(nameComponents, (UIntPtr)nameCount, certUsages, subjectKey, outError);
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Cert* c4cert_signRequest(C4Cert* certRequest, C4CertIssuerParameters* @params, C4KeyPair* issuerPrivateKey, C4Cert* issuerCert, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Cert* c4cert_nextInChain(C4Cert* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4KeyPair* c4keypair_generate(C4KeyPairAlgorithm algorithm, uint sizeInBits, [MarshalAs(UnmanagedType.U1)]bool persistent, C4Error* outError);

        public static byte[]? c4keypair_privateKeyData(C4KeyPair* x)
        {
            using(var retVal = NativeRaw.c4keypair_privateKeyData(x)) {
                return ((FLSlice)retVal).ToArrayFast();
            }
        }

        public static C4KeyPair* c4keypair_fromExternal(C4KeyPairAlgorithm algorithm, ulong keySizeInBits, void* externalKey, C4ExternalKeyCallbacks callbacks, C4Error* outError)
        {
            return NativeRaw.c4keypair_fromExternal(algorithm, (UIntPtr)keySizeInBits, externalKey, callbacks, outError);
        }


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Cert* c4cert_fromData(FLSlice certData, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult c4cert_copyData(C4Cert* x, [MarshalAs(UnmanagedType.U1)]bool pemEncoded);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Cert* c4cert_createRequest(C4CertNameComponent* nameComponents, UIntPtr nameCount, C4CertUsage certUsages, C4KeyPair* subjectKey, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult c4keypair_privateKeyData(C4KeyPair* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4KeyPair* c4keypair_fromExternal(C4KeyPairAlgorithm algorithm, UIntPtr keySizeInBits, void* externalKey, C4ExternalKeyCallbacks callbacks, C4Error* outError);


    }
}
