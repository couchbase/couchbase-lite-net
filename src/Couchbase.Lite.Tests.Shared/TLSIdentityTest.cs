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

using Test.Util;
#if COUCHBASE_ENTERPRISE
using Couchbase.Lite.P2P;
using ProtocolType = Couchbase.Lite.P2P.ProtocolType;
#endif

#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
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
        }

        //#region TLSIdentity tests

        //[Fact]
        //public void TestCreateGetDeleteServerIdentity() => CreateGetDeleteServerIdentity(true);

        //[Fact]
        //public void TestCreateDuplicateServerIdentity() => CreateDuplicateServerIdentity(true);

        //[Fact]
        //public void TestCreateGetDeleteClientIdentity() => CreateGetDeleteServerIdentity(false);

        //[Fact]
        //public void TestCreateDuplicateClientIdentity() => CreateDuplicateServerIdentity(false);

        //[Fact]
        //public void TestGetIdentityWithCertCollection()
        //{
        //    TLSIdentity id;
        //    X509Chain certChain = new X509Chain();
        //    TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
        //    TLSIdentity identity = TLSIdentity.CreateIdentity(false,
        //        new Dictionary<string, string>() { { Certificate.CommonNameAttribute, "CA-P2PTest1" } },
        //        null,
        //        _store,
        //        ClientCertLabel,
        //        null);

        //    var certs = identity.Certs;

        //    id = TLSIdentity.GetIdentity(certs);
        //    id.Should().NotBeNull();

        //    // Delete
        //    TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
        //}
        
        //[Fact]
        //public void TestImportIdentityFromCertCollection()
        //{
        //    TLSIdentity id;
        //    X509Certificate2Collection certs = new X509Certificate2Collection();
        //    X509Chain certChain = new X509Chain();
        //    TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);

        //    //needs to know how to move the cert to the test location
        //    X509Certificate2 cert = GetMyCert();

        //    bool validChain = certChain.Build(cert);

        //    if (!validChain) {
        //        // Whatever you want to do about that.

        //        foreach (var status in certChain.ChainStatus) {
        //            // In reality you can == this, since X509Chain.ChainStatus builds
        //            // an object per flag, but since it's [Flags] let's play it safe.
        //            if ((status.Status & X509ChainStatusFlags.PartialChain) != 0) {
        //                // Incomplete chain.
        //            }
        //        }
        //    }

        //    foreach (var element in certChain.ChainElements) {
        //        certs.Add(element.Certificate);
        //    }

        //    // Import
        //    var pkc12 = certs.Export(X509ContentType.Pfx, "123");
        //    id = TLSIdentity.ImportIdentity(_store, pkc12, "123", null, ClientCertLabel);

        //    //// Get
        //    //id = TLSIdentity.GetIdentity(_store, ClientCertLabel, null);
        //    id.Should().NotBeNull();

        //    // Delete
        //    TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
        //}

        //[Fact]
        //public void TestCreateIdentityWithNoAttributesOrEmptyAttributes()
        //{
        //    // Delete 
        //    TLSIdentity.DeleteIdentity(_store, ServerCertLabel, null);

        //    //Get
        //    var id = TLSIdentity.GetIdentity(_store, ServerCertLabel, null);
        //    id.Should().BeNull();

        //    // Create id with empty Attributes
        //    Action badAction = (() => TLSIdentity.CreateIdentity(true,
        //        new Dictionary<string, string>() { },
        //        null,
        //        _store,
        //        ServerCertLabel,
        //        null));
        //    badAction.Should().Throw<CouchbaseLiteException>(CouchbaseLiteErrorMessage.CreateCertAttributeEmpty);

        //    // Create id with null Attributes
        //    badAction = (() => TLSIdentity.CreateIdentity(true,
        //        null,
        //        null,
        //        _store,
        //        ServerCertLabel,
        //        null));
        //    badAction.Should().Throw<CouchbaseLiteException>(CouchbaseLiteErrorMessage.CreateCertAttributeEmpty);
        //}

        //[Fact]
        //public void TestCertificateExpiration()
        //{
        //    TLSIdentity id;

        //    // Delete 
        //    TLSIdentity.DeleteIdentity(_store, ServerCertLabel, null);

        //    //Get
        //    id = TLSIdentity.GetIdentity(_store, ServerCertLabel, null);
        //    id.Should().BeNull();

        //    var fiveMinToExpireCert = DateTimeOffset.UtcNow.AddMinutes(5);
        //    id = TLSIdentity.CreateIdentity(true,
        //        new Dictionary<string, string>() { { Certificate.CommonNameAttribute, "CA-P2PTest" } },
        //        fiveMinToExpireCert,
        //        _store,
        //        ServerCertLabel,
        //        null);

        //    (id.Expiration - DateTimeOffset.UtcNow).Should().BeGreaterThan(TimeSpan.MinValue);
        //    (id.Expiration - DateTimeOffset.UtcNow).Should().BeLessOrEqualTo(TimeSpan.FromMinutes(5));

        //    // Delete 
        //    TLSIdentity.DeleteIdentity(_store, ServerCertLabel, null);
        //}

        //#endregion

        //#region TLSIdentity tests helpers

        //static private X509Certificate2 GetMyCert()
        //{
        //    //TODO move the cert into the test local location
        //    X509Certificate2 cert = null;

        //    // Load the certificate
        //    var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        //    store.Open(OpenFlags.ReadOnly);
        //    X509Certificate2Collection certCollection = store.Certificates.Find
        //    (
        //        X509FindType.FindBySubjectName,
        //        "certs",
        //        false    // Including invalid certificates
        //    );

        //    if (certCollection.Count > 0) {
        //        cert = certCollection[0];
        //    }

        //    store.Close();

        //    return cert;
        //}

        //private void AddRootCert()
        //{
        //    X509Store store = new X509Store("Trust");
        //    store.Open(OpenFlags.ReadWrite);

        //    string certString = "MIIDGjCCAgKgAwIBAgICApowDQYJKoZIhvcNAQEFBQAwLjELMAkGA1UEBhMCQ1oxDjAMBgNVBAoTBVJlYmV4MQ8wDQYDVQQDEwZUZXN0Q0EwHhcNMDAwMTAxMDAwMDAwWhcNNDkxMjMxMDAwMDAwWjAuMQswCQYDVQQGEwJDWjEOMAwGA1UEChMFUmViZXgxDzANBgNVBAMTBlRlc3RDQTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAMgeRAcaNLTFaaBhFx8RDJ8b9K655dNUXmO11mbImDbPq4qVeZXDgAjnzPov8iBscwfqBvBpF38LsxBIPA2i1d0RMsQruOhJHttA9I0enElUXXj63sOEVNMSQeg1IMyvNeEotag+Gcx6SF+HYnariublETaZGzwAOD2SM49mfqUyfkgeTjdO6qp8xnoEr7dS5pEBHDg70byj/JEeZd3gFea9TiOXhbCrI89dKeWYBeoHFYhhkaSB7q9EOaUEzKo/BQ6PBHFu6odfGkOjXuwfPkY/wUy9U4uj75LmdhzvJf6ifsJS9BQZF4//JcUYSxiyzpxDYqSbTF3g9w5Ds2LOAscCAwEAAaNCMEAwDgYDVR0PAQH/BAQDAgB/MA8GA1UdEwEB/wQFMAMBAf8wHQYDVR0OBBYEFD1v20tPgvHTEK/0eLO09j0rL2qXMA0GCSqGSIb3DQEBBQUAA4IBAQAZIjcdR3EZiFJ67gfCnPBrxVgFNvaRAMCYEYYIGDCAUeB4bLTu9dvun9KFhgVNqjgx+xTTpx9d/5mAZx5W3YAG6faQPCaHccLefB1M1hVPmo8md2uw1a44RHU9LlM0V5Lw8xTKRkQiZz3Ysu0sY27RvLrTptbbfkE4Rp9qAMguZT9cFrgPAzh+0zuo8NNj9Jz7/SSa83yIFmflCsHYSuNyKIy2iaX9TCVbTrwJmRIB65gqtTb6AKtFGIPzsb6nayHvgGHFchrFovcNrvRpE71F38oVG+eCjT23JfiIZim+yJLppSf56167u8etDcQ39j2b9kzWlHIVkVM0REpsKF7S";
        //    X509Certificate2 rootCert = new X509Certificate2(Convert.FromBase64String(certString));
        //    if (!store.Certificates.Contains(rootCert))
        //        store.Add(rootCert);

        //    store.Close();
        //}


        //private void CreateGetDeleteServerIdentity(bool isServer)
        //{
        //    string commonName = isServer ? "CBL-Server" : "CBL-Client";
        //    string label = isServer ? ServerCertLabel : ClientCertLabel;
        //    TLSIdentity id;

        //    // Delete 
        //    TLSIdentity.DeleteIdentity(_store, label, null);

        //    //Get
        //    id = TLSIdentity.GetIdentity(_store, label, null);
        //    id.Should().BeNull();

        //    // Create
        //    id = TLSIdentity.CreateIdentity(isServer,
        //        new Dictionary<string, string>() { { Certificate.CommonNameAttribute, commonName } },
        //        null,
        //        _store,
        //        label,
        //        null);
        //    id.Should().NotBeNull();
        //    id.Certs.Count.Should().Be(1);

        //    // Delete
        //    TLSIdentity.DeleteIdentity(_store, label, null);

        //    // Get
        //    TLSIdentity.GetIdentity(_store, label, null);
        //}

        //private void CreateDuplicateServerIdentity(bool isServer)
        //{
        //    string commonName = isServer ? "CBL-Server" : "CBL-Client";
        //    string label = isServer ? ServerCertLabel : ClientCertLabel;
        //    TLSIdentity id;
        //    Dictionary<string, string> attr = new Dictionary<string, string>() { { Certificate.CommonNameAttribute, commonName } };

        //    // Delete 
        //    TLSIdentity.DeleteIdentity(_store, label, null);

        //    // Create
        //    id = TLSIdentity.CreateIdentity(isServer,
        //        attr,
        //        null,
        //        _store,
        //        label,
        //        null);
        //    id.Should().NotBeNull();
        //    id.Certs.Count.Should().Be(1);

        //    //Get - Need to check why CryptographicException: Invalid provider type specified
        //    //id = TLSIdentity.GetIdentity(_store, label, null);
        //    //id.Should().NotBeNull();

        //    // Create again with the same label
        //    Action badAction = (() => TLSIdentity.CreateIdentity(isServer,
        //        attr,
        //        null,
        //        _store,
        //        label,
        //        null));
        //    badAction.Should().Throw<CouchbaseLiteException>(CouchbaseLiteErrorMessage.DuplicateCertificate);
        //}

        //private byte[] GetPublicKeyHashFromCert(X509Certificate2 cert)
        //{
        //    return cert.GetPublicKey();
        //}

        //protected override void Dispose(bool disposing)
        //{
        //    base.Dispose(disposing);
        //    _store.Dispose();
        //}

        //#endregion
    }
}

#endif
