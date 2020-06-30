//
// C4Certificate.cs
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

using LiteCore.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace LiteCore.Interop
{
    /// <summary>
    /// Completion routine called when an async \ref c4cert_sendSigningRequest finishes.
    /// </summary>
    /// <param name="context">The same `context` value passed to \ref c4cert_sendSigningRequest.</param>
    /// <param name="signedCert">The signed certificate, if the operation was successful, else NULL.</param>
    /// <param name="error">The error, if the operation failed.</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void C4CertSigningCallback(void* context, C4Cert* signedCert, C4Error error);

    /// <summary>
    /// Provides the _public_ key's raw data, as an ASN.1 DER sequence of [modulus, exponent].
    /// </summary>
    /// <param name="externalKey">The client-provided key token given to c4keypair_fromExternal.</param>
    /// <param name="output">Where to copy the key data.</param>
    /// <param name="outputMaxLen">Maximum length of output that can be written.</param>
    /// <param name="outputLen">Store the length of the output here before returning.</param>
    /// <returns>True on success, false on failure.</returns>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate bool ExternalKeyPublicKeyDataCallback(void* externalKey, void* output, UIntPtr outputMaxLen, UIntPtr* outputLen);

    /// <summary>
    /// Decrypts data using the private key.
    /// </summary>
    /// <param name="externalKey">The client-provided key token given to c4keypair_fromExternal.</param>
    /// <param name="input">The encrypted data (size is always equal to the key size.)</param>
    /// <param name="output">Where to write the decrypted data.</param>
    /// <param name="outputMaxLen">Maximum length of output that can be written.</param>
    /// <param name="outputLen">Store the length of the output here before returning.</param>
    /// <returns>True on success, false on failure.</returns>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate bool ExternalKeyDecryptCallback(void* externalKey, FLSlice input, void* output, UIntPtr outputMaxLen, UIntPtr* outputLen);

    /// <summary>
    /// Uses the private key to generate a signature of input data.
    /// </summary>
    /// <param name="externalKey">The client-provided key value given to c4keypair_fromExternal.</param>
    /// <param name="digestAlgorithm">Indicates what type of digest to create the signature from.</param>
    /// <param name="inputData">The data to be signed.</param>
    /// <param name="outSignature">Write the signature here; length must be equal to the key size.</param>
    /// <returns>True on success, false on failure.</returns>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate bool ExternalKeySignCallback(void* externalKey, C4SignatureDigestAlgorithm digestAlgorithm, FLSlice inputData, void* outSignature);

    /// <summary>
    /// Called when the C4KeyPair is released and the externalKey is no longer needed, so that
    /// your code can free any associated resources. (This callback is optionaly and may be NULL.)
    /// </summary>
    /// <param name="externalKey">The client-provided key value given when the C4KeyPair was created.</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void ExternalKeyFreeCallback(void* externalKey);

    /// <summary>
    /// Callbacks that must be provided to create an external key; these perform the crypto operations.
    /// For Xamarin Android
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal static unsafe class ExternalKeyCallbacks
    {
        private static C4ExternalKeyCallbacks _c4ExternalKeyCallbacks;
        private static ExternalKeyPublicKeyDataCallback _publicKeyData;
        private static ExternalKeyDecryptCallback _decrypt;
        private static ExternalKeySignCallback _sign;
        private static ExternalKeyFreeCallback _free;

        public static C4ExternalKeyCallbacks C4ExternalKeyCallbacks => _c4ExternalKeyCallbacks;

        public static ExternalKeyPublicKeyDataCallback ExternalKeyPublicKeyDataCallback
        {
            get => _publicKeyData;
            set {
                _publicKeyData = value;
                _c4ExternalKeyCallbacks.publicKeyData = Marshal.GetFunctionPointerForDelegate(value);
            }
        }

        public static ExternalKeyDecryptCallback ExternalKeyDecryptCallback
        {
            get => _decrypt;
            set {
                _decrypt = value;
                _c4ExternalKeyCallbacks.decrypt = Marshal.GetFunctionPointerForDelegate(value);
            }
        }

        public static ExternalKeySignCallback ExternalKeySignCallback
        {
            get => _sign;
            set {
                _sign = value;
                _c4ExternalKeyCallbacks.sign = Marshal.GetFunctionPointerForDelegate(value);
            }
        }

        public static ExternalKeyFreeCallback ExternalKeyFreeCallback
        {
            get => _free;
            set {
                _free = value;
                _c4ExternalKeyCallbacks.free = Marshal.GetFunctionPointerForDelegate(value);
            }
        }
    }

    [ExcludeFromCodeCoverage]
    internal sealed class CertIssuerParameters : IDisposable
    {

        #region Constants

        private const uint OneYearInSec = 31536000;

        #endregion

        #region Variables

        private C4CertIssuerParameters _c4CertIssuerParams;

        #endregion

        #region Properties

        public C4CertIssuerParameters C4CertIssuerParams => _c4CertIssuerParams;

        /// <summary>
        /// seconds from signing till expiration (default 1yr)
        /// </summary>
        public uint ValidityInSeconds
        {
            get => _c4CertIssuerParams.validityInSeconds;
            set => _c4CertIssuerParams.validityInSeconds = value;
        }

        /// <summary>
        /// serial number string (default "1")
        /// </summary>
        public string SerialNumber
        {
            get => _c4CertIssuerParams.serialNumber.CreateString();
            set => _c4CertIssuerParams.serialNumber = new C4String(value).AsFLSlice();
        }

        /// <summary>
        /// maximum CA path length (default -1, meaning none)
        /// </summary>
        public int MaxPathLen
        {
            get => _c4CertIssuerParams.maxPathLen;
            set => _c4CertIssuerParams.maxPathLen = value;
        }

        /// <summary>
        /// will this be a CA certificate? (default false)
        /// </summary>
        public bool IsCA
        {
            get => _c4CertIssuerParams.isCA;
            set => _c4CertIssuerParams.isCA = value;
        }

        /// <summary>
        /// add authority identifier to cert? (default true)
        /// </summary>
        public bool AddAuthorityIdentifier
        {
            get => _c4CertIssuerParams.addAuthorityIdentifier;
            set => _c4CertIssuerParams.addAuthorityIdentifier = value;
        }

        /// <summary>
        /// add subject identifier to cert? (default true)
        /// </summary>
        public bool AddSubjectIdentifier
        {
            get => _c4CertIssuerParams.addSubjectIdentifier;
            set => _c4CertIssuerParams.addSubjectIdentifier = value;
        }

        /// <summary>
        /// add basic constraints extension? (default true)
        /// </summary>
        public bool AddBasicConstraints
        {
            get => _c4CertIssuerParams.addBasicConstraints;
            set => _c4CertIssuerParams.addBasicConstraints = value;
        }

        #endregion

        #region Constructors

        public CertIssuerParameters()
        {
            // Default Cert Issuer Parameters
            _c4CertIssuerParams.validityInSeconds = OneYearInSec;
            _c4CertIssuerParams.serialNumber = new C4String("1").AsFLSlice();
            _c4CertIssuerParams.maxPathLen = -1;
            _c4CertIssuerParams.isCA = false;
            _c4CertIssuerParams.addAuthorityIdentifier = true;
            _c4CertIssuerParams.addSubjectIdentifier = true;
            _c4CertIssuerParams.addBasicConstraints = true;
        }

        ~CertIssuerParameters()
        {
            Dispose(true);
        }

        #endregion

        #region Private Methods

        private unsafe void Dispose(bool finalizing)
        {
            
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
