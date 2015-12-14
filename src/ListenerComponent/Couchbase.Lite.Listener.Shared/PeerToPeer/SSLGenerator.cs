//
// SSLGenerator.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using System.Security.Cryptography;
using Mono.Security.Authenticode;
using Mono.Security.X509;
using System.IO;

namespace Couchbase.Lite.Listener.Security
{
    internal static class SSLGenerator
    {
        public static void GenerateTempCert(ushort port, string issuerName)
        {
            byte[] sn = Guid.NewGuid ().ToByteArray ();
            DateTime notBefore = DateTime.Now;
            DateTime notAfter = new DateTime (643445675990000000); // 12/31/2039 23:59:59Z

            RSA issuerKey = (RSA)RSA.Create ();
            PrivateKey key = new PrivateKey ();
            key.RSA = issuerKey;

            var qualifiedName = "CN=" + issuerName;
            X509CertificateBuilder cb = new X509CertificateBuilder (3);
            cb.SerialNumber = sn;
            cb.IssuerName = qualifiedName;
            cb.NotBefore = notBefore;
            cb.NotAfter = notAfter;
            cb.SubjectName = qualifiedName;
            cb.SubjectPublicKey = issuerKey;
            cb.Hash = "SHA512";
            var rawcert = cb.Sign(issuerKey);

            string dirname = Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData);
            string path = Path.Combine (dirname, ".mono");
            path = Path.Combine (path, "httplistener");
            string cert_file = Path.Combine (path, String.Format ("{0}.cer", port));
            WriteCertificate (cert_file, rawcert);

            string pvk_file = Path.Combine (path, String.Format ("{0}.pvk", port));
            key.Save(pvk_file);
        }

        private static void WriteCertificate (string filename, byte[] rawcert) 
        {
            FileStream fs = File.Open (filename, FileMode.Create, FileAccess.Write);
            fs.Write (rawcert, 0, rawcert.Length);
            fs.Close ();
        }
    }
}

