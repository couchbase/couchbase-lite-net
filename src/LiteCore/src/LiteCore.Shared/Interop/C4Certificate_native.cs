//
// C4Certificate_native.cs
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

using LiteCore.Util;

namespace LiteCore.Interop
{

    internal unsafe static partial class Native
    {
        public static C4Cert* c4cert_fromData(byte[] certData, C4Error* outError)
        {
            fixed(byte *certData_ = certData) {
                return NativeRaw.c4cert_fromData(new FLSlice(certData_, certData == null ? 0 : (ulong)certData.Length), outError);
            }
        }

        public static byte[] c4cert_copyData(C4Cert* x, bool pemEncoded)
        {
            using(var retVal = NativeRaw.c4cert_copyData(x, pemEncoded)) {
                return ((FLSlice)retVal).ToArrayFast();
            }
        }

        //public static string c4cert_summary(C4Cert* x)
        //{
        //    using(var retVal = NativeRaw.c4cert_summary(x)) {
        //        return ((FLSlice)retVal).CreateString();
        //    }
        //}

        //public static string c4cert_subjectName(C4Cert* x)
        //{
        //    using(var retVal = NativeRaw.c4cert_subjectName(x)) {
        //        return ((FLSlice)retVal).CreateString();
        //    }
        //}

        //public static string c4cert_subjectNameComponent(C4Cert* x, byte[] y)
        //{
        //    fixed(byte *y_ = y) {
        //        using(var retVal = NativeRaw.c4cert_subjectNameComponent(x, new FLSlice(y_, (ulong)y.Length))) {
        //            return ((FLSlice)retVal).CreateString();
        //        }
        //    }
        //}

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //[return: MarshalAs(UnmanagedType.U1)]
        //public static extern bool c4cert_subjectNameAtIndex(C4Cert* cert, uint index, C4CertNameInfo* outInfo);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4cert_getValidTimespan(C4Cert* cert, long* outCreated, long* outExpires);

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //public static extern C4CertUsage c4cert_usages(C4Cert* x);

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //[return: MarshalAs(UnmanagedType.U1)]
        //public static extern bool c4cert_isSelfSigned(C4Cert* x);

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //public static extern C4KeyPair* c4cert_getPublicKey(C4Cert* x);

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //public static extern C4KeyPair* c4cert_loadPersistentPrivateKey(C4Cert* x, C4Error* outError);

        public static C4Cert* c4cert_createRequest(C4CertNameComponent* nameComponents, ulong nameCount, C4CertUsage certUsages, C4KeyPair* subjectKey, C4Error* outError)
        {
            return NativeRaw.c4cert_createRequest(nameComponents, (UIntPtr)nameCount, certUsages, subjectKey, outError);
        }

        //public static C4Cert* c4cert_requestFromData(byte[] certRequestData, C4Error* outError)
        //{
        //    fixed(byte *certRequestData_ = certRequestData) {
        //        return NativeRaw.c4cert_requestFromData(new FLSlice(certRequestData_, certRequestData == null ? 0 : (ulong)certRequestData.Length), outError);
        //    }
        //}

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //[return: MarshalAs(UnmanagedType.U1)]
        //public static extern bool c4cert_isSigned(C4Cert* x);

