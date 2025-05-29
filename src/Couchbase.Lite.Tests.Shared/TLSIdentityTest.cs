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

using Xunit;
using Xunit.Abstractions;
using System.Security.Cryptography;

namespace Test
{
    public sealed class TLSIdentityTest : TestCase
    {
        const string ServerCertLabel = "CBL-Server-Cert";
        const string ClientCertLabel = "CBL-Client-Cert";

        private X509Store _store;
        public TLSIdentityTest(ITestOutputHelper output) : base(output)
        {
            _store = new X509Store(StoreName.My);
            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
            TLSIdentity.DeleteIdentity(_store, ServerCertLabel, null);
        }

        #region TLSIdentity tests

        [Fact]
        public void TestCreateGetDeleteServerIdentity() => CreateGetDeleteServerIdentity(KeyUsages.ServerAuth);

        [Fact]
        public void TestCreateDuplicateServerIdentity() => CreateDuplicateServerIdentity(KeyUsages.ServerAuth);

        [Fact]
        public void TestCreateGetDeleteClientIdentity() => CreateGetDeleteServerIdentity(KeyUsages.ClientAuth);

        [Fact]
        public void TestCreateDuplicateClientIdentity() => CreateDuplicateServerIdentity(KeyUsages.ClientAuth);

        [Fact]
        public void TestGetIdentityWithCertCollection()
        {
            TLSIdentity id;
            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
            TLSIdentity? identity = TLSIdentity.CreateIdentity(KeyUsages.ClientAuth,
                new Dictionary<string, string>() { { Certificate.CommonNameAttribute, "CA-P2PTest1" } },
                null,
                _store,
                ClientCertLabel,
                null);

            identity.Should().NotBeNull();
            var certs = identity!.Certs;

            id = TLSIdentity.GetIdentity(certs);
            id.Should().NotBeNull();

            // Delete
            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
        }

        [Fact]
        [Obsolete]
        public void TestGetIdentityWithCertCollection_Old()
        {
            TLSIdentity id;
            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
            TLSIdentity? identity = TLSIdentity.CreateIdentity(false,
                new Dictionary<string, string>() { { Certificate.CommonNameAttribute, "CA-P2PTest1" } },
                null,
                _store,
                ClientCertLabel,
                null);

            identity.Should().NotBeNull();
            var certs = identity!.Certs;

            id = TLSIdentity.GetIdentity(certs);
            id.Should().NotBeNull();

            // Delete
            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
        }

        [SkippableFact]
        public void TestImportIdentity()
        {
            TLSIdentity? id;
#if NET_ANDROID
            Skip.If(Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.M, 
                "An apparent Android bug appears to affect this test on API < 23");

            //Note: Maui Android cert requirement: https://stackoverflow.com/questions/70100597/read-x509-certificate-in-android-net-6-0-application
            //When export the cert, encryption has to be TripleDES-SHA1, AES256-SHA256 will not work...
            byte[] data = GetFileByteArray("certs.pfx", typeof(TLSIdentityTest));
#else
            byte[] data = GetFileByteArray("certs.p12", typeof(TLSIdentityTest));
#endif

            // Import
            id = TLSIdentity.ImportIdentity(_store, data, "123", ServerCertLabel, null);
            id.Should().NotBeNull();
            id!.Certs.Count.Should().Be(2);
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
            Action badAction = (() => TLSIdentity.CreateIdentity(KeyUsages.ServerAuth,
                new Dictionary<string, string>() { },
                null,
                _store,
                ServerCertLabel,
                null));
            badAction.Should().Throw<CouchbaseLiteException>(CouchbaseLiteErrorMessage.CreateCertAttributeEmpty);
        }

        [Fact]
        public void TestCertificateExpiration()
        {
            TLSIdentity? id;

            // Delete 
            TLSIdentity.DeleteIdentity(_store, ServerCertLabel, null);

            //Get
            id = TLSIdentity.GetIdentity(_store, ServerCertLabel, null);
            id.Should().BeNull();

            var fiveMinToExpireCert = DateTimeOffset.UtcNow.AddMinutes(5);
            id = TLSIdentity.CreateIdentity(KeyUsages.ServerAuth,
                new Dictionary<string, string>() { { Certificate.CommonNameAttribute, "CA-P2PTest" } },
                fiveMinToExpireCert,
                _store,
                ServerCertLabel,
                null);

            id.Should().NotBeNull();
            (id!.Expiration - DateTimeOffset.UtcNow).Should().BeGreaterThan(TimeSpan.MinValue);
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

        private void CreateGetDeleteServerIdentity(KeyUsages keyUsages)
        {
            string commonName = keyUsages.HasFlag(KeyUsages.ServerAuth) ? "CBL-Server" : "CBL-Client";
            string label = keyUsages.HasFlag(KeyUsages.ServerAuth) ? ServerCertLabel : ClientCertLabel;
            TLSIdentity? id;

            // Delete 
            TLSIdentity.DeleteIdentity(_store, label, null);

            //Get
            id = TLSIdentity.GetIdentity(_store, label, null);
            id.Should().BeNull();

            // Create
            id = TLSIdentity.CreateIdentity(keyUsages,
                new Dictionary<string, string>() { { Certificate.CommonNameAttribute, commonName } },
                null,
                _store,
                label,
                null);
            id.Should().NotBeNull();
            id!.Certs.Count.Should().Be(1);
            ValidateCertsInStore(id.Certs, _store).Should().BeTrue();

            // Get
            id = TLSIdentity.GetIdentity(_store, label, null);
            id.Should().NotBeNull();
            id!.Certs.Count.Should().Be(1);
            ValidateCertsInStore(id.Certs, _store).Should().BeTrue();

            // Delete
            TLSIdentity.DeleteIdentity(_store, label, null);

            // Get
            id = TLSIdentity.GetIdentity(_store, label, null);
            id.Should().BeNull();
        }

        private void CreateDuplicateServerIdentity(KeyUsages keyUsages)
        {
            string commonName = keyUsages.HasFlag(KeyUsages.ServerAuth) ? "CBL-Server" : "CBL-Client";
            string label = keyUsages.HasFlag(KeyUsages.ServerAuth) ? ServerCertLabel : ClientCertLabel;
            TLSIdentity? id;
            Dictionary<string, string> attr = new Dictionary<string, string>() { { Certificate.CommonNameAttribute, commonName } };

            // Delete 
            TLSIdentity.DeleteIdentity(_store, label, null);

            // Create
            id = TLSIdentity.CreateIdentity(keyUsages,
                attr,
                null,
                _store,
                label,
                null);
            id.Should().NotBeNull();
            id!.Certs.Count.Should().Be(1);

            //Get - Need to check why CryptographicException: Invalid provider type specified
            //id = TLSIdentity.GetIdentity(_store, label, null);
            //id.Should().NotBeNull();

            // Create again with the same label
            Action badAction = (() => TLSIdentity.CreateIdentity(keyUsages,
                attr,
                null,
                _store,
                label,
                null));
            badAction.Should().Throw<CouchbaseLiteException>(CouchbaseLiteErrorMessage.DuplicateCertificate);
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
