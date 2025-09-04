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
using System.Security.Cryptography.X509Certificates;
using Couchbase.Lite;
using Shouldly;
using Couchbase.Lite.P2P;
using Xunit;
using Xunit.Abstractions;

namespace Test;

public sealed class TLSIdentityTest : TestCase
{
    private const string ServerCertLabel = "CBL-Server-Cert";
    private const string ClientCertLabel = "CBL-Client-Cert";

    private readonly X509Store _store;
    public TLSIdentityTest(ITestOutputHelper output) : base(output)
    {
        _store = new(StoreName.My);
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
        TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
        TLSIdentity? identity = TLSIdentity.CreateIdentity(KeyUsages.ClientAuth,
            new Dictionary<string, string>() { { Certificate.CommonNameAttribute, "CA-P2PTest1" } },
            null,
            _store,
            ClientCertLabel,
            null);

        identity.ShouldNotBeNull();
        var certs = identity.Certs;

        var id = TLSIdentity.GetIdentity(certs);
        id.ShouldNotBeNull();

        // Delete
        TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
    }

    [Fact]
    [Obsolete]
    public void TestGetIdentityWithCertCollection_Old()
    {
        TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
        TLSIdentity? identity = TLSIdentity.CreateIdentity(false,
            new Dictionary<string, string>() { { Certificate.CommonNameAttribute, "CA-P2PTest1" } },
            null,
            _store,
            ClientCertLabel,
            null);

        identity.ShouldNotBeNull();
        var certs = identity.Certs;

        var id = TLSIdentity.GetIdentity(certs);
        id.ShouldNotBeNull();

        // Delete
        TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
    }

    [SkippableFact]
    public void TestImportIdentity()
    {
#if CBL_PLATFORM_ANDROID
        Skip.If(Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.M, 
            "An apparent Android bug appears to affect this test on API < 23");

        //Note: Maui Android cert requirement: https://stackoverflow.com/questions/70100597/read-x509-certificate-in-android-net-6-0-application
        //When export the cert, encryption has to be TripleDES-SHA1, AES256-SHA256 will not work...
        var data = GetFileByteArray("certs.pfx", typeof(TLSIdentityTest));
#else
        var data = GetFileByteArray("certs.p12", typeof(TLSIdentityTest));
#endif

        // Import
        var id = TLSIdentity.ImportIdentity(_store, data, "123", ServerCertLabel, null);
        id.ShouldNotBeNull();
        id.Certs.Count.ShouldBe(2);
        ValidateCertsInStore(id.Certs, _store).ShouldBeTrue();

        // Get
        id = TLSIdentity.GetIdentity(_store, ServerCertLabel, null);
        id.ShouldNotBeNull();
    }

    [Fact]
    public void TestCreateIdentityWithNoAttributesOrEmptyAttributes()
    {
        // Delete 
        TLSIdentity.DeleteIdentity(_store, ServerCertLabel, null);

        //Get
        var id = TLSIdentity.GetIdentity(_store, ServerCertLabel, null);
        id.ShouldBeNull();

        // Create id with empty Attributes
        void BadAction() => TLSIdentity.CreateIdentity(KeyUsages.ServerAuth, new(), null, _store, ServerCertLabel, null);
        Should.Throw<CouchbaseLiteException>(BadAction)
            .Message.ShouldBe(CouchbaseLiteErrorMessage.CreateCertAttributeEmpty);
    }

    [Fact]
    public void TestCertificateExpiration()
    {
        // Delete 
        TLSIdentity.DeleteIdentity(_store, ServerCertLabel, null);

        //Get
        var id = TLSIdentity.GetIdentity(_store, ServerCertLabel, null);
        id.ShouldBeNull();

        var fiveMinToExpireCert = DateTimeOffset.UtcNow.AddMinutes(5);
        id = TLSIdentity.CreateIdentity(KeyUsages.ServerAuth,
            new Dictionary<string, string>() { { Certificate.CommonNameAttribute, "CA-P2PTest" } },
            fiveMinToExpireCert,
            _store,
            ServerCertLabel,
            null);

        id.ShouldNotBeNull();
        (id.Expiration - DateTimeOffset.UtcNow).ShouldBeGreaterThan(TimeSpan.MinValue);
        (id.Expiration - DateTimeOffset.UtcNow).ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(5));

        // Delete 
        TLSIdentity.DeleteIdentity(_store, ServerCertLabel, null);
    }

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
        var commonName = keyUsages.HasFlag(KeyUsages.ServerAuth) ? "CBL-Server" : "CBL-Client";
        var label = keyUsages.HasFlag(KeyUsages.ServerAuth) ? ServerCertLabel : ClientCertLabel;

        // Delete 
        TLSIdentity.DeleteIdentity(_store, label, null);

        //Get
        var id = TLSIdentity.GetIdentity(_store, label, null);
        id.ShouldBeNull();

        // Create
        id = TLSIdentity.CreateIdentity(keyUsages,
            new Dictionary<string, string>() { { Certificate.CommonNameAttribute, commonName } },
            null,
            _store,
            label,
            null);
        id.ShouldNotBeNull();
        id.Certs.Count.ShouldBe(1);
        ValidateCertsInStore(id.Certs, _store).ShouldBeTrue();

        // Get
        id = TLSIdentity.GetIdentity(_store, label, null);
        id.ShouldNotBeNull();
        id.Certs.Count.ShouldBe(1);
        ValidateCertsInStore(id.Certs, _store).ShouldBeTrue();

        // Delete
        TLSIdentity.DeleteIdentity(_store, label, null);

        // Get
        id = TLSIdentity.GetIdentity(_store, label, null);
        id.ShouldBeNull();
    }

    private void CreateDuplicateServerIdentity(KeyUsages keyUsages)
    {
        var commonName = keyUsages.HasFlag(KeyUsages.ServerAuth) ? "CBL-Server" : "CBL-Client";
        var label = keyUsages.HasFlag(KeyUsages.ServerAuth) ? ServerCertLabel : ClientCertLabel;
        var attr = new Dictionary<string, string>() { { Certificate.CommonNameAttribute, commonName } };

        // Delete 
        TLSIdentity.DeleteIdentity(_store, label, null);

        // Create
        var id = TLSIdentity.CreateIdentity(keyUsages,
            attr,
            null,
            _store,
            label,
            null);
        id.ShouldNotBeNull();
        id.Certs.Count.ShouldBe(1);

        //Get - Need to check why CryptographicException: Invalid provider type specified
        //id = TLSIdentity.GetIdentity(_store, label, null);
        //id.ShouldNotBeNull();

        // Create again with the same label
        void BadAction() => TLSIdentity.CreateIdentity(keyUsages, attr, null, _store, label, null);
        Should.Throw<CouchbaseLiteException>(BadAction)
            .Message.ShouldBe(CouchbaseLiteErrorMessage.DuplicateCertificate);
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

#endif
