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

namespace LiteCore.Interop
{
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