        //public static bool c4cert_sendSigningRequest(C4Cert* certRequest, C4Address address, byte[] optionsDictFleece, C4CertSigningCallback callback, void* context, C4Error* outError)
        //{
        //    fixed(byte *optionsDictFleece_ = optionsDictFleece) {
        //        return NativeRaw.c4cert_sendSigningRequest(certRequest, address, new FLSlice(optionsDictFleece_, optionsDictFleece == null ? 0 : (ulong)optionsDictFleece.Length), callback, context, outError);
        //    }
        //}

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Cert* c4cert_signRequest(C4Cert* certRequest, C4CertIssuerParameters* @params, C4KeyPair* issuerPrivateKey, C4Cert* issuerCert, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Cert* c4cert_nextInChain(C4Cert* x);

        public static byte[] c4cert_copyChainData(C4Cert* x)
        {
            using(var retVal = NativeRaw.c4cert_copyChainData(x)) {
                return ((FLSlice)retVal).ToArrayFast();
            }
        }

        //public static bool c4cert_save(C4Cert* cert, bool entireChain, string name, C4Error* outError)
        //{
        //    using(var name_ = new C4String(name)) {
        //        return NativeRaw.c4cert_save(cert, entireChain, name_.AsFLSlice(), outError);
        //    }
        //}

        //public static C4Cert* c4cert_load(string name, C4Error* outError)
        //{
        //    using(var name_ = new C4String(name)) {
        //        return NativeRaw.c4cert_load(name_.AsFLSlice(), outError);
        //    }
        //}

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //public static extern C4KeyPair* c4keypair_generate(C4KeyPairAlgorithm algorithm, uint sizeInBits, [MarshalAs(UnmanagedType.U1)]bool persistent, C4Error* outError);

        //public static C4KeyPair* c4keypair_fromPublicKeyData(byte[] publicKeyData, C4Error* outError)
        //{
        //    fixed(byte *publicKeyData_ = publicKeyData) {
        //        return NativeRaw.c4keypair_fromPublicKeyData(new FLSlice(publicKeyData_, publicKeyData == null ? 0 : (ulong)publicKeyData.Length), outError);
        //    }
        //}

        //public static C4KeyPair* c4keypair_fromPrivateKeyData(byte[] privateKeyData, byte[] passwordOrNull, C4Error* outError)
        //{
        //    fixed(byte *privateKeyData_ = privateKeyData)
        //    fixed(byte *passwordOrNull_ = passwordOrNull) {
        //        return NativeRaw.c4keypair_fromPrivateKeyData(new FLSlice(privateKeyData_, privateKeyData == null ? 0 : (ulong)privateKeyData.Length), new FLSlice(passwordOrNull_, passwordOrNull == null ? 0 : (ulong)passwordOrNull.Length), outError);
        //    }
        //}

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //[return: MarshalAs(UnmanagedType.U1)]
        //public static extern bool c4keypair_hasPrivateKey(C4KeyPair* x);

        //public static byte[] c4keypair_publicKeyDigest(C4KeyPair* x)
        //{
        //    using(var retVal = NativeRaw.c4keypair_publicKeyDigest(x)) {
        //        return ((FLSlice)retVal).ToArrayFast();
        //    }
        //}

        //public static byte[] c4keypair_publicKeyData(C4KeyPair* x)
        //{
        //    using(var retVal = NativeRaw.c4keypair_publicKeyData(x)) {
        //        return ((FLSlice)retVal).ToArrayFast();
        //    }
        //}

        //public static byte[] c4keypair_privateKeyData(C4KeyPair* x)
        //{
        //    using(var retVal = NativeRaw.c4keypair_privateKeyData(x)) {
        //        return ((FLSlice)retVal).ToArrayFast();
        //    }
        //}

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //[return: MarshalAs(UnmanagedType.U1)]
        //public static extern bool c4keypair_isPersistent(C4KeyPair* x);

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //public static extern C4KeyPair* c4keypair_persistentWithPublicKey(C4KeyPair* x, C4Error* outError);

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //[return: MarshalAs(UnmanagedType.U1)]
        //public static extern bool c4keypair_removePersistent(C4KeyPair* x, C4Error* outError);

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

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //public static extern FLSliceResult c4cert_summary(C4Cert* x);

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //public static extern FLSliceResult c4cert_subjectName(C4Cert* x);

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //public static extern FLSliceResult c4cert_subjectNameComponent(C4Cert* x, FLSlice y);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Cert* c4cert_createRequest(C4CertNameComponent* nameComponents, UIntPtr nameCount, C4CertUsage certUsages, C4KeyPair* subjectKey, C4Error* outError);

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //public static extern C4Cert* c4cert_requestFromData(FLSlice certRequestData, C4Error* outError);

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //[return: MarshalAs(UnmanagedType.U1)]
        //public static extern bool c4cert_sendSigningRequest(C4Cert* certRequest, C4Address address, FLSlice optionsDictFleece, C4CertSigningCallback callback, void* context, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult c4cert_copyChainData(C4Cert* x);

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //[return: MarshalAs(UnmanagedType.U1)]
        //public static extern bool c4cert_save(C4Cert* cert, [MarshalAs(UnmanagedType.U1)]bool entireChain, FLSlice name, C4Error* outError);

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //public static extern C4Cert* c4cert_load(FLSlice name, C4Error* outError);

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //public static extern C4KeyPair* c4keypair_fromPublicKeyData(FLSlice publicKeyData, C4Error* outError);

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //public static extern C4KeyPair* c4keypair_fromPrivateKeyData(FLSlice privateKeyData, FLSlice passwordOrNull, C4Error* outError);

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //public static extern FLSliceResult c4keypair_publicKeyDigest(C4KeyPair* x);

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //public static extern FLSliceResult c4keypair_publicKeyData(C4KeyPair* x);

        //[DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        //public static extern FLSliceResult c4keypair_privateKeyData(C4KeyPair* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4KeyPair* c4keypair_fromExternal(C4KeyPairAlgorithm algorithm, UIntPtr keySizeInBits, void* externalKey, C4ExternalKeyCallbacks callbacks, C4Error* outError);


    }
}
