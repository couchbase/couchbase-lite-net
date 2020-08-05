//
//  TLSIdentityTest.cs
//
//  Copyright (c) 2020 Couchbase, Inc All rights reserved.
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

#if COUCHBASE_ENTERPRISE    

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Lite;
using Couchbase.Lite.Logging;

using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;
using Couchbase.Lite.Query;

using FluentAssertions;
using LiteCore;
using LiteCore.Interop;

using Newtonsoft.Json;
using System.Collections.Immutable;
using System.Reflection;

using Test.Util;
using Couchbase.Lite.P2P;
using ProtocolType = Couchbase.Lite.P2P.ProtocolType;

#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
using System.Security.Cryptography;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace Test
{
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public sealed class TLSIdentityTest : TestCase
    {
        const string ServerCertLabel = "CBL-Server-Cert";
        const string ClientCertLabel = "CBL-Client-Cert";

        private X509Store _store;
#if !WINDOWS_UWP
        public TLSIdentityTest(ITestOutputHelper output) : base(output)
#else
        public TLSIdentityTest()
#endif
        {
            _store = new X509Store(StoreName.My);
            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
            TLSIdentity.DeleteIdentity(_store, ServerCertLabel, null);
        }

        #region TLSIdentity tests

        [Fact]
        public void TestCreateGetDeleteServerIdentity() => CreateGetDeleteServerIdentity(true);

        [Fact]
        public void TestCreateDuplicateServerIdentity() => CreateDuplicateServerIdentity(true);

        [Fact]
        public void TestCreateGetDeleteClientIdentity() => CreateGetDeleteServerIdentity(false);

        [Fact]
        public void TestCreateDuplicateClientIdentity() => CreateDuplicateServerIdentity(false);

        [Fact]
        public void TestGetIdentityWithCertCollection()
        {
            TLSIdentity id;
            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
            TLSIdentity identity = TLSIdentity.CreateIdentity(false,
                new Dictionary<string, string>() { { Certificate.CommonNameAttribute, "CA-P2PTest1" } },
                null,
                _store,
                ClientCertLabel,
                null);

            var certs = identity.Certs;

            id = TLSIdentity.GetIdentity(certs);
            id.Should().NotBeNull();

            // Delete
            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
        }
        
        [Fact]
        public void TestImportIdentity()
        {
            TLSIdentity id;
            byte[] data = null;
            using(var stream = typeof(TLSIdentityTest).GetTypeInfo().Assembly.GetManifestResourceStream("certs.p12"))
            using (var reader = new BinaryReader(stream)) {
                data = reader.ReadBytes((int)stream.Length);
            }

            // Import
            id = TLSIdentity.ImportIdentity(_store, data, "123", ServerCertLabel, null);
            id.Should().NotBeNull();
            id.Certs.Count.Should().Be(2);
            ValidateCertsInStore(id.Certs, _store).Should().BeTrue();

            // Get
            id = TLSIdentity.GetIdentity(_store, ServerCertLabel, null);
            id.Should().NotBeNull();
        }

        [Fact]
        public void TestCreateIdentityWithNoAttributesOrEmptyAttributes()
        {
            // Delete 
            TLSIdentity.DeleteIdentity(_store, ServerCertLabel, null);

            //Get
            var id = TLSIdentity.GetIdentity(_store, ServerCertLabel, null);
            id.Should().BeNull();

            // Create id with empty Attributes
            Action badAction = (() => TLSIdentity.CreateIdentity(true,
                new Dictionary<string, string>() { },
                null,
                _store,
                ServerCertLabel,
                null));
            badAction.Should().Throw<CouchbaseLiteException>(CouchbaseLiteErrorMessage.CreateCertAttributeEmpty);

            // Create id with null Attributes
            badAction = (() => TLSIdentity.CreateIdentity(true,
                null,
                null,
                _store,
                ServerCertLabel,
                null));
            badAction.Should().Throw<CouchbaseLiteException>(CouchbaseLiteErrorMessage.CreateCertAttributeEmpty);
        }

        [Fact]
        public void TestCertificateExpiration()
        {
            TLSIdentity id;

            // Delete 
            TLSIdentity.DeleteIdentity(_store, ServerCertLabel, null);

            //Get
            id = TLSIdentity.GetIdentity(_store, ServerCertLabel, null);
            id.Should().BeNull();

            var fiveMinToExpireCert = DateTimeOffset.UtcNow.AddMinutes(5);
            id = TLSIdentity.CreateIdentity(true,
                new Dictionary<string, string>() { { Certificate.CommonNameAttribute, "CA-P2PTest" } },
                fiveMinToExpireCert,
                _store,
                ServerCertLabel,
                null);

            (id.Expiration - DateTimeOffset.UtcNow).Should().BeGreaterThan(TimeSpan.MinValue);
            (id.Expiration - DateTimeOffset.UtcNow).Should().BeLessOrEqualTo(TimeSpan.FromMinutes(5));

            // Delete 
            TLSIdentity.DeleteIdentity(_store, ServerCertLabel, null);
        }

        #endregion

        #region TLSIdentity tests helpers

        private static bool ValidateCertsInStore(X509Certificate2Collection certs, X509Store store)
        {
            store.Close();
            store.Open(OpenFlags.ReadOnly);
            if (!certs[0].HasPrivateKey) {
                return false;
            }

            foreach (var cert in certs) {
                var found = store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false);
                if (found.Count != 1) {
                    return false;
                }
            }

            return true;
        }

        private void CreateGetDeleteServerIdentity(bool isServer)
        {
            string commonName = isServer ? "CBL-Server" : "CBL-Client";
            string label = isServer ? ServerCertLabel : ClientCertLabel;
            TLSIdentity id;

            // Delete 
            TLSIdentity.DeleteIdentity(_store, label, null);

            //Get
            id = TLSIdentity.GetIdentity(_store, label, null);
            id.Should().BeNull();

            // Create
            id = TLSIdentity.CreateIdentity(isServer,
                new Dictionary<string, string>() { { Certificate.CommonNameAttribute, commonName } },
                null,
                _store,
                label,
                null);
            id.Should().NotBeNull();
            id.Certs.Count.Should().Be(1);
            ValidateCertsInStore(id.Certs, _store).Should().BeTrue();

            // Get
            id = TLSIdentity.GetIdentity(_store, label, null);
            id.Should().NotBeNull();
            id.Certs.Count.Should().Be(1);
            ValidateCertsInStore(id.Certs, _store).Should().BeTrue();

            // Delete
            TLSIdentity.DeleteIdentity(_store, label, null);

            // Get
            id = TLSIdentity.GetIdentity(_store, label, null);
            id.Should().BeNull();
        }

        private void CreateDuplicateServerIdentity(bool isServer)
        {
            string commonName = isServer ? "CBL-Server" : "CBL-Client";
            string label = isServer ? ServerCertLabel : ClientCertLabel;
            TLSIdentity id;
            Dictionary<string, string> attr = new Dictionary<string, string>() { { Certificate.CommonNameAttribute, commonName } };

            // Delete 
            TLSIdentity.DeleteIdentity(_store, label, null);

            // Create
            id = TLSIdentity.CreateIdentity(isServer,
                attr,
                null,
                _store,
                label,
                null);
            id.Should().NotBeNull();
            id.Certs.Count.Should().Be(1);

            //Get - Need to check why CryptographicException: Invalid provider type specified
            //id = TLSIdentity.GetIdentity(_store, label, null);
            //id.Should().NotBeNull();

            // Create again with the same label
            Action badAction = (() => TLSIdentity.CreateIdentity(isServer,
                attr,
                null,
                _store,
                label,
                null));
            badAction.Should().Throw<CouchbaseLiteException>(CouchbaseLiteErrorMessage.DuplicateCertificate);
        }

        private byte[] GetPublicKeyHashFromCert(X509Certificate2 cert)
        {
            return cert.GetPublicKey();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            
            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
            TLSIdentity.DeleteIdentity(_store, ServerCertLabel, null);
            _store.Dispose();
        }

        #endregion
    }
}

#endif
